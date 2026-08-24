using System.Windows.Media;
using System.Windows.Media.Imaging;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AlphaHitMaskTests
{
    private static BitmapSource OnePixel(int w, int h, int x, int y)
    {
        var pixels = new byte[w * h * 4];
        var i = (y * w + x) * 4;
        pixels[i + 3] = 255;
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
    }

    [Fact]
    public void OpaquePixelIsAHit()
    {
        var mask = AlphaHitMask.From(OnePixel(8, 8, 4, 4));
        Assert.True(mask.Contains(4, 4, tolerance: 0));
    }

    [Fact]
    public void TransparentPixelIsAMiss()
    {
        var mask = AlphaHitMask.From(OnePixel(8, 8, 4, 4));
        Assert.False(mask.Contains(0, 0, tolerance: 0));
    }

    [Fact]
    public void ToleranceGrowsTheHitAreaAroundOpaquePixels()
    {
        var mask = AlphaHitMask.From(OnePixel(8, 8, 4, 4));
        Assert.True(mask.Contains(6, 4, tolerance: 2));
        Assert.False(mask.Contains(7, 4, tolerance: 2));
    }

    [Fact]
    public void PointsOutsideTheImageAreAMiss()
    {
        var mask = AlphaHitMask.From(OnePixel(8, 8, 4, 4));
        Assert.False(mask.Contains(-1, 4, tolerance: 0));
        Assert.False(mask.Contains(8, 4, tolerance: 0));
    }
}
