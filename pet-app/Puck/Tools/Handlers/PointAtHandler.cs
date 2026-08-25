using System.Text.Json;
using System.Windows;

namespace Puck.Tools.Handlers;

/// 펫이 그 자리를 가리킨다. 사람에게 "여기요"라고 말하는 방법이고,
/// 클릭할 수 없는 창(권한이 더 높은 창)에서 할 수 있는 유일한 일이기도 하다.
public sealed class PointAtHandler(Action<Point> pointAt) : IToolHandler
{
    public string Name => "point_at";

    public static ToolSpec Spec => new()
    {
        Name = "point_at",
        Description =
            "펫을 그 자리로 보내 가리킨다. 사람이 직접 눌러야 하는 것을 알려 줄 때 쓴다. " +
            "find_ui_element가 준 frame을 그대로 넘겨도 되고 {x, y}를 줘도 된다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["frame"] = ToolFrame.PointOrRectParam("가리킬 곳. {x,y} 또는 {left,top,width,height}."),
        },
        Required = ["frame"],
        Approval = ToolApproval.NotRequired,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        if (Args.PointFrom(arguments, "frame") is not { } target)
            return Task.FromResult("가리킬 곳(frame)이 필요합니다. {x,y} 또는 {left,top,width,height}.");

        pointAt(target);
        return Task.FromResult($"({target.X:0}, {target.Y:0})을(를) 가리켰습니다.");
    }
}
