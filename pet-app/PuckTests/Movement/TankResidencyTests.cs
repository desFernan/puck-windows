using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

public class TankResidencyTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void AnUnusableTankLeavesNowhereToGo()
    {
        var residency = new TankResidency();

        residency.Report(new Rect(0, 0, 20, 20), Screen, new Size(100, 130));

        Assert.Null(residency.Area);
        Assert.Null(residency.StandingPoint());
    }

    [Fact]
    public void ThePetStandsInTheMiddleOfTheTanksFloor()
    {
        var residency = new TankResidency();
        residency.Report(new Rect(100, 200, 800, 200), Screen, new Size(60, 80));

        Assert.Equal(new Point(500, 400), residency.StandingPoint());
    }

    [Fact]
    public void ANarrowTankSizesThePetDownRatherThanRefusingIt()
    {
        // 거절은 밖에서 보면 조용하다 — 펫이 그냥 남고 창이 고장 난 것처럼
        // 보인다. 그래서 크기가 양보한다.
        var residency = new TankResidency { PetHeight = 100 };
        residency.Report(new Rect(0, 0, 200, 400), Screen, new Size(50, 50));

        // 펫이 정사각형이면 200 너비에는 두 마리가 100씩 들어간다.
        Assert.Equal(1, residency.ScaleFor(new Size(100, 100)));

        residency.PetHeight = 300;
        Assert.Equal(1, residency.ScaleFor(new Size(100, 100)));
    }

    [Fact]
    public void WithNoTankTheAskedHeightIsWhatItGets()
    {
        var residency = new TankResidency { PetHeight = 56 };

        Assert.Equal(0.56, residency.ScaleFor(new Size(100, 100)), precision: 6);
    }

    [Fact]
    public void AnAvatarWithNoSizeYetDoesNotDivideByIt()
    {
        Assert.Equal(1, new TankResidency().ScaleFor(new Size(0, 0)));
    }

    [Fact]
    public void ReportingNothingClearsWhatWasThere()
    {
        var residency = new TankResidency();
        residency.Report(new Rect(100, 200, 800, 200), Screen, new Size(60, 80));
        Assert.NotNull(residency.Area);

        residency.Report(null, Screen, new Size(60, 80));

        Assert.Null(residency.Area);
        Assert.Null(residency.LastReportedSize);
    }
}
