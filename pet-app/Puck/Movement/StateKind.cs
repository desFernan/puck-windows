namespace Puck.Movement;

/// FSM 상태 식별자. Phase 2 이후(Climb, Ceiling, Point, Type, Listen,
/// Spin, Petting, Pinned, MoveTo, Travel, WalkOnTop, 공놀이 셋)가 여기에 더한다.
public enum StateKind
{
    Idle,
    Walk,
    Fall,
    Land,
    /// 모니터 경계의 턱을 타고 위 화면으로. Phase 2의 Climb(창 옆면)과는 다른 것이다.
    ClimbLedge,
    ReactClick,
    ReactDrag,
}
