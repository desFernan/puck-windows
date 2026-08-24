using System.Windows;
using Puck.Overlay;

namespace PuckTests.Overlay;

public class OverlayPositionerTests
{
    private static readonly Rect Pet = new(-50, -200, 100, 200);

    [Fact]
    public void FrameSurroundsTheArtworkWithPadding()
    {
        var frame = OverlayPositioner.FrameFor(new Point(500, 800), Pet, padding: 48);
        // 그림은 (450, 600)-(550, 800). 사방으로 48씩.
        Assert.Equal(402, frame.X);
        Assert.Equal(552, frame.Y);
        Assert.Equal(196, frame.Width);
        Assert.Equal(296, frame.Height);
    }

    [Fact]
    public void ZeroPaddingIsExactlyTheArtwork()
    {
        var frame = OverlayPositioner.FrameFor(new Point(0, 0), Pet, padding: 0);
        Assert.Equal(-50, frame.X);
        Assert.Equal(-200, frame.Y);
        Assert.Equal(100, frame.Width);
        Assert.Equal(200, frame.Height);
    }

    [Fact]
    public void FractionalPositionsRoundOutwardSoNothingIsClipped()
    {
        var frame = OverlayPositioner.FrameFor(new Point(500.7, 800.2), Pet, padding: 0);
        Assert.Equal(450, frame.X);              // floor(450.7)
        Assert.Equal(600, frame.Y);              // floor(600.2)
        Assert.True(frame.Width >= 100);
        Assert.True(frame.Height >= 200);
    }

    [Fact]
    public void ADegenerateOutlineStillYieldsAUsableFrame()
    {
        var frame = OverlayPositioner.FrameFor(new Point(0, 0), Rect.Empty, padding: 4);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }
}
