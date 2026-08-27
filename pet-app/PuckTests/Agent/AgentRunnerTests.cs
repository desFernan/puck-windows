using System.Net.Http;
using System.Text.Json;
using Puck.Agent;
using Puck.Tools;

namespace PuckTests.Agent;

/// 대본대로 답하는 모델. 네트워크 없이 루프를 돌린다.
internal sealed class ScriptedClient(params AgentTurn[] turns) : IAgentClient
{
    private int _next;

    public List<IReadOnlyList<AgentMessage>> Seen { get; } = [];
    public List<IReadOnlyList<ToolSpec>> ToolsOffered { get; } = [];

    public Task<AgentTurn> SendAsync(string systemPrompt, IReadOnlyList<AgentMessage> messages,
                                     IReadOnlyList<ToolSpec> tools, CancellationToken cancellation)
    {
        Seen.Add(messages.ToList());
        ToolsOffered.Add(tools);
        return Task.FromResult(_next < turns.Length ? turns[_next++] : turns[^1]);
    }
}

internal sealed class FakeTool(string name, Func<string> result, ToolApproval approval = ToolApproval.NotRequired)
    : IToolHandler
{
    public string Name => name;
    public int Calls { get; private set; }

    public ToolSpec Spec => new()
    {
        Name = name,
        Description = "테스트용",
        Properties = new Dictionary<string, JsonElement>(),
        Approval = approval,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        Calls++;
        return Task.FromResult(result());
    }
}

/// 두 번째 부름에서 무너지는 모델. 끊긴 네트워크와 취소를 흉내 낸다.
internal sealed class FailsAfter(int successes, params AgentTurn[] turns) : IAgentClient
{
    private int _next;

    public Task<AgentTurn> SendAsync(string systemPrompt, IReadOnlyList<AgentMessage> messages,
                                     IReadOnlyList<ToolSpec> tools, CancellationToken cancellation)
    {
        if (_next >= successes) throw new HttpRequestException("끊겼다");
        return Task.FromResult(turns[_next++]);
    }
}

internal sealed class AlwaysAllow : IApprovalPrompt
{
    public Task<bool> RequestAsync(string t, IReadOnlyDictionary<string, JsonElement> a, CancellationToken c)
        => Task.FromResult(true);
}

public class AgentRunnerTests
{
    private static readonly AgentConfiguration Usable = new() { ApiKey = "sk-test" };

    private static AgentBlock.ToolUse Call(string id, string name)
        => new(id, name, new Dictionary<string, JsonElement>());

    private static AgentRunner Make(IAgentClient client, ToolRegistry tools,
                                    IApprovalPrompt? prompt = null, AgentConfiguration? config = null)
        => new(client, tools, new ToolApprovals(prompt ?? new AlwaysAllow()), () => config ?? Usable);

    [Fact]
    public async Task WithNoApiKeyItSaysSoInsteadOfDoingNothing()
    {
        var runner = Make(new ScriptedClient(new AgentTurn([new AgentBlock.Text("hi")])),
            ToolRegistry.Of(), config: new AgentConfiguration());

        var answer = await runner.AskAsync("안녕");

        Assert.Contains("ANTHROPIC_API_KEY", answer);
    }

    [Fact]
    public async Task APlainAnswerComesStraightBack()
    {
        var runner = Make(new ScriptedClient(new AgentTurn([new AgentBlock.Text("안녕!")])),
            ToolRegistry.Of());

        Assert.Equal("안녕!", await runner.AskAsync("안녕"));
    }

    [Fact]
    public async Task ATooluseTurnRunsTheToolAndAsksAgain()
    {
        var tool = new FakeTool("peek", () => "창 세 개");
        var client = new ScriptedClient(
            new AgentTurn([Call("t1", "peek")]),
            new AgentTurn([new AgentBlock.Text("창이 셋 있어")]));

        var runner = Make(client, ToolRegistry.Of((tool.Spec, tool)));
        var answer = await runner.AskAsync("뭐 보여?");

        Assert.Equal(1, tool.Calls);
        Assert.Equal("창이 셋 있어", answer);
    }

