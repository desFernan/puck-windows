using System.Windows;
using Puck.Avatar;
using Puck.Movement;

namespace PuckTests.Movement;

public class MovementSolverTests
{
    [Fact]
    public void ConstantsMatchTheMacOriginal()
    {
        Assert.Equal(90, MovementSolver.WalkSpeed);
        Assert.Equal(2400, MovementSolver.Gravity);
        Assert.Equal(1200, MovementSolver.TerminalVelocity);
        Assert.Equal(2, MovementSolver.ArrivalRadius);
        Assert.Equal(2500, MovementSolver.MaxThrowSpeed);
        Assert.Equal(3.0, MovementSolver.GroundFrictionRate);
    }

    [Fact]
    public void OneSecondAtWalkSpeedCoversWalkSpeedPixels()
    {
        var step = MovementSolver.StepToward(new Point(0, 0), new Point(1000, 0), dt: 1.0);
        Assert.Equal(90, step.Position.X, precision: 6);
        Assert.False(step.HasArrived);
    }

    [Fact]
    public void DiagonalTravelIsNotFasterThanAxisAligned()
    {
        var step = MovementSolver.StepToward(new Point(0, 0), new Point(1000, 1000), dt: 1.0);
        var travelled = Math.Sqrt(step.Position.X * step.Position.X + step.Position.Y * step.Position.Y);
        Assert.Equal(90, travelled, precision: 6);
    }

    [Fact]
    public void ATravelLongerThanTheRemainingDistanceLandsExactlyOnTarget()
    {
        var target = new Point(10, 0);
        var step = MovementSolver.StepToward(new Point(0, 0), target, dt: 1.0);
        Assert.Equal(target, step.Position);
        Assert.True(step.HasArrived);
    }

    [Fact]
    public void InsideArrivalRadiusIsAlreadyThere()
    {
        var from = new Point(0, 0);
        var step = MovementSolver.StepToward(from, new Point(1, 0), dt: 1.0);
        Assert.Equal(from, step.Position);
        Assert.True(step.HasArrived);
    }

    [Fact]
    public void ThrowIsCappedAlongItsOwnDirection()
    {
        var capped = MovementSolver.CappedThrow(new Vector(9000, 9000));
        var speed = Math.Sqrt(capped.X * capped.X + capped.Y * capped.Y);
        Assert.Equal(MovementSolver.MaxThrowSpeed, speed, precision: 6);
        // 방향은 그대로 — 대각선을 축별로 자르면 어디로 가는지가 휜다.
        Assert.Equal(capped.X, capped.Y, precision: 6);
    }

    [Fact]
    public void ThrowBelowTheCapIsUntouched()
    {
        var velocity = new Vector(100, -200);
        var capped = MovementSolver.CappedThrow(velocity);
        Assert.Equal(velocity.X, capped.X);
        Assert.Equal(velocity.Y, capped.Y);
    }

    [Fact]
    public void FacingFollowsHorizontalDirectionOnly()
    {
        Assert.Equal(AvatarFacing.Right, MovementSolver.FacingToward(new Point(0, 0), new Point(10, 0)));
        Assert.Equal(AvatarFacing.Left, MovementSolver.FacingToward(new Point(10, 0), new Point(0, 0)));
        // 순수 수직 이동은 방향을 바꾸지 않는다 — 벽을 타는 펫이 뒤집히면 안 된다.
        Assert.Null(MovementSolver.FacingToward(new Point(0, 0), new Point(0, 100)));
    }

    [Fact]
    public void FallAcceleratesDownward()
    {
        var step = MovementSolver.FallStep(new Point(0, 0), velocity: 0, dt: 0.1, landingY: 10_000);
        Assert.Equal(240, step.Velocity, precision: 6);   // 2400 * 0.1
        Assert.Equal(24, step.Position.Y, precision: 6);
        Assert.False(step.HasLanded);
        Assert.False(step.TouchedFloor);
    }

    [Fact]
    public void FallSettlesAtTerminalVelocity()
    {
        var step = MovementSolver.FallStep(new Point(0, 0), velocity: 1190, dt: 1.0, landingY: 10_000);
        Assert.Equal(MovementSolver.TerminalVelocity, step.Velocity, precision: 6);
    }

    [Fact]
    public void FallStopsOnTheLandingSurfaceInsteadOfSinkingThrough()
    {
        var step = MovementSolver.FallStep(new Point(0, 95), velocity: 1000, dt: 1.0, landingY: 100);
        Assert.Equal(100, step.Position.Y);
        Assert.True(step.HasLanded);
        Assert.True(step.TouchedFloor);
    }

    [Fact]
    public void GroundFrictionDecaysExponentiallyAndIsFrameRateIndependent()
    {
        // 한 번의 0.2초 = 두 번의 0.1초.
        var once = MovementSolver.ApplyGroundFriction(1000, 0.2);
        var twice = MovementSolver.ApplyGroundFriction(MovementSolver.ApplyGroundFriction(1000, 0.1), 0.1);
        Assert.Equal(once, twice, precision: 9);
        Assert.True(once < 1000);
    }
}
