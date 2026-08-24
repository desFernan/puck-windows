using System.Windows;
using Puck.Avatar;
using Puck.Movement;
using Puck.Movement.States;

namespace PuckTests.Movement;

public class MoveToStateTests
{
    private static readonly Rect Area = new(0, 0, 1000, 800);
    private static readonly Rect Pet = new(-50, -100, 100, 100);

    private static (StateContext Context, CharacterBody Body, List<StateKind> Requested)
        MakeContext(Point start, Func<Point, bool>? hasGroundUnder = null, Func<Point, double>? landingY = null)
    {
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, start);
        var requested = new List<StateKind>();
        return (new StateContext
        {
            Body = body, RoamableArea = Area, AvatarHeight = 100, VisualBounds = Pet,
            WalkSpeed = MovementSolver.WalkSpeed,
            LandingY = landingY ?? (_ => 800),
            HasGroundUnder = hasGroundUnder ?? (_ => true),
            SnapToGround = (p, _) => p, LedgeBeyond = (_, _, _) => null,
            Windows = [], RequestTransition = requested.Add,
        }, body, requested);
    }

    [Fact]
    public void ItWalksTowardWhereItWasCalledAndFacesThatWay()
    {
        var (context, body, _) = MakeContext(new Point(100, 800));
        var move = new MoveToState { Target = new Point(900, 800) };
        move.Enter();
        move.Update(1.0, context);

        Assert.Equal(190, body.Position.X, precision: 6);
        Assert.Equal(AvatarFacing.Right, body.Facing);
    }

    [Fact]
    public void ArrivingGoesBackToStandingAround()
    {
        var (context, _, requested) = MakeContext(new Point(100, 800));
        var move = new MoveToState { Target = new Point(101, 800) };
        move.Enter();
        move.Update(1.0, context);

        Assert.Equal(StateKind.Idle, Assert.Single(requested));
    }

    [Fact]
    public void TheTargetIsConsumedSoThePetDoesNotKeepReturningToIt()
    {
        var (context, _, requested) = MakeContext(new Point(100, 800));
        var move = new MoveToState { Target = new Point(900, 800) };

        move.Enter();
        Assert.Null(move.Target);      // Enter가 읽고 비웠다

        move.Enter();                  // 다시 들어왔는데 아무도 안 불렀다
        move.Update(0.016, context);
        Assert.Equal(StateKind.Idle, Assert.Single(requested));
    }

    [Fact]
    public void BeingCalledOffTheEdgeOfTheWorldJustStops()
    {
        var (context, body, requested) = MakeContext(new Point(500, 800),
            hasGroundUnder: p => p.X <= 600);
        var move = new MoveToState { Target = new Point(2000, 800) };
        move.Enter();
        move.Update(2.0, context);

        Assert.Equal(500, body.Position.X);
        Assert.Equal(StateKind.Idle, Assert.Single(requested));
    }

    [Fact]
    public void WalkingOffASurfaceOnTheWayIsStillAFall()
    {
        var (context, _, requested) = MakeContext(new Point(100, 400),
            landingY: p => p.X > 150 ? 800 : 400);
        var move = new MoveToState { Target = new Point(900, 400) };
        move.Enter();
        move.Update(1.0, context);

        Assert.Contains(StateKind.Fall, requested);
    }
}
