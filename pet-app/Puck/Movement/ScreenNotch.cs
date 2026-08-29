using System.Windows;

namespace Puck.Movement;

/// 화면 위쪽에 매달린 노치. 펫이 돌아서 지나가야 하는 물건이다.
///
/// puck-mac에서 이것은 MacBook의 카메라 하우징이고, 평소에는 펫의 세계에
/// 아예 없다 — 거기 작업 영역은 메뉴 막대를 뺀 값이고 노치는 메뉴 막대와
/// 정확히 같은 높이라, 펫의 천장이 이미 그 아래다. 전체화면 Space에서
/// 메뉴 막대가 사라질 때만 노치가 펫의 세계로 내려온다.
///
/// Windows에는 카메라 하우징도 메뉴 막대도 없다. 그래서 여기서는:
///
/// - **전부 가상이다.** 하드웨어 노치가 없으니 mac의 `isVirtual` 경로만
///   남는다. 재는 대신 만든다.
/// - **깊이가 고정이다.** 유래를 메뉴 막대에서 끌어올 수 없으니 논리 단위
///   상수를 디스플레이 DPI로 환산해 쓴다.
/// - **늘 펫의 세계 안이다.** 작업 영역의 윗변은 보통 화면의 윗변이라
///   (작업 표시줄은 아래에 있다) 노치는 언제나 천장에 걸쳐 있다. 펫은
///   전체화면일 때만이 아니라 천장을 길 때마다 이걸 만난다.
///
/// 그래서 **노치 패널이 켜져 있을 때만** 존재한다. mac의 주석이 말하는
/// 그대로다: 그려지지 않는 하우징은 펫이 없는 것을 피해 돌고 없는 것
/// 아래서 멈추게 만들 뿐이라, 기능이 없는 것만 못하다. 여기서는 그 위험이
/// mac보다 크다 — mac의 가상 하우징은 전체화면에서만 천장에 닿지만
/// 여기 것은 늘 닿기 때문이다.
///
/// 순수하다. 노치가 없는 기계에서도 노치를 테스트할 수 있다.
public readonly record struct ScreenNotch(Rect Rect)
{
    /// 하우징이 없는 디스플레이에 주는 폭, 논리 단위.
    ///
    /// MacBook의 것이 대략 이만하다. 폭은 카메라와 그 옆 센서들의 성질이지
    /// 화면의 성질이 아니라서, 더 넓은 모니터라고 비례해서 거대해지지 않고
    /// 같은 막대를 받는다.
    public const double VirtualWidth = 185;

    /// 하우징이 없는 디스플레이에 주는 깊이, 논리 단위.
    ///
    /// mac은 이걸 메뉴 막대에서 잰다 — 노치가 있는 Mac에서 메뉴 막대는
    /// 하우징과 정확히 같은 높이이기 때문이다. Windows에는 잴 메뉴 막대가
    /// 없으므로 상수다. macOS 메뉴 막대의 높이(약 24pt)에 맞췄다.
    public const double VirtualDepth = 24;

    /// `x`에서 펫의 세계가 위로 얼마나 뻗을 수 있는가.
    ///
    /// <param name="areaTop">펫이 있는 디스플레이의 윗변. 노치가 없는
    /// 모든 자리에서의 답이다.</param>
    public double Ceiling(double x, double areaTop)
    {
        // 노치의 옆면도 그 아래로 친다. 엄밀히 안쪽에서만 천장이 바뀌면
        // 펫이 모서리를 스치고 지나간다.
        if (x < Rect.Left || x > Rect.Right) return areaTop;
        // 영역 자신의 윗변보다 **위**는 절대 아니다. 펫이 있지도 않은
        // 디스플레이의 노치나, 잘못 옮겨 앉은 노치가 펫을 화면 밖으로
        // 내보내는 천장을 돌려주면 안 된다.
        return Math.Max(areaTop, Rect.Bottom);
    }

    /// `position`에서 펫의 **그림 전체**가 노치를 비켜 가는가.
    ///
    /// 발밑 한 점이 아니라 외곽선에 묻는다. 이유는 PetBounds가 그렇게
    /// 쓰여 있는 것과 같다 — 카메라 하우징 뒤에 펫의 절반이 들어가 있는
    /// 것은 전부 들어가 있는 것만큼이나 틀렸다.
    public bool Clears(Point position, Rect visualBounds, double areaTop)
    {
        var head = position.Y + visualBounds.Top;
        var left = position.X + visualBounds.Left;
        var right = position.X + visualBounds.Right;
        return head >= Ceiling(left, areaTop) && head >= Ceiling(right, areaTop);
    }

    /// 하우징이 없는 디스플레이에 주는 하우징. 없어야 하면 null.
    ///
    /// 화면 위 한가운데다. 진짜가 있는 자리가 거기이고, 노치라고 불릴
    /// 물건이 있을 자리도 거기다.
    ///
    /// <param name="screenBounds">디스플레이 전체. 작업 영역이 아니다 —
    /// 노치는 화면의 물리적 윗변에 매달린 것이지 창이 놓일 수 있는 자리의
    /// 윗변에 매달린 것이 아니다.</param>
    /// <param name="scale">이 디스플레이의 DPI 배율. 좌표계가 물리 픽셀이라
    /// 논리 단위 상수를 여기서 환산한다.</param>
    public static ScreenNotch? Virtual(Rect screenBounds, double scale)
    {
        var width = VirtualWidth * scale;
        var depth = VirtualDepth * scale;
        // 노치가 화면만큼 넓으면 그건 노치가 아니라 그냥 낮아진 천장이다.
        if (depth <= 0 || screenBounds.Width <= width) return null;

        var left = screenBounds.Left + (screenBounds.Width - width) / 2;
        return new ScreenNotch(new Rect(left, screenBounds.Top, width, depth));
    }
}
