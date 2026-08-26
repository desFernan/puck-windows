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

    [Theory]
    [InlineData(StateKind.Climb)]
    [InlineData(StateKind.ClimbLedge)]
    [InlineData(StateKind.ReactDrag)]
    [InlineData(StateKind.Fall)]
    public void APetHoldingSomethingThatIsNotBelowItKeepsItsPlace(StateKind state)
    {
        // 그런 펫의 발을 바닥에 놓으면 화면을 가로질러 떨어뜨리는 것이 된다.
        Assert.False(DisplayChangeRelocation.StandsOnGround(state));
    }

    [Theory]
    [InlineData(StateKind.Idle)]
    [InlineData(StateKind.Walk)]
    [InlineData(StateKind.MoveTo)]
    [InlineData(StateKind.WalkOnTop)]
    public void APetOnTheGroundIsPutDownAgain(StateKind state)
    {
        Assert.True(DisplayChangeRelocation.StandsOnGround(state));
    }
}
