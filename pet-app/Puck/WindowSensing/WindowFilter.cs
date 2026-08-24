using System.Windows;

namespace Puck.WindowSensing;

/// 열거된 창 목록에서 펫이 상대할 수 있는 것만 남긴다.
///
/// 창을 *가져오는* 코드(WindowListSource)는 실제 화면에 의존해 테스트할 수
/// 없으므로 얇게 두고, 판단은 전부 여기 순수 함수에 모은다. mac 원본이
/// `WindowListWatcher.filter`를 따로 뽑아 둔 것과 같은 이유다.
public static class WindowFilter
{
    /// 펫보다 작은 자투리 창은 발판이 아니라 방해물이다.
    public static Size DefaultMinimumSize { get; } = new(40, 40);

    /// **Z 순서(앞→뒤)를 그대로 보존한다.** 착지면 판정이 "앞 창이 이 지점을
    /// 가리고 있나"를 그 순서로 보기 때문에, 여기서 순서가 흐트러지면 펫이
    /// 보이지도 않는 창의 윗변에 선다.
    public static IReadOnlyList<WindowInfo> Keep(
        IReadOnlyList<WindowInfo> windows, int selfProcessId, Size minimumSize)
    {
        var kept = new List<WindowInfo>(windows.Count);
        foreach (var window in windows)
        {
            if (window.ProcessId == selfProcessId) continue;
            if (window.IsCloaked) continue;
            if (window.IsToolWindow) continue;
            if (window.Frame.Width < minimumSize.Width) continue;
            if (window.Frame.Height < minimumSize.Height) continue;
            kept.Add(window);
        }
        return kept;
    }
}