    [Fact]
    public async Task ToolResultsGoBackInOneUserMessage()
    {
        // 나눠 보내면 모델이 도구를 하나씩만 부르는 쪽으로 배운다.
        var tool = new FakeTool("peek", () => "ok");
        var client = new ScriptedClient(
            new AgentTurn([Call("t1", "peek"), Call("t2", "peek")]),
            new AgentTurn([new AgentBlock.Text("끝")]));

        var runner = Make(client, ToolRegistry.Of((tool.Spec, tool)));
        await runner.AskAsync("두 번 해 줘");

        var toolResultMessages = runner.History
            .Where(m => m.Role == AgentRole.User && m.Blocks.OfType<AgentBlock.ToolResult>().Any())
            .ToList();

        var single = Assert.Single(toolResultMessages);
        Assert.Equal(2, single.Blocks.OfType<AgentBlock.ToolResult>().Count());
        Assert.Equal(2, tool.Calls);
    }

    [Fact]
    public async Task EveryToolUseGetsExactlyOneResultKeyedByItsId()
    {
        // 짝이 안 맞으면 다음 요청이 통째로 거절된다.
        var tool = new FakeTool("peek", () => "ok");
        var client = new ScriptedClient(
            new AgentTurn([Call("a", "peek"), Call("b", "peek")]),
            new AgentTurn([new AgentBlock.Text("끝")]));

        await Make(client, ToolRegistry.Of((tool.Spec, tool))).AskAsync("x");

        var ids = client.Seen[^1]
            .SelectMany(m => m.Blocks).OfType<AgentBlock.ToolResult>()
            .Select(r => r.ToolUseId).ToList();

        Assert.Equal(["a", "b"], ids);
    }

    [Fact]
    public async Task AToolThatThrowsBecomesAnErrorResultNotADeadTurn()
    {
        var tool = new FakeTool("boom", () => throw new InvalidOperationException("펑"));
        var client = new ScriptedClient(
            new AgentTurn([Call("t1", "boom")]),
            new AgentTurn([new AgentBlock.Text("실패했대")]));

        var answer = await Make(client, ToolRegistry.Of((tool.Spec, tool))).AskAsync("해 봐");

        Assert.Equal("실패했대", answer);
        var result = client.Seen[^1].SelectMany(m => m.Blocks).OfType<AgentBlock.ToolResult>().Single();
        Assert.True(result.IsError);
        Assert.Contains("펑", result.Content);
    }

