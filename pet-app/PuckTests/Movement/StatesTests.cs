using System.Windows;
using Puck.Avatar;
using Puck.Movement;
using Puck.Movement.States;

namespace PuckTests.Movement;

public class StatesTests
{
    private static readonly Rect Area = new(0, 0, 1000, 800);
    private static readonly Rect Pet = new(-50, -100, 100, 100);

    private static (StateContext Context, CharacterBody Body, List<StateKind> Requested)
        MakeContext(Point start, Func<Point, double>? landingY = null)
    {
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, start);
        var requested = new List<StateKind>();
        var context = new StateContext
        {
            Body = body,
            RoamableArea = Area,
            AvatarHeight = 100,
            VisualBounds = Pet,
            WalkSpeed = MovementSolver.WalkSpeed,
            LandingY = landingY ?? (_ => 800),
            RequestTransition = requested.Add,
        };
        return (context, body, requested);
    }

    // --- Idle ---

    [Fact]
    public void IdleAsksToWanderWhenItsTimerFires()
    {
        var (context, _, requested) = MakeContext(new Point(500, 800));
        var idle = new IdleState(new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(1),
        });
        idle.Enter();

        idle.Update(0.5, context);
        Assert.Empty(requested);

        idle.Update(0.6, context);
        Assert.Equal(StateKind.Walk, Assert.Single(requested));
    }

    [Fact]
    public void IdleFallsWhenTheSurfaceUnderfootIsGone()
    {
        // 발밑(y=400)보다 훨씬 아래(800)에 바닥이 있다 = 서 있던 것이 사라졌다.
        var (context, _, requested) = MakeContext(new Point(500, 400));
        var idle = new IdleState(new WanderScheduler(new Random(1)));
        idle.Enter();
        idle.Update(0.016, context);
        Assert.Equal(StateKind.Fall, Assert.Single(requested));
    }

    [Fact]
    public void IdleDoesNotRestartOnReentry()
    {
        // 같은 종류의 이벤트가 반복될 때마다 타이머가 초기화되면 안 된다.
        // Idle은 인터페이스 기본값(false)을 그대로 쓰므로 인터페이스로 봐야 보인다 —
        // 컨트롤러가 상태를 보는 방식도 이것이다.
        IStateHandler idle = new IdleState(new WanderScheduler());
        Assert.False(idle.RestartsOnReentry);
    }

    // --- Walk ---

    [Fact]
    public void WalkMovesTowardItsTargetAndFacesThatWay()
    {
        var (context, body, _) = MakeContext(new Point(100, 800));
        var walk = new WalkState { TargetX = 900 };
        walk.Enter();
        walk.Update(1.0, context);

        Assert.Equal(190, body.Position.X, precision: 6);
        Assert.Equal(800, body.Position.Y);
        Assert.Equal(AvatarFacing.Right, body.Facing);
    }

    [Fact]
    public void WalkGoesIdleOnArrival()
    {
        var (context, _, requested) = MakeContext(new Point(100, 800));
        var walk = new WalkState { TargetX = 101 };
        walk.Enter();
        walk.Update(1.0, context);
        Assert.Equal(StateKind.Idle, Assert.Single(requested));
    }

    [Fact]
    public void WalkStopsWhenTheArtworkMeetsTheScreenEdge()
    {
        var (context, body, _) = MakeContext(new Point(940, 800));
        var walk = new WalkState { TargetX = 5000 };
        walk.Enter();
        walk.Update(1.0, context);
        // 오른쪽 한계는 1000 - 50 = 950.
        Assert.Equal(950, body.Position.X);
    }

    [Fact]
    public void WalkFallsWhenItWalksOffAnEdge()
    {
        // x가 600을 넘으면 바닥이 400에서 800으로 떨어진다.
        var (context, _, requested) = MakeContext(new Point(595, 400),
            landingY: p => p.X > 600 ? 800 : 400);
        var walk = new WalkState { TargetX = 900 };
        walk.Enter();
        walk.Update(1.0, context);
        Assert.Contains(StateKind.Fall, requested);
    }

    // --- Fall ---

    [Fact]
    public void FallConsumesTheLaunchVelocityOnItsFirstFrameOnly()
    {
        var (context, body, _) = MakeContext(new Point(500, 100));
        body.LaunchVelocity = new Vector(200, -300);

        var fall = new FallState();
        fall.Enter();
        fall.Update(0.1, context);

        Assert.Equal(new Vector(0, 0), body.LaunchVelocity);
        // 수평 성분이 살아 있어야 던지기가 던지기로 보인다.
        Assert.Equal(520, body.Position.X, precision: 6);
    }

    [Fact]
    public void FallAcceleratesAndThenLands()
    {
        var (context, body, requested) = MakeContext(new Point(500, 100));
        var fall = new FallState();
        fall.Enter();

        for (var i = 0; i < 120 && requested.Count == 0; i++)
            fall.Update(1.0 / 60, context);

        Assert.Equal(StateKind.Land, Assert.Single(requested));
        Assert.Equal(800, body.Position.Y);
    }

    [Fact]
    public void FallBouncesOffTheSideWalls()
    {
        var (context, body, _) = MakeContext(new Point(940, 100));
        body.LaunchVelocity = new Vector(2000, 0);

        var fall = new FallState();
        fall.Enter();
        fall.Update(0.1, context);

        // 오른쪽 한계 950을 지나쳤으니 되튕겨 나와야 한다.
        Assert.True(body.Position.X <= 950);
    }

    // --- Land ---

    [Fact]
    public void LandGoesIdleAfterItsClipLength()
    {
        var (context, _, requested) = MakeContext(new Point(500, 800));
        var land = new LandState();
        land.Enter();

        land.Update(0.1, context);
        Assert.Empty(requested);

        land.Update(LandState.Duration, context);
        Assert.Equal(StateKind.Idle, Assert.Single(requested));
    }

    [Fact]
    public void LandRestartsOnReentrySoARepeatedBounceReplaysIt()
    {
        Assert.True(new LandState().RestartsOnReentry);
    }
}
