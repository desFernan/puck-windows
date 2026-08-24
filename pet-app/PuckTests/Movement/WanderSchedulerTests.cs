using Puck.Movement;

namespace PuckTests.Movement;

public class WanderSchedulerTests
{
    [Fact]
    public void DoesNotFireBeforeTheMinimumInterval()
    {
        var scheduler = new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(3),
            MaximumInterval = TimeSpan.FromSeconds(3),
        };
        scheduler.Reset();
        Assert.False(scheduler.Tick(2.9));
    }

    [Fact]
    public void FiresOnceTheIntervalElapses()
    {
        var scheduler = new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(3),
            MaximumInterval = TimeSpan.FromSeconds(3),
        };
        scheduler.Reset();
        Assert.False(scheduler.Tick(2.0));
        Assert.True(scheduler.Tick(1.5));
    }

    [Fact]
    public void RearmsItselfAfterFiring()
    {
        var scheduler = new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(1),
        };
        scheduler.Reset();
        Assert.True(scheduler.Tick(1.0));
        Assert.False(scheduler.Tick(0.5));
        Assert.True(scheduler.Tick(0.5));
    }

    [Fact]
    public void IntervalStaysWithinItsRange()
    {
        var scheduler = new WanderScheduler(new Random(42))
        {
            MinimumInterval = TimeSpan.FromSeconds(3),
            MaximumInterval = TimeSpan.FromSeconds(9),
        };
        for (var i = 0; i < 100; i++)
        {
            scheduler.Reset();
            Assert.InRange(scheduler.NextInterval.TotalSeconds, 3, 9);
        }
    }
}
