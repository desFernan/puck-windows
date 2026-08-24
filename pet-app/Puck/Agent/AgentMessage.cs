using System.Text.Json;

namespace Puck.Agent;

/// 대화 한 조각. SDK 타입을 앱 전체에 퍼뜨리지 않으려고 우리 모델을 따로 둔다 —
/// 모델 공급자를 바꾸거나 SDK가 올라갈 때 바뀌는 곳이 클라이언트 한 파일이면 된다.
public abstract record AgentBlock
{
    /// 사람이 읽는 글.
    public sealed record Text(string Value) : AgentBlock;

    /// 모델이 도구를 부르겠다고 한 것.
    public sealed record ToolUse(string Id, string Name, IReadOnlyDictionary<string, JsonElement> Input) : AgentBlock;

    /// 그 부름에 대한 답. `ToolUse.Id`와 짝이 맞아야 한다.
    public sealed record ToolResult(string ToolUseId, string Content, bool IsError = false) : AgentBlock;

    /// 모델의 생각. 같은 모델로 대화를 이어갈 때 **서명을 그대로** 돌려보내야
    /// 해서 들고 다닌다 — 손대면 API가 거절한다.
    public sealed record Thinking(string Value, string Signature) : AgentBlock;
}

public enum AgentRole { User, Assistant }

public sealed record AgentMessage(AgentRole Role, IReadOnlyList<AgentBlock> Blocks)
{
    public static AgentMessage FromUser(string text) =>
        new(AgentRole.User, [new AgentBlock.Text(text)]);

    /// 이 메시지에서 사람에게 보여 줄 글만.
    public string TextContent =>
        string.Join("\n", Blocks.OfType<AgentBlock.Text>().Select(b => b.Value));
}

/// 모델이 한 번 답한 결과.
public sealed record AgentTurn(
    IReadOnlyList<AgentBlock> Blocks,
    /// 도구를 부르려고 멈춘 것인가. 루프가 이걸 보고 한 바퀴 더 돈다.
    bool WantsToolUse)
{
    public IEnumerable<AgentBlock.ToolUse> ToolUses => Blocks.OfType<AgentBlock.ToolUse>();

    public string TextContent =>
        string.Join("\n", Blocks.OfType<AgentBlock.Text>().Select(b => b.Value));
}
