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

    /// 목적지. null이면 Enter에서 뽑는다.
    public double? TargetX { get; init; }

    public void Enter() => _target = TargetX ?? double.NaN;

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

        var next = PetBounds.Contain(step.Position, context.VisualBounds, context.RoamableArea);

        // RoamableArea는 경계 상자라 디스플레이 사이의 빈 공간도 포함한다.
        // 그리로 한 걸음 내디디면 펫이 화면 밖으로 사라지므로 가장자리에서 선다.
        if (!context.HasGroundUnder(next))
        {
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
