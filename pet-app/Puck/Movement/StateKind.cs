namespace Puck.Movement;

/// FSM 상태 식별자. Phase 2 이후(Climb, Ceiling, Point, Type, Listen,
/// Spin, Petting, Pinned, MoveTo, Travel, WalkOnTop, 공놀이 셋)가 여기에 더한다.
public enum StateKind
{
    Idle,
    Walk,
    Fall,
    Land,
    ReactClick,
    ReactDrag,
}
