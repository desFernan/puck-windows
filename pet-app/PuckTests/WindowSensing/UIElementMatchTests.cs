using System.Windows;
using Puck.WindowSensing;

namespace PuckTests.WindowSensing;

public class UIElementMatchTests
{
    private static UIElementInfo El(string? name, bool enabled = true, bool offscreen = false)
        => new(name, "Button", new Rect(0, 0, 100, 30), enabled, offscreen, IsInvokable: true);

    [Fact]
    public void TheExactNameScoresHighest()
    {
        Assert.Equal(1.0, UIElementMatch.Score(El("저장"), "저장"));
    }

    [Fact]
    public void CaseAndSpacingDoNotMatter()
    {
        Assert.Equal(1.0, UIElementMatch.Score(El("Save As"), "saveas"));
        Assert.Equal(1.0, UIElementMatch.Score(El("  SAVE  "), "save"));
    }

    [Fact]
    public void MnemonicMarkersAreIgnored()
    {
        // 사람은 "저장"이라고 말하고 화면에는 "저장(&S)"라고 적혀 있다.
        Assert.Equal(1.0, UIElementMatch.Score(El("저장(&S)"), "저장"));
        Assert.Equal(1.0, UIElementMatch.Score(El("&Save"), "save"));
    }

    [Fact]
    public void ShortcutHintsInParenthesesAreIgnored()
    {
        Assert.Equal(1.0, UIElementMatch.Score(El("복사 (Ctrl+C)"), "복사"));
    }

    [Fact]
    public void APrefixBeatsAContainmentWhichBeatsNothing()
    {
        var prefix = UIElementMatch.Score(El("저장하기"), "저장");
        var contains = UIElementMatch.Score(El("파일 저장하기"), "저장");
        var unrelated = UIElementMatch.Score(El("열기"), "저장");

        Assert.True(prefix > contains);
        Assert.True(contains > unrelated);
        Assert.Equal(0, unrelated);
    }

    [Fact]
    public void AnUnnamedElementNeverMatches()
    {
        Assert.Equal(0, UIElementMatch.Score(El(null), "저장"));
        Assert.Equal(0, UIElementMatch.Score(El(""), "저장"));
    }

    [Fact]
    public void SomethingOffscreenOrDisabledScoresLowerButIsStillFound()
    {
        // "있는데 꺼져 있음"은 "없음"과 다른 답이다 — 후보에서 빼지 않는다.
        var disabled = UIElementMatch.Score(El("저장", enabled: false), "저장");
        Assert.True(disabled is > 0 and < 1.0);

        var offscreen = UIElementMatch.Score(El("저장", offscreen: true), "저장");
        Assert.True(offscreen is > 0 and < 1.0);
    }

    [Fact]
    public void TheBestMatchesComeBackInOrderAndAreCapped()
    {
        var elements = new[]
        {
            El("파일 저장하기"),   // contains
            El("저장"),            // exact
            El("저장하기"),        // prefix
            El("열기"),            // 무관
        };

        var best = UIElementMatch.Best(elements, "저장", limit: 2);

        Assert.Equal(2, best.Count);
        Assert.Equal("저장", best[0].Name);
        Assert.Equal("저장하기", best[1].Name);
    }

    [Fact]
    public void TiesKeepTheTreeOrderBecauseThatIsReadingOrder()
    {
        var elements = new[] { El("저장 A"), El("저장 B") };
        var best = UIElementMatch.Best(elements, "저장", limit: 2);
        Assert.Equal(["저장 A", "저장 B"], best.Select(e => e.Name));
    }

    [Fact]
    public void NothingCloseEnoughComesBackEmpty()
    {
        Assert.Empty(UIElementMatch.Best([El("열기"), El("닫기")], "저장", limit: 5));
    }
}
