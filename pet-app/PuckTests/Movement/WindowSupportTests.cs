using System.Windows;
using Puck.Movement;
using Puck.WindowSensing;

namespace PuckTests.Movement;

public class WindowSupportTests
{
    private const double AvatarHeight = 133;

    private static WindowInfo At(int handle, double left, double top, double width, double height, int pid = 1)
        => new(new IntPtr(handle), pid, "App", "창", new Rect(left, top, width, height), false, false);

    // --- CoveringWindow ---

    [Fact]
    public void ThePetIsCoveredWhenItsBodyIsInsideAWindow()
    {
        // 발이 아니라 몸통 가운데로 본다: 발(y=500)은 창 밖이지만 몸통은 안이다.
        var window = At(1, 0, 300, 800, 190);
        Assert.NotNull(WindowSupport.CoveringWindow(new Point(400, 500), AvatarHeight, [window]));
    }

    [Fact]
    public void ThePetStandingOnTopOfAWindowIsNotInsideIt()
    {
        var window = At(1, 0, 500, 800, 300);
        Assert.Null(WindowSupport.CoveringWindow(new Point(400, 500), AvatarHeight, [window]));
    }

    // --- PerchTarget ---

    [Fact]
    public void PerchingPutsTheWholeBodyOverTheEdge()
    {
        var window = At(1, 500, 400, 700, 300);
        var perch = WindowSupport.PerchTarget(window, new Point(480, 1000),
            roamableTop: 0, avatarHeight: AvatarHeight, petHalfWidth: 65);

        Assert.Equal(new Point(565, 400), perch);   // 500 + 65
    }

    [Fact]
    public void PerchingKeepsTheXItAlreadyHasWhenThatFits()
    {
        var window = At(1, 500, 400, 700, 300);
        var perch = WindowSupport.PerchTarget(window, new Point(800, 1000), 0, AvatarHeight, 65);
        Assert.Equal(new Point(800, 400), perch);
    }

    [Fact]
    public void AWindowWithoutHeadroomIsNoPerch()
    {
        var maximised = At(1, 0, 10, 1920, 1000);
        Assert.Null(WindowSupport.PerchTarget(maximised, new Point(500, 1000), 0, AvatarHeight, 65));
    }

    [Fact]
    public void AWindowNarrowerThanThePetIsNoPerch()
    {
        var sliver = At(1, 500, 400, 60, 300);
        Assert.Null(WindowSupport.PerchTarget(sliver, new Point(520, 1000), 0, AvatarHeight, 65));
    }

    // --- SupportingWindow ---

    [Fact]
    public void TheWindowUnderfootIsTheOneWhoseTopEdgeThePetIsOn()
    {
        var window = At(1, 200, 400, 600, 300);
        Assert.NotNull(WindowSupport.SupportingWindow(new Point(500, 402), [window]));
        Assert.Null(WindowSupport.SupportingWindow(new Point(500, 450), [window]));
        Assert.Null(WindowSupport.SupportingWindow(new Point(900, 400), [window]));
    }

    // --- BlockingWindow ---

    [Fact]
    public void WalkingIntoAWindowsSideIsBlocked()
    {
        var window = At(1, 600, 300, 400, 500);
        var blocking = WindowSupport.BlockingWindow(
            new Point(400, 500), new Point(900, 500), [window], roamableTop: 0, avatarHeight: AvatarHeight);
        Assert.NotNull(blocking);
    }

    [Fact]
    public void AnEdgeBeyondWhereThePetIsHeadedIsNotAWall()
    {
        var window = At(1, 600, 300, 400, 500);
        Assert.Null(WindowSupport.BlockingWindow(
            new Point(400, 500), new Point(550, 500), [window], 0, AvatarHeight));
    }

    [Fact]
    public void AWindowThePetIsNotLevelWithIsNotAWall()
    {
        // 펫은 y=900, 창은 300..800에만 있다 — 손이 닿지 않는다.
        var window = At(1, 600, 300, 400, 500);
        Assert.Null(WindowSupport.BlockingWindow(
            new Point(400, 900), new Point(900, 900), [window], 0, AvatarHeight));
    }

