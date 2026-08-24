using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

public class ScreenSpaceTests
{
    // 1920×1080 주 모니터(작업표시줄 40px) + 오른쪽에 붙은 1280×1024 보조 모니터.
    private static ScreenSpace TwoMonitors() => new(
        screenBoundsList: [new Rect(0, 0, 1920, 1080), new Rect(1920, 0, 1280, 1024)],
        workingAreas: [new Rect(0, 0, 1920, 1040), new Rect(1920, 0, 1280, 1024)]);

    [Fact]
    public void BoundsIsTheUnionOfEveryDisplay()
    {
        Assert.Equal(new Rect(0, 0, 3200, 1080), TwoMonitors().Bounds);
    }

    [Fact]
    public void RoamableAreaExcludesTheTaskbar()
    {
        // 작업표시줄이 있는 주 모니터의 아래 40px은 작업 영역 밖이지만,
        // 합집합 경계 상자는 보조 모니터를 포함하므로 높이는 1040이 아니라 1040이다.
        var roamable = TwoMonitors().RoamableArea;
        Assert.Equal(0, roamable.X);
        Assert.Equal(3200, roamable.Width);
        Assert.Equal(1040, roamable.Bottom);
    }

    [Fact]
    public void ScreenContainingFindsTheRightDisplay()
    {
        var space = TwoMonitors();
        Assert.Equal(new Rect(0, 0, 1920, 1080), space.ScreenContaining(new Point(100, 100)));
        Assert.Equal(new Rect(1920, 0, 1280, 1024), space.ScreenContaining(new Point(2000, 100)));
    }

    [Fact]
    public void PointOffEveryDisplayFallsBackToTheNearestOne()
    {
        // 두 모니터의 높이가 달라서 생기는 계단 자리 — 실제로 존재하는 좌표다.
        var space = TwoMonitors();
        Assert.Equal(new Rect(1920, 0, 1280, 1024), space.ScreenContaining(new Point(2000, 1060)));
    }

    [Fact]
    public void FloorIsTheWorkingAreaBottomOfTheDisplayUnderfoot()
    {
        var space = TwoMonitors();
        Assert.Equal(1040, space.FloorY(new Point(100, 500)));   // 작업표시줄 위
        Assert.Equal(1024, space.FloorY(new Point(2000, 500)));  // 보조 모니터 바닥
    }

    [Fact]
    public void APointOnADisplayHasGroundUnderIt()
    {
        Assert.True(TwoMonitors().HasGroundUnder(new Point(100, 500)));
        Assert.True(TwoMonitors().HasGroundUnder(new Point(2000, 1024)));
    }

    [Fact]
    public void TheGapInAStaircaseLayoutHasNoGroundUnderIt()
    {
        // 두 모니터의 높이가 달라서 생기는 계단 자리. 경계 상자(RoamableArea)
        // 안이지만 어느 디스플레이에도 속하지 않는다 — 여기 선 펫은 안 보인다.
        var staircase = new ScreenSpace(
            screenBoundsList: [new Rect(0, 0, 1920, 1080), new Rect(1920, -407, 1080, 1920)],
            workingAreas: [new Rect(0, 0, 1920, 1032), new Rect(1920, -407, 1080, 1872)]);

        Assert.True(staircase.RoamableArea.Contains(new Point(500, 1465)));
        Assert.False(staircase.HasGroundUnder(new Point(500, 1465)));
    }

    // 1920×1080 주 모니터 + 오른쪽에 위로 어긋나게 붙은 세로 1080×1920 모니터.
    // 경계 상자는 (0,-407)-(3000,1465)라, 주 모니터 아래이면서 세로 모니터
    // 왼쪽인 자리가 어느 화면에도 속하지 않는다.
    private static ScreenSpace Staircase() => new(
        screenBoundsList: [new Rect(0, 0, 1920, 1080), new Rect(1920, -407, 1080, 1920)],
        workingAreas: [new Rect(0, 0, 1920, 1032), new Rect(1920, -407, 1080, 1872)]);

    [Fact]
    public void APetThrownAboveADisplayStillHasGroundUnderIt()
    {
        // 위로 솟은 펫은 아직 자기 화면 위에 있다 — 내려오면 그 바닥에 착지한다.
        Assert.True(Staircase().HasGroundUnder(new Point(500, -200)));
    }

    [Fact]
    public void TheGapIsPulledBackOntoTheNearestDisplay()
    {
        var staircase = Staircase();
        // 세로 모니터 바닥 높이인데 그 왼쪽 — 여기 착지하면 펫이 보이지 않는다.
        var stuck = new Point(1842, 1465);
        Assert.False(staircase.HasGroundUnder(stuck));

        // 폭 130짜리 펫: 그림이 통째로 화면 안에 들어와야 하므로 발은
        // 세로 모니터 왼쪽 끝(1920)이 아니라 거기서 반 폭만큼 안쪽이다.
        var pet = new Rect(-65, -133, 130, 133);
        var rescued = staircase.NearestStandablePoint(stuck, pet);
        Assert.True(staircase.HasGroundUnder(rescued));
        Assert.Equal(1985, rescued.X);
        Assert.Equal(1465, rescued.Y);
    }

    [Fact]
    public void APointBelowTheMainDisplayComesUpToItsFloor()
    {
        var pet = new Rect(-65, -133, 130, 133);
        var rescued = Staircase().NearestStandablePoint(new Point(500, 1465), pet);
        Assert.Equal(500, rescued.X);
        Assert.Equal(1032, rescued.Y);
    }

    [Fact]
    public void TheHigherScreenNextDoorIsAClimbableLedge()
    {
        // 세로 모니터 바닥(1465)에 선 펫이 왼쪽으로 가려 한다. 그쪽에는
        // 바닥이 1032인 주 모니터가 있다 — 걸어서는 못 가고, 타고 올라야 한다.
        var pet = new Rect(-65, -133, 130, 133);
        var ledge = Staircase().LedgeBeyond(new Point(1985, 1465), directionX: -1, pet);

        Assert.NotNull(ledge);
        Assert.Equal(1855, ledge!.Value.X);   // 1920 - 65: 그림이 통째로 주 모니터 안
        Assert.Equal(1032, ledge.Value.Y);
    }

    [Fact]
    public void ThereIsNoLedgeTowardsTheOpenSideOfTheScreen()
    {
        var pet = new Rect(-65, -133, 130, 133);
        // 오른쪽에는 더 높은 화면이 없다.
        Assert.Null(Staircase().LedgeBeyond(new Point(1985, 1465), directionX: 1, pet));
        // 주 모니터 바닥에서는 어느 쪽으로도 올라갈 턱이 없다.
        Assert.Null(Staircase().LedgeBeyond(new Point(100, 1032), directionX: -1, pet));
    }

    [Fact]
    public void EmptyDisplayListIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new ScreenSpace([], []));
    }
}
