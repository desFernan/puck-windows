using System.Windows;

namespace Puck.ClientWindow.Island;

/// 섬의 크기와 그 안에 그림이 놓이는 방식. 순수한 계산만 있어서 창 없이
/// 시험할 수 있다.
public static class IslandLayout
{
    /// 펫이 설 수 있는 가장 낮은 섬. 이보다 낮으면 펫이 들어갈 자리가 없다.
    public const double MinimumHeight = 60;

    /// 설계가 정한 천장. 그림이 더 버틸 수 있어도 여기서 멈춘다.
    public const double MaximumIslandHeight = 260;

    /// 접었을 때 남는 띠. 펫 하나가 걸어 다니는 것이 보일 만큼은 되어야 한다.
    public const double CollapsedHeight = 38;

    /// 접힌 띠 위에 선 펫의 키. 띠가 짧은 섬이 아니라 펫이 걷는 선으로
    /// 읽힐 만큼 작고, 그래도 펫으로 보일 만큼은 크다.
    public const double CollapsedPetHeight = 26;

    /// 복사본 하나가 다음 것 밑으로 파고드는 깊이. 정확히 맞물린 사각형
    /// 둘은 그 사이로 배경이 비치는 실선을 남긴다.
    public const double TileOverlap = 0.5;

    /// 그림 때문에 섬을 실제로 얼마나 높이 끌 수 있는가.
    ///
    /// 그림은 **높이로** 맞춰지고 가로로 넓다. 그래서 섬이 한 점 자랄 때마다
    /// 그림은 가로로 여러 점을 쓴다. 복사본 하나가 그림이 가진 픽셀만큼
    /// 넓어지는 높이를 넘으면, 섬은 그림을 확대해 달라고 하는 것이 된다 —
    /// 원래 작게 보이도록 그린 부드러운 경계가 갑자기 몇 배로 커진다.
    /// 세로 비율이 고정이고 장면 전체가 프레임 안에 있어야 하므로 짧은
    /// 그림으로 높은 섬을 채울 방법은 없다. 그래서 그림이 못 버티는 자리에서
    /// 끄는 것을 멈춘다.
    ///
    /// `artworkPixelHeight`가 0이면 그림이 아예 없다는 뜻이고, 그때는
    /// 흐려질 것이 없으니 설계의 천장만 남는다. `displayScale`은 200%
    /// 배율에서 2 — 섬 한 점이 그림 두 픽셀을 쓴다.
    public static double MaximumHeight(double artworkPixelHeight, double displayScale)
    {
        if (artworkPixelHeight <= 0 || displayScale <= 0) return MaximumIslandHeight;

        var sharpest = artworkPixelHeight / displayScale;

        // 바닥 아래로는 내려가지 않는다. 작은 그림이 섬을 못 끌게 만들면 안
        // 되고, 설 자리를 거절당한 펫은 집에 오기를 거부하는 것처럼 보인다.
        // 그 그림은 확대되겠지만, 둘 중에는 그쪽이 낫다.
        return Math.Min(MaximumIslandHeight, Math.Max(MinimumHeight, sharpest));
    }

    /// 그림을 섬 가로로 끝에서 끝까지 늘어놓은 자리들. 필요한 만큼 복사한다.
    ///
    /// 가운데를 기준으로 놓아서, 남는 만큼이 양쪽으로 똑같이 넘친다 —
    /// 한쪽 끝에서만 잘리면 그쪽이 잘렸다는 것이 보인다.
    public static IReadOnlyList<Rect> Tiles(Size size, double aspect)
    {
        var unit = Math.Max(size.Height * aspect, 1);
        var copies = Math.Max((int)Math.Ceiling(size.Width / unit), 1);
        var start = (size.Width - unit * copies) / 2;

        var tiles = new List<Rect>(copies);
        for (var index = 0; index < copies; index++)
            tiles.Add(new Rect(start + unit * index, 0, unit + TileOverlap, size.Height));

        return tiles;
    }
}
