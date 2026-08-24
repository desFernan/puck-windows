using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

public class PetBoundsTests
{
    // 폭 100, 높이 200짜리 펫: 발밑 기준으로 좌우 50씩, 위로 200.
    private static readonly Rect Pet = new(-50, -200, 100, 200);
    private static readonly Rect Area = new(0, 0, 1000, 800);

    [Fact]
    public void ConstantsMatchTheMacOriginal()
    {
        Assert.Equal(0.55, PetBounds.Restitution);
        Assert.Equal(60, PetBounds.MinimumBounceSpeed);
        Assert.Equal(0.35, PetBounds.LandingRestitution);
        Assert.Equal(100, PetBounds.MinimumLandingBounceSpeed);
    }

    [Fact]
    public void ContainStopsWhenTheArtworkMeetsTheEdgeNotTheCentre()
    {
        // 발밑 x=10이면 그림의 왼쪽은 -40 — 화면 밖이다.
        var contained = PetBounds.Contain(new Point(10, 400), Pet, Area);
        Assert.Equal(50, contained.X);
        Assert.Equal(400, contained.Y);   // Y는 건드리지 않는다
    }

    [Fact]
    public void ContainLeavesAPositionThatAlreadyFits()
    {
        var position = new Point(500, 400);
        Assert.Equal(position, PetBounds.Contain(position, Pet, Area));
    }

    [Fact]
    public void APetWiderThanTheScreenIsPinnedToTheLeftEdge()
    {
        var huge = new Rect(-2000, -100, 4000, 100);
        Assert.True(PetBounds.IsOversizedHorizontally(huge, Area));
        Assert.Equal(2000, PetBounds.Contain(new Point(500, 400), huge, Area).X);
    }

    [Fact]
    public void HittingTheRightWallReversesAndDampsTheVelocity()
    {
        // 오른쪽 한계는 1000 - 50 = 950. 960까지 갔으니 10 지나쳤다.
        var bounce = PetBounds.BounceHorizontally(new Point(960, 400), 1000, Pet, Area);
        Assert.Equal(940, bounce.Position.X);            // 2*950 - 960
        Assert.Equal(-550, bounce.Velocity);             // -1000 * 0.55
    }

    [Fact]
    public void MovingAwayFromAWallIsNotABounce()
    {
        var position = new Point(960, 400);
        var bounce = PetBounds.BounceHorizontally(position, -1000, Pet, Area);
        Assert.Equal(position, bounce.Position);
        Assert.Equal(-1000, bounce.Velocity);
    }

    [Fact]
    public void ABounceWithTooLittleEnergyComesToRestAgainstTheEdge()
    {
        // 100 * 0.55 = 55 < 60 → 가장자리에 붙어 멈춘다.
        var bounce = PetBounds.BounceHorizontally(new Point(960, 400), 100, Pet, Area);
        Assert.Equal(950, bounce.Position.X);
        Assert.Equal(0, bounce.Velocity);
    }

    [Fact]
    public void OnlyUpwardMotionBouncesOffTheCeiling()
    {
        // 머리 한계는 0 - (-200) = 200. 190까지 올라갔으니 10 지나쳤다.
        var up = PetBounds.BounceOffCeiling(new Point(500, 190), -1000, Pet, Area);
        Assert.Equal(210, up.Position.Y);
        Assert.Equal(550, up.Velocity);

        // 내려오는 건 착지이고, 어느 면에 닿는지는 여기 소관이 아니다.
        var down = PetBounds.BounceOffCeiling(new Point(500, 190), 1000, Pet, Area);
        Assert.Equal(190, down.Position.Y);
        Assert.Equal(1000, down.Velocity);
    }

    [Fact]
    public void LandingLosesMoreEnergyThanAWallHit()
    {
        var bounce = PetBounds.BounceOffFloor(new Point(500, 810), 1000, floorY: 800);
        Assert.Equal(790, bounce.Position.Y);            // 2*800 - 810
        Assert.Equal(-350, bounce.Velocity);             // -1000 * 0.35
    }

    [Fact]
    public void ASoftLandingJustRests()
    {
        // 200 * 0.35 = 70 < 100 → 바닥에 눕는다.
        var bounce = PetBounds.BounceOffFloor(new Point(500, 810), 200, floorY: 800);
        Assert.Equal(800, bounce.Position.Y);
        Assert.Equal(0, bounce.Velocity);
    }

    [Fact]
    public void ADeepOvershootComesBackOutAsFarAsItWentIn()
    {
        // 반사는 가장자리에 대해 대칭 — 한계에 딱 붙이면 빠른 튕김이
        // 눈에 보이게 거리를 잃는다.
        var bounce = PetBounds.BounceHorizontally(new Point(1000, 400), 2000, Pet, Area);
        Assert.Equal(900, bounce.Position.X);            // 2*950 - 1000
    }
}
