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
        MakeContext(Point start, IReadOnlyList<WindowInfo>? windows = null)
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

    // --- Ceiling ---

    [Fact]
    public void 천장에_들어서면_거꾸로_매달린다()
    {
        var (context, body, _) = MakeContext(new Point(500, 0));
        var state = new CeilingState(duration: () => 5);
        state.Enter();

        state.Update(0.1, context);

        Assert.True(body.IsUpsideDown);
    }

    /// 창 윗변에서는 걸어 나가 떨어지지만, 천장에는 걸어 나갈 끝이 없다.
    [Fact]
    public void 영역의_가로_끝에서는_떨어지지_않고_돌아선다()
    {
        var (context, body, requested) = MakeContext(new Point(940, 0));
        var state = new CeilingState(duration: () => 100);
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
        var state = new CeilingState(duration: () => 1);
        state.Enter();

        for (var i = 0; i < 40 && requested.Count == 0; i++) state.Update(0.05, context);

        Assert.Equal([StateKind.Fall], requested);
    }
}
