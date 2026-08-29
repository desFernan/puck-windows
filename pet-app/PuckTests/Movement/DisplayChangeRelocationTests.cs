using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

/// 모니터를 뽑아 보지 않고 규칙을 시험한다.
public class DisplayChangeRelocationTests
{
    /// 발이 원점, 그림은 위로 100.
    private static readonly Rect Pet = new(-50, -100, 100, 100);

    [Fact]
    public void AShorterScreenBringsThePetDownToItsNewFloor()
    {
        // 새 바닥이 펫 위로 올라왔다. 가두기만으로 답이 된다.
        var area = new Rect(0, 0, 1000, 600);

        Assert.Equal(600, DisplayChangeRelocation.Contained(new Point(500, 800), Pet, area).Y);
    }

    [Fact]
    public void ATallerScreenPutsTheStandingPetOnWhatIsActuallyUnderIt()
    {
        // 영역이 커진 순간 펫은 이미 그 안에 있어서 가두기가 할 일을 찾지
        // 못한다 — 옛 바닥이 있던 선 위에 아무것도 없이 남는다.
        var area = new Rect(0, 0, 1000, 1400);

        Assert.Equal(800, DisplayChangeRelocation.Contained(new Point(500, 800), Pet, area).Y);
        Assert.Equal(1400, DisplayChangeRelocation.Standing(new Point(500, 800), Pet, area, _ => 1400).Y);
    }

    [Fact]
    public void ItComesDownOnASurfaceNotJustTheAreasFloor()
    {
        // 딛고 있는 것은 화면 바닥만큼이나 자주 창의 윗변이다.
        var area = new Rect(0, 0, 1000, 1400);

        Assert.Equal(900, DisplayChangeRelocation.Standing(new Point(500, 800), Pet, area, _ => 900).Y);
    }

    [Fact]
    public void TheHeadDoesNotGoThroughTheCeiling()
    {
        var area = new Rect(0, 200, 1000, 600);

        // 발이 올라갈 수 있는 가장 높은 자리는 영역 위 끝에서 그림 높이만큼 아래.
        Assert.Equal(300, DisplayChangeRelocation.Contained(new Point(500, -500), Pet, area).Y);
    }

    [Fact]
    public void WhenTheAreaIsShorterThanThePetTheFloorWins()
    {
        // 맞는 자리가 없다. 머리가 삐져나오더라도 발은 바닥에 둔다.
        var area = new Rect(0, 0, 1000, 40);

        Assert.Equal(40, DisplayChangeRelocation.Contained(new Point(500, 500), Pet, area).Y);
    }

    [Fact]
    public void ThePetIsKeptInsideHorizontallyToo()
    {
        var area = new Rect(0, 0, 1000, 800);

        Assert.Equal(950, DisplayChangeRelocation.Contained(new Point(5000, 800), Pet, area).X);
    }
}

/// 무언가를 붙잡고 있는 펫은 세계가 다시 재어져도 놓지 않는다.
///
/// 상태를 새로 만들고 이 목록에 더하기를 잊으면 조용히 "바닥에 서 있다"가
/// 되어, 발밑에 아무것도 없는 펫이 해상도가 바뀌는 순간 바닥으로
/// 순간이동한다. 천장 상태 둘이 실제로 그렇게 빠져 있었다.
public class HoldingOnTests
{
    [Theory]
    [InlineData(StateKind.Climb)]
    [InlineData(StateKind.ClimbLedge)]
    [InlineData(StateKind.ClimbToCeiling)]
    [InlineData(StateKind.Ceiling)]
    [InlineData(StateKind.ReactDrag)]
    [InlineData(StateKind.Fall)]
    public void 붙잡고_있으면_내려놓지_않는다(StateKind state)
    {
        Assert.False(DisplayChangeRelocation.StandsOnGround(state));
    }

    [Theory]
    [InlineData(StateKind.Idle)]
    [InlineData(StateKind.Walk)]
    [InlineData(StateKind.Land)]
    [InlineData(StateKind.WalkOnTop)]
    [InlineData(StateKind.MoveTo)]
    [InlineData(StateKind.ReactClick)]
    public void 발로_서_있으면_새_바닥에_내려놓는다(StateKind state)
    {
        Assert.True(DisplayChangeRelocation.StandsOnGround(state));
    }

    /// 두 목록이 모든 상태를 덮는지. 새 상태가 생기면 여기서 걸린다.
    ///
    /// 세는 것이 아니라 맞춰 본다 — 개수만 세면 상태를 하나 지우고 하나
    /// 더하는 변경이 그대로 지나간다.
    [Fact]
    public void 모든_상태가_둘_중_하나로_분류된다()
    {
        StateKind[] holding =
            [StateKind.Climb, StateKind.ClimbLedge, StateKind.ClimbToCeiling,
             StateKind.Ceiling, StateKind.ReactDrag, StateKind.Fall];
        StateKind[] standing =
            [StateKind.Idle, StateKind.Walk, StateKind.Land,
             StateKind.WalkOnTop, StateKind.MoveTo, StateKind.ReactClick];

        Assert.Equal(
            Enum.GetValues<StateKind>().OrderBy(s => s),
            holding.Concat(standing).OrderBy(s => s));

        Assert.All(holding, s => Assert.False(DisplayChangeRelocation.StandsOnGround(s)));
        Assert.All(standing, s => Assert.True(DisplayChangeRelocation.StandsOnGround(s)));
    }
}
