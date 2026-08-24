using System.Windows;
using Puck.Tools.Handlers;
using Puck.WindowSensing;

namespace Puck.Tools;

/// 펫이 할 수 있는 일의 전부. 모델에게 보여 줄 목록과 실제로 돌릴 핸들러가
/// **같은 곳에서** 나온다 — 한쪽만 아는 도구가 생기면 모델이 부르는데
/// 실행할 것이 없다.
public sealed class ToolRegistry
{
    private readonly Dictionary<string, (ToolSpec Spec, IToolHandler Handler)> _tools;

    private ToolRegistry(IEnumerable<(ToolSpec, IToolHandler)> tools)
        => _tools = tools.ToDictionary(t => t.Item1.Name, t => (t.Item1, t.Item2), StringComparer.Ordinal);

    public IReadOnlyList<ToolSpec> Specs => _tools.Values.Select(t => t.Spec).ToList();

    public IReadOnlyDictionary<string, IToolHandler> Handlers =>
        _tools.ToDictionary(t => t.Key, t => t.Value.Handler, StringComparer.Ordinal);

    public ToolSpec? SpecFor(string name) => _tools.TryGetValue(name, out var t) ? t.Spec : null;

    /// 펫이 들고 다니는 기본 도구들. 창을 보는 것들은 워처에서, 클릭은
    /// 화면 좌표에서, 가리키기는 펫 자신에게서 온다.
    public static ToolRegistry CreateDefault(
        Func<IReadOnlyList<WindowInfo>> windows,
        Func<Rect> virtualScreen,
        Action<Point> pointAt)
        => new(
        [
            (ListRunningAppsHandler.Spec, new ListRunningAppsHandler(windows)),
            (GetFrontmostWindowHandler.Spec, new GetFrontmostWindowHandler(windows)),
            (FindUIElementHandler.Spec, new FindUIElementHandler(windows)),
            (PointAtHandler.Spec, new PointAtHandler(pointAt)),
            (CaptureScreenHandler.Spec, new CaptureScreenHandler(virtualScreen)),
            (LaunchAppHandler.Spec, new LaunchAppHandler()),
            (ClickElementHandler.Spec, new ClickElementHandler(virtualScreen)),
            (RunPowerShellHandler.ShellSpec, new RunPowerShellHandler("run_shell", "command")),
            (RunPowerShellHandler.ScriptSpec, new RunPowerShellHandler("run_powershell", "script")),
        ]);

    /// 테스트가 자기 도구만 들고 돌 수 있게.
    public static ToolRegistry Of(params (ToolSpec Spec, IToolHandler Handler)[] tools) => new(tools);
}
