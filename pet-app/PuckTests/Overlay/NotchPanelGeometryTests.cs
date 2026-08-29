using System.Windows;
using Puck.Overlay;

namespace PuckTests.Overlay;

/// 노치가 닫혔을 때와 열렸을 때의 크기, 그리고 그 둘 사이의 이력.
///
/// 곤란한 부분은 "커서가 노치 위에 있다"와 "커서가 아직 패널 위에 있다"가
/// 서로 다른 질문이라는 것이고, 그 둘을 같은 방향으로 맞춰 두는 것이 패널이
/// 자기 모서리에서 깜빡이지 않게 하는 유일한 방법이다.
public class NotchPanelGeometryTests
{
    /// 화면 위 한가운데, 폭 185 깊이 24 — 가상 노치의 기본값과 같은 모양.
    private static readonly Rect Notch = new(867, 0, 185, 24);

    /// 띠 하나를 바꾸면 창도 따라 움직여야 한다. 잰 숫자를 적어 두면
    /// 패널이 잘리거나 자기 아랫변 위에 뜬다.
    [Fact]
    public void 열린_높이는_안에_든_것들의_합이다()
    {
        var sum = NotchPanelGeometry.TopInset
                + NotchPanelGeometry.MusicBandHeight
                + NotchPanelGeometry.BandGap
                + 1
                + NotchPanelGeometry.BandGap
                + NotchPanelGeometry.ActionBandHeight
                + NotchPanelGeometry.BottomInset;

        Assert.Equal(NotchPanelGeometry.OpenHeight, sum);
    }

    /// mac은 좌하단 원점이라 패널이 아래로 자라는 것을 `maxY - openHeight`로
    /// 적지만, 여기서는 노치의 윗변이 곧 창의 윗변이다.
    [Fact]
    public void 창은_노치_아래로_자란다()
    {
        var frame = NotchPanelGeometry.WindowFrame(Notch);

        Assert.Equal(Notch.Top, frame.Top);
        Assert.Equal(NotchPanelGeometry.OpenHeight, frame.Height);
    }

    [Fact]
    public void 창은_노치와_가운데가_같다()
    {
        var frame = NotchPanelGeometry.WindowFrame(Notch);

        Assert.Equal(Notch.Left + Notch.Width / 2, frame.Left + frame.Width / 2);
        Assert.Equal(NotchPanelGeometry.OpenWidth, frame.Width);
    }

    /// 노치보다 넉넉히 넓어야 나타나는 것이 "노치가 아래로 자란 것"이
    /// 아니라 "노치에서 나온 패널"로 읽힌다.
    [Fact]
    public void 창은_노치보다_넓다()
    {
        Assert.True(NotchPanelGeometry.OpenWidth > Notch.Width * 2);
    }

    // --- 도착과 머무름 ---

    [Fact]
    public void 노치_안이면_도착이다()
    {
        Assert.True(NotchPanelGeometry.IsArriving(new Point(960, 12), Notch));
    }

    /// 노치의 아랫변이 곧 화면 내용의 윗변이라, 위로 올라오는 포인터는
    /// 정확히 그 경계에서 멈춘다. 여유가 없으면 베젤 안쪽까지 지나쳐야만
    /// 열린다.
    [Fact]
    public void 아랫변_바로_아래도_도착이다()
    {
        Assert.True(NotchPanelGeometry.IsArriving(new Point(960, 26), Notch));
        Assert.False(NotchPanelGeometry.IsArriving(new Point(960, 40), Notch));
    }

    [Fact]
    public void 노치에서_멀면_도착이_아니다()
    {
        Assert.False(NotchPanelGeometry.IsArriving(new Point(200, 12), Notch));
    }

    /// 한 번 열리고 나면 포인터가 향하고 있는 것은 패널이다. 노치를
    /// 벗어나는 순간 닫으면 도착하기도 전에 닫힌다.
    [Fact]
    public void 열린_패널_안이면_머무름이다()
    {
        var frame = NotchPanelGeometry.WindowFrame(Notch);
        var insidePanel = new Point(frame.Left + 20, frame.Bottom - 10);

        Assert.True(NotchPanelGeometry.IsLingering(insidePanel, Notch));
        // 같은 자리가 여는 데는 모자란다 — 그 차이가 이력이다.
        Assert.False(NotchPanelGeometry.IsArriving(insidePanel, Notch));
    }

    /// 같은 위치가 움직여 온 방향에 따라 다른 답을 주는 것이 모서리에서의
    /// 깜빡임을 막는다.
    [Fact]
    public void 같은_자리가_열려_있느냐에_따라_다르게_답한다()
    {
        var frame = NotchPanelGeometry.WindowFrame(Notch);
        var between = new Point(frame.Left + 20, frame.Bottom - 10);

        Assert.False(NotchPanelGeometry.ShouldBeOpen(between, Notch, isOpen: false));
        Assert.True(NotchPanelGeometry.ShouldBeOpen(between, Notch, isOpen: true));
    }

    [Fact]
    public void 패널_밖으로_나가면_닫힌다()
    {
        var frame = NotchPanelGeometry.WindowFrame(Notch);
        var outside = new Point(frame.Left - 40, frame.Bottom + 40);

        Assert.False(NotchPanelGeometry.ShouldBeOpen(outside, Notch, isOpen: true));
    }

    /// 노치 위에 커서를 올린 채로는 닫히지 않아야 한다 — 여는 사각형이
    /// 머무름 사각형 안에 통째로 들어 있는지 확인한다.
    [Fact]
    public void 여는_자리는_전부_머무는_자리이기도_하다()
    {
        foreach (var x in new[] { Notch.Left, Notch.Left + Notch.Width / 2, Notch.Right })
        foreach (var y in new[] { Notch.Top, Notch.Bottom })
        {
            var point = new Point(x, y);
            if (!NotchPanelGeometry.IsArriving(point, Notch)) continue;
            Assert.True(NotchPanelGeometry.IsLingering(point, Notch));
        }
    }
}
