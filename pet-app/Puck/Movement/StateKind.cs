namespace Puck.Movement;

/// FSM 상태 식별자. 아직 포팅되지 않은 상태(Point, Type, Listen, Spin,
/// Petting, Pinned, 공놀이 셋)가 여기에 더해진다.
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
    /// 벽을 타고 천장까지 곧장. 창 윗변에서 멈추는 Climb과 달리 끝까지 간다.
    ClimbToCeiling,
    /// 천장에 거꾸로 매달려 긴다.
    Ceiling,
    /// 창 윗변 위를 거닌다.
    WalkOnTop,
    /// 남이 정해 준 곳으로 간다 — 핫키로 부르면 오는 것이 이것이다.
    MoveTo,
    /// 바탕화면과 채팅 창의 섬 사이를 날아서 오간다. 둘 사이에는 걸어갈
    /// 바닥이 없다.
    Travel,
    ReactClick,
    ReactDrag,
}
