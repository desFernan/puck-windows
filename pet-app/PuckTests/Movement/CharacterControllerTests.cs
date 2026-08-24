using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

/// 무엇이 호출됐는지만 기록하는 상태.
internal sealed class RecordingState(string name, string clipKey = "idle") : IStateHandler
{
    public string Name => name;
    public string ClipKey => clipKey;
    public bool LoopsClip { get; init; }
    public bool RestartsOnReentry { get; init; }

    public int Enters { get; private set; }
    public int Exits { get; private set; }
    public List<double> Updates { get; } = [];

    /// 이 상태가 update에서 요청할 전이. null이면 아무것도 요청하지 않는다.
    public StateKind? RequestOnUpdate { get; set; }

    public void Enter() => Enters++;
    public void Exit() => Exits++;

    public void Update(double dt, StateContext context)
    {
        Updates.Add(dt);
        if (RequestOnUpdate is { } kind) context.RequestTransition(kind);
    }
}

public class CharacterControllerTests
{
    private static CharacterController Make(
        IReadOnlyDictionary<StateKind, IStateHandler> states,
        StateKind initial,
        CharacterBody? body = null,
        Func<Point, bool>? hasGroundUnder = null,
        Func<Point, Rect, Point>? snapToGround = null)
    {
        body ??= new CharacterBody(new FakeAvatar(), new Point(0, 0));
        return new CharacterController(body, states, initial, () => new StateContext
        {
            Body = body,
            RoamableArea = new Rect(0, 0, 1000, 800),
            AvatarHeight = 100,
            VisualBounds = new Rect(-50, -100, 100, 100),
            WalkSpeed = MovementSolver.WalkSpeed,
            LandingY = _ => 800,
            HasGroundUnder = hasGroundUnder ?? (_ => true),
            SnapToGround = snapToGround ?? ((p, _) => p),
            LedgeBeyond = (_, _, _) => null,
            Windows = [],
            RequestTransition = _ => { },
        });
    }

    [Fact]
    public void TheInitialStateIsEnteredOnce()
    {
        var idle = new RecordingState("Idle");
        _ = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle }, StateKind.Idle);
        Assert.Equal(1, idle.Enters);
    }

    [Fact]
    public void AdvanceForwardsDtToTheCurrentState()
    {
        var idle = new RecordingState("Idle");
        var controller = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle }, StateKind.Idle);
        controller.Advance(0.016);
        Assert.Equal([0.016], idle.Updates);
    }

    [Fact]
    public void ATransitionExitsTheOldStateAndEntersTheNew()
    {
        var idle = new RecordingState("Idle");
        var walk = new RecordingState("Walk", "walk");
        var controller = Make(new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = idle,
            [StateKind.Walk] = walk,
        }, StateKind.Idle);

        controller.Request(StateKind.Walk);
        controller.Advance(0.016);

        Assert.Equal(1, idle.Exits);
        Assert.Equal(1, walk.Enters);
        Assert.Equal(StateKind.Walk, controller.Current);
    }

    [Fact]
    public void ATransitionRequestedDuringUpdateTakesEffectAfterThatFrame()
    {
        // 어떤 상태도 자기 update 도중에 교체되면 안 된다.
        var idle = new RecordingState("Idle") { RequestOnUpdate = StateKind.Walk };
        var walk = new RecordingState("Walk", "walk");
        var controller = Make(new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = idle,
            [StateKind.Walk] = walk,
        }, StateKind.Idle);

        controller.Advance(0.016);

        Assert.Single(idle.Updates);       // 이 프레임은 끝까지 Idle의 것
        Assert.Equal(StateKind.Walk, controller.Current);
        Assert.Empty(walk.Updates);        // Walk의 첫 update는 다음 프레임
    }

    [Fact]
    public void ReenteringTheSameStateIsANoOpByDefault()
    {
        var idle = new RecordingState("Idle");
        var controller = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle }, StateKind.Idle);

        controller.Request(StateKind.Idle);
        controller.Advance(0.016);

        Assert.Equal(1, idle.Enters);
        Assert.Equal(0, idle.Exits);
    }

    [Fact]
    public void AStateThatRestartsOnReentryIsRestarted()
    {
        var land = new RecordingState("Land", "land") { RestartsOnReentry = true };
        var controller = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Land] = land }, StateKind.Land);

        controller.Request(StateKind.Land);
        controller.Advance(0.016);

        Assert.Equal(2, land.Enters);
        Assert.Equal(1, land.Exits);
    }

    [Fact]
    public void EnteringAStatePlaysItsClipWithItsLoopFlag()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0));
        var idle = new RecordingState("Idle") { LoopsClip = true };
        var walk = new RecordingState("Walk", "walk") { LoopsClip = true };
        var controller = Make(new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = idle,
            [StateKind.Walk] = walk,
        }, StateKind.Idle, body);

        controller.Request(StateKind.Walk);
        controller.Advance(0.016);

        Assert.Equal(("walk", true), avatar.Played[^1]);
    }

    [Fact]
    public void AnUnknownStateIsRefusedRatherThanCrashingTheFrameLoop()
    {
        var idle = new RecordingState("Idle");
        var controller = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle }, StateKind.Idle);

        controller.Request(StateKind.Fall);   // 등록되지 않았다
        controller.Advance(0.016);

        Assert.Equal(StateKind.Idle, controller.Current);
    }

    [Fact]
    public void APetLeftOverAGapBetweenDisplaysIsPulledBackOntoOne()
    {
        // 디스플레이가 계단처럼 놓이면 경계 상자 안에도 화면이 없는 자리가 있다.
        // 던지기·드래그·모니터 착탈 어느 쪽으로 거기 놓이든, 프레임이 끝나면
        // 실제 화면 위로 돌아와야 한다 — 아니면 영영 보이지 않는다.
        var body = new CharacterBody(new FakeAvatar(), new Point(0, 0));
        var stray = new RecordingState("Stray");
        var controller = Make(
            new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = stray },
            StateKind.Idle, body,
            hasGroundUnder: p => p.X <= 600,
            snapToGround: (p, _) => new Point(Math.Min(p.X, 600), p.Y));

        body.Position = new Point(1800, 500);   // 빈 공간
        controller.Advance(0.016);

        Assert.Equal(new Point(600, 500), body.Position);
    }

    [Fact]
    public void APetStandingOnADisplayIsLeftAlone()
    {
        var body = new CharacterBody(new FakeAvatar(), new Point(0, 0));
        var idle = new RecordingState("Idle");
        var controller = Make(
            new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle },
            StateKind.Idle, body,
            hasGroundUnder: _ => true,
            snapToGround: (_, _) => new Point(-1, -1));   // 불렸다면 테스트가 깨진다

        body.Position = new Point(300, 500);
        controller.Advance(0.016);

        Assert.Equal(new Point(300, 500), body.Position);
    }

    [Fact]
    public void TransitionedFiresWithBothStates()
    {
        var idle = new RecordingState("Idle");
        var walk = new RecordingState("Walk", "walk");
        var controller = Make(new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = idle,
            [StateKind.Walk] = walk,
        }, StateKind.Idle);

        (StateKind From, StateKind To)? seen = null;
        controller.Transitioned += (from, to) => seen = (from, to);

        controller.Request(StateKind.Walk);
        controller.Advance(0.016);

        Assert.Equal((StateKind.Idle, StateKind.Walk), seen);
    }
}
