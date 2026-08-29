using System.Windows;
using WinForms = System.Windows.Forms;

namespace Puck.Movement;

/// 펫이 사는 좌표계. 가상 화면 물리 픽셀, 좌상단 원점, Y는 아래로.
///
/// macOS 원본에 있던 좌표 변환(AppKit 좌하단 ↔ Quartz 좌상단)은 여기 없다.
/// Win32가 이미 좌상단 원점이라 뒤집을 것이 없다.
public sealed record ScreenSpace
{
    public ScreenSpace(IReadOnlyList<Rect> screenBoundsList, IReadOnlyList<Rect> workingAreas,
                       IReadOnlyList<ScreenNotch?>? notches = null)
    {
        if (screenBoundsList.Count == 0)
            throw new ArgumentException("디스플레이가 하나도 없습니다", nameof(screenBoundsList));
        if (screenBoundsList.Count != workingAreas.Count)
            throw new ArgumentException("디스플레이 수와 작업 영역 수가 다릅니다", nameof(workingAreas));
        if (notches is not null && notches.Count != screenBoundsList.Count)
            throw new ArgumentException("디스플레이 수와 노치 수가 다릅니다", nameof(notches));

        ScreenBoundsList = screenBoundsList;
        WorkingAreas = workingAreas;
        Notches = notches ?? new ScreenNotch?[screenBoundsList.Count];
    }

    public IReadOnlyList<Rect> ScreenBoundsList { get; }
    public IReadOnlyList<Rect> WorkingAreas { get; }

    /// 디스플레이마다 하나씩, 없으면 null. ScreenBoundsList와 같은 순서다.
    ///
    /// 프레임마다 묻지 않고 여기 들고 있는 이유는 이것이 하드웨어가 바뀔 때
    /// 바뀌는 값이기 때문이다. 화면 구성이 다시 측정되는 순간(모니터 착탈,
    /// 해상도 변경)이 곧 이 답이 바뀔 수 있는 모든 순간이고, 사라진
    /// 디스플레이의 노치는 그냥 목록에 없다.
    public IReadOnlyList<ScreenNotch?> Notches { get; }

    public Rect Bounds => Union(ScreenBoundsList);
    public Rect RoamableArea => Union(WorkingAreas);

    /// 지금 연결된 디스플레이 구성. 전부 잠들어 목록이 비면 null —
    /// 호출자는 마지막으로 알던 ScreenSpace를 그대로 쓴다.
    ///
    /// <param name="withNotches">노치를 세계에 넣을 것인가. 노치 패널이
    /// 꺼져 있으면 false여야 한다 — 그려지지 않는 노치는 펫이 없는 것을
    /// 피해 돌고 없는 것 아래서 멈추게 만들 뿐이다. mac에서는 가상 노치가
    /// 전체화면에서만 천장에 닿지만 여기서는 늘 닿기 때문에 그 위험이 더
    /// 크다. ScreenNotch의 주석이 같은 이야기를 한다.</param>
    public static ScreenSpace? Current(bool withNotches = false)
    {
        var screens = WinForms.Screen.AllScreens;
        if (screens.Length == 0) return null;

        var bounds = screens.Select(s => ToRect(s.Bounds)).ToList();
        var working = screens.Select(s => ToRect(s.WorkingArea)).ToList();
        var notches = withNotches
            ? bounds.Select(b => ScreenNotch.Virtual(b, ScaleOf(b))).ToList()
            : null;
        return new ScreenSpace(bounds, working, notches);
    }

