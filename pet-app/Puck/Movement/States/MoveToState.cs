using System.Windows;

namespace Puck.Movement.States;

/// 정해진 곳으로 간다. 배회와 달리 목적지를 남이 정해 준다 —
/// 핫키로 부르면 커서 쪽으로 오는 것이 이 상태다.
///
/// 걷는 것이지 나는 것이 아니라서, 목적지가 지금 서 있는 면보다 위에 있으면
/// 그 X까지만 간다. 거기서 올라갈 수 있으면 Walk가 알아서 창을 탄다.
public sealed class MoveToState : IStateHandler
{
    public string Name => "MoveTo";
    public string ClipKey => "walk";
    public bool LoopsClip => true;

    /// 목적지. Enter가 읽고 비운다 — 한 번 부른 것이 남으면 그 자리로
    /// 영원히 돌아간다.
    public Point? Target { get; set; }

    private Point? _target;

    public void Enter()
    {
        _target = Target;
        Target = null;
    }

    public void Update(double dt, StateContext context)
    {
        if (_target is not { } target)
        {
            context.RequestTransition(StateKind.Idle);
            return;
        }

        var body = context.Body;
        var aim = new Point(target.X, body.Position.Y);

        if (MovementSolver.FacingToward(body.Position, aim) is { } facing) body.Facing = facing;

        var step = MovementSolver.StepToward(body.Position, aim, dt, context.WalkSpeed);
        var next = PetBounds.Contain(step.Position, context.VisualBounds, context.RoamableArea);

        // 부르는 사람이 화면 밖을 가리켰을 수도 있다. 걷기와 같은 규칙으로 선다.
        if (!context.ArtworkHasGround(next))
        {
            context.RequestTransition(StateKind.Idle);
            return;
        }

        body.Position = next;

        var surfaceY = context.LandingY(body.Position);
        if (surfaceY > body.Position.Y + IdleState.FootTolerance)
        {
            context.RequestTransition(StateKind.Fall);
            return;
        }

        var blocked = Math.Abs(body.Position.X - step.Position.X) > 0.001;
        if (step.HasArrived || blocked)
            context.RequestTransition(StateKind.Idle);
    }
}
