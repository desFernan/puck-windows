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

    private readonly ToolExecutor _executor = new(tools.Handlers);

    /// 대화 기록. 턴을 이어 부르면 앞의 맥락이 남는다.
    public List<AgentMessage> History { get; } = [];

    public event Action<AgentEvent>? Progress;

    /// 사람이 한 말에 답한다. 돌려주는 것은 사람에게 보여 줄 글.
    public async Task<string> AskAsync(string userText, CancellationToken cancellation = default)
    {
        var config = configuration();
        if (!config.IsUsable)
            return "API 키가 없습니다. %LOCALAPPDATA%\\Puck\\.env에 ANTHROPIC_API_KEY를 넣어 주세요.";

        History.Add(AgentMessage.FromUser(userText));

        var specs = tools.Specs;
        var systemPrompt = specs.Count > 0 ? AgentPrompts.System : AgentPrompts.SystemWithoutTools;

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var turn = await client.SendAsync(systemPrompt, History, specs, cancellation);

            History.Add(new AgentMessage(AgentRole.Assistant, turn.Blocks));

            var said = turn.TextContent;
            if (!string.IsNullOrWhiteSpace(said)) Progress?.Invoke(new AgentEvent.Said(said));

            if (!turn.WantsToolUse) return said;

            var results = new List<AgentBlock>();
            foreach (var call in turn.ToolUses)
            {
                cancellation.ThrowIfCancellationRequested();
                results.Add(await RunOneAsync(call, config, cancellation));
            }

            // 도구 결과는 **user 메시지 하나에 모아** 돌려준다. 나눠 보내면
            // 모델이 도구를 하나씩만 부르는 쪽으로 배운다.
            History.Add(new AgentMessage(AgentRole.User, results));
        }

        AppLogger.Warning("agent", "한 턴의 도구 호출 상한에 걸렸습니다",
            new Dictionary<string, object?> { ["rounds"] = MaxToolRounds });
        return "도구를 너무 여러 번 부르게 되어 여기서 멈췄습니다. 조금 더 좁혀서 다시 물어봐 주세요.";
    }

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
