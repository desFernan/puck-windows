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
    /// 세계가 다시 재어졌을 때 새 바닥에 내려놓아도 되는 상태인가.
    ///
    /// 아래에 있지 않은 것을 붙잡고 있는 펫(천장, 창 옆면, 화면 턱, 사람
    /// 손)과 공중에 있는 펫은 제자리를 지킨다 — 그런 펫의 발을 바닥에 놓으면 화면을 가로질러
    /// 떨어뜨리는 것이 된다.
    ///
    /// **딛고 선 것만 적고, 모르는 것은 제자리에 둔다.** 반대로 적으면
    /// 나중에 더해지는 상태가 잠자코 "땅에 서 있다"가 되는데, 그중 하나가
    /// 매달린 것일 때 아무도 모르는 사이 펫이 화면을 가로질러 떨어진다.
    /// 실제로 그럴 뻔했다 — Ceiling과 ClimbToCeiling이 이 목록보다 늦게 왔다.
    ///
    /// 반대 방향으로 틀리면 대신 벌어지는 일은 작다: 딛고 선 펫이 한 프레임
    /// 동안 옛 바닥 선에 남고, 다음 프레임에 떨어진다. 이 규칙이 생기기 전의
    /// 동작 그대로다.
    public static bool StandsOnGround(StateKind state) => state switch
    {
        StateKind.Idle or StateKind.Walk or StateKind.Land or StateKind.WalkOnTop
            or StateKind.MoveTo or StateKind.ReactClick => true,
        _ => false,
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
