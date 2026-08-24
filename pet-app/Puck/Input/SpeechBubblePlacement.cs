using System.Windows;

namespace Puck.Input;

/// 펫이 선 자리에 대해 말풍선(또는 입력 버블)이 갈 곳.
///
/// 창도 화면도 살아 있는 펫도 없이 규칙만 테스트할 수 있게 순수 함수로 뺐다.
/// mac 원본과 달리 Y가 아래로 증가하므로 "위"는 빼기다.
public static class SpeechBubblePlacement
{
    /// 붙어 있는 것으로 읽힐 만큼 가깝고, 펫을 가리지 않을 만큼 떨어진 거리.
    public const double Gap = 8;

    /// 화면 가장자리에 이보다 가까이는 붙지 않는다.
    public const double Margin = 8;

    /// <param name="petGroundPoint">펫의 발 위치(가상 화면 물리 픽셀).</param>
    /// <param name="petHeight">그려진 펫의 높이. 머리 위로 띄우기 위해 필요하다.</param>
    /// <param name="bubbleSize">이미 재어 둔 버블 크기.</param>
    /// <param name="workingArea">쓸 수 있는 화면 영역(작업표시줄 제외).</param>
    /// <returns>버블의 좌상단.</returns>
    public static Point Origin(Point petGroundPoint, double petHeight, Size bubbleSize, Rect workingArea)
    {
        var headY = petGroundPoint.Y - petHeight;
        var x = petGroundPoint.X - bubbleSize.Width / 2;
        var y = headY - Gap - bubbleSize.Height;

        // 구석에 선 펫은 이게 없으면 자기 말의 절반을 화면 밖에 둔다.
        var left = workingArea.Left + Margin;
        var right = workingArea.Right - bubbleSize.Width - Margin;
        x = left > right ? left : Math.Clamp(x, left, right);

        // 머리 위로 안 들어가면 발밑으로 뒤집는다 — 머리를 덮어쓰지 않는다.
        if (y < workingArea.Top + Margin)
            y = petGroundPoint.Y + Gap;

        // 그래도 안 들어가면(아주 좁은 화면) 화면 안에는 둔다.
        var bottomLimit = workingArea.Bottom - bubbleSize.Height - Margin;
        y = Math.Min(y, Math.Max(workingArea.Top + Margin, bottomLimit));

        return new Point(x, y);
    }
}
