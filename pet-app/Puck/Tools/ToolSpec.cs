using System.Text.Json;

namespace Puck.Tools;

/// 이 도구를 쓰기 전에 사람에게 물어야 하는가. mac의 `ToolRegistry.Approval` 그대로다.
///
/// 셋으로 나뉜 이유는 *물을지*가 아니라 *누가 정하는지*가 다르기 때문이다.
public enum ToolApproval
{
    /// 묻지 않는다. 읽기만 하거나, 되돌릴 수 있는 것들.
    NotRequired,
    /// 언제나 묻는다.
    Required,
    /// 허용 목록에 있는 명령이면 묻지 않고, 그 밖이면 묻는다.
    RequiredUnlessAllowlisted,
}

/// 모델에게 보여 줄 도구 하나의 생김새.
public sealed record ToolSpec
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// JSON Schema의 `properties`.
    public required IReadOnlyDictionary<string, JsonElement> Properties { get; init; }

    public IReadOnlyList<string> Required { get; init; } = [];

    public ToolApproval Approval { get; init; } = ToolApproval.Required;

    /// 스키마 한 조각을 만드는 잔손. 도구마다 같은 JSON을 손으로 쓰면
    /// 오타가 런타임에야 드러난다.
    public static JsonElement Param(string type, string description)
        => JsonSerializer.SerializeToElement(new { type, description });

    public static JsonElement ObjectParam(string description, object properties)
        => JsonSerializer.SerializeToElement(new { type = "object", description, properties });
}

/// 도구가 실제로 하는 일.
public interface IToolHandler
{
    string Name { get; }

    /// 인자를 받아 결과 문자열을 돌려준다. **던져도 된다** — 위에서 잡아
    /// 오류 결과로 바꾼다. 던지는 편이 오류를 문자열로 꾸며 내는 것보다 정직하다.
    Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation);
}

/// 도구 실행의 결과. 오류도 결과다 — 모델은 실패를 읽고 다음 수를 정해야 한다.
public sealed record ToolOutcome(string ToolUseId, string Content, bool IsError);
