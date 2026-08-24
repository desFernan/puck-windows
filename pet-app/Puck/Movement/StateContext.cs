using System.Windows;

namespace Puck.Movement;

/// 한 프레임 동안 상태가 볼 수 있고 할 수 있는 것.
/// 모든 좌표는 가상 화면 물리 픽셀, 좌상단 원점, Y는 아래로.
public sealed class StateContext
{
    public required CharacterBody Body { get; init; }

    /// 펫이 돌아다녀도 되는 영역. 보통 모든 디스플레이 작업 영역의 합집합.
    public required Rect RoamableArea { get; init; }

    /// 그려진 아바타의 현재 높이 (매니페스트 hitbox × scale).
    public required double AvatarHeight { get; init; }

    /// 접지점 기준 펫의 외곽선. 상태는 맨 위치가 아니라 이것을 기준으로
    /// 가두고 튕긴다 — 그래야 그림이 화면 가장자리에 닿을 때 멈춘다.
    public required Rect VisualBounds { get; init; }

    /// Walk/MoveTo용 px/sec. MovementSolver.WalkSpeed에 설정의 이동 속도
    /// 슬라이더를 곱한 값.
    public required double WalkSpeed { get; init; }

    /// 그 지점에서 곧장 떨어지면 닿는 표면의 Y. 상태가 창 목록에
    /// 의존하지 않도록 클로저로 주입한다 — Phase 2에서 창 윗면이
    /// 여기 끼어들 때 상태 코드는 한 줄도 바뀌지 않는다.
    public required Func<Point, double> LandingY { get; init; }

    /// 그 지점 아래에 실제 디스플레이가 있는가. RoamableArea는 경계 상자라
    /// 디스플레이 사이의 빈 공간까지 포함하고, 거기 있는 펫은 화면 밖이라
    /// 보이지 않는다. LandingY와 같은 이유로 클로저다.
    public required Func<Point, bool> HasGroundUnder { get; init; }

    /// 그런 빈 공간에 놓인 펫을 가장 가까운 실제 화면 위로 끌어오는 함수.
    /// 컨트롤러가 프레임 끝마다 불변식으로 적용한다.
    public required Func<Point, Rect, Point> SnapToGround { get; init; }

    /// (위치, 진행 방향, 그림 외곽선) -> 타고 올라갈 수 있는 턱 위의 설 자리.
    /// 없으면 null. 낮은 화면에서 높은 화면으로 돌아가는 유일한 걸음이다.
    public required Func<Point, double, Rect, Point?> LedgeBeyond { get; init; }

    /// 이 프레임이 끝난 뒤 다른 상태로 가 달라는 요청. 즉시가 아니라
    /// 지연되는 이유는, 어떤 상태도 자기 update 도중에 컨트롤러를
    /// 변형하면 안 되기 때문이다.
    public required Action<StateKind> RequestTransition { get; init; }

    /// 그 자리에서 **그림 전체**가 화면 위에 있는가. 발 한 점만 보면
    /// 디스플레이 경계에서 펫의 절반이 빈 공간에 걸린 채로 멈춘다.
    public bool ArtworkHasGround(Point position)
        => HasGroundUnder(new Point(position.X + VisualBounds.Left, position.Y))
        && HasGroundUnder(new Point(position.X + VisualBounds.Right, position.Y));
}
