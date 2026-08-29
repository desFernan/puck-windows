using System.Windows;
using Puck.Avatar;
using Puck.Movement;
using Puck.Movement.States;
using Puck.WindowSensing;

namespace PuckTests.Movement;

/// 화면 자신의 옆면을 벽으로 치는 것, 그리고 그 덕분에 닿게 된 천장.
///
/// 이게 없으면 오르기는 붙잡을 **창**을 요구하는데, `NearestClimbTarget`이
/// 머리 여유가 없는 창을 제외하므로 최대화된 창 하나짜리 바탕화면에서는
/// 오를 것이 하나도 없다. 그래서 천장은 배회가 뽑아 놓고 매번 버리는
/// 선택지였다.
public class ScreenEdgeClimbTests
{
    private static readonly Rect Area = new(0, 0, 1000, 800);
    private static readonly Rect Pet = new(-50, -100, 100, 100);

    private static WindowInfo At(int handle, double left, double top, double width, double height)
        => new(new IntPtr(handle), 1, "App", "창", new Rect(left, top, width, height), false, false);

    // --- IsAgainstScreenEdge ---

    /// 발밑 한 점이 아니라 **외곽선**에 묻는다. 가두기가 그림 전체를 화면
    /// 안에 두므로 접지점은 가장자리에서 반 폭 못 미쳐 멈추고 영영 닿지
    /// 않는다 — 맨 위치로 재는 검사는 참이 될 수 없는 검사다.
    [Fact]
    public void 그림의_왼쪽_끝이_화면_왼쪽에_닿으면_벽이다()
    {
        // 발이 50이면 그림은 0..100.
        Assert.True(WindowSupport.IsAgainstScreenEdge(new Point(50, 800), Pet, Area));
    }

    [Fact]
    public void 그림의_오른쪽_끝이_화면_오른쪽에_닿으면_벽이다()
    {
        // 발이 950이면 그림은 900..1000.
        Assert.True(WindowSupport.IsAgainstScreenEdge(new Point(950, 800), Pet, Area));
    }

    /// 발 위치로 재면 절대 참이 되지 않는다는 것을 못 박아 둔다.
    [Fact]
    public void 발이_화면_가장자리에_있는_것으로는_모자라다()
    {
        // 발이 0이면 그림은 -50..50 — 애초에 가두기가 여기 두지 않는다.
        // 반대로 발이 화면 안쪽 깊이 있으면 어느 쪽 끝도 가장자리가 아니다.
        Assert.False(WindowSupport.IsAgainstScreenEdge(new Point(500, 800), Pet, Area));
    }

    [Fact]
    public void 가장자리에서_멀면_벽이_아니다()
    {
        Assert.False(WindowSupport.IsAgainstScreenEdge(new Point(200, 800), Pet, Area));
    }

    // --- HasWall ---

    [Fact]
    public void 창도_화면_가장자리도_없으면_벽이_없다()
    {
        Assert.False(WindowSupport.HasWall(new Point(500, 800), Pet, [], Area));
    }

    [Fact]
    public void 창의_옆면은_벽이다()
    {
        var window = At(1, 400, 200, 300, 400);
        Assert.True(WindowSupport.HasWall(new Point(400, 400), Pet, [window], Area));
    }

    [Fact]
    public void 창이_하나도_없어도_화면_가장자리는_벽이다()
    {
        Assert.True(WindowSupport.HasWall(new Point(50, 800), Pet, [], Area));
    }

    /// 이 조합이 이 커밋의 이유다: 최대화된 창 하나만 있는 바탕화면.
    /// 그 창은 머리 여유가 없어 오를 수 없지만, 화면의 옆면은 남아 있다.
    [Fact]
    public void 최대화된_창만_있어도_오를_벽이_있다()
    {
        var maximised = At(1, 0, 0, 1000, 800);

        Assert.Null(WindowSupport.NearestClimbTarget(
            new Point(500, 800), [maximised], roamableTop: 0, avatarHeight: 100));
        Assert.True(WindowSupport.HasWall(new Point(50, 800), Pet, [maximised], Area));
    }

