using System.Windows;

namespace Puck.Overlay;

/// 노치가 닫혀 있을 때와 열려 있을 때의 크기, 그리고 커서가 그중 어디에
/// 있는가.
///
/// 사각형 둘과 히트 테스트 하나. WPF에서 떼어 놓은 이유는 곤란한 부분이
/// 테스트 가능해야 하기 때문이고, 그 곤란한 부분이란 "커서가 노치 위에
/// 있다"와 "커서가 아직 패널 위에 있다"가 서로 다른 질문이고 답도 다르다는
/// 것이다. 이 둘을 같은 방향으로 맞춰 두는 것이 패널이 자기 모서리에서
/// 열렸다 닫혔다 깜빡이지 않게 하는 유일한 방법이다.
///
/// 전부 펫의 좌표계다 — 가상 화면 물리 픽셀, 좌상단 원점, Y는 아래로.
/// mac 원본은 AppKit의 좌하단 원점이라 패널이 노치에서 **아래로** 자라는
/// 것을 `y = maxY - openHeight`로 적지만, 여기서는 노치의 윗변이 곧 패널의
/// 윗변이다.
public static class NotchPanelGeometry
{
    /// 무엇이 재생 중인지 보여 주는 띠의 높이.
    ///
    /// 앨범 아트가 정한다. 그 안에서 가장 큰 것이라 — 제목과 진행 막대를
    /// 쌓아도 앨범을 알아볼 만한 정사각형보다는 낮다.
    public const double MusicBandHeight = 62;

    /// 누르는 것들이 놓인 줄의 높이. 컨트롤 하나 높이인 이유는 그 안의
    /// 모든 것이 과녁이고, 높이가 제각각인 과녁은 겨누기 어렵기 때문이다.
    public const double ActionBandHeight = 34;

    /// 두 띠 사이 선의 위아래 여백.
    public const double BandGap = 12;

    /// 내용 둘레의 여백. 아래가 위보다 깊은 이유는 아래 모서리가 둥글기
    /// 때문이다 — 곡선에 붙은 내용은 직선 옆에 있는 것보다 가장자리에 더
    /// 가까워 보인다.
    public const double TopInset = 13;
    public const double BottomInset = 16;

    /// 열렸을 때 노치 아래로 얼마나 내려오는가.
    ///
    /// 두 띠, 그 사이의 실선, 그 둘레의 여백, 그리고 패딩. 잰 숫자 하나가
    /// 아니라 합으로 적는 이유는 띠 하나를 바꾸면 창도 따라 움직여야 하기
    /// 때문이다 — 안 그러면 패널이 잘리거나 자기 아랫변 위에 떠 있다.
    public const double OpenHeight =
        TopInset + MusicBandHeight + BandGap + 1 + BandGap + ActionBandHeight + BottomInset;

    /// 얼마나 넓게 열리는가. 노치보다 넉넉히 넓다 — 그래야 나타나는 것이
    /// 노치가 아래로 자란 것이 아니라 노치에서 나온 패널로 읽힌다.
    public const double OpenWidth = 560;

    /// 닫힌 노치 밖으로 커서가 이만큼 벗어나도 도착한 것으로 친다.
    ///
    /// 노치의 아랫변이 곧 화면 내용의 윗변이라, 위로 올라오는 포인터는
    /// 정확히 그 경계에서 멈춘다. 여유가 없으면 베젤 안쪽까지 지나쳐야만
    /// 패널이 열린다.
    public const double ApproachSlack = 4;

    /// 창의 사각형. 안에 무엇이 그려지든 언제나 열린 크기다.
    ///
    /// 호버마다 크기를 바꾸지 않고 하나로 두는 이유는, 크기가 변하는 창은
    /// 줄어드는 그 프레임에 커서를 자기 밖으로 내보내기 때문이다 — 호버
    /// 패널이 깜빡이게 되는 경로가 이것이다. 바뀌는 것은 안에 칠하는 것과
    /// 마우스를 받는지 여부뿐이다.
    public static Rect WindowFrame(Rect notch)
        => new(notch.Left + notch.Width / 2 - OpenWidth / 2, notch.Top, OpenWidth, OpenHeight);

    /// 커서가 닫힌 노치에 열 만큼 가까운가.
    public static bool IsArriving(Point cursor, Rect notch)
        => Inflated(notch).Contains(cursor);

    /// 커서가 아직 패널을 열어 둘 자리에 있는가.
    ///
    /// 노치가 아니라 창 전체다. 한 번 열리고 나면 포인터가 향하고 있는 것은
    /// 패널이고, 노치 자체를 벗어나는 순간 닫으면 도착하기도 전에 닫힌다.
    public static bool IsLingering(Point cursor, Rect notch)
        => Inflated(WindowFrame(notch)).Contains(cursor);

    /// 커서가 어디 있고 지금 열려 있는지를 보고, 열려 있어야 하는가.
    ///
    /// 한 질문으로 묻는 이유는 두 사각형이 겹치기 때문이다. 여는 데는 작은
    /// 쪽을, 열어 두는 데는 큰 쪽을 쓰므로 그 차이에 앉은 커서는 열린
    /// 패널을 열어 두지만 그 자리에서 열 수는 없다. 그 이력(hysteresis)이
    /// 요점이다 — 같은 위치가 움직여 온 방향에 따라 다른 답을 주는 것이
    /// 모서리에서의 깜빡임을 막는다.
    public static bool ShouldBeOpen(Point cursor, Rect notch, bool isOpen)
        => isOpen ? IsLingering(cursor, notch) : IsArriving(cursor, notch);

    private static Rect Inflated(Rect rect)
    {
        var inflated = rect;
        inflated.Inflate(ApproachSlack, ApproachSlack);
        return inflated;
    }
}
