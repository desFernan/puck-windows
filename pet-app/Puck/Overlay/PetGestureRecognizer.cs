using System.Windows;
using Puck.Movement;

namespace Puck.Overlay;

/// 누르고, 끌고, 놓는 것을 클릭 / 드래그 / 던지기로 읽는다.
/// 좌표는 가상 화면 물리 픽셀, timestamp는 초.
public sealed class PetGestureRecognizer
{
    /// 이만큼 움직이기 전까지는 아직 클릭이다. 손 떨림 때문에 클릭이
    /// 사라지면 안 된다.
    public const double DragThreshold = 4;

    private readonly CursorVelocityTracker _velocity = new();

    private Point _origin;
    private bool _pressed;
    private bool _dragging;

    public event Action? Clicked;
    public event Action<Point>? Dragged;
    public event Action<Vector>? Released;

    public void OnMouseDown(Point position, double timestamp)
    {
        _pressed = true;
        _dragging = false;
        _origin = position;
        _velocity.Reset();
        _velocity.Record(position, timestamp);
    }

    public void OnMouseMove(Point position, double timestamp)
    {
        if (!_pressed) return;

        _velocity.Record(position, timestamp);

        if (!_dragging)
        {
            var moved = (position - _origin).Length;
            if (moved < DragThreshold) return;
            _dragging = true;
        }

        Dragged?.Invoke(position);
    }

    public void OnMouseUp(Point position, double timestamp)
    {
        if (!_pressed) return;
        _pressed = false;

        if (!_dragging)
        {
            Clicked?.Invoke();
            return;
        }

        _velocity.Record(position, timestamp);
        Released?.Invoke(_velocity.Velocity);
        _dragging = false;
    }
}
