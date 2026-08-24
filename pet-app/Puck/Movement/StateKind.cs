namespace Puck.Movement;

/// FSM 상태 식별자. Phase 2 이후(Climb, Ceiling, Point, Type, Listen,
/// Spin, Petting, Pinned, MoveTo, Travel, WalkOnTop, 공놀이 셋)가 여기에 더한다.
public enum StateKind
{
    Idle,
    Walk,
    Fall,
    Land,
    /// 모니터 경계의 턱을 타고 위 화면으로. 창 옆면을 타는 Climb과는 다른 것이다.
    ClimbLedge,
    /// 창 옆면을 타고 그 윗변까지.
    Climb,
    /// 창 윗변 위를 거닌다.
    WalkOnTop,
    /// 남이 정해 준 곳으로 간다 — 핫키로 부르면 오는 것이 이것이다.
    MoveTo,
    ReactClick,
    ReactDrag,
}