    [Fact]
    public async Task AnUnknownToolIsAnErrorResultSoTheModelCanCorrectItself()
    {
        var client = new ScriptedClient(
            new AgentTurn([Call("t1", "없는도구")]),
            new AgentTurn([new AgentBlock.Text("아 없구나")]));

        await Make(client, ToolRegistry.Of()).AskAsync("x");

        var result = client.Seen[^1].SelectMany(m => m.Blocks).OfType<AgentBlock.ToolResult>().Single();
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ARefusalIsToldToTheModelRatherThanSilentlySkipped()
    {
        var tool = new FakeTool("danger", () => "실행됨", ToolApproval.Required);
        var client = new ScriptedClient(
            new AgentTurn([Call("t1", "danger")]),
            new AgentTurn([new AgentBlock.Text("알겠어")]));

        var runner = Make(client, ToolRegistry.Of((tool.Spec, tool)), new DenyingApprovalPrompt());
        await runner.AskAsync("위험한 거 해 줘");

        Assert.Equal(0, tool.Calls);
        var result = client.Seen[^1].SelectMany(m => m.Blocks).OfType<AgentBlock.ToolResult>().Single();
        Assert.Contains("거절", result.Content);
    }

    [Fact]
    public async Task AModelThatNeverStopsCallingToolsIsCutOff()
    {
        var tool = new FakeTool("loop", () => "또 해");
        var client = new ScriptedClient(new AgentTurn([Call("t", "loop")]));

        var answer = await Make(client, ToolRegistry.Of((tool.Spec, tool))).AskAsync("무한히");

        Assert.Equal(AgentRunner.MaxToolRounds, tool.Calls);
        Assert.Contains("멈췄습니다", answer);
    }

    [Fact]
    public async Task ProgressIsReportedSoThePetCanActItOut()
    {
        var tool = new FakeTool("peek", () => "ok");
        var client = new ScriptedClient(
            new AgentTurn([new AgentBlock.Text("볼게"), Call("t1", "peek")]),
            new AgentTurn([new AgentBlock.Text("끝")]));

        var runner = Make(client, ToolRegistry.Of((tool.Spec, tool)));
        var events = new List<AgentEvent>();
        runner.Progress += events.Add;

        await runner.AskAsync("x");

        Assert.Contains(events, e => e is AgentEvent.UsingTool { Name: "peek" });
        Assert.Contains(events, e => e is AgentEvent.ToolDone { Name: "peek", IsError: false });
        Assert.Contains(events, e => e is AgentEvent.Said { Text: "볼게" });
    }

    [Fact]
    public async Task TheConversationCarriesAcrossTurns()
    {
        var client = new ScriptedClient(new AgentTurn([new AgentBlock.Text("응")]));
        var runner = Make(client, ToolRegistry.Of());

        await runner.AskAsync("첫 번째");
        await runner.AskAsync("두 번째");

        var lastSeen = client.Seen[^1];
        Assert.Contains(lastSeen, m => m.TextContent.Contains("첫 번째"));
        Assert.Contains(lastSeen, m => m.TextContent.Contains("두 번째"));
    }

    [Fact]
    public async Task AFailedTurnLeavesNoTraceInTheHistory()
    {
        // 답 없는 user 메시지가 남으면 다음 요청이 통째로 거절된다. 한 번
        // 끊긴 것으로 펫이 영영 벙어리가 되면 안 된다.
        var runner = Make(new FailsAfter(0), ToolRegistry.Of());

        await Assert.ThrowsAsync<HttpRequestException>(() => runner.AskAsync("안녕"));

        Assert.Empty(runner.History);
    }

    [Fact]
    public async Task AFailureMidToolLoopDoesNotLeaveADanglingToolUse()
    {
        // 결과가 짝지어지지 않은 tool_use가 기록에 남으면 그 뒤의 모든
        // 요청이 400으로 돌아온다.
        var tool = new FakeTool("peek", () => "ok");
        var client = new FailsAfter(1, new AgentTurn([Call("t1", "peek")]));

        var runner = Make(client, ToolRegistry.Of((tool.Spec, tool)));
        await Assert.ThrowsAsync<HttpRequestException>(() => runner.AskAsync("봐 줘"));

        Assert.DoesNotContain(runner.History.SelectMany(m => m.Blocks), b => b is AgentBlock.ToolUse);
        Assert.Empty(runner.History);
    }

    [Fact]
    public async Task ToolCallsAreAnsweredEvenWhenTheTurnLooksFinished()
    {
        // max_tokens로 잘린 응답에도 tool_use가 들어 있다. 블록을 보고
        // 정하지 않으면 그 부름이 답 없이 기록에 남는다.
        var tool = new FakeTool("peek", () => "ok");
        var client = new ScriptedClient(
            new AgentTurn([Call("t1", "peek")]),
            new AgentTurn([new AgentBlock.Text("끝")]));

        await Make(client, ToolRegistry.Of((tool.Spec, tool))).AskAsync("x");

        Assert.Equal(1, tool.Calls);
    }

    [Fact]
    public async Task RolesStillAlternateAfterTheRoundLimit()
    {
        // 도구 결과로 끝난 기록 뒤에 다음 질문을 붙이면 user가 연달아 둘이 된다.
        var tool = new FakeTool("loop", () => "또 해");
        var runner = Make(new ScriptedClient(new AgentTurn([Call("t", "loop")])),
            ToolRegistry.Of((tool.Spec, tool)));

        await runner.AskAsync("무한히");

        Assert.Collection(runner.History,
            m => Assert.Equal(AgentRole.User, m.Role),
            m => Assert.Equal(AgentRole.Assistant, m.Role));
        Assert.DoesNotContain(runner.History.SelectMany(m => m.Blocks), b => b is AgentBlock.ToolResult);
    }

    [Fact]
    public async Task ATurnThatOnlyThinksStillSaysSomething()
    {
        var client = new ScriptedClient(new AgentTurn([new AgentBlock.Thinking("음…", "sig")]));

        Assert.False(string.IsNullOrWhiteSpace(await Make(client, ToolRegistry.Of()).AskAsync("x")));
    }

    [Fact]
    public async Task WithNoToolsTheShorterPromptIsUsed()
    {
        var client = new ScriptedClient(new AgentTurn([new AgentBlock.Text("응")]));
        await Make(client, ToolRegistry.Of()).AskAsync("x");
        Assert.Empty(client.ToolsOffered[0]);
    }
}
