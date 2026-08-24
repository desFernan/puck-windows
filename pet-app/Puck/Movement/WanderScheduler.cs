namespace Puck.Movement;

/// 가만히 있는 펫이 다음에 뭔가 할 때까지의 시간. 무작위인 이유는
/// 정확히 N초마다 움직이는 것이 살아 있는 것으로 읽히지 않기 때문이다.
///
/// Random을 주입받는 이유는 테스트가 결정적으로 돌아야 하기 때문이다.
public sealed class WanderScheduler(Random random)
{
    private double _elapsed;

    public WanderScheduler() : this(Random.Shared) { }

    public TimeSpan MinimumInterval { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan MaximumInterval { get; init; } = TimeSpan.FromSeconds(9);

    /// 지금 재무장된 간격. 테스트와 로깅용.
    public TimeSpan NextInterval { get; private set; }

    public void Reset()
    {
        _elapsed = 0;
        var span = MaximumInterval.TotalSeconds - MinimumInterval.TotalSeconds;
        NextInterval = TimeSpan.FromSeconds(MinimumInterval.TotalSeconds + random.NextDouble() * span);
    }

    /// 간격이 찼으면 true를 돌려주고 스스로 다시 무장한다.
    public bool Tick(double dt)
    {
        if (NextInterval == TimeSpan.Zero) Reset();

        _elapsed += dt;
        if (_elapsed < NextInterval.TotalSeconds) return false;

        Reset();
        return true;
    }
}
