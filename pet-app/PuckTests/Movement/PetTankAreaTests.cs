using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

public class PetTankAreaTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void ATankTooNarrowToWalkInIsRefusedRatherThanSqueezedInto()
    {
        // 좁은 틈에 끼워 넣은 펫은 펫이 아니라 버그로 읽힌다.
        var pet = new Size(100, 130);
        var narrow = new Rect(0, 0, 150, 200);

        Assert.Null(PetTankArea.RoamableArea(narrow, Screen, pet));
    }

    [Fact]
    public void ATankTooShortForThePetIsRefusedToo()
    {
        Assert.Null(PetTankArea.RoamableArea(new Rect(0, 0, 900, 40), Screen, new Size(100, 130)));
    }

    [Fact]
    public void ATankWithRoomToWalkIsAccepted()
    {
        var tank = new Rect(100, 200, 800, 200);

        Assert.Equal(tank, PetTankArea.RoamableArea(tank, Screen, new Size(100, 130)));
    }

    [Fact]
    public void OnlyThePartActuallyOnScreenCounts()
    {
        // 창을 화면 밖으로 반쯤 끌어냈으면, 든 만큼만 돌아다닐 수 있다.
        var half = PetTankArea.RoamableArea(new Rect(1600, 200, 800, 200), Screen, new Size(100, 130));

        Assert.NotNull(half);
        Assert.Equal(320, half!.Value.Width);
    }

    [Fact]
    public void ATankDraggedFarEnoughOffScreenStopsBeingOne()
    {
        Assert.Null(PetTankArea.RoamableArea(new Rect(1900, 200, 800, 200), Screen, new Size(100, 130)));
    }

    [Fact]
    public void AnEmptyReportIsNoTank()
    {
        Assert.Null(PetTankArea.RoamableArea(new Rect(0, 0, 0, 0), Screen, new Size(10, 10)));
    }

    [Fact]
    public void WidthDecidesTheSizeBeforeHeightDoes()
    {
        // 섬의 높이만 보면 좁은 창이 거절된다 — 높이로는 통과할 펫이
        // 너비에서 막힌다. 그래서 크기가 양보한다.
        var fitted = PetTankArea.FittedPetHeight(desired: 90, tank: new Size(120, 200), aspect: 1);

        Assert.Equal(60, fitted);
    }

    [Fact]
    public void AWideTankGivesThePetTheHeightItAskedFor()
    {
        Assert.Equal(90, PetTankArea.FittedPetHeight(90, new Size(1000, 200), aspect: 1));
    }

    [Fact]
    public void AShortTankCapsTheHeightAtItsOwn()
    {
        Assert.Equal(50, PetTankArea.FittedPetHeight(90, new Size(1000, 50), aspect: 1));
    }
}

public class PetHomeDeciderTests
{
    [Fact]
    public void AWindowPassingThroughFocusDoesNotSendThePetAnywhere()
    {
        // 스쳐 지나가는 Alt+Tab에 펫이 갔다 오면 안 된다.
        double now = 0;
        var decider = new PetHomeDecider(() => now);

        Assert.Null(decider.Decide(tankIsAvailable: true));

        now = PetHomeDecider.HoldSeconds / 2;
        Assert.Null(decider.Decide(tankIsAvailable: true));

        now += 0.01;
        Assert.Null(decider.Decide(tankIsAvailable: false));
    }

    [Fact]
    public void AStateThatHoldsLongEnoughIsActedOn()
    {
        double now = 0;
        var decider = new PetHomeDecider(() => now);

        decider.Decide(tankIsAvailable: true);
        now = PetHomeDecider.HoldSeconds + 0.01;

        Assert.Equal(PetHomeDecider.Move.Home, decider.Decide(tankIsAvailable: true));
    }

    [Fact]
    public void ADecisionIsGivenOnceRatherThanEveryFrame()
    {
        double now = 0;
        var decider = new PetHomeDecider(() => now);

        decider.Decide(true);
        now = PetHomeDecider.HoldSeconds + 0.01;
        Assert.NotNull(decider.Decide(true));
        Assert.Null(decider.Decide(true));
    }

    [Fact]
    public void AHiddenPetIsNowhereRatherThanSomewhere()
    {
        // 숨기기가 다른 무엇보다 세다. 갈 수 있는 수조가 그대로 있어도
        // 숨겨진 펫은 거기 있지 않다.
        double now = 0;
        var decider = new PetHomeDecider(() => now);

        decider.Decide(tankIsAvailable: true);
        now = PetHomeDecider.HoldSeconds + 0.01;
        Assert.Equal(PetHomeDecider.Move.Home, decider.Decide(tankIsAvailable: true));

        decider.IsPetHidden = true;
        decider.Decide(tankIsAvailable: true);
        now += PetHomeDecider.HoldSeconds + 0.01;

        Assert.Equal(PetHomeDecider.Move.Desktop, decider.Decide(tankIsAvailable: true));
    }

    [Fact]
    public void NothingHappensUntilSomethingChanges()
    {
        // 시작할 때 펫은 이미 바탕화면에 있다. 아무 일도 없었으면
        // 옮길 이유도 없다.
        double now = 0;
        var decider = new PetHomeDecider(() => now);

        now = PetHomeDecider.HoldSeconds * 10;

        Assert.Null(decider.Decide(tankIsAvailable: false));
    }

    [Fact]
    public void TheTankGoingAwaySendsThePetBackOut()
    {
        double now = 0;
        var decider = new PetHomeDecider(() => now);

        decider.Decide(true);
        now = PetHomeDecider.HoldSeconds + 0.01;
        Assert.Equal(PetHomeDecider.Move.Home, decider.Decide(true));

        decider.Decide(false);
        now += PetHomeDecider.HoldSeconds + 0.01;
        Assert.Equal(PetHomeDecider.Move.Desktop, decider.Decide(false));
    }
}
