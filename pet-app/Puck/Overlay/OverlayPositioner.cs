using System.Windows;

namespace Puck.Overlay;

/// 펫 좌표 → 오버레이 창이 있어야 할 물리 픽셀 사각형.
///
/// 오버레이는 화면 전체가 아니라 펫만 한 크기다 (플랜의 "macOS 원본과
/// 다르게 가는 것" 표 참고). 여유(padding)를 두는 이유는 스쿼시&스트레치와
/// 점프가 외곽선 밖으로 나가고, 클릭 판정의 tolerance도 밖으로 자라기 때문이다.
public static class OverlayPositioner
{
    public const double DefaultPadding = 48;

    public static Int32Rect FrameFor(Point petPosition, Rect visualBounds, double padding = DefaultPadding)
    {
        var outline = visualBounds.IsEmpty ? new Rect(0, 0, 1, 1) : visualBounds;

        var left = petPosition.X + outline.Left - padding;
        var top = petPosition.Y + outline.Top - padding;
        var right = petPosition.X + outline.Right + padding;
        var bottom = petPosition.Y + outline.Bottom + padding;

        // 바깥쪽으로 반올림 — 안쪽으로 자르면 서브픽셀 위치에서 그림의
        // 가장자리 한 줄이 창 밖으로 잘려 나간다.
        var x = (int)Math.Floor(left);
        var y = (int)Math.Floor(top);
        var width = Math.Max(1, (int)Math.Ceiling(right) - x);
        var height = Math.Max(1, (int)Math.Ceiling(bottom) - y);

        return new Int32Rect(x, y, width, height);
    }
}
