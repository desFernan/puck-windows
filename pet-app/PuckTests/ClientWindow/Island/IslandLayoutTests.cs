using System.Windows;
using Puck.ClientWindow.Island;

namespace PuckTests.ClientWindow.Island;

public class IslandLayoutTests
{
    /// 동봉 seabed와 같은 모양: 3596x447, 세로 한 점당 가로 여덟 점.
    private const double SeabedAspect = 3596.0 / 447;

    [Fact]
    public void OneCopyIsAsWideAsTheIslandIsTallTimesTheAspect()
    {
        var tiles = IslandLayout.Tiles(new Size(200, 90), SeabedAspect);

        Assert.Single(tiles);
        Assert.Equal(90 * SeabedAspect, tiles[0].Width - IslandLayout.TileOverlap, precision: 6);
    }

    [Fact]
    public void AWideIslandGetsAsManyCopiesAsItNeeds()
    {
        var tiles = IslandLayout.Tiles(new Size(2000, 90), SeabedAspect);

        Assert.True(tiles.Count >= 3);
        Assert.True(tiles[^1].Right >= 2000);
    }

    [Fact]
    public void TheCopiesAreCentredSoBothEndsSpillEqually()
    {
        // 한쪽 끝에서만 잘리면 그쪽이 잘렸다는 것이 보인다.
        var size = new Size(1000, 90);
        var tiles = IslandLayout.Tiles(size, SeabedAspect);

        var left = tiles[0].Left;
        var right = size.Width - (tiles[^1].Right - IslandLayout.TileOverlap);

        Assert.Equal(left, right, precision: 6);
    }

    [Fact]
    public void EachCopyReachesUnderTheNextOne()
    {
        // 정확히 맞물린 사각형 둘은 그 사이로 배경이 비치는 실선을 남긴다.
        var tiles = IslandLayout.Tiles(new Size(2000, 90), SeabedAspect);

        Assert.True(tiles[0].Right > tiles[1].Left);
    }

    [Fact]
    public void AZeroSizedIslandStillAsksForOneCopy()
    {
        Assert.Single(IslandLayout.Tiles(new Size(0, 0), SeabedAspect));
    }

    [Fact]
    public void TheDragStopsWhereThePictureStopsBeingAbleToServeIt()
    {
        // 447픽셀짜리 그림은 100% 배율에서 447점까지 버티지만, 설계의
        // 천장이 그보다 낮다.
        Assert.Equal(IslandLayout.MaximumIslandHeight,
            IslandLayout.MaximumHeight(447, displayScale: 1));

        // 200% 배율에서는 한 점이 두 픽셀을 쓰므로 절반만 버틴다.
        Assert.Equal(223.5, IslandLayout.MaximumHeight(447, displayScale: 2));
    }

    [Fact]
    public void NoPictureMeansNothingCanLookSoft()
    {
        Assert.Equal(IslandLayout.MaximumIslandHeight,
            IslandLayout.MaximumHeight(artworkPixelHeight: 0, displayScale: 2));
    }

    [Fact]
    public void ATinyPictureStillLeavesAnIslandThePetCanStandIn()
    {
        // 설 자리를 거절당한 펫은 집에 오기를 거부하는 것처럼 보인다.
        // 그 그림은 확대되겠지만, 둘 중에는 그쪽이 낫다.
        Assert.Equal(IslandLayout.MinimumHeight, IslandLayout.MaximumHeight(10, displayScale: 2));
    }
}