    /// 그 화면의 DPI 배율. 물어볼 수 없으면 1 — 노치가 조금 작게 그려지는
    /// 것이 노치가 없는 것보다 낫다.
    private static double ScaleOf(Rect screenBounds)
    {
        var middle = new Interop.Win32.POINT
        {
            X = (int)(screenBounds.Left + screenBounds.Width / 2),
            Y = (int)(screenBounds.Top + screenBounds.Height / 2),
        };
        var monitor = Interop.Win32.MonitorFromPoint(middle, Interop.Win32.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return 1;
        if (Interop.Win32.GetDpiForMonitor(monitor, Interop.Win32.MDT_EFFECTIVE_DPI, out var dpiX, out _) != 0)
            return 1;
        return dpiX == 0 ? 1 : dpiX / 96.0;
    }

    public Rect ScreenContaining(Point point)
    {
        foreach (var screen in ScreenBoundsList)
            if (screen.Contains(point))
                return screen;

        // 모니터 크기가 다르면 어느 디스플레이에도 속하지 않는 좌표가 실제로 생긴다.
        return ScreenBoundsList.MinBy(s => SquaredDistance(s, point));
    }

    /// 이 지점에서 곧장 아래로 내려가면 언젠가 화면 바닥을 만나는가.
    ///
    /// RoamableArea는 작업 영역들의 **경계 상자**라, 디스플레이가 계단처럼
    /// 놓이면 그 안에 어느 디스플레이에도 속하지 않는 빈 공간이 생긴다.
    /// 거기 있는 펫은 화면 밖이라 보이지 않는다.
    ///
    /// 위쪽은 보지 않는다 — 던져져서 화면 위로 솟은 펫은 아직 자기 화면
    /// 위에 있는 것이고, 내려오면 그 바닥에 착지한다.
    public bool HasGroundUnder(Point point)
        => WorkingAreas.Any(a => point.X >= a.Left && point.X <= a.Right && a.Bottom >= point.Y);

    /// 발밑에 화면이 없는 자리에 놓인 펫을 가장 가까운 실제 화면 위로 끌어온다.
    /// Y는 그 화면 바닥보다 아래로 두지 않고, X는 **그림이** 그 화면 안에
    /// 들어오도록 민다 — 발 위치만 맞추면 그림 절반이 빈 공간에 걸린다.
    public Point NearestStandablePoint(Point point, Rect visualBounds)
    {
        var area = WorkingAreas.MinBy(a => SquaredDistance(a, point));
        var contained = PetBounds.Contain(point, visualBounds, area);
        return new Point(contained.X, Math.Min(point.Y, area.Bottom));
    }

    /// 그 방향으로 더 갈 수 없을 때, 타고 올라갈 수 있는 턱이 있으면
    /// 올라선 뒤 설 자리. 없으면 null.
    ///
    /// 모니터가 계단처럼 놓이면 낮은 화면에서 높은 화면으로는 걸어서 갈 수
    /// 없다 — 내려가는 건 떨어지면 되지만 올라오는 힘이 없다. 그러면 펫은
    /// 낮은 모니터에 영영 갇힌다.
    public Point? LedgeBeyond(Point from, double directionX, Rect visualBounds)
    {
        // 진행 방향에 있으면서 바닥이 지금보다 위인 화면들.
        var candidates = WorkingAreas
            .Where(a => a.Bottom < from.Y && (directionX > 0 ? a.Left >= from.X : a.Right <= from.X))
            .OrderBy(a => directionX > 0 ? a.Left - from.X : from.X - a.Right)
            .ToList();

        if (candidates.Count == 0) return null;

        // 올라선 자리에서 그림이 통째로 그 화면 안에 들어와야 한다.
        var area = candidates[0];
        var standing = PetBounds.Contain(new Point(from.X, area.Bottom), visualBounds, area);
        return new Point(standing.X, area.Bottom);
    }

    /// 그 지점에서 곧장 떨어지면 닿는 바닥. Phase 2에서 창 윗면이
    /// 착지면으로 끼어들기 전까지는 언제나 화면 바닥이다.
    public double FloorY(Point point)
    {
        var index = IndexOfScreenContaining(point);
        return WorkingAreas[index].Bottom;
    }

    /// 그 지점이 속한 화면의 작업 영역. 말풍선처럼 "이 화면 안에" 두어야
    /// 하는 것들이 쓴다.
    public Rect WorkingAreaContaining(Point point) => WorkingAreas[IndexOfScreenContaining(point)];

    /// 그 지점이 속한 화면의 위쪽 끝.
    ///
    /// RoamableArea.Top(경계 상자)을 쓰면 안 된다. 세로 모니터가 위로 어긋나
    /// 붙어 있으면 경계 상자의 top이 주 모니터보다 한참 위라, 주 모니터에서
    /// 최대화된 창(윗변 y=0)조차 "머리 위 여유가 충분하다"로 판정된다 —
    /// 거기 세운 펫은 몸이 화면 위로 넘어가 보이지 않는다.
    public double CeilingY(Point point)
    {
        var index = IndexOfScreenContaining(point);
        return WorkingAreas[index].Top;
    }

    /// 그 작업 영역 위에 걸린 노치. 없으면 null.
    ///
    /// 겹침이 아니라 **가로로** 맞춘다. mac에서 이렇게 하는 이유는 메뉴
    /// 막대가 있을 때 하우징이 작업 영역보다 통째로 위에 있어서 겹침 검사가
    /// 있는 것도 못 찾기 때문이다. 여기서는 겹치기는 하지만 규칙은 같게
    /// 두었다 — 작업 표시줄을 화면 위쪽에 두면 mac과 똑같은 배치가 된다.
    ///
    /// 노치 하나가 아니라 목록인 이유: 디스플레이가 둘이면 펫이 지금 어느
    /// 화면의 천장을 기고 있느냐가 머리 위에 뭐가 있는지를 정한다.
    public ScreenNotch? NotchOver(Rect area)
    {
        for (var i = 0; i < WorkingAreas.Count; i++)
        {
            if (WorkingAreas[i] != area) continue;
            return Notches[i];
        }

        // 넘겨받은 영역이 우리 목록에 없다 — 화면 구성이 이 프레임 도중에
        // 바뀌었다는 뜻이다. 가로로 가장 잘 맞는 것을 준다.
        var centre = area.Left + area.Width / 2;
        for (var i = 0; i < WorkingAreas.Count; i++)
        {
            var a = WorkingAreas[i];
            if (centre >= a.Left && centre <= a.Right) return Notches[i];
        }
        return null;
    }

    /// `x`에서 그 영역의 천장. 노치 아래에서는 노치의 아랫변이다.
    ///
    /// 천장이 선이 아니라 x의 함수인 이유는 노치가 거기 매달려 있기
    /// 때문이다. "여기서 펫이 얼마나 높이 갈 수 있는가"를 묻는 모든 곳이
    /// 이렇게 묻는다.
    public double CeilingY(double x, Rect area)
        => NotchOver(area) is { } notch ? notch.Ceiling(x, area.Top) : area.Top;

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
