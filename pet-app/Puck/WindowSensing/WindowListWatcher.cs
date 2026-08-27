using System.Diagnostics;
using System.Windows.Threading;
using Puck.Diagnostics;

namespace Puck.WindowSensing;

/// 주기적으로 무언가를 시키는 것. 진짜 타이머와 테스트용 가짜를 바꿔 끼우려고
/// 인터페이스로 둔다.
public interface IWatcherTicker
{
    Action? Tick { get; set; }
    void Start(double hz);
    void Stop();
}

/// WPF 디스패처 타이머. 창 목록은 프레임 루프(UI 스레드)가 읽으므로 갱신도
/// 같은 스레드에서 일어나야 한다.
public sealed class DispatcherWatcherTicker : IWatcherTicker
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background);

    public DispatcherWatcherTicker() => _timer.Tick += (_, _) => Tick?.Invoke();

    public Action? Tick { get; set; }

    public void Start(double hz)
    {
        _timer.Stop();
        _timer.Interval = TimeSpan.FromSeconds(1.0 / hz);
        _timer.Start();
    }

    public void Stop() => _timer.Stop();
}

/// 화면의 창 목록을 계속 최신으로 들고 있는다.
///
/// 프레임 루프(60Hz)와 분리한 이유는 창 목록이 프레임마다 바뀌지 않기 때문이다 —
/// EnumWindows를 초당 60번 도는 것은 그냥 낭비다. 대신 사람이 창을 바꾼 직후
/// (포그라운드 변경)에는 잠깐 더 자주 본다. 그때가 목록이 가장 많이 흔들린다.
public sealed class WindowListWatcher : IDisposable
{
    public const double BurstSeconds = 3;

    private readonly Func<IReadOnlyList<WindowInfo>> _source;
    private readonly IWatcherTicker _ticker;
    private readonly Func<double> _now;
    private readonly WindowPollPolicy _policy = new();

    private double? _burstEnd;
    private double _currentHz;
    private double _lastTickAt;
    private bool _disposed;

    /// 펫이 창 목록을 읽지 않는 상태인가 — 어딘가에 서서 쉬는 중이거나,
    /// 치워져 있거나. 그럴 때만 주기를 늦춘다.
    ///
    /// 워처가 펫을 아는 대신 물어보는 이유는, 창 감지가 움직임에 기대면
    /// 둘을 따로 시험할 수 없기 때문이다.
    public Func<bool> PetIsResting { get; set; } = () => false;

    public WindowListWatcher(
        Func<IReadOnlyList<WindowInfo>> source,
        IWatcherTicker ticker,
        Func<double> now)
    {
        _source = source;
        _ticker = ticker;
        _now = now;
        _ticker.Tick = OnTick;
    }

    /// 실제 창 목록을 단조 증가 시계로 보는 기본 구성.
    public static WindowListWatcher CreateDefault()
    {
        var uptime = Stopwatch.StartNew();
        return new WindowListWatcher(
            () => WindowFilter.Keep(WindowListSource.Fetch(),
                                    Environment.ProcessId, WindowFilter.DefaultMinimumSize),
            new DispatcherWatcherTicker(),
            () => uptime.Elapsed.TotalSeconds);
    }

    private IReadOnlyList<WindowInfo> _windows = [];

    /// 앞에서 뒤 순서. 프레임 루프가 매 프레임 읽고, 에이전트의 도구는
    /// 스레드 풀에서 읽는다. 목록은 고쳐 쓰지 않고 **통째로 갈아 끼우므로**
    /// 참조 하나만 제때 보이면 된다 — 그래서 잠금 대신 Volatile이면 충분하다.
    public IReadOnlyList<WindowInfo> Windows => Volatile.Read(ref _windows);

    public void Start()
    {
        Refresh();
        _lastTickAt = _now();
        Apply(WindowPollPolicy.ActiveHz);
    }

    /// 포그라운드 창이 바뀌었다 — 지금 한 번 더 보고, 잠깐 더 자주 본다.
    public void NoteForegroundChanged()
    {
        if (_disposed) return;
        Refresh();
        _burstEnd = _now() + BurstSeconds;
        Apply(WindowPollPolicy.BurstHz);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // 타이머를 남겨 둔 워처는 아무도 참조하지 않아도 계속 돈다.
        _ticker.Stop();
        _ticker.Tick = null;
    }

    private void OnTick()
    {
        if (_disposed) return;

        Refresh();

        var now = _now();
        var dt = now - _lastTickAt;
        _lastTickAt = now;

        var bursting = _burstEnd is { } end && now < end;
        if (!bursting) _burstEnd = null;

        Apply(_policy.Hertz(PetIsResting(), bursting, dt));
    }

    /// 주기가 실제로 달라졌을 때만 타이머를 다시 건다 — 매 틱 다시 걸면
    /// 아끼려던 것을 그 자리에서 도로 쓴다.
    private void Apply(double hz)
    {
        if (Math.Abs(hz - _currentHz) < 0.001) return;

        _currentHz = hz;
        _ticker.Start(hz);
    }

    private void Refresh()
    {
        try
        {
            Volatile.Write(ref _windows, _source());
        }
        catch (Exception ex)
        {
            // 열거하는 사이에 창이 사라지면 예외가 날 수 있다. 프레임 루프가
            // 매 프레임 이 목록을 읽으므로, 한 번 실패했다고 비워 버리면
            // 창 위에 서 있던 펫이 그 프레임에 바닥으로 떨어진다.
            AppLogger.Warning("windows", "창 목록을 갱신하지 못해 이전 목록을 유지합니다",
                new Dictionary<string, object?> { ["error"] = ex.Message });
        }
    }
}
