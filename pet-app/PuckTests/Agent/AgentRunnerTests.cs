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
        var runner = Make(new ScriptedClient(new AgentTurn([new AgentBlock.Text("hi")], false)),
            ToolRegistry.Of(), config: new AgentConfiguration());

        var answer = await runner.AskAsync("안녕");

        Assert.Contains("ANTHROPIC_API_KEY", answer);
    }

    [Fact]
    public async Task APlainAnswerComesStraightBack()
    {
        var runner = Make(new ScriptedClient(new AgentTurn([new AgentBlock.Text("안녕!")], false)),
            ToolRegistry.Of());

        Assert.Equal("안녕!", await runner.AskAsync("안녕"));
    }

    [Fact]
    public async Task ATooluseTurnRunsTheToolAndAsksAgain()
    {
        var tool = new FakeTool("peek", () => "창 세 개");
        var client = new ScriptedClient(
            new AgentTurn([Call("t1", "peek")], WantsToolUse: true),
            new AgentTurn([new AgentBlock.Text("창이 셋 있어")], WantsToolUse: false));

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
            new AgentTurn([Call("t1", "peek"), Call("t2", "peek")], WantsToolUse: true),
            new AgentTurn([new AgentBlock.Text("끝")], WantsToolUse: false));

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
            new AgentTurn([Call("a", "peek"), Call("b", "peek")], WantsToolUse: true),
            new AgentTurn([new AgentBlock.Text("끝")], WantsToolUse: false));

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
            new AgentTurn([Call("t1", "boom")], WantsToolUse: true),
            new AgentTurn([new AgentBlock.Text("실패했대")], WantsToolUse: false));

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
            new AgentTurn([Call("t1", "없는도구")], WantsToolUse: true),
            new AgentTurn([new AgentBlock.Text("아 없구나")], WantsToolUse: false));

        await Make(client, ToolRegistry.Of()).AskAsync("x");

        var result = client.Seen[^1].SelectMany(m => m.Blocks).OfType<AgentBlock.ToolResult>().Single();
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ARefusalIsToldToTheModelRatherThanSilentlySkipped()
    {
        var tool = new FakeTool("danger", () => "실행됨", ToolApproval.Required);
        var client = new ScriptedClient(
            new AgentTurn([Call("t1", "danger")], WantsToolUse: true),
            new AgentTurn([new AgentBlock.Text("알겠어")], WantsToolUse: false));

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
        var client = new ScriptedClient(new AgentTurn([Call("t", "loop")], WantsToolUse: true));

        var answer = await Make(client, ToolRegistry.Of((tool.Spec, tool))).AskAsync("무한히");

        Assert.Equal(AgentRunner.MaxToolRounds, tool.Calls);
        Assert.Contains("멈췄습니다", answer);
    }

    [Fact]
    public async Task ProgressIsReportedSoThePetCanActItOut()
    {
        var tool = new FakeTool("peek", () => "ok");
        var client = new ScriptedClient(
            new AgentTurn([new AgentBlock.Text("볼게"), Call("t1", "peek")], WantsToolUse: true),
            new AgentTurn([new AgentBlock.Text("끝")], WantsToolUse: false));

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
        var client = new ScriptedClient(new AgentTurn([new AgentBlock.Text("응")], false));
        var runner = Make(client, ToolRegistry.Of());

        await runner.AskAsync("첫 번째");
        await runner.AskAsync("두 번째");

        var lastSeen = client.Seen[^1];
        Assert.Contains(lastSeen, m => m.TextContent.Contains("첫 번째"));
        Assert.Contains(lastSeen, m => m.TextContent.Contains("두 번째"));
    }

    [Fact]
    public async Task WithNoToolsTheShorterPromptIsUsed()
    {
        var client = new ScriptedClient(new AgentTurn([new AgentBlock.Text("응")], false));
        await Make(client, ToolRegistry.Of()).AskAsync("x");
        Assert.Empty(client.ToolsOffered[0]);
    }
}
