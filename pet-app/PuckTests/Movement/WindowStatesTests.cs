using System.Windows;
using Puck.Avatar;
using Puck.Movement;
using Puck.Movement.States;
using Puck.WindowSensing;

namespace PuckTests.Movement;

public class WindowStatesTests
{
    private static readonly Rect Area = new(0, 0, 1000, 800);
    private static readonly Rect Pet = new(-50, -100, 100, 100);

    private static WindowInfo At(int handle, double left, double top, double width, double height)
        => new(new IntPtr(handle), 1, "App", "창", new Rect(left, top, width, height), false, false);

    private static (StateContext Context, CharacterBody Body, List<StateKind> Requested)
        MakeContext(Point start, IReadOnlyList<WindowInfo> windows)
    {
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, start);
        var requested = new List<StateKind>();
        return (new StateContext
        {
            Body = body,
            RoamableArea = Area,
            AvatarHeight = 100,
            VisualBounds = Pet,
            WalkSpeed = MovementSolver.WalkSpeed,
            LandingY = _ => 800,
            HasGroundUnder = _ => true,
            SnapToGround = (p, _) => p,
            LedgeBeyond = (_, _, _) => null,
            Windows = windows,
            RequestTransition = requested.Add,
        }, body, requested);
    }

    private sealed class RecordingWander : IWanderDelegate
    {
        public List<WanderOutcome> Outcomes { get; } = [];
        public List<WindowInfo> LostBehind { get; } = [];
        public void WanderRequested(WanderOutcome outcome) => Outcomes.Add(outcome);
        public void LostFootingBehind(WindowInfo window) => LostBehind.Add(window);
    }

    // --- Idle: 가려짐 vs 사라짐 ---

    [Fact]
    public void FootingGoingBehindAWindowIsNotAFall()
    {
        // 사람이 창을 앞으로 가져왔을 뿐이다. 여기서 떨어뜨리면 숨어 있던
        // 펫이 사람이 지금 쓰는 창 한가운데로 나온다.
        var covering = At(1, 0, 300, 1000, 500);
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, new Point(500, 400));
        var requested = new List<StateKind>();
        var wander = new RecordingWander();
        var context = new StateContext
        {
            Body = body, RoamableArea = Area, AvatarHeight = 100, VisualBounds = Pet,
            WalkSpeed = MovementSolver.WalkSpeed,
            LandingY = _ => 800,               // 발밑(400)보다 아래 = 틈이 생겼다
            HasGroundUnder = _ => true, SnapToGround = (p, _) => p, LedgeBeyond = (_, _, _) => null,
            Windows = [covering], RequestTransition = requested.Add,
        };

        var idle = new IdleState(new WanderScheduler(new Random(1))) { Wander = wander };
        idle.Enter();
        idle.Update(0.016, context);

        Assert.Empty(requested);
        Assert.Equal(new IntPtr(1), Assert.Single(wander.LostBehind).Handle);
    }

    [Fact]
    public void FootingSimplyVanishingIsStillAFall()
    {
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, new Point(500, 400));
        var requested = new List<StateKind>();
        var wander = new RecordingWander();
        var context = new StateContext
        {
            Body = body, RoamableArea = Area, AvatarHeight = 100, VisualBounds = Pet,
            WalkSpeed = MovementSolver.WalkSpeed, LandingY = _ => 800,
            HasGroundUnder = _ => true, SnapToGround = (p, _) => p, LedgeBeyond = (_, _, _) => null,
            Windows = [],                       // 덮는 창이 없다 = 그냥 사라졌다
            RequestTransition = requested.Add,
        };

        var idle = new IdleState(new WanderScheduler(new Random(1))) { Wander = wander };
        idle.Enter();
        idle.Update(0.016, context);

        Assert.Equal(StateKind.Fall, Assert.Single(requested));
        Assert.Empty(wander.LostBehind);
    }

    [Fact]
    public void TheWanderOutcomeGoesToWhoeverKnowsTheWindows()
    {
        var (context, _, requested) = MakeContext(new Point(500, 800), []);
        var wander = new RecordingWander();
        var idle = new IdleState(new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(1),
        })
        { Wander = wander };
        idle.Enter();
        idle.Update(1.5, context);

        // 델리게이트가 있으면 상태는 스스로 전이를 정하지 않는다.
        Assert.Empty(requested);
        Assert.Single(wander.Outcomes);
    }

    // --- Walk -> Climb ---

    [Fact]
    public void WalkingIntoAWindowWalksToItsEdgeAndClimbs()
    {
        var window = At(1, 600, 300, 300, 500);
        var (context, body, requested) = MakeContext(new Point(400, 500), [window]);
        var walk = new WalkState { TargetX = 900 };
        walk.Enter();

        for (var i = 0; i < 300 && requested.Count == 0; i++)
            walk.Update(1.0 / 60, context);

        Assert.Equal(StateKind.Climb, Assert.Single(requested));

        // 도착 반경(2px) 안에서 멈추므로 정확히 600은 아니다. 중요한 건
        // 거기서 실제로 벽을 붙잡을 수 있는가 — EdgeTolerance가 그 여유다.
        Assert.True(Math.Abs(body.Position.X - 600) <= WindowSupport.EdgeTolerance);
        Assert.NotNull(WindowSupport.WindowBeingClimbed(body.Position, [window]));
    }

    [Fact]
    public void AWindowTheSettingsSayToAvoidIsWalkedPast()
    {
        var window = At(1, 600, 300, 300, 500);
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, new Point(400, 500));
        var requested = new List<StateKind>();
        var context = new StateContext
        {
            Body = body, RoamableArea = Area, AvatarHeight = 100, VisualBounds = Pet,
            WalkSpeed = MovementSolver.WalkSpeed, LandingY = _ => 500,
            HasGroundUnder = _ => true, SnapToGround = (p, _) => p, LedgeBeyond = (_, _, _) => null,
            Windows = [window],
            UnclimbableWindows = new HashSet<IntPtr> { new(1) },
            RequestTransition = requested.Add,
        };
        var walk = new WalkState { TargetX = 900 };
        walk.Enter();
        walk.Update(1.0, context);

        Assert.DoesNotContain(StateKind.Climb, requested);
        Assert.Equal(490, body.Position.X, precision: 6);   // 그냥 지나쳐 걷는다
    }

    // --- Climb ---

    [Fact]
    public void ClimbingRidesTheEdgeStraightUp()
    {
        var window = At(1, 600, 300, 300, 500);
        var (context, body, _) = MakeContext(new Point(600, 700), [window]);
        var climb = new ClimbState();
        climb.Update(1.0, context);

        Assert.Equal(600, body.Position.X);      // 좌우로는 움직이지 않는다
        Assert.Equal(610, body.Position.Y);      // 90px/s 만큼 위로
    }

    [Fact]
    public void ReachingTheTopHandsOverToWalkingOnIt()
    {
        var window = At(1, 600, 300, 300, 500);
        var (context, body, requested) = MakeContext(new Point(600, 700), [window]);
        var climb = new ClimbState();

        for (var i = 0; i < 600 && requested.Count == 0; i++)
            climb.Update(1.0 / 60, context);

        Assert.Equal(StateKind.WalkOnTop, Assert.Single(requested));
        Assert.Equal(300, body.Position.Y);
    }

    [Fact]
    public void ArrivingOnTopPullsThePetInsideTheWindowsSpan()
    {
        // 모서리는 EdgeTolerance만큼 밖에서도 잡히므로 매달린 X가 프레임 밖일
        // 수 있다. 발판 판정에는 X 여유가 없어서, 당겨 주지 않으면 올라서자마자
        // 발밑을 잃고 떨어진다 — 실제 창에서 1.5px 차이로 그렇게 됐다.
        var window = At(1, 600, 300, 300, 500);      // 오른쪽 끝 900
        var (context, body, requested) = MakeContext(new Point(901.5, 700), [window]);
        var climb = new ClimbState();

        for (var i = 0; i < 600 && requested.Count == 0; i++)
            climb.Update(1.0 / 60, context);

        Assert.Equal(StateKind.WalkOnTop, Assert.Single(requested));
        Assert.Equal(900, body.Position.X);
        Assert.NotNull(WindowSupport.SupportingWindow(body.Position, [window]));
    }

    [Fact]
    public void AWindowClosedMidClimbDropsThePet()
    {
        // 잡고 있던 것이 사라졌다. 창은 언제든 닫힌다.
        var (context, _, requested) = MakeContext(new Point(600, 700), []);
        var climb = new ClimbState();
        climb.Update(0.016, context);

        Assert.Equal(StateKind.Fall, Assert.Single(requested));
    }

    // --- WalkOnTop ---

    [Fact]
    public void WalkingOnTopStaysOnTheEdgeAndHeadsInward()
    {
        // 왼쪽 모서리로 올라왔으니 오른쪽(창 안쪽)으로 걷는다.
        var window = At(1, 300, 400, 400, 300);
        var (context, body, _) = MakeContext(new Point(300, 400), [window]);
        var walk = new WalkOnTopState();
        walk.Enter();
        walk.Update(1.0, context);

        Assert.Equal(390, body.Position.X, precision: 6);
        Assert.Equal(400, body.Position.Y);
        Assert.Equal(AvatarFacing.Right, body.Facing);
    }

    [Fact]
    public void ArrivingAtTheRightEdgeItHeadsInwardToo()
    {
        var window = At(1, 300, 400, 400, 300);
        var (context, body, _) = MakeContext(new Point(700, 400), [window]);
        var walk = new WalkOnTopState();
        walk.Enter();
        walk.Update(1.0, context);

        Assert.Equal(610, body.Position.X, precision: 6);
        Assert.Equal(AvatarFacing.Left, body.Facing);
    }

    [Fact]
    public void LosingTheWindowUnderfootIsAFall()
    {
        var (context, _, requested) = MakeContext(new Point(500, 400), []);
        var walk = new WalkOnTopState();
        walk.Enter();
        walk.Update(0.016, context);

        Assert.Equal(StateKind.Fall, Assert.Single(requested));
    }

    [Fact]
    public void AWindowReachingTheScreenEdgeTurnsThePetAround()
    {
        // 화면 끝까지 닿은 창에는 걸어 나갈 끝이 없다. 돌아서지 않으면
        // 가장자리에 박힌 채 걷는 클립만 계속 재생된다.
        var window = At(1, 0, 400, 1000, 300);
        var (context, body, _) = MakeContext(new Point(945, 400), [window]);
        var walk = new WalkOnTopState();
        walk.Enter();
        walk.Update(1.0, context);   // 오른쪽으로 90px 가면 한계(950)를 넘는다

        Assert.True(body.Position.X <= 950);
        Assert.Equal(AvatarFacing.Left, body.Facing);
    }
}
