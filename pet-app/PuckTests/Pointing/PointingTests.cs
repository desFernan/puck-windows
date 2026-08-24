using System.Windows;
using Puck.Pointing;

namespace PuckTests.Pointing;

public class PendingPointTrackerTests
{
    [Fact]
    public void NothingIsPendingUntilThePetPoints()
    {
        var tracker = new PendingPointTracker();
        Assert.False(tracker.IsPending);
        Assert.False(tracker.Accepts(new Point(100, 100), 0));
    }

    [Fact]
    public void AClickOnWhatWasPointedAtCounts()
    {
        var tracker = new PendingPointTracker();
        tracker.Point(new Point(500, 400), now: 0);
        Assert.True(tracker.Accepts(new Point(510, 410), now: 2));
    }

    [Fact]
    public void AClickSomewhereElseDoesNotCount()
    {
        // 사람이 딴 데를 누른 것까지 성공으로 보고하면 안 된다.
        var tracker = new PendingPointTracker();
        tracker.Point(new Point(500, 400), now: 0);
        Assert.False(tracker.Accepts(new Point(900, 400), now: 2));
        Assert.True(tracker.IsPending);      // 아직 기다린다
    }

    [Fact]
    public void AClickLongAfterwardsDoesNotCount()
    {
        var tracker = new PendingPointTracker();
        tracker.Point(new Point(500, 400), now: 0);
        Assert.False(tracker.Accepts(new Point(500, 400), now: PendingPointTracker.WindowSeconds + 1));
        Assert.False(tracker.IsPending);
    }

    [Fact]
    public void AcceptingEndsTheWait()
    {
        var tracker = new PendingPointTracker();
        tracker.Point(new Point(500, 400), now: 0);
        Assert.True(tracker.Accepts(new Point(500, 400), now: 1));
        Assert.False(tracker.IsPending);
        Assert.False(tracker.Accepts(new Point(500, 400), now: 2));
    }

    [Fact]
    public void TheWaitExpiresOnItsOwn()
    {
        var tracker = new PendingPointTracker();
        tracker.Point(new Point(500, 400), now: 0);
        tracker.Expire(now: 5);
        Assert.True(tracker.IsPending);
        tracker.Expire(now: PendingPointTracker.WindowSeconds + 1);
        Assert.False(tracker.IsPending);
    }
}

public class SyntheticClickTests
{
    private static readonly Rect VirtualScreen = new(0, -407, 3000, 1920);

    [Fact]
    public void TheTopLeftOfTheVirtualScreenIsZero()
    {
        var (x, y) = SyntheticClick.Normalise(new Point(0, -407), VirtualScreen);
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void TheBottomRightIsTheFullRange()
    {
        var (x, y) = SyntheticClick.Normalise(new Point(3000, 1513), VirtualScreen);
        Assert.Equal(65535, x);
        Assert.Equal(65535, y);
    }

    [Fact]
    public void APointOnASecondMonitorIsNotMeasuredFromThePrimaryOne()
    {
        // 주 모니터 기준으로 계산하면 보조 모니터에서 엉뚱한 곳을 누른다.
        var (x, _) = SyntheticClick.Normalise(new Point(2400, 0), VirtualScreen);
        Assert.Equal((int)Math.Round(2400.0 / 3000 * 65535), x);
    }

    [Fact]
    public void APointOffScreenIsClampedRatherThanWrappingAround()
    {
        var (x, y) = SyntheticClick.Normalise(new Point(-500, 99999), VirtualScreen);
        Assert.Equal(0, x);
        Assert.Equal(65535, y);
    }

    [Fact]
    public void ADegenerateScreenDoesNotDivideByZero()
    {
        var (x, y) = SyntheticClick.Normalise(new Point(10, 10), Rect.Empty);
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }
}
