using System.Windows;
using Puck.Input;

namespace PuckTests.Input;

public class SpeechBubblePlacementTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1032);
    private static readonly Size Bubble = new(300, 80);
    private const double PetHeight = 133;

    [Fact]
    public void TheBubbleSitsAboveThePetsHeadAndCentredOnIt()
    {
        var origin = SpeechBubblePlacement.Origin(new Point(960, 1000), PetHeight, Bubble, Screen);

        Assert.Equal(810, origin.X);                       // 960 - 300/2
        Assert.Equal(1000 - 133 - 8 - 80, origin.Y);       // 머리 위로 Gap만큼
    }

    [Fact]
    public void APetInTheLeftCornerDoesNotPutHalfItsSpeechOffScreen()
    {
        var origin = SpeechBubblePlacement.Origin(new Point(20, 1000), PetHeight, Bubble, Screen);
        Assert.Equal(SpeechBubblePlacement.Margin, origin.X);
    }

    [Fact]
    public void APetInTheRightCornerIsClampedToo()
    {
        var origin = SpeechBubblePlacement.Origin(new Point(1900, 1000), PetHeight, Bubble, Screen);
        Assert.Equal(1920 - 300 - SpeechBubblePlacement.Margin, origin.X);
    }

    [Fact]
    public void APetNearTheTopFlipsItsBubbleBelowRatherThanOverItsHead()
    {
        // 머리 위로 넣을 자리가 없다. 아래로 뒤집는다.
        var origin = SpeechBubblePlacement.Origin(new Point(960, 150), PetHeight, Bubble, Screen);
        Assert.Equal(150 + SpeechBubblePlacement.Gap, origin.Y);
    }

    [Fact]
    public void TheBubbleStaysOnScreenEvenWhenNeitherSideFits()
    {
        var narrow = new Rect(0, 0, 1920, 200);
        var origin = SpeechBubblePlacement.Origin(new Point(960, 190), PetHeight, Bubble, narrow);

        Assert.True(origin.Y >= narrow.Top);
        Assert.True(origin.Y + Bubble.Height <= narrow.Bottom);
    }

    [Fact]
    public void ABubbleWiderThanTheScreenStartsAtTheLeftMargin()
    {
        var wide = new Size(3000, 80);
        var origin = SpeechBubblePlacement.Origin(new Point(960, 1000), PetHeight, wide, Screen);
        Assert.Equal(SpeechBubblePlacement.Margin, origin.X);
    }

    [Fact]
    public void ThePlacementFollowsThePetRatherThanBeingFixedOnce()
    {
        // 프레임마다 다시 계산되는 값이라, 펫이 걸으면 결과도 따라와야 한다.
        var first = SpeechBubblePlacement.Origin(new Point(500, 1000), PetHeight, Bubble, Screen);
        var later = SpeechBubblePlacement.Origin(new Point(700, 1000), PetHeight, Bubble, Screen);
        Assert.Equal(200, later.X - first.X);
    }
}
