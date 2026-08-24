using System.Windows;
using Puck.WindowSensing;

namespace Puck.Movement;

/// 펫이 어느 창 위에 서 있고 어느 창에 막혔는가. 창 목록 위의 순수 조회라
/// 위의 상태들은 기하를 몰라도 된다.
///
/// 전부 펫의 좌표 공간이다 — 가상 화면 물리 픽셀, 좌상단 원점, Y는 아래로.
/// 그래서 창의 윗변은 `Frame.Top`이다. puck-mac의 `WindowSupport`를 옮긴 것.
public static class WindowSupport
{
    /// 윗변에서 이만큼 떨어진 것까지는 그 위에 서 있는 것으로 친다.
    public const double FootTolerance = 4;

    /// 옆면에서 이만큼 가까우면 부딪힌 것으로 친다.
    public const double EdgeTolerance = 4;

    /// 발끝이 아니라 **몸통 가운데**로 판정한다. 펫은 모든 창 위에 그려지므로
    /// 중요한 건 몸이 창 내용 위에 있는가다. 발만 보면 창의 아래 모서리가
    /// 바닥에서 조금 떠 있을 때 "안 덮였다"가 나온다.
    public static WindowInfo? CoveringWindow(Point groundPoint, double petHeight, IReadOnlyList<WindowInfo> windows)
    {
        var middle = new Point(groundPoint.X, groundPoint.Y - petHeight / 2);
        return windows.FirstOrDefault(w => w.Frame.Contains(middle));
    }

    /// 그 창의 윗변에서 펫이 설 자리. 지금 있는 곳에 최대한 가깝게, 다만
    /// 몸 전체가 윗변 위에 올라오도록 반 폭만큼 안쪽으로 민다.
    ///
    /// 거기 서면 머리가 화면 위로 잘리는 창(거의 전체화면)이면 null.
    public static Point? PerchTarget(WindowInfo window, Point from,
                                     double roamableTop, double avatarHeight, double petHalfWidth)
    {
        if (window.Frame.Top - roamableTop < avatarHeight) return null;

        var left = window.Frame.Left + petHalfWidth;
        var right = window.Frame.Right - petHalfWidth;
        if (left > right) return null;

        return new Point(Math.Clamp(from.X, left, right), window.Frame.Top);
    }

    /// 지금 발밑을 받치고 있는 맨 앞 창.
    public static WindowInfo? SupportingWindow(Point position, IReadOnlyList<WindowInfo> windows)
        => windows.FirstOrDefault(w =>
            position.X >= w.Frame.Left && position.X <= w.Frame.Right &&
            Math.Abs(position.Y - w.Frame.Top) <= FootTolerance);

    /// 지금 매달려 있는 창. 오르는 매 프레임 다시 묻는다 — 창은 언제든 닫힌다.
    ///
    /// 그 높이에서 **실제로 보이는** 옆면만 벽으로 친다. 안 그러면 뒤에 겹친
    /// 창들의 가려진 모서리도 벽이 되어, 밖에서 보기에 펫이 허공을 올라간다.
    public static WindowInfo? WindowBeingClimbed(Point position, IReadOnlyList<WindowInfo> windows,
                                                 ISet<IntPtr>? excluding = null)
    {
        for (var i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            if (excluding is not null && excluding.Contains(window.Handle)) continue;
            if (position.Y < window.Frame.Top || position.Y > window.Frame.Bottom) continue;

            var holds = new[] { window.Frame.Left, window.Frame.Right }
                .Where(edge => Math.Abs(position.X - edge) <= EdgeTolerance)
                .Any(edge => IsEdgeVisible(edge, position.Y, i, windows));

            if (holds) return window;
        }
        return null;
    }

    /// 사람이 실제로 쓰고 있는 창 — 맨 앞 앱의 맨 앞 창. 목록이 앞에서 뒤
    /// 순서이므로 첫 일치가 맨 위다.
    ///
    /// 프로세스 ID를 인자로 받는 이유는 "포커스된 창 위로는 올라가지 않기"
    /// 설정을 실제 포그라운드 앱 없이도 테스트하기 위해서다.
    public static WindowInfo? FocusedWindow(int? processId, IReadOnlyList<WindowInfo> windows)
        => processId is null ? null : windows.FirstOrDefault(w => w.ProcessId == processId);

