using System.Diagnostics;
using System.Windows.Media;

namespace Puck.Movement;

public interface IFrameClock
{
    /// 인자는 지난 프레임 이후 경과한 초.
    event Action<double>? Tick;
    void Start();
    void Stop();
}

/// WPF의 합성 프레임에 얹은 시계. 화면 주사율을 따라가므로 60Hz든
/// 144Hz든 그리기와 어긋나지 않는다.
public sealed class CompositionFrameClock : IFrameClock
{
    /// 한 프레임에 허용하는 최대 dt. 노트북 뚜껑을 닫았다 열면 마지막
    /// 프레임 이후 몇 시간이 지나 있고, 상한이 없으면 펫이 그 한 프레임에
    /// 몇 킬로픽셀을 이동한다.
    public const double MaximumDelta = 0.1;

    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _last;
    private bool _running;

    public event Action<double>? Tick;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _stopwatch.Restart();
        _last = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
        _stopwatch.Stop();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _stopwatch.Elapsed;
        var dt = (now - _last).TotalSeconds;
        _last = now;

        if (dt <= 0) return;
        Tick?.Invoke(Math.Min(dt, MaximumDelta));
    }
}
