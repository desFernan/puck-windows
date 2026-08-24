using Puck.Tools;

namespace Puck.Agent;

/// 모델에게 한 번 묻는 것. 인터페이스인 이유는 둘이다 — 루프를 네트워크 없이
/// 테스트하려고, 그리고 mac처럼 공급자를 하나 더(GPT) 붙일 자리를 남기려고.
public interface IAgentClient
{
    Task<AgentTurn> SendAsync(
        string systemPrompt,
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<ToolSpec> tools,
        CancellationToken cancellation);
}
