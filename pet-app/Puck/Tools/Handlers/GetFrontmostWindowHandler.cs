using System.Text.Json;
using Puck.Interop;
using Puck.WindowSensing;

namespace Puck.Tools.Handlers;

/// 사람이 지금 쓰고 있는 창. mac의 `get_frontmost_window`.
public sealed class GetFrontmostWindowHandler(Func<IReadOnlyList<WindowInfo>> windows) : IToolHandler
{
    public string Name => "get_frontmost_window";

    public static ToolSpec Spec => new()
    {
        Name = "get_frontmost_window",
        Description = "사람이 지금 보고 있는 맨 앞 창의 프로그램 이름, 제목, 화면 위 사각형을 돌려준다.",
        Properties = new Dictionary<string, JsonElement>(),
        Approval = ToolApproval.NotRequired,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        var foreground = Win32.GetForegroundWindow();
        var window = windows().FirstOrDefault(w => w.Handle == foreground) ?? windows().FirstOrDefault();

        if (window is null) return Task.FromResult("맨 앞 창을 찾지 못했습니다.");

        return Task.FromResult(
            $"{window.OwnerName ?? "(알 수 없음)"} — \"{window.Title ?? "(제목 없음)"}\"\n" +
            ToolFrame.Format(window.Frame));
    }
}
