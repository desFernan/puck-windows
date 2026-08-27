using System.Text.Json;
using Puck.Agent;

namespace PuckTests.Agent;

/// 다듬는 규칙은 러너를 통해서는 예순 턴을 돌려야 닿는 자리다.
public class AgentConversationTests
{
    private static AgentBlock.ToolUse Call(string id, string name)
        => new(id, name, new Dictionary<string, JsonElement>());

    private static List<AgentMessage> Exchange(string question) =>
    [
        AgentMessage.FromUser(question),
        new AgentMessage(AgentRole.Assistant, [Call("t", "peek")]),
        new AgentMessage(AgentRole.User, [new AgentBlock.ToolResult("t", "ok")]),
        new AgentMessage(AgentRole.Assistant, [new AgentBlock.Text("답")]),
    ];

    [Fact]
    public void AShortConversationIsLeftAlone()
    {
        var history = Exchange("하나").Concat(Exchange("둘")).ToList();
        var before = history.ToList();

        AgentConversation.Trim(history);

        Assert.Equal(before, history);
    }

    [Fact]
    public void ALongConversationIsCutAtAQuestionNotInTheMiddleOfToolResults()
    {
        // 도구 결과 한가운데를 자르면 짝 없는 tool_use가 맨 앞에 남는다.
        var history = Enumerable.Range(0, 20).SelectMany(i => Exchange($"질문 {i}")).ToList();

        AgentConversation.Trim(history);

        Assert.True(history.Count <= AgentConversation.MaxMessages);
        Assert.Equal(AgentRole.User, history[0].Role);
        Assert.DoesNotContain(history[0].Blocks, b => b is AgentBlock.ToolResult);

        // 남은 것 안에서 모든 부름은 짝이 있다.
        var uses = history.SelectMany(m => m.Blocks).OfType<AgentBlock.ToolUse>().Select(u => u.Id);
        var results = history.SelectMany(m => m.Blocks).OfType<AgentBlock.ToolResult>().Select(r => r.ToolUseId);
        Assert.Equal(uses.Count(), results.Count());
    }

    [Fact]
    public void AConversationWithNowhereSafeToCutIsLeftWhole()
    {
        // 한 번 묻고 도구만 계속 오간 대화. 새 질문이 맨 앞 하나뿐이라
        // 안전하게 자를 자리가 없다 — 그럴 땐 그냥 둔다.
        var history = new List<AgentMessage> { AgentMessage.FromUser("한 번만 묻고") };
        for (var i = 0; i < AgentConversation.MaxMessages; i++)
        {
            history.Add(new AgentMessage(AgentRole.Assistant, [Call($"t{i}", "peek")]));
            history.Add(new AgentMessage(AgentRole.User, [new AgentBlock.ToolResult($"t{i}", "ok")]));
        }

        var count = history.Count;
        AgentConversation.Trim(history);

        Assert.Equal(count, history.Count);
    }

    [Fact]
    public void AFinishedTurnGoesInWhole()
    {
        var conversation = new AgentConversation();
        var turn = conversation.Branch(AgentMessage.FromUser("안녕"));
        turn.Add(new AgentMessage(AgentRole.Assistant, [new AgentBlock.Text("안녕!")]));

        conversation.Commit(turn);

        Assert.Equal(2, conversation.Messages.Count);
    }

    [Fact]
    public void ABranchDoesNotTouchWhatIsAlreadyThere()
    {
        // 도중에 끊긴 턴이 기록을 건드리면 다음 요청이 통째로 거절된다.
        var conversation = new AgentConversation();
        var abandoned = conversation.Branch(AgentMessage.FromUser("안녕"));
        abandoned.Add(new AgentMessage(AgentRole.Assistant, [Call("t", "peek")]));

        Assert.Empty(conversation.Messages);
    }
}