    // --- NearestScreenEdge ---

    /// 돌려주는 것은 펫의 **외곽선이** 그 가장자리에 닿는 발 자리다 —
    /// 가두기가 놓을 자리와 같은 곳이라, 걸음이 자기 목표 앞에서 영영
    /// 잘리지 않고 실제로 도착한다.
    [Fact]
    public void 가까운_쪽_가장자리로_그림이_닿는_발_자리를_준다()
    {
        var left = WindowSupport.NearestScreenEdge(new Point(200, 800), Pet, Area);
        Assert.Equal(50, left.X);
        Assert.Equal(800, left.Y);

        var right = WindowSupport.NearestScreenEdge(new Point(800, 800), Pet, Area);
        Assert.Equal(950, right.X);
    }

    /// 도착한 자리가 곧 벽으로 판정되어야 한다. 이 둘이 어긋나면 펫이
    /// 걸어가서는 오르지 않고 그냥 선다.
    [Fact]
    public void 그_자리에_서면_벽으로_판정된다()
    {
        foreach (var from in new[] { 200.0, 800.0 })
        {
            var target = WindowSupport.NearestScreenEdge(new Point(from, 800), Pet, Area);
            Assert.True(WindowSupport.IsAgainstScreenEdge(target, Pet, Area));
        }
    }

    /// 걸어 나갈 화면이 두 번째 모니터면 그 화면의 가장자리여야 한다.
    [Fact]
    public void 두_번째_화면에서는_그_화면의_가장자리다()
    {
        var right = new Rect(1000, 0, 1000, 800);
        var target = WindowSupport.NearestScreenEdge(new Point(1100, 800), Pet, right);
        Assert.Equal(1050, target.X);
    }

    // --- 배회 ---

    /// 천장이 다시 뽑히지 않으면 위의 모든 것에 닿을 길이 없다.
    [Fact]
    public void 배회가_천장을_뽑는다()
    {
        var outcomes = new HashSet<WanderOutcome>();
        var scheduler = new WanderScheduler(new Random(7))
        {
            MinimumInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(1),
        };
        scheduler.Reset();

        for (var i = 0; i < 200; i++)
            if (scheduler.Tick(1.1) is { } outcome) outcomes.Add(outcome);

        Assert.Contains(WanderOutcome.CrawlCeiling, outcomes);
    }

    // --- 뒤집힘 되돌리기 ---

    /// 어떤 상태든 다른 어떤 상태를 가로챌 수 있으므로(클릭, 드래그,
    /// 에이전트 명령) 들어설 때 한 곳에서 되돌리는 것만이 Ceiling에서
    /// **곧장** 넘어간 상태까지 바로 선다고 보장한다.
    [Fact]
    public void 천장을_떠나면_바로_선다()
    {
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, new Point(500, 0));
        var states = new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Ceiling] = new CeilingState(duration: () => 100, hang: () => 1),
            [StateKind.Idle] = new IdleState(new WanderScheduler(new Random(1))),
        };
        var controller = new CharacterController(body, states, StateKind.Ceiling, () => new StateContext
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
            Windows = [],
            RequestTransition = _ => { },
        });

        controller.Advance(0.1);
        Assert.True(body.IsUpsideDown);

        controller.Request(StateKind.Idle);
        controller.Advance(0.1);
        Assert.False(body.IsUpsideDown);
    }

    /// 천장에 머무는 동안은 뒤집힌 채로 둔다 — 매 프레임 세우고 바로
    /// 되돌리면 그림이 떨린다.
    [Fact]
    public void 천장에_머무는_동안은_뒤집힌_채로_둔다()
    {
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, new Point(500, 0));
        var ceiling = new CeilingState(duration: () => 100, hang: () => 1);
        var states = new Dictionary<StateKind, IStateHandler> { [StateKind.Ceiling] = ceiling };
        var controller = new CharacterController(body, states, StateKind.Ceiling, () => new StateContext
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
            Windows = [],
            RequestTransition = _ => { },
        });

        for (var i = 0; i < 10; i++) controller.Advance(0.05);
        Assert.True(body.IsUpsideDown);
    }
}
