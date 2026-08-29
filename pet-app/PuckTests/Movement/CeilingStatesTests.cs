using System.Windows;
using Puck.Avatar;
using Puck.Movement;
using Puck.Movement.States;
using Puck.WindowSensing;

namespace PuckTests.Movement;

/// 천장으로 오르기와 천장 기어가기.
public class CeilingStatesTests
{
    private static readonly Rect Area = new(0, 0, 1000, 800);
    private static readonly Rect Pet = new(-50, -100, 100, 100);

    private static WindowInfo At(int handle, double left, double top, double width, double height)
        => new(new IntPtr(handle), 1, "App", "창", new Rect(left, top, width, height), false, false);

    private static (StateContext Context, CharacterBody Body, List<StateKind> Requested)
        MakeContext(Point start, IReadOnlyList<WindowInfo>? windows = null, ScreenNotch? notch = null)
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
            AreaAt = _ => Area,
            NotchOver = _ => notch,
            CeilingAt = (x, area) => notch?.Ceiling(x, area.Top) ?? area.Top,
            Windows = windows ?? [],
            RequestTransition = requested.Add,
        }, body, requested);
    }

    // --- ClimbToCeiling ---

    /// 창의 옆면이 아니라 화면의 옆면을 잡고도 오를 수 있어야 한다. 그게
    /// 없으면 최대화된 창 하나짜리 바탕화면에서 천장은 닿을 수 없는 곳이다.
    [Fact]
    public void 화면_가장자리에_붙어_있으면_오른다()
    {
        // 발이 50이면 그림은 0..100 — 왼쪽 끝이 화면의 왼쪽 모서리다.
        var (context, body, requested) = MakeContext(new Point(50, 800));
        var state = new ClimbToCeilingState();

        state.Update(0.1, context);

        Assert.Empty(requested);
        Assert.True(body.Position.Y < 800);
    }

    [Fact]
    public void 잡을_것이_없으면_떨어진다()
    {
        var (context, _, requested) = MakeContext(new Point(500, 800));
        var state = new ClimbToCeilingState();

        state.Update(0.1, context);

        Assert.Equal([StateKind.Fall], requested);
    }

    [Fact]
    public void 창의_옆면을_잡고도_오른다()
    {
        var window = At(1, 400, 200, 300, 400);
        var (context, body, requested) = MakeContext(new Point(400, 400), [window]);
        var state = new ClimbToCeilingState();

        state.Update(0.1, context);

        Assert.Empty(requested);
        Assert.True(body.Position.Y < 400);
    }

    /// 발이 천장까지 가면 도착 전에 머리가 화면 밖으로 나간다. 머리가 천장에
    /// 막 닿는 데서 멈춰야, 뒤집힘이 도약이 아니라 제자리에서 도는 것으로 읽힌다.
    [Fact]
    public void 머리가_천장에_닿는_데서_멈추고_천장으로_넘긴다()
    {
        var (context, body, requested) = MakeContext(new Point(50, 105));
        var state = new ClimbToCeilingState();

        for (var i = 0; i < 60 && requested.Count == 0; i++) state.Update(0.05, context);

        Assert.Equal([StateKind.Ceiling], requested);
        // 천장 0 + 아바타 높이 100. 도착 반경 안에서 멈추므로 딱 떨어지지는 않는다.
        Assert.InRange(body.Position.Y, 100, 100 + MovementSolver.ArrivalRadius);
    }

    /// 노치 아래에서 오르면 천장은 화면의 윗변이 아니라 노치의 아랫변이다.
    [Fact]
    public void 노치_아래에서는_노치의_아랫변까지만_오른다()
    {
        var notch = new ScreenNotch(new Rect(0, 0, 200, 30));
        var (context, body, requested) = MakeContext(new Point(50, 400), notch: notch);
        var state = new ClimbToCeilingState();

        for (var i = 0; i < 120 && requested.Count == 0; i++) state.Update(0.05, context);

        Assert.Equal([StateKind.Ceiling], requested);
        // 노치 아랫변 30 + 아바타 높이 100.
        Assert.InRange(body.Position.Y, 130, 130 + MovementSolver.ArrivalRadius);
    }

    // --- Ceiling ---

    [Fact]
    public void 천장에_들어서면_거꾸로_매달린다()
    {
        var (context, body, _) = MakeContext(new Point(500, 0));
        var state = new CeilingState(duration: () => 5, hang: () => 1);
        state.Enter();

        state.Update(0.1, context);

        Assert.True(body.IsUpsideDown);
    }

    /// 창 윗변에서는 걸어 나가 떨어지지만, 천장에는 걸어 나갈 끝이 없다.
    [Fact]
    public void 영역의_가로_끝에서는_떨어지지_않고_돌아선다()
    {
        var (context, body, requested) = MakeContext(new Point(940, 0));
        var state = new CeilingState(duration: () => 100, hang: () => 1);
        state.Enter();

        for (var i = 0; i < 40; i++) state.Update(0.05, context);

        Assert.Empty(requested);
        Assert.True(body.Position.X <= 950);          // 그림이 화면 안에 남는다
        Assert.Equal(AvatarFacing.Left, body.Facing); // 돌아섰다
    }

    [Fact]
    public void 시간이_다하면_떨어진다()
    {
        var (context, _, requested) = MakeContext(new Point(500, 0));
        var state = new CeilingState(duration: () => 1, hang: () => 1);
        state.Enter();

        for (var i = 0; i < 40 && requested.Count == 0; i++) state.Update(0.05, context);

        Assert.Equal([StateKind.Fall], requested);
    }

    /// 기어가기는 3~8초, 오른 벽에서 노치까지는 7초쯤 걸린다. 보지 않고
    /// 방향을 고르면 대부분의 기어가기를 노치에서 멀어지는 데 쓴다.
    [Fact]
    public void 노치가_있으면_그쪽으로_출발한다()
    {
        var notch = new ScreenNotch(new Rect(700, 0, 200, 30));
        var (context, body, _) = MakeContext(new Point(60, 0), notch: notch);
        var state = new CeilingState(duration: () => 100, hang: () => 1);
        state.Enter();

        state.Update(0.1, context);

        Assert.Equal(AvatarFacing.Right, body.Facing);
        Assert.True(body.Position.X > 60);
    }

    [Fact]
    public void 노치가_왼쪽에_있으면_왼쪽으로_출발한다()
    {
        var notch = new ScreenNotch(new Rect(100, 0, 200, 30));
        var (context, body, _) = MakeContext(new Point(900, 0), notch: notch);
        var state = new CeilingState(duration: () => 100, hang: () => 1);
        state.Enter();

        state.Update(0.1, context);

        Assert.Equal(AvatarFacing.Left, body.Facing);
        Assert.True(body.Position.X < 900);
    }

    /// 갈 데가 있는 기어가기는 중간에 멈추지 않는다 — 그렇지 않으면 위에서
    /// 볼 만한 단 하나를 못 본 채 끝난다.
    [Fact]
    public void 아직_매달리지_않았으면_시간이_다해도_떨어지지_않는다()
    {
        var notch = new ScreenNotch(new Rect(700, 0, 200, 30));
        var (context, _, requested) = MakeContext(new Point(60, 0), notch: notch);
        var state = new CeilingState(duration: () => 0.1, hang: () => 1);
        state.Enter();

        for (var i = 0; i < 20; i++) state.Update(0.05, context);

        Assert.Empty(requested);
    }

    [Fact]
    public void 노치_아래에서_한_번_멈춰_매달린다()
    {
        var notch = new ScreenNotch(new Rect(400, 0, 200, 30));
        var (context, body, _) = MakeContext(new Point(390, 0), notch: notch);
        var state = new CeilingState(duration: () => 100, hang: () => 1);
        state.Enter();

        // 노치 아래로 들어갈 때까지 긴다.
        for (var i = 0; i < 20 && body.Position.X < 400; i++) state.Update(0.05, context);
        var stopped = body.Position.X;

        // 매달려 있는 동안은 움직이지 않는다.
        for (var i = 0; i < 10; i++) state.Update(0.05, context);
        Assert.Equal(stopped, body.Position.X);

        // 매달림이 끝나면 다시 긴다.
        for (var i = 0; i < 30; i++) state.Update(0.05, context);
        Assert.NotEqual(stopped, body.Position.X);
    }

    /// 지날 때마다 멈추면 쉬는 것이 아니라 걸린 것으로 읽힌다.
    [Fact]
    public void 매달림은_기어가기마다_한_번뿐이다()
    {
        var notch = new ScreenNotch(new Rect(400, 0, 200, 30));
        var (context, body, _) = MakeContext(new Point(390, 0), notch: notch);
        var state = new CeilingState(duration: () => 100, hang: () => 0.2);
        state.Enter();

        // 노치를 지나 오른쪽 끝까지 갔다가 돌아와 다시 노치 아래를 지난다.
        for (var i = 0; i < 400; i++) state.Update(0.05, context);

        // 두 번째로 노치 밑을 지날 때 멈췄다면 여기 갇혀 있을 것이다.
        var before = body.Position.X;
        for (var i = 0; i < 5; i++) state.Update(0.05, context);
        Assert.NotEqual(before, body.Position.X);
    }

    /// 노치를 그대로 통과해 걸으면 카메라를 뚫고 지나가는 것으로 보인다.
    [Fact]
    public void 노치_아래를_지날_때_천장이_내려온다()
    {
        var notch = new ScreenNotch(new Rect(400, 0, 200, 30));
        var (context, body, _) = MakeContext(new Point(390, 0), notch: notch);
        var state = new CeilingState(duration: () => 100, hang: () => 0);
        state.Enter();

        for (var i = 0; i < 30 && body.Position.X < 450; i++) state.Update(0.05, context);

        Assert.InRange(body.Position.X, 400, 600);
        Assert.Equal(30, body.Position.Y);   // 화면 윗변 0이 아니라 노치 아랫변 30
    }
}
