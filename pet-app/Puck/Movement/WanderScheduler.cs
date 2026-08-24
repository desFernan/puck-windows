namespace Puck.Movement;

/// 가만히 있던 펫이 다음에 할 일.
public enum WanderOutcome
{
    /// 아무 데나 걸어간다.
    WalkToRandomPoint,
    /// 가장 가까운 창으로 가서 타고 오른다.
    ClimbNearestWindow,
    /// 이번엔 그냥 있는다.
    Stay,
}

/// 가만히 있는 펫이 다음에 뭔가 할 때까지의 시간과, 그때 할 일.
/// 무작위인 이유는 정확히 N초마다 같은 일을 하는 것이 살아 있는 것으로
/// 읽히지 않기 때문이다.
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

    /// 간격이 찼으면 이번에 할 일을 뽑아 돌려주고 스스로 다시 무장한다.
    /// 아직이면 null.
    public WanderOutcome? Tick(double dt)
    {
        if (NextInterval == TimeSpan.Zero) Reset();

        _elapsed += dt;
        if (_elapsed < NextInterval.TotalSeconds) return null;

        Reset();
        return Draw();
    }

    /// mac의 가중치는 35% 걷기 / 25% 창 오르기 / 15% 천장 / 10% 장난감 / 15% 가만히다.
    /// 천장과 장난감은 아직 없으므로 그 몫을 걷기에 합친다 — 원본 주석도
    /// "없는 선택지는 어차피 걷기로 떨어진다"고 적어 두었다.
    private WanderOutcome Draw()
    {
        var roll = random.NextDouble();
        if (roll < 0.60) return WanderOutcome.WalkToRandomPoint;
        if (roll < 0.85) return WanderOutcome.ClimbNearestWindow;
        return WanderOutcome.Stay;
    }
}
