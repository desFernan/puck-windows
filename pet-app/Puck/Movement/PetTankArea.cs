using System.Windows;

namespace Puck.Movement;

/// 수조를 움직임 엔진이 원하는 모양으로 — 펫이 돌아다녀도 되는 영역 하나로.
///
/// 순수한 이유는 까다로운 두 가지가 화면 없이 시험할 값어치가 있기 때문이다:
/// 펫이 설 수 없는 수조를 거절하는 것과, 들어갈 수 있는 크기를 정하는 것.
public static class PetTankArea
{
    /// 설 값어치가 있으려면 수조가 펫보다 몇 배 넓어야 하는가.
    /// 펫 하나 너비로는 걸을 자리가 없다.
    public const double MinimumWidthInPets = 2;

    /// 보내진 수조에 들어가려면 펫이 얼마나 작아져야 하는가.
    ///
    /// 섬의 **높이**만 보면 좁은 창이 거절당한다 — 수조는 펫 두 마리 너비는
    /// 되어야 설 값어치가 있어서, 높이로는 통과할 펫이 너비에서 막힌다.
    /// 밖에서 보면 그 거절은 조용하다: 펫이 그냥 바탕화면에 남고 창이 고장 난
    /// 것처럼 보인다. 그래서 거절하는 대신 크기가 양보한다.
    public static double FittedPetHeight(double desired, Size tank, double aspect)
    {
        if (tank.Width <= 0 || tank.Height <= 0 || aspect <= 0) return desired;

        var byWidth = tank.Width / (MinimumWidthInPets * aspect);
        return Math.Max(0, Math.Min(Math.Min(desired, tank.Height), byWidth));
    }

    /// 보고된 수조를 펫이 살 수 있는 영역으로. 쓸 수 없으면 null —
    /// **자른 사각형이 아니라** null이다. 좁은 틈에 끼워 넣은 펫은 펫이 아니라
    /// 버그로 읽히므로, 그때는 "바탕화면에 그대로 있어라"가 답이다.
    ///
    /// `screen`은 오버레이가 실제로 보여 줄 수 있는 범위다. 창이 화면 밖으로
    /// 반쯤 끌려 나갔으면 그 안에 든 만큼만 돌아다닐 수 있다.
    public static Rect? RoamableArea(Rect reported, Rect screen, Size petSize)
    {
        // Rect는 음수 크기를 허용하지 않지만 보고하는 쪽이 빈 사각형을 줄 수
        // 있다. 크기 검사 앞에서 걸러 낸다.
        if (reported.Width <= 0 || reported.Height <= 0) return null;

        var visible = Rect.Intersect(reported, screen);
        if (visible.IsEmpty) return null;

        if (visible.Width < petSize.Width * MinimumWidthInPets) return null;
        if (visible.Height < petSize.Height) return null;

        return visible;
    }
}