    [Fact]
    public void AnEdgeHiddenBehindAFrontWindowIsNotAWall()
    {
        // 가려진 모서리를 벽으로 치면, 밖에서 보기에 펫이 허공을 올라간다.
        var front = At(1, 0, 200, 1200, 700);
        var behind = At(2, 600, 300, 400, 500);
        Assert.Null(WindowSupport.BlockingWindow(
            new Point(400, 500), new Point(900, 500), [front, behind], 0, AvatarHeight));
    }

    [Fact]
    public void AMaximisedWindowIsWalkedPastRatherThanClimbed()
    {
        var maximised = At(1, 0, 10, 1920, 1000);
        Assert.Null(WindowSupport.BlockingWindow(
            new Point(400, 500), new Point(900, 500), [maximised], 0, AvatarHeight));
    }

    [Fact]
    public void AnExcludedWindowIsWalkedPast()
    {
        // 설정의 "포커스된 창 위로는 올라가지 않기"가 여기로 들어온다.
        var window = At(1, 600, 300, 400, 500);
        Assert.Null(WindowSupport.BlockingWindow(
            new Point(400, 500), new Point(900, 500), [window], 0, AvatarHeight,
            excluding: new HashSet<IntPtr> { new(1) }));
    }

    [Fact]
    public void TheNearestWallWinsWhenTwoAreOnTheWay()
    {
        var near = At(1, 600, 300, 100, 500);
        var far = At(2, 800, 300, 100, 500);
        var blocking = WindowSupport.BlockingWindow(
            new Point(400, 500), new Point(1000, 500), [near, far], 0, AvatarHeight);
        Assert.Equal(new IntPtr(1), blocking!.Handle);
    }

    // --- NearestClimbTarget ---

    [Fact]
    public void TheClimbTargetIsJustPastTheNearestReachableEdge()
    {
        var window = At(1, 600, 300, 400, 500);
        var target = WindowSupport.NearestClimbTarget(
            new Point(400, 500), [window], roamableTop: 0, avatarHeight: AvatarHeight);

        Assert.NotNull(target);
        Assert.Equal(604, target!.Value.X);      // 600 + 넘겨 겨누기 4
        Assert.Equal(500, target.Value.Y);
    }

    [Fact]
    public void StandingInFrontOfAWindowThereIsNothingToAimAt()
    {
        // 두 모서리 다 펫의 반대편이 아니다 — 걸어가 봐야 그냥 지나친다.
        var window = At(1, 300, 300, 600, 500);
        Assert.Null(WindowSupport.NearestClimbTarget(
            new Point(500, 500), [window], 0, AvatarHeight));
    }

    [Fact]
    public void AlreadyAtTheEdgeMeansPickSomethingElse()
    {
        var window = At(1, 600, 300, 400, 500);
        Assert.Null(WindowSupport.NearestClimbTarget(
            new Point(598, 500), [window], 0, AvatarHeight));
    }

    [Fact]
    public void WithNothingClimbableTheAnswerIsNothing()
    {
        Assert.Null(WindowSupport.NearestClimbTarget(new Point(400, 500), [], 0, AvatarHeight));
    }

    // --- WindowBeingClimbed / FocusedWindow ---

    [Fact]
    public void TheWallBeingHeldIsTheOneThePetIsPressedAgainst()
    {
        var window = At(1, 600, 300, 400, 500);
        Assert.NotNull(WindowSupport.WindowBeingClimbed(new Point(602, 500), [window]));
        Assert.Null(WindowSupport.WindowBeingClimbed(new Point(500, 500), [window]));
    }

    [Fact]
    public void TheFocusedWindowIsTheFrontmostOneOfThatProcess()
    {
        var other = At(1, 0, 0, 500, 500, pid: 7);
        var mine = At(2, 0, 0, 500, 500, pid: 9);
        Assert.Equal(new IntPtr(2), WindowSupport.FocusedWindow(9, [other, mine])!.Handle);
        Assert.Null(WindowSupport.FocusedWindow(null, [other, mine]));
    }
}
