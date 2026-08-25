using System.Text.Json;
using System.Windows;
using Puck.Pointing;

namespace Puck.Tools.Handlers;

/// 실제로 누른다. 승인이 필요한 도구다.
public sealed class ClickElementHandler(Func<Rect> virtualScreen) : IToolHandler
{
    public string Name => "click_element";

    public static ToolSpec Spec => new()
    {
        Name = "click_element",
        Description =
            "그 자리를 실제로 왼쪽 클릭한다. find_ui_element가 준 frame을 그대로 넘겨도 된다. " +
            "사람 대신 누르는 것이므로 되돌릴 수 없는 일에는 쓰지 말고, point_at으로 알려 주는 편이 낫다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["frame"] = ToolFrame.PointOrRectParam("누를 곳. {x,y} 또는 {left,top,width,height}."),
        },
        Required = ["frame"],
        Approval = ToolApproval.Required,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        if (Args.PointFrom(arguments, "frame") is not { } target)
            return Task.FromResult("누를 곳(frame)이 필요합니다.");

        var ok = SyntheticClick.Click(target, virtualScreen());

        // 실패는 조용하다. 눌렀는데 아무 일도 없는 것과 구분되지 않으므로
        // 가능성을 말해 준다.
        return Task.FromResult(ok
            ? $"({target.X:0}, {target.Y:0})을(를) 클릭했습니다."
            : $"({target.X:0}, {target.Y:0}) 클릭이 전달되지 않았습니다. " +
              "권한이 더 높은 창이면 입력이 차단됩니다(UIPI).");
    }
}
