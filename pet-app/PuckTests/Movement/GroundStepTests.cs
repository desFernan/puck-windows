using System.Windows;
using Puck.Avatar;
using Puck.Movement;
using Puck.Movement.States;

namespace PuckTests.Movement;

/// Walk와 MoveTo가 함께 쓰는 딛기 판정. 여기가 틀리면 두 상태가 같이 틀린다.
public class GroundStepTests
{
    private static readonly Rect Area = new(0, 0, 1000, 800);
    private static readonly Rect Pet = new(-50, -100, 100, 100);

    private static (StateContext Context, CharacterBody Body) Make(
        Point start, Func<Point, bool>? hasGroundUnder = null, Func<Point, double>? landingY = null)
    {
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, start);
        return (new StateContext
        {
            Body = body, RoamableArea = Area, AvatarHeight = 100, VisualBounds = Pet,
            WalkSpeed = MovementSolver.WalkSpeed,
            LandingY = landingY ?? (_ => 800),
            HasGroundUnder = hasGroundUnder ?? (_ => true),
            SnapToGround = (p, _) => p, LedgeBeyond = (_, _, _) => null,
            Windows = [], RequestTransition = _ => { },
        }, body);
    }

    private static MovementSolver.Step Step(double x, bool arrived = false)
        => new(new Point(x, 800), arrived);

    [Fact]
    public void AnOrdinaryStepMovesThePetAndKeepsGoing()
    {
        var (context, body) = Make(new Point(100, 800));

        Assert.Equal(GroundStep.Outcome.Continue, GroundStep.Take(Step(200), context));
        Assert.Equal(200, body.Position.X, precision: 6);
    }

    [Fact]
    public void SteppingOffTheWorldLeavesThePetWhereItWas()
    {
        // 자리를 옮긴 뒤에 판정하면 펫이 한 프레임 동안 빈 공간에 서 있게 된다.
        var (context, body) = Make(new Point(100, 800), hasGroundUnder: p => p.X < 150);

        Assert.Equal(GroundStep.Outcome.OffWorld, GroundStep.Take(Step(200), context));
        Assert.Equal(100, body.Position.X, precision: 6);
    }

    [Fact]
    public void GroundVanishingUnderfootFalls()
    {
        var (context, _) = Make(new Point(100, 500), landingY: _ => 800);

        Assert.Equal(GroundStep.Outcome.Fell, GroundStep.Take(Step(200), context));
    }

    [Fact]
    public void LandingWithinFootToleranceIsStillStanding()
    {
        // 반올림과 픽셀 경계 때문에 정확히 같은 값이 나오지 않는다.
        var (context, _) = Make(new Point(100, 800),
            landingY: _ => 800 + WindowSupport.FootTolerance - 0.5);

        Assert.Equal(GroundStep.Outcome.Continue, GroundStep.Take(Step(200), context));
    }

    [Fact]
    public void ArrivingIsArrived()
    {
        var (context, _) = Make(new Point(100, 800));

        Assert.Equal(GroundStep.Outcome.Arrived, GroundStep.Take(Step(200, arrived: true), context));
    }

    [Fact]
    public void BeingPressedAgainstTheEdgeCountsAsArriving()
    {
        // 아니면 벽에 붙어 걷는 클립을 영원히 재생한다.
        var (context, _) = Make(new Point(900, 800));

        Assert.Equal(GroundStep.Outcome.Arrived, GroundStep.Take(Step(5000), context));
    }
}
