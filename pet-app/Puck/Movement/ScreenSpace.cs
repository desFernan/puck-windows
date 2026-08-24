using System.Windows;
using WinForms = System.Windows.Forms;

namespace Puck.Movement;

/// 펫이 사는 좌표계. 가상 화면 물리 픽셀, 좌상단 원점, Y는 아래로.
///
/// macOS 원본에 있던 좌표 변환(AppKit 좌하단 ↔ Quartz 좌상단)은 여기 없다.
/// Win32가 이미 좌상단 원점이라 뒤집을 것이 없다.
public sealed record ScreenSpace
{
    public ScreenSpace(IReadOnlyList<Rect> screenBoundsList, IReadOnlyList<Rect> workingAreas)
    {
        if (screenBoundsList.Count == 0)
            throw new ArgumentException("디스플레이가 하나도 없습니다", nameof(screenBoundsList));
        if (screenBoundsList.Count != workingAreas.Count)
            throw new ArgumentException("디스플레이 수와 작업 영역 수가 다릅니다", nameof(workingAreas));

        ScreenBoundsList = screenBoundsList;
        WorkingAreas = workingAreas;
    }

    public IReadOnlyList<Rect> ScreenBoundsList { get; }
    public IReadOnlyList<Rect> WorkingAreas { get; }

    public Rect Bounds => Union(ScreenBoundsList);
    public Rect RoamableArea => Union(WorkingAreas);

    /// 지금 연결된 디스플레이 구성. 전부 잠들어 목록이 비면 null —
    /// 호출자는 마지막으로 알던 ScreenSpace를 그대로 쓴다.
    public static ScreenSpace? Current()
    {
        var screens = WinForms.Screen.AllScreens;
        if (screens.Length == 0) return null;

        var bounds = screens.Select(s => ToRect(s.Bounds)).ToList();
        var working = screens.Select(s => ToRect(s.WorkingArea)).ToList();
        return new ScreenSpace(bounds, working);
    }

    public Rect ScreenContaining(Point point)
    {
        foreach (var screen in ScreenBoundsList)
            if (screen.Contains(point))
                return screen;

        // 모니터 크기가 다르면 어느 디스플레이에도 속하지 않는 좌표가 실제로 생긴다.
        return ScreenBoundsList.MinBy(s => SquaredDistance(s, point));
    }

    /// 그 지점에서 곧장 떨어지면 닿는 바닥. Phase 2에서 창 윗면이
    /// 착지면으로 끼어들기 전까지는 언제나 화면 바닥이다.
    public double FloorY(Point point)
    {
        var index = IndexOfScreenContaining(point);
        return WorkingAreas[index].Bottom;
    }

    private int IndexOfScreenContaining(Point point)
    {
        for (var i = 0; i < ScreenBoundsList.Count; i++)
            if (ScreenBoundsList[i].Contains(point))
                return i;

        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < ScreenBoundsList.Count; i++)
        {
            var distance = SquaredDistance(ScreenBoundsList[i], point);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }
        return best;
    }

    private static double SquaredDistance(Rect rect, Point point)
    {
        var dx = Math.Max(Math.Max(rect.Left - point.X, 0), point.X - rect.Right);
        var dy = Math.Max(Math.Max(rect.Top - point.Y, 0), point.Y - rect.Bottom);
        return dx * dx + dy * dy;
    }

    private static Rect Union(IReadOnlyList<Rect> rects)
    {
        var union = rects[0];
        for (var i = 1; i < rects.Count; i++) union.Union(rects[i]);
        return union;
    }

    private static Rect ToRect(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}
