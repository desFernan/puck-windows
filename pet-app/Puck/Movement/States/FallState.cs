using System.Windows;

namespace Puck.Movement.States;

/// 자유낙하. 첫 프레임에 발사 충격량을 소화하고, 옆벽에서 튕기며,
/// 착지면에 닿으면 Land로 넘긴다.
public sealed class FallState : IStateHandler
{
    private double _verticalVelocity;
    private double _horizontalVelocity;
    private bool _consumedLaunch;

    public string Name => "Fall";
    public string ClipKey => "fall";

    public void Enter()
    {
        _verticalVelocity = 0;
        _horizontalVelocity = 0;
        _consumedLaunch = false;
    }

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;

        // 던져진 속도는 그것을 측정한 드래그 상태보다 오래 살아야 한다.
        // 첫 프레임에 읽고 지운다 — 0이면 그냥 떨어뜨린 것이고,
        // Fall로 들어오는 다른 경로는 영향을 받지 않는다.
        if (!_consumedLaunch)
        {
            var launch = MovementSolver.CappedThrow(body.LaunchVelocity);
            _horizontalVelocity = launch.X;
            _verticalVelocity = launch.Y;
            body.LaunchVelocity = new Vector(0, 0);
            _consumedLaunch = true;
        }

        var floorY = context.LandingY(body.Position);
        var next = MovementSolver.FallStep(body.Position, _verticalVelocity, dt, floorY);
        _verticalVelocity = next.Velocity;

        var position = new Point(next.Position.X + _horizontalVelocity * dt, next.Position.Y);

        var horizontal = PetBounds.BounceHorizontally(
            position, _horizontalVelocity, context.VisualBounds, context.RoamableArea);
        position = horizontal.Position;
        _horizontalVelocity = horizontal.Velocity;

        var ceiling = PetBounds.BounceOffCeiling(
            position, _verticalVelocity, context.VisualBounds, context.RoamableArea);
        position = ceiling.Position;
        _verticalVelocity = ceiling.Velocity;

        if (next.TouchedFloor)
        {
            var floor = PetBounds.BounceOffFloor(position, _verticalVelocity, floorY);
            position = floor.Position;
            _verticalVelocity = floor.Velocity;
            _horizontalVelocity = MovementSolver.ApplyGroundFriction(_horizontalVelocity, dt);
        }

        body.Position = position;

        if (MovementSolver.FacingToward(body.Position, position) is { } facing)
            body.Facing = facing;

        // 튕길 에너지가 남아 있으면 아직 착지가 아니다.
        if (next.HasLanded && _verticalVelocity == 0)
            context.RequestTransition(StateKind.Land);
    }
}
