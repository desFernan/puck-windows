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
    public void EmptyDisplayListIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new ScreenSpace([], []));
    }
}
