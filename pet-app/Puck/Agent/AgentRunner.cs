using Puck.Diagnostics;
using Puck.Tools;

namespace Puck.Agent;

/// 한 턴 동안 무슨 일이 있었는지. 펫 연출과 (Phase 4의) 채팅 UI가 여기 붙는다.
public abstract record AgentEvent
{
    public sealed record Said(string Text) : AgentEvent;
    public sealed record UsingTool(string Name) : AgentEvent;
    public sealed record ToolDone(string Name, bool IsError) : AgentEvent;
    public sealed record Refused(string Name) : AgentEvent;
}

/// 대화 한 턴을 끝까지 돌린다.
///
/// 모델이 도구를 부르면 실행하고 결과를 붙여 다시 묻기를, 모델이 그만 부를
/// 때까지 반복한다. SDK에 이 루프를 대신 도는 헬퍼가 있지만 직접 들고 있는
/// 이유는 승인 게이트·도구별 타임아웃·실행 로그·펫 연출·취소를 이 안에서
/// 걸어야 하기 때문이다.
public sealed class AgentRunner(
    IAgentClient client,
    ToolRegistry tools,
    ToolApprovals approvals,
    Func<AgentConfiguration> configuration)
{
    /// 한 턴에 허용하는 도구 호출 왕복 수. 없으면 스스로를 부르며 도는 모델이
    /// 사람의 돈과 시간을 무한히 쓴다.
    public const int MaxToolRounds = 12;

    /// 기록에 남기는 메시지 수. 대화가 길어지면 매 요청이 그만큼 비싸지고
    /// 느려진다 — 지난 대화 전부를 매번 다시 올려 보내기 때문이다.
    public const int MaxHistoryMessages = 40;

    private readonly ToolExecutor _executor = new(tools.Handlers);

    /// 턴은 하나씩만 돈다. 사람이 답을 기다리는 동안 한 번 더 물으면 두 턴이
    /// 같은 기록을 서로 밟아, 짝이 안 맞는 대화가 만들어진다.
    private readonly SemaphoreSlim _oneTurnAtATime = new(1, 1);

    /// 대화 기록. 턴을 이어 부르면 앞의 맥락이 남는다.
    ///
    /// **끝난 턴만 들어 있다.** 도는 중인 턴은 사본 위에서 자라다가 끝날 때
    /// 한 번에 들어온다 — 아래 `RunTurnAsync` 참고.
    public List<AgentMessage> History { get; } = [];

    public event Action<AgentEvent>? Progress;

    /// 사람이 한 말에 답한다. 돌려주는 것은 사람에게 보여 줄 글.
    public async Task<string> AskAsync(string userText, CancellationToken cancellation = default)
    {
        var config = configuration();
        if (!config.IsUsable)
            return Say("API 키가 없습니다. %LOCALAPPDATA%\\Puck\\.env에 ANTHROPIC_API_KEY를 넣어 주세요.");

        await _oneTurnAtATime.WaitAsync(cancellation);
        try
        {
            return await RunTurnAsync(userText, config, cancellation);
        }
        finally
        {
            _oneTurnAtATime.Release();
        }
    }

    /// 한 턴은 **통째로 남거나 통째로 없던 일이 된다.**
    ///
    /// 기록을 진행하며 조금씩 채우면, 도중에 네트워크가 끊기거나 사람이
    /// 취소했을 때 답 없는 user 메시지나 결과가 짝지어지지 않은 `tool_use`가
    /// 남는다. API는 그런 대화를 통째로 거절하므로, 한 번 실패한 펫은 앱을
    /// 껐다 켜기 전까지 다시는 말하지 못한다. 그래서 사본 위에서 짓는다.
    private async Task<string> RunTurnAsync(string userText, AgentConfiguration config, CancellationToken cancellation)
    {
        var working = new List<AgentMessage>(History) { AgentMessage.FromUser(userText) };

        var specs = tools.Specs;
        var systemPrompt = specs.Count > 0 ? AgentPrompts.System : AgentPrompts.SystemWithoutTools;

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var turn = await client.SendAsync(systemPrompt, working, specs, cancellation);

            working.Add(new AgentMessage(AgentRole.Assistant, turn.Blocks));

            var said = turn.TextContent;
            if (!string.IsNullOrWhiteSpace(said)) Progress?.Invoke(new AgentEvent.Said(said));

            // 부름이 있으면 `stop_reason`이 무엇이든 답을 붙인다. 짝 없는
            // `tool_use`를 기록에 남기는 것이 곧 다음 요청의 400이다.
            var calls = turn.ToolUses.ToList();
            if (calls.Count == 0)
            {
                Commit(working);
                // 생각만 하고 말을 안 하고 끝내는 턴이 있다. 빈 글을 그대로
                // 올려 보내면 부르는 쪽에서 침묵과 구분되지 않는다.
                return string.IsNullOrWhiteSpace(said) ? "…" : said;
            }

            var results = new List<AgentBlock>();
            foreach (var call in calls)
            {
                cancellation.ThrowIfCancellationRequested();
                results.Add(await RunOneAsync(call, config, cancellation));
            }

            // 도구 결과는 **user 메시지 하나에 모아** 돌려준다. 나눠 보내면
            // 모델이 도구를 하나씩만 부르는 쪽으로 배운다.
            working.Add(new AgentMessage(AgentRole.User, results));
        }

        AppLogger.Warning("agent", "한 턴의 도구 호출 상한에 걸렸습니다",
            new Dictionary<string, object?> { ["rounds"] = MaxToolRounds });

        // 도중에 멈춘 왕복은 기록에 남기지 않는다. 도구 결과로 끝나는 기록
        // 뒤에 다음 질문을 붙이면 user 메시지가 연달아 두 개가 되고, 그
        // 열두 바퀴어치 결과는 이후 모든 요청에 계속 실려 나간다.
        const string stopped = "도구를 너무 여러 번 부르게 되어 여기서 멈췄습니다. 조금 더 좁혀서 다시 물어봐 주세요.";
        Commit([.. History, AgentMessage.FromUser(userText),
                new AgentMessage(AgentRole.Assistant, [new AgentBlock.Text(stopped)])]);
        return Say(stopped);
    }

    /// 펫이 하는 말은 **전부** 여기를 지난다 — 모델이 한 말이든, 우리가 대신
    /// 하는 말이든. 채팅 창이 한 곳만 보고 있으면 되게.
    private string Say(string text)
    {
        Progress?.Invoke(new AgentEvent.Said(text));
        return text;
    }

    private void Commit(List<AgentMessage> turn)
    {
        History.Clear();
        History.AddRange(turn);
        Trim(History);
    }

    /// 오래된 것부터 버리되 **사람의 새 질문 자리에서만 자른다.**
    ///
    /// 아무 데서나 자르면 결과가 짝지어지지 않은 `tool_use`가 맨 앞에 남거나
    /// 같은 역할이 연달아 오는 기록이 되고, API는 그런 대화를 통째로 거절한다.
    /// 자를 자리를 못 찾으면 그냥 둔다 — 비싼 대화가 망가진 대화보다 낫다.
    public static void Trim(List<AgentMessage> history)
    {
        if (history.Count <= MaxHistoryMessages) return;

        for (var cut = history.Count - MaxHistoryMessages; cut < history.Count; cut++)
        {
            if (!IsFreshQuestion(history[cut])) continue;
            history.RemoveRange(0, cut);
            return;
        }
    }

    /// 도구 결과가 아니라 사람이 새로 꺼낸 말. 대화는 언제나 여기서 시작한다.
    private static bool IsFreshQuestion(AgentMessage message)
        => message.Role == AgentRole.User && !message.Blocks.OfType<AgentBlock.ToolResult>().Any();

    private async Task<AgentBlock> RunOneAsync(
        AgentBlock.ToolUse call, AgentConfiguration config, CancellationToken cancellation)
    {
        var spec = tools.SpecFor(call.Name);
        if (spec is null)
            return new AgentBlock.ToolResult(call.Id, $"그런 도구는 없습니다: {call.Name}", IsError: true);

        var allowed = await approvals.IsAllowedAsync(spec, call.Input, config.Permissions, cancellation);
        if (!allowed)
        {
            Progress?.Invoke(new AgentEvent.Refused(call.Name));
            // 거절도 결과다. 모델은 이걸 읽고 다른 길을 찾거나 이유를 설명한다.
            return new AgentBlock.ToolResult(call.Id, $"사람이 {call.Name} 실행을 거절했습니다.", IsError: false);
        }

        Progress?.Invoke(new AgentEvent.UsingTool(call.Name));
        var outcome = await _executor.ExecuteAsync(call.Id, call.Name, call.Input, cancellation);
        Progress?.Invoke(new AgentEvent.ToolDone(call.Name, outcome.IsError));

        return new AgentBlock.ToolResult(outcome.ToolUseId, outcome.Content, outcome.IsError);
    }
}
