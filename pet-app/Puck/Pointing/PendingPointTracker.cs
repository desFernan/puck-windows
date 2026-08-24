using System.Windows;

namespace Puck.Pointing;

/// 펫이 무언가를 가리킨 뒤, 사람이 실제로 거기를 누르는지 지켜본다.
///
/// 가리키는 것과 눌리는 것 사이에는 시간이 있다. 그 사이의 아무 클릭이나
/// "가리킨 그것을 눌렀다"로 치면, 사람이 딴 데를 누른 것까지 성공으로
/// 보고하게 된다 — 시간과 거리 두 가지로 좁힌다.
public sealed class PendingPointTracker
{
    /// 가리킨 뒤 이 시간 안의 클릭만 그 대상으로 친다.
    public const double WindowSeconds = 20;

    /// 가리킨 지점에서 이만큼 안이면 그것을 누른 것으로 친다.
    public const double Radius = 48;

    private Point? _target;
    private double _pointedAt;

    /// 지금 무언가를 가리키고 있는가.
    public bool IsPending => _target is not null;

    public void Point(Point target, double now)
    {
        _target = target;
        _pointedAt = now;
    }

    public void Clear() => _target = null;

    /// 그 클릭이 가리킨 것을 누른 것인가. 맞으면 대기를 끝낸다.
    public bool Accepts(Point click, double now)
    {
        if (_target is not { } target) return false;
        if (now - _pointedAt > WindowSeconds) { _target = null; return false; }

        var dx = click.X - target.X;
        var dy = click.Y - target.Y;
        if (dx * dx + dy * dy > Radius * Radius) return false;

        _target = null;
        return true;
    }

    /// 시간이 다 됐으면 대기를 접는다. 프레임 루프가 부른다.
    public void Expire(double now)
    {
        if (_target is not null && now - _pointedAt > WindowSeconds) _target = null;
    }
}
