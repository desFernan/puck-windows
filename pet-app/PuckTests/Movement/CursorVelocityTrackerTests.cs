using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

public class CursorVelocityTrackerTests
{
    [Fact]
    public void NoSamplesMeansNoVelocity()
    {
        Assert.Equal(new Vector(0, 0), new CursorVelocityTracker().Velocity);
    }

    [Fact]
    public void ASingleSampleStillMeansNoVelocity()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0);
        Assert.Equal(new Vector(0, 0), tracker.Velocity);
    }

    [Fact]
    public void TwoSamplesGiveThePixelsPerSecondBetweenThem()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0);
        tracker.Record(new Point(100, -50), 0.5);
        Assert.Equal(200, tracker.Velocity.X, precision: 6);
        Assert.Equal(-100, tracker.Velocity.Y, precision: 6);
    }

    [Fact]
    public void VelocityIsSmoothedAcrossTheRecentSamplesNotJustTheLastPair()
    {
        // 마지막 한 쌍만 보면 손을 멈춘 채로 놓는 순간 속도가 0이 되어
        // 세게 휘두른 던지기가 제자리 낙하가 된다.
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0.00);
        tracker.Record(new Point(100, 0), 0.02);
        tracker.Record(new Point(200, 0), 0.04);
        tracker.Record(new Point(200, 0), 0.06);   // 마지막 순간 정지
        Assert.True(tracker.Velocity.X > 1000);
    }

    [Fact]
    public void SamplesOlderThanTheWindowAreForgotten()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0);
        tracker.Record(new Point(10_000, 0), 0.001);   // 아주 빠른 옛날 움직임
        tracker.Record(new Point(10_000, 0), 5.0);     // 5초 뒤
        tracker.Record(new Point(10_010, 0), 5.1);
        Assert.True(tracker.Velocity.X < 200);
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0);
        tracker.Record(new Point(500, 0), 0.1);
        tracker.Reset();
        Assert.Equal(new Vector(0, 0), tracker.Velocity);
    }

    [Fact]
    public void SamplesAtTheSameTimestampDoNotDivideByZero()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 1.0);
        tracker.Record(new Point(50, 0), 1.0);
        Assert.Equal(new Vector(0, 0), tracker.Velocity);
    }
}
