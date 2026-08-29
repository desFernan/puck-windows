using Puck.App;
using Puck.ClientWindow;
using Puck.Localization;
using Puck.Movement;

namespace PuckTests.App;

/// 움직임 줄이기. 배회는 이 앱에서 아무도 요청하지 않은 유일한 움직임이라,
/// 그 설정이 실제로 거는 곳도 거기 하나뿐이다.
public class ReducedMotionTests
{
    /// 배회는 타이머로 시작해서 사람이 실제로 읽고 있는 것 옆에서 일어난다.
    /// 설정이 켜져 있으면 무엇을 뽑았든 "가만히"로 나온다.
    [Theory]
    [InlineData(WanderOutcome.WalkToRandomPoint)]
    [InlineData(WanderOutcome.ClimbNearestWindow)]
    [InlineData(WanderOutcome.CrawlCeiling)]
    [InlineData(WanderOutcome.Stay)]
    public void 켜져_있으면_모든_뽑기가_가만히가_된다(WanderOutcome drawn)
    {
        Assert.Equal(WanderOutcome.Stay, ReducedMotion.Apply(drawn, reduceMotion: true));
    }

    /// 꺼져 있으면 아무것도 달라지지 않는다 — 이 설정만이 이걸 정하므로,
    /// 다른 이유로 배회를 멈춘 펫은 기능과 구별할 수 없는 버그가 된다.
    [Theory]
    [InlineData(WanderOutcome.WalkToRandomPoint)]
    [InlineData(WanderOutcome.ClimbNearestWindow)]
    [InlineData(WanderOutcome.CrawlCeiling)]
    [InlineData(WanderOutcome.Stay)]
    public void 꺼져_있으면_뽑기가_그대로다(WanderOutcome drawn)
    {
        Assert.Equal(drawn, ReducedMotion.Apply(drawn, reduceMotion: false));
    }

    /// 물어볼 수 없을 때 켜진 것으로 보면, 아무도 요청하지 않았는데 펫이
    /// 가만히 있게 된다. 그래서 못 읽으면 꺼진 쪽이다.
    [Fact]
    public void 읽을_수_있는_값을_돌려준다()
    {
        // 이 기계의 실제 설정이 무엇이든 예외 없이 답해야 한다.
        _ = ReducedMotion.IsOn;
    }
}

/// 화면에서 색으로만 말하던 것에 말을 붙인 부분.
public class TranscriptSpokenTextTests
{
    /// 한 줄이 누구 것인지는 화면에서 색이 말한다. 색은 읽히지 않는다.
    [Theory]
    [InlineData(TranscriptKind.Pet, "펫")]
    [InlineData(TranscriptKind.User, "나")]
    [InlineData(TranscriptKind.Tool, "진행")]
    [InlineData(TranscriptKind.Error, "오류")]
    [InlineData(TranscriptKind.Notice, "안내")]
    public void 종류를_글로_앞에_붙인다(TranscriptKind kind, string spoken)
    {
        var entry = new TranscriptEntry(kind, "안녕");
        Assert.Equal($"{spoken}: 안녕", entry.SpokenText);
    }

    /// 종류가 하나 늘어날 때 표에 넣는 것을 잊으면 키가 그대로 읽힌다.
    [Fact]
    public void 모든_종류에_읽을_말이_있다()
    {
        foreach (TranscriptKind kind in Enum.GetValues<TranscriptKind>())
        {
            var word = Strings.KindOf(kind.ToString().ToLowerInvariant());
            Assert.DoesNotContain(".", word);
        }
    }

    [Fact]
    public void 본문은_그대로_남는다()
    {
        Assert.EndsWith("무엇이든 시켜 보세요", new TranscriptEntry(TranscriptKind.Notice, "무엇이든 시켜 보세요").SpokenText);
    }
}
