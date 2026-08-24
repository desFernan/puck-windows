using System.Windows;
using Puck.Overlay;

namespace PuckTests.Overlay;

public class PetGestureRecognizerTests
{
    [Fact]
    public void PressAndReleaseWithoutMovingIsAClick()
    {
        var recognizer = new PetGestureRecognizer();
        var clicks = 0;
        var drags = 0;
        recognizer.Clicked += () => clicks++;
        recognizer.Dragged += _ => drags++;

        recognizer.OnMouseDown(new Point(100, 100), 0);
        recognizer.OnMouseUp(new Point(101, 100), 0.1);

        Assert.Equal(1, clicks);
        Assert.Equal(0, drags);
    }

    [Fact]
    public void MovingPastTheThresholdBecomesADrag()
    {
        var recognizer = new PetGestureRecognizer();
        var clicks = 0;
        var positions = new List<Point>();
        recognizer.Clicked += () => clicks++;
        recognizer.Dragged += positions.Add;

        recognizer.OnMouseDown(new Point(100, 100), 0);
        recognizer.OnMouseMove(new Point(120, 100), 0.05);
        recognizer.OnMouseUp(new Point(120, 100), 0.1);

        Assert.Equal(0, clicks);           // 드래그였다면 클릭이 아니다
        Assert.Equal([new Point(120, 100)], positions);
    }

    [Fact]
    public void MovementBelowTheThresholdIsStillAClick()
    {
        // 손 떨림으로 클릭이 사라지면 안 된다.
        var recognizer = new PetGestureRecognizer();
        var clicks = 0;
        recognizer.Clicked += () => clicks++;

        recognizer.OnMouseDown(new Point(100, 100), 0);
        recognizer.OnMouseMove(new Point(102, 101), 0.02);
        recognizer.OnMouseUp(new Point(102, 101), 0.05);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ReleasingADragReportsTheThrowVelocity()
    {
        var recognizer = new PetGestureRecognizer();
        Vector? released = null;
        recognizer.Released += v => released = v;

        recognizer.OnMouseDown(new Point(0, 0), 0.00);
        recognizer.OnMouseMove(new Point(50, 0), 0.02);
        recognizer.OnMouseMove(new Point(100, 0), 0.04);
        recognizer.OnMouseUp(new Point(100, 0), 0.04);

        Assert.NotNull(released);
        Assert.True(released!.Value.X > 1000);
    }

    [Fact]
    public void AClickReleaseReportsNoThrow()
    {
        var recognizer = new PetGestureRecognizer();
        var releases = 0;
        recognizer.Released += _ => releases++;

        recognizer.OnMouseDown(new Point(0, 0), 0);
        recognizer.OnMouseUp(new Point(0, 0), 0.1);

        Assert.Equal(0, releases);
    }

    [Fact]
    public void MoveWithoutAPressIsIgnored()
    {
        var recognizer = new PetGestureRecognizer();
        var drags = 0;
        recognizer.Dragged += _ => drags++;

        recognizer.OnMouseMove(new Point(500, 500), 0);

        Assert.Equal(0, drags);
    }

    [Fact]
    public void ASecondPressStartsAFreshGesture()
    {
        var recognizer = new PetGestureRecognizer();
        var clicks = 0;
        recognizer.Clicked += () => clicks++;

        recognizer.OnMouseDown(new Point(0, 0), 0);
        recognizer.OnMouseMove(new Point(200, 0), 0.05);
        recognizer.OnMouseUp(new Point(200, 0), 0.1);      // 드래그

        recognizer.OnMouseDown(new Point(200, 0), 1.0);
        recognizer.OnMouseUp(new Point(200, 0), 1.1);      // 클릭

        Assert.Equal(1, clicks);
    }
}
