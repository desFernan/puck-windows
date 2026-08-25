using Puck.Avatar;

namespace PuckTests.Avatar;

/// puck-linux `emotion.rs`의 테스트를 그대로 옮겼다. 저쪽은 틱, 여기는 초다.
public class EmotionOverrideTests
{
    [Fact]
    public void ItShowsTheClipForTheRequestedTimeThenExpires()
    {
        var emotion = new EmotionOverride();
        emotion.Show("type", seconds: 2);

        Assert.Equal("type", emotion.Tick(1));
        Assert.Equal("type", emotion.Tick(0.5));
        Assert.Null(emotion.Tick(0.5));
        Assert.Null(emotion.Tick(0.5));
    }

    [Fact]
    public void ShowingAgainRefillsTheClock()
    {
        // 도구를 연달아 쓰는 동안 표정이 중간에 깜빡이면 안 된다.
        var emotion = new EmotionOverride();
        emotion.Show("type", seconds: 1);
        Assert.Equal("type", emotion.Tick(0.9));

        emotion.Show("type", seconds: 1);
        Assert.Equal("type", emotion.Tick(0.9));
        Assert.Null(emotion.Tick(0.2));
    }

    [Fact]
    public void ADifferentClipReplacesTheOneShowingNow()
    {
        var emotion = new EmotionOverride();
        emotion.Show("type", seconds: 5);
        emotion.Show("listen", seconds: 5);

        Assert.Equal("listen", emotion.Tick(1));
    }

    [Fact]
    public void ItIsInactiveUntilAskedFor()
    {
        var emotion = new EmotionOverride();

        Assert.False(emotion.IsActive);
        Assert.Null(emotion.Tick(1));
    }

    [Fact]
    public void ClearingStopsItAtOnce()
    {
        // 펫이 답을 내놓았으면 더 생각하는 척할 이유가 없다.
        var emotion = new EmotionOverride();
        emotion.Show("type", seconds: 10);
        emotion.Clear();

        Assert.False(emotion.IsActive);
        Assert.Null(emotion.Tick(0.016));
    }

    [Fact]
    public void AFrameLongerThanTheWholeClipStillEndsCleanly()
    {
        // 프레임이 밀려 dt가 커져도 표정이 음수 시간에 갇히지 않는다.
        var emotion = new EmotionOverride();
        emotion.Show("type", seconds: 0.5);

        Assert.Null(emotion.Tick(10));
        Assert.False(emotion.IsActive);
    }
}
