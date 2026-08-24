using System.Windows;
using System.Windows.Automation;
using Puck.Diagnostics;

namespace Puck.WindowSensing;

/// 창 안의 UI 요소를 UIA로 훑는다. mac의 AXUIElement 자리다.
///
/// 트리 순회는 실제 창에 의존해 테스트할 수 없으므로 얇게 두고, "무엇이
/// 질의에 맞는가"는 전부 `UIElementMatch`의 순수 함수에 있다.
public static class UIElementSearch
{
    /// 훑을 노드 수의 상한. Electron 앱 하나가 수만 개를 낸다 — 상한이 없으면
    /// 도구 하나가 몇 초씩 UI 스레드를 잡는다.
    public const int MaxNodes = 2000;

    /// 트리 깊이 상한. 깊은 쪽에 있는 것은 사람도 눈으로 못 찾는다.
    public const int MaxDepth = 24;

    public static UIElementSearchResult Find(IntPtr windowHandle, string query, int limit = 5)
    {
        var (status, elements, truncated) = Collect(windowHandle);
        if (status != UIElementSearchStatus.Ok)
            return new UIElementSearchResult(status, [], truncated);

        return new UIElementSearchResult(
            UIElementSearchStatus.Ok, UIElementMatch.Best(elements, query, limit), truncated);
    }

    /// 그 창의 요소들을 트리 순서(사람 눈이 훑는 순서)대로.
    public static (UIElementSearchStatus Status, IReadOnlyList<UIElementInfo> Elements, bool Truncated)
        Collect(IntPtr windowHandle)
    {
        AutomationElement root;
        try
        {
            root = AutomationElement.FromHandle(windowHandle);
        }
        catch (Exception ex)
        {
            // UIA는 창이 사라졌을 때도, 권한이 더 높아 들여다볼 수 없을 때도
            // 같은 모양으로 실패한다. 창이 아직 살아 있으면 권한 쪽이다.
            var status = Interop.Win32.IsWindowVisible(windowHandle)
                ? UIElementSearchStatus.BlockedByPrivilege
                : UIElementSearchStatus.WindowNotFound;

            AppLogger.Warning("uia", "UI 요소 트리를 열지 못했습니다",
                new Dictionary<string, object?> { ["reason"] = status.ToString(), ["error"] = ex.Message });
            return (status, [], false);
        }

        var found = new List<UIElementInfo>();
        var visited = 0;
        var truncated = false;

        void Walk(AutomationElement element, int depth)
        {
            if (truncated) return;
            if (visited++ >= MaxNodes) { truncated = true; return; }

            var info = Describe(element);
            if (info is not null) found.Add(info);

            if (depth >= MaxDepth) return;

            try
            {
                var walker = TreeWalker.ControlViewWalker;
                for (var child = walker.GetFirstChild(element);
                     child is not null;
                     child = walker.GetNextSibling(child))
                {
                    Walk(child, depth + 1);
                    if (truncated) return;
                }
            }
            catch (ElementNotAvailableException)
            {
                // 훑는 사이에 그 부분이 사라졌다. 지금까지 모은 것은 유효하다.
            }
        }

        try
        {
            Walk(root, 0);
        }
        catch (Exception ex)
        {
            AppLogger.Warning("uia", "UI 요소 트리를 훑다 멈췄습니다",
                new Dictionary<string, object?> { ["error"] = ex.Message, ["visited"] = visited });
        }

        return (UIElementSearchStatus.Ok, found, truncated);
    }

    /// 요소 하나를 우리 모델로. 읽지 못하는 요소는 null — 순회가 멈추면 안 된다.
    public static UIElementInfo? Describe(AutomationElement element)
    {
        try
        {
            var current = element.Current;
            return new UIElementInfo(
                Name: string.IsNullOrWhiteSpace(current.Name) ? null : current.Name,
                ControlType: current.ControlType?.ProgrammaticName ?? "Unknown",
                Bounds: ToRect(current.BoundingRectangle),
                IsEnabled: current.IsEnabled,
                IsOffscreen: current.IsOffscreen,
                IsInvokable: element.TryGetCurrentPattern(InvokePattern.Pattern, out _));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Rect ToRect(System.Windows.Rect r)
        => double.IsInfinity(r.Width) || double.IsInfinity(r.Height) || r.IsEmpty
            ? Rect.Empty
            : r;
}
