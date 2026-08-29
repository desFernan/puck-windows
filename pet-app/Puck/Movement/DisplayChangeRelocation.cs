using System.Windows;

namespace Puck.Movement;

/// 디스플레이 구성이 바뀐 뒤 펫이 있어야 할 자리.
///
/// 가두기(`PetBounds.Contain`)는 화면이 **짧아졌을 때만** 답의 전부다.
/// 그때는 새 바닥이 펫 위로 올라오므로 안으로 당기면 그 바닥에 내려앉는다.
/// 화면이 길어지면 반대다 — 영역이 커진 순간 펫은 이미 그 안에 있어서
/// 가두기가 할 일을 찾지 못하고, 펫은 옛 바닥이 있던 선 위에 아무것도 없이
/// 서 있게 된다.
///
/// puck-mac의 `DisplayChangeRelocation`을 옮긴 것이다. 순수 함수인 이유도
/// 같다 — 모니터를 뽑아 보지 않고 규칙을 시험할 수 있어야 한다.
public static class DisplayChangeRelocation
{
    /// 세계가 다시 재어졌을 때 발밑을 잃는 상태인가.
    ///
    /// 아래에 있지 않은 것을 붙잡고 있는 펫(창 옆면, 화면 턱, 천장, 사람
    /// 손)은 제자리를 지킨다 — 그런 펫의 발을 바닥에 놓으면 화면을 가로질러
    /// 떨어뜨리는 것이 된다.
    ///
    /// 목록이지 기본값이 아닌 이유: 여기 빠진 상태는 조용히 "바닥에 서
    /// 있다"가 되어, 발밑에 아무것도 없는 펫이 해상도가 바뀌는 순간
    /// 바닥으로 순간이동한다. 매달리거나 붙잡는 상태를 새로 만들면 여기에
    /// 더해야 한다.
    public static bool StandsOnGround(StateKind state) => state switch
    {
        StateKind.Climb or StateKind.ClimbLedge or StateKind.ReactDrag or StateKind.Fall => false,
        // 천장에 거꾸로 매달려 있거나 거기로 오르는 중이다. 붙잡은 것이
        // 위에 있으므로 바닥이 움직여도 놓지 않는다.
        StateKind.Ceiling or StateKind.ClimbToCeiling => false,
        _ => true,
    };

    /// 영역 안으로. 가로는 `PetBounds.Contain` 그대로고, 세로를 여기서
    /// 더한다.
    ///
    /// `Contain`이 가로만 보는 것은 일부러다 — 어느 면에 내려앉는지는 화면
    /// 가장자리가 아니라 떨어지는 상태들의 일이다. 여기서는 내려앉을 것이
    /// 없다(서 있던 바닥이 한 프레임 사이에 존재하기를 그만뒀다). 발은
    /// 바닥보다 아래로 가지 않고, 머리는 천장보다 위로 가지 않으며,
    /// 영역이 펫보다 짧으면 바닥이 이긴다.
    public static Point Contained(Point position, Rect visualBounds, Rect area)
    {
        var horizontal = PetBounds.Contain(position, visualBounds, area);

        // Y는 아래로 증가한다. 외곽선은 발에서 위로 뻗으므로 Top이 음수이고,
        // 발이 올라갈 수 있는 가장 높은 자리는 영역 위 끝에서 그만큼 아래다.
        var ceiling = area.Top - visualBounds.Top;
        var floor = area.Bottom;

        return new Point(horizontal.X, Math.Clamp(position.Y, Math.Min(ceiling, floor), floor));
    }

    /// 무언가를 딛고 서 있던 펫이 바뀐 뒤에 있어야 할 자리.
    ///
    /// 영역의 바닥이 아니라 **착지면**을 묻는 이유는, 펫이 딛고 있는 것이
    /// 화면 바닥만큼이나 자주 창의 윗변이기 때문이다(`LandingSurfaceResolver`).
    /// 그것도 나머지와 함께 다시 재어졌다.
    public static Point Standing(Point position, Rect visualBounds, Rect area, Func<Point, double> surfaceY)
    {
        var contained = Contained(position, visualBounds, area);
        return new Point(contained.X, surfaceY(contained));
    }
}
