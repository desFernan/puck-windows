using System.Windows;

namespace Puck.Movement.States;

/// 목표 X까지 등속으로 걷는다. 화면 가장자리에서 멈추고, 발밑이 사라지면 떨어진다.
public sealed class WalkState : IStateHandler
{
    private readonly Random _random;
    private double _target;

    public WalkState(Random? random = null) => _random = random ?? Random.Shared;

    public string Name => "Walk";
    public string ClipKey => "walk";
    public bool LoopsClip => true;

    /// 목적지. null이면 Enter에서 무작위로 뽑는다.
    ///
    /// Enter가 **읽고 비운다** — 한 번 겨눈 것이 다음 걸음까지 남으면, 배회가
    /// 정해 준 목적지로 영원히 같은 자리를 오간다. ReactDrag의 DragPosition,
    /// ClimbLedge의 Target과 같은 일회성 값이다.
    public double? TargetX { get; set; }

    /// 화면 턱에서 막혔을 때 올라갈 목적지를 넘겨줄 상대. null이면
    /// 오르지 않고 가장자리에 선다(모니터가 하나면 그럴 일 자체가 없다).
    public ClimbLedgeState? Ledge { get; init; }

    public void Enter()
    {
        _target = TargetX ?? double.NaN;
        TargetX = null;
    }

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;

        if (double.IsNaN(_target))
            _target = context.RoamableArea.Left +
                      _random.NextDouble() * context.RoamableArea.Width;

        var step = MovementSolver.StepToward(
            body.Position, new Point(_target, body.Position.Y), dt, context.WalkSpeed);

        var facing = MovementSolver.FacingToward(body.Position, new Point(_target, body.Position.Y));
        if (facing is not null) body.Facing = facing.Value;

        // 앞을 막은 창이 오르기를 시작한다 — 펫은 창을 통과하지 않고 그 옆면까지
        // 걸어가서 붙잡는다. 화면 턱보다 먼저 보는 이유는 눈앞에 있는 것이
        // 그것이기 때문이다.
        var blocking = WindowSupport.BlockingWindow(
            body.Position, new Point(_target, body.Position.Y), context.Windows,
            roamableTop: context.RoamableArea.Top,
            avatarHeight: context.AvatarHeight,
            excluding: context.UnclimbableWindows);

        if (blocking is not null)
        {
            var edgeX = _target > body.Position.X ? blocking.Frame.Left : blocking.Frame.Right;
            var toEdge = MovementSolver.StepToward(
                body.Position, new Point(edgeX, body.Position.Y), dt, context.WalkSpeed);
            body.Position = toEdge.Position;
            if (toEdge.HasArrived) context.RequestTransition(StateKind.Climb);
            return;
        }

        var next = PetBounds.Contain(step.Position, context.VisualBounds, context.RoamableArea);

        // RoamableArea는 경계 상자라 디스플레이 사이의 빈 공간도 포함한다.
        // 그리로 한 걸음 내디디면 펫이 화면 밖으로 사라지므로 가장자리에서 선다.
        // 발 한 점이 아니라 그림 좌우 끝을 보는 이유는, 경계에서 절반만
        // 걸친 채 멈추면 그것도 "잘려 보이는" 것이기 때문이다.
        if (!context.ArtworkHasGround(next))
        {
            // 옆 화면의 바닥이 위에 있으면 그 턱을 타고 올라간다. 없으면
            // 여기가 세상 끝이니 그냥 선다.
            var direction = _target >= body.Position.X ? 1.0 : -1.0;
            if (Ledge is not null &&
                context.LedgeBeyond(body.Position, direction, context.VisualBounds) is { } ledge)
            {
                Ledge.Target = ledge;
                context.RequestTransition(StateKind.ClimbLedge);
                return;
            }

            context.RequestTransition(StateKind.Idle);
            return;
        }

        body.Position = next;

        // 걸어 나간 자리에 바닥이 없으면 떨어진다. Idle과 같은 판정.
        var surfaceY = context.LandingY(body.Position);
        if (surfaceY > body.Position.Y + IdleState.FootTolerance)
        {
            context.RequestTransition(StateKind.Fall);
            return;
        }

        // 가장자리에 눌려 더 못 가는 경우도 도착으로 친다 — 아니면
        // 벽에 붙어 걷는 클립을 영원히 재생한다.
        var blocked = Math.Abs(body.Position.X - step.Position.X) > 0.001;
        if (step.HasArrived || blocked)
            context.RequestTransition(StateKind.Idle);
    }
}
