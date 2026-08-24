using System.Windows;
using Puck.WindowSensing;

namespace PuckTests.WindowSensing;

public class WindowListWatcherTests
{
    private static WindowInfo Window(int handle)
        => new(new IntPtr(handle), 1234, "App", "창", new Rect(0, 0, 800, 600), false, false);

    /// 진짜 타이머 대신 테스트가 직접 돌리는 시계.
    private sealed class FakeTicker : IWatcherTicker
    {
        public double Hz { get; private set; }
        public Action? Tick { get; set; }
        public bool Running { get; private set; }

        public void Start(double hz) { Hz = hz; Running = true; }
        public void Stop() { Running = false; }
        public void Fire() => Tick?.Invoke();
    }

    [Fact]
    public void StartingReadsTheListImmediately()
    {
        var reads = 0;
        var ticker = new FakeTicker();
        var watcher = new WindowListWatcher(() => { reads++; return [Window(1)]; }, ticker, () => 0);

        watcher.Start();

        Assert.Equal(1, reads);
        Assert.Single(watcher.Windows);
        Assert.Equal(WindowListWatcher.IdlePollHz, ticker.Hz);
    }

    [Fact]
    public void EachTickReadsAgain()
    {
        var reads = 0;
        var ticker = new FakeTicker();
        var watcher = new WindowListWatcher(() => { reads++; return [Window(1)]; }, ticker, () => 0);

        watcher.Start();
        ticker.Fire();
        ticker.Fire();

        Assert.Equal(3, reads);
    }

    [Fact]
    public void AForegroundChangeBurstsToTheFasterRate()
    {
        // 사람이 창을 바꾼 직후가 목록이 가장 많이 흔들리는 때다.
        var ticker = new FakeTicker();
        var watcher = new WindowListWatcher(() => [Window(1)], ticker, () => 0);
        watcher.Start();

        watcher.NoteForegroundChanged();

        Assert.Equal(WindowListWatcher.BurstPollHz, ticker.Hz);
    }

    [Fact]
    public void TheBurstEndsAndTheRateGoesBackDown()
    {
        var ticker = new FakeTicker();
        double now = 0;
        var watcher = new WindowListWatcher(() => [Window(1)], ticker, () => now);
        watcher.Start();

        watcher.NoteForegroundChanged();
        Assert.Equal(WindowListWatcher.BurstPollHz, ticker.Hz);

        now = WindowListWatcher.BurstSeconds + 0.1;
        ticker.Fire();

        Assert.Equal(WindowListWatcher.IdlePollHz, ticker.Hz);
    }

    [Fact]
    public void ADisposedWatcherStopsReading()
    {
        // 타이머를 run loop에 남겨 둔 워처는 아무도 참조하지 않아도 계속 돈다.
        var reads = 0;
        var ticker = new FakeTicker();
        var watcher = new WindowListWatcher(() => { reads++; return [Window(1)]; }, ticker, () => 0);

        watcher.Start();
        watcher.Dispose();
        var after = reads;
        ticker.Fire();

        Assert.False(ticker.Running);
        Assert.Equal(after, reads);
    }

    [Fact]
    public void TheListSurvivesASourceThatThrows()
    {
        // 열거 도중 창이 사라지면 예외가 날 수 있다. 프레임 루프가 매 프레임
        // 이 목록을 읽으므로, 한 번 실패했다고 목록이 비면 펫이 바닥으로 떨어진다.
        var fail = false;
        var ticker = new FakeTicker();
        var watcher = new WindowListWatcher(
            () => fail ? throw new InvalidOperationException("창이 사라짐") : [Window(1)],
            ticker, () => 0);

        watcher.Start();
        fail = true;
        ticker.Fire();

        Assert.Single(watcher.Windows);
    }
}
