using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

/// 노치가 없는 기계에서 노치를 시험한다. ScreenNotch가 순수한 이유가 이것이다.
public class ScreenNotchTests
{
    private static readonly Rect Area = new(0, 0, 1000, 800);

    /// 화면 위 한가운데에 폭 200, 깊이 30.
    private static ScreenNotch Notch() => new(new Rect(400, 0, 200, 30));

    [Fact]
    public void 노치_밖에서는_천장이_영역의_윗변이다()
    {
        Assert.Equal(0, Notch().Ceiling(100, areaTop: 0));
        Assert.Equal(0, Notch().Ceiling(900, areaTop: 0));
    }

    [Fact]
    public void 노치_아래에서는_천장이_노치의_아랫변이다()
    {
        Assert.Equal(30, Notch().Ceiling(500, areaTop: 0));
    }

    /// 엄밀히 안쪽에서만 천장이 바뀌면 펫이 모서리를 스치고 지나간다.
    [Fact]
    public void 노치의_옆면도_그_아래로_친다()
    {
        Assert.Equal(30, Notch().Ceiling(400, areaTop: 0));
        Assert.Equal(30, Notch().Ceiling(600, areaTop: 0));
    }

    /// 펫이 있지도 않은 디스플레이의 노치나 잘못 옮겨 앉은 노치가 펫을
    /// 화면 밖으로 내보내는 천장을 돌려주면 안 된다.
    [Fact]
    public void 천장은_영역의_윗변보다_위로_가지_않는다()
    {
        var above = new ScreenNotch(new Rect(400, -200, 200, 30));
        Assert.Equal(100, above.Ceiling(500, areaTop: 100));
    }

    /// 발밑 한 점이 아니라 그림 전체로 판정한다 — 하우징 뒤에 펫의 절반이
    /// 들어가 있는 것은 전부 들어가 있는 것만큼 틀렸다.
    [Fact]
    public void 그림의_한쪽_끝만_노치에_걸려도_비켜간_것이_아니다()
    {
        var notch = Notch();
        var pet = new Rect(-50, -100, 100, 100);

        // 발이 x=350이면 그림은 300..400 — 오른쪽 끝이 노치의 왼쪽 모서리다.
        var head = new Point(350, 100);
        Assert.False(notch.Clears(head, pet, areaTop: 0));

        // 왼쪽으로 더 가면 그림 전체가 노치 밖이다.
        Assert.True(notch.Clears(new Point(340, 100), pet, areaTop: 0));
    }

    [Fact]
    public void 머리가_노치의_아랫변보다_아래면_비켜간_것이다()
    {
        Assert.True(Notch().Clears(new Point(500, 131), new Rect(-50, -100, 100, 100), areaTop: 0));
        Assert.False(Notch().Clears(new Point(500, 129), new Rect(-50, -100, 100, 100), areaTop: 0));
    }

    // --- 만들어 주는 노치 ---

    [Fact]
    public void 가상_노치는_화면_위_한가운데다()
    {
        var notch = ScreenNotch.Virtual(new Rect(0, 0, 1920, 1080), scale: 1);

        Assert.NotNull(notch);
        Assert.Equal(ScreenNotch.VirtualWidth, notch!.Value.Rect.Width);
        Assert.Equal(ScreenNotch.VirtualDepth, notch.Value.Rect.Height);
        Assert.Equal(0, notch.Value.Rect.Top);
        Assert.Equal(960, notch.Value.Rect.Left + notch.Value.Rect.Width / 2);
    }

    /// 좌표계가 물리 픽셀이라, 논리 단위로 적힌 상수는 그 화면의 DPI로
    /// 환산되어야 한다. 안 그러면 200% 화면에서 노치가 반쪽만 하다.
    [Fact]
    public void 가상_노치는_디스플레이_배율을_따른다()
    {
        var notch = ScreenNotch.Virtual(new Rect(0, 0, 3840, 2160), scale: 2);

        Assert.NotNull(notch);
        Assert.Equal(ScreenNotch.VirtualWidth * 2, notch!.Value.Rect.Width);
        Assert.Equal(ScreenNotch.VirtualDepth * 2, notch.Value.Rect.Height);
    }

    /// 두 번째 화면은 원점이 0이 아니다. 노치는 그 화면 위에 있어야 한다.
    [Fact]
    public void 가상_노치는_자기_화면_위에_놓인다()
    {
        var notch = ScreenNotch.Virtual(new Rect(1920, -200, 1920, 1080), scale: 1);

        Assert.NotNull(notch);
        Assert.Equal(-200, notch!.Value.Rect.Top);
        Assert.Equal(2880, notch.Value.Rect.Left + notch.Value.Rect.Width / 2);
    }

    /// 화면만큼 넓은 노치는 노치가 아니라 그냥 낮아진 천장이다.
    [Fact]
    public void 노치보다_좁은_화면에는_주지_않는다()
    {
        Assert.Null(ScreenNotch.Virtual(new Rect(0, 0, 100, 100), scale: 1));
    }

    [Fact]
    public void 배율이_0이면_주지_않는다()
    {
        Assert.Null(ScreenNotch.Virtual(new Rect(0, 0, 1920, 1080), scale: 0));
    }

    // --- 영역과 맞물리는 부분 ---

    [Fact]
    public void 노치가_없는_화면_공간의_천장은_영역의_윗변이다()
    {
        var screens = new ScreenSpace([Area], [Area]);
        Assert.Equal(0, screens.CeilingY(500, Area));
        Assert.Null(screens.NotchOver(Area));
    }

    [Fact]
    public void 노치가_있으면_그_아래에서만_천장이_내려온다()
    {
        var screens = new ScreenSpace([Area], [Area], [Notch()]);

        Assert.Equal(30, screens.CeilingY(500, Area));
        Assert.Equal(0, screens.CeilingY(100, Area));
    }

    /// 디스플레이가 둘이면 펫이 지금 어느 화면의 천장을 기고 있느냐가
    /// 머리 위에 뭐가 있는지를 정한다.
    [Fact]
    public void 노치는_자기_화면_위에서만_천장을_내린다()
    {
        var right = new Rect(1000, 0, 1000, 800);
        var screens = new ScreenSpace([Area, right], [Area, right], [Notch(), null]);

        Assert.Equal(30, screens.CeilingY(500, Area));
        // 오른쪽 화면에는 하우징이 없다 — 같은 x라도 답이 다르다.
        Assert.Equal(0, screens.CeilingY(1500, right));
        Assert.Null(screens.NotchOver(right));
    }

    [Fact]
    public void 디스플레이_수와_노치_수가_다르면_거부한다()
    {
        Assert.Throws<ArgumentException>(() => new ScreenSpace([Area], [Area], [Notch(), null]));
    }
}