    /// `position`에서 `target` 쪽으로 걷다가 처음 부딪히는 창 옆면.
    /// Walk가 Climb으로 넘어가는 방아쇠다.
    public static WindowInfo? BlockingWindow(Point position, Point target, IReadOnlyList<WindowInfo> windows,
                                             double roamableTop = double.NegativeInfinity,
                                             double avatarHeight = 0,
                                             ISet<IntPtr>? excluding = null)
    {
        var goingRight = target.X > position.X;
        WindowInfo? best = null;
        var bestEdge = 0.0;

        foreach (var (index, window) in Climbable(position, windows, roamableTop, avatarHeight, excluding))
        {
            var edge = goingRight ? window.Frame.Left : window.Frame.Right;

            var onTheWay = goingRight
                ? edge > position.X && edge <= target.X
                : edge < position.X && edge >= target.X;
            if (!onTheWay) continue;

            // 아무도 볼 수 없는 모서리는 어떻게 도달했든 벽이 아니다.
            if (!IsEdgeVisible(edge, position.Y, index, windows)) continue;

            if (best is null || (goingRight ? edge < bestEdge : edge > bestEdge))
            {
                best = window;
                bestEdge = edge;
            }
        }

        return best;
    }

    /// 가장 가까운 오를 수 있는 창의 옆면에 붙는 지점. 이게 없으면 펫이
    /// 바닥에만 붙어 지내고, 창을 오르는 일은 무작위 걷기가 우연히 모서리를
    /// 넘을 때만 일어난다.
    ///
    /// 오를 것이 없으면 null — 호출자는 그냥 평소처럼 배회하면 된다.
    public static Point? NearestClimbTarget(Point from, IReadOnlyList<WindowInfo> windows,
                                            double roamableTop = double.NegativeInfinity,
                                            double avatarHeight = 0,
                                            ISet<IntPtr>? excluding = null)
    {
        // 모서리에 정확히 맞춰 걸으면 어떤 프레임에서는 반올림 때문에 조금
        // 못 미쳐서 오르기가 시작되지 않는다. 조금 넘겨서 겨눈다.
        const double overshoot = 4;

        double? nearest = null;

        foreach (var (index, window) in Climbable(from, windows, roamableTop, avatarHeight, excluding))
        {
            // 잡을 수 있는 모서리만. 오르기는 오른쪽으로 걷다가 창의 왼쪽
            // 모서리를 넘거나 그 반대로 일어난다 — 펫의 반대편에 있는 모서리는
            // 걸어가서 그냥 지나칠 뿐이다.
            var candidates = new List<double>(2);
            if (from.X < window.Frame.Left) candidates.Add(window.Frame.Left);
            if (from.X > window.Frame.Right) candidates.Add(window.Frame.Right);

            foreach (var edge in candidates)
            {
                if (Math.Abs(edge - from.X) <= EdgeTolerance) continue;   // 이미 거기 서 있다
                if (!IsEdgeVisible(edge, from.Y, index, windows)) continue;
                if (nearest is null || Math.Abs(edge - from.X) < Math.Abs(nearest.Value - from.X))
                    nearest = edge;
            }
        }

        if (nearest is not { } target) return null;
        return new Point(target > from.X ? target + overshoot : target - overshoot, from.Y);
    }

    /// 이 자리에서 오를 수 있는 창들과 그 Z 순서. 모서리가 보이는지는 그
    /// 순서로 정해진다.
    private static IEnumerable<(int Index, WindowInfo Window)> Climbable(
        Point position, IReadOnlyList<WindowInfo> windows,
        double roamableTop, double avatarHeight, ISet<IntPtr>? excluding)
    {
        for (var i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            if (excluding is not null && excluding.Contains(window.Handle)) continue;
            if (position.Y < window.Frame.Top || position.Y > window.Frame.Bottom) continue;
            // 머리 여유가 없는 창(거의 전체화면)은 올라가 봐야 머리가 잘린다.
            if (window.Frame.Top - roamableTop < avatarHeight) continue;
            yield return (i, window);
        }
    }

    /// 그 모서리가 펫의 높이에서 실제로 화면에 보이는가.
    ///
    /// 착지는 늘 이걸 물었고(다른 창 밑의 윗변은 발판이 아니다) 오르기는
    /// 묻지 않았다. 그래서 완전히 가려진 모서리도 걸어가 오를 수 있었고,
    /// 밖에서 보면 펫이 이유 없이 돌아서서 걸어가 허공을 올라갔다.
    private static bool IsEdgeVisible(double edgeX, double y, int index, IReadOnlyList<WindowInfo> windows)
    {
        for (var front = 0; front < index; front++)
        {
            var f = windows[front].Frame;
            if (f.Left <= edgeX && edgeX <= f.Right && f.Top <= y && y <= f.Bottom) return false;
        }
        return true;
    }
}
