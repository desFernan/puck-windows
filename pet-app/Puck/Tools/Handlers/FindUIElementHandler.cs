using System.Text.Json;
using Puck.Interop;
using Puck.WindowSensing;

namespace Puck.Tools.Handlers;

/// 창 안에서 버튼 같은 것을 찾는다. 창 감지 쪽의 UIA 검색을 그대로 쓴다.
public sealed class FindUIElementHandler(Func<IReadOnlyList<WindowInfo>> windows) : IToolHandler
{
    public string Name => "find_ui_element";

    public static ToolSpec Spec => new()
    {
        Name = "find_ui_element",
        Description =
            "창 안에서 이름으로 UI 요소(버튼, 메뉴, 입력칸)를 찾는다. " +
            "찾은 것의 사각형을 돌려주므로 point_at이나 click_element에 그대로 넘길 수 있다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["query"] = ToolSpec.Param("string", "찾을 이름. 화면에 보이는 글자 그대로 쓰면 된다."),
            ["window_title"] = ToolSpec.Param("string", "어느 창에서 찾을지. 비우면 맨 앞 창."),
        },
        Required = ["query"],
        Approval = ToolApproval.NotRequired,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        var query = Args.String(arguments, "query");
        if (string.IsNullOrWhiteSpace(query)) return Task.FromResult("찾을 이름(query)이 필요합니다.");

        var title = Args.String(arguments, "window_title");
        var window = FindWindow(title);
        if (window is null) return Task.FromResult("그 창을 찾지 못했습니다.");

        var result = UIElementSearch.Find(window.Handle, query, limit: 5);

        if (result.Status == UIElementSearchStatus.BlockedByPrivilege)
            return Task.FromResult(
                $"\"{window.Title}\"은(는) 권한이 더 높은 창이라 들여다볼 수 없습니다(UIPI). " +
                "그 창을 조작하려면 Puck을 관리자 권한으로 실행해야 합니다.");

        if (result.Matches.Count == 0)
            return Task.FromResult(result.Truncated
                ? $"\"{query}\"을(를) 찾지 못했습니다. 다만 트리가 너무 커서 도중에 멈췄으니 없다고 단정할 수는 없습니다."
                : $"\"{query}\"에 맞는 요소가 없습니다.");

        var lines = result.Matches.Select(m =>
            $"\"{m.Name}\" [{m.ControlType.Replace("ControlType.", "")}] " +
            ToolFrame.Format(m.Bounds) +
            (m.IsEnabled ? "" : " (꺼져 있음)") +
            (m.IsOffscreen ? " (화면 밖)" : ""));

        return Task.FromResult(string.Join("\n", lines));
    }

    private WindowInfo? FindWindow(string? title)
    {
        var all = windows();
        if (!string.IsNullOrWhiteSpace(title))
            return all.FirstOrDefault(w => w.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) == true);

        var foreground = Win32.GetForegroundWindow();
        return all.FirstOrDefault(w => w.Handle == foreground) ?? all.FirstOrDefault();
    }
}
