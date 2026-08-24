using System.Text.Json;
using System.Windows;
using Puck.Interop;
using Puck.WindowSensing;

namespace Puck.Tools.Handlers;

/// 인자 꺼내기의 잔손. 모델이 보내는 JSON은 스키마를 대체로 지키지만
/// 언제나는 아니다 — 없으면 없다고 말하지 터지지 않는다.
internal static class Args
{
    public static string? String(IReadOnlyDictionary<string, JsonElement> args, string key)
        => args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static double? Number(IReadOnlyDictionary<string, JsonElement> args, string key)
        => args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    public static int? Int(IReadOnlyDictionary<string, JsonElement> args, string key)
        => (int?)Number(args, key);

    /// `{x, y}` 또는 `{left, top, width, height}` 어느 쪽으로 와도 한 점으로.
    public static Point? PointFrom(IReadOnlyDictionary<string, JsonElement> args, string key)
    {
        if (!args.TryGetValue(key, out var v) || v.ValueKind != JsonValueKind.Object) return null;

        double? Get(string name) => v.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetDouble() : null;

        if (Get("x") is { } x && Get("y") is { } y) return new Point(x, y);

        // 사각형이 오면 그 한가운데를 가리킨다 — find_ui_element가 돌려주는
        // 모양이 사각형이고, 모델은 그걸 그대로 넘긴다.
        if (Get("left") is { } l && Get("top") is { } t &&
            Get("width") is { } w && Get("height") is { } h)
            return new Point(l + w / 2, t + h / 2);

        return null;
    }
}

/// 지금 창을 가진 프로세스들. mac의 `list_running_apps`.
public sealed class ListRunningAppsHandler(Func<IReadOnlyList<WindowInfo>> windows) : IToolHandler
{
    public string Name => "list_running_apps";

    public static ToolSpec Spec => new()
    {
        Name = "list_running_apps",
        Description = "지금 화면에 창을 띄우고 있는 프로그램들의 이름과 창 제목을 돌려준다.",
        Properties = new Dictionary<string, JsonElement>(),
        Approval = ToolApproval.NotRequired,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        var apps = windows()
            .GroupBy(w => w.OwnerName ?? "(알 수 없음)")
            .Select(g => $"{g.Key}: " + string.Join(" | ", g.Select(w => w.Title ?? "(제목 없음)").Take(4)))
            .ToList();

        return Task.FromResult(apps.Count == 0 ? "창을 가진 프로그램이 없습니다." : string.Join("\n", apps));
    }
}

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
            $"사각형: left={window.Frame.Left:0} top={window.Frame.Top:0} " +
            $"width={window.Frame.Width:0} height={window.Frame.Height:0}");
    }
}

/// 창 안에서 버튼 같은 것을 찾는다. Phase 2의 UIA 검색을 그대로 쓴다.
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
            $"frame={{left:{m.Bounds.Left:0}, top:{m.Bounds.Top:0}, width:{m.Bounds.Width:0}, height:{m.Bounds.Height:0}}}" +
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
