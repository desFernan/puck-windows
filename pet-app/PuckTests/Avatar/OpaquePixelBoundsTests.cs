using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class OpaquePixelBoundsTests
{
    /// (x,y)에 한 픽셀만 불투명한 w×h BGRA 비트맵.
    private static BitmapSource OnePixel(int w, int h, int x, int y)
    {
        var pixels = new byte[w * h * 4];
        var i = (y * w + x) * 4;
        pixels[i] = 255; pixels[i + 1] = 255; pixels[i + 2] = 255; pixels[i + 3] = 255;
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
    }

    [Fact]
    public void FindsTheSingleOpaquePixel()
    {
        var bounds = OpaquePixelBounds.Compute(OnePixel(10, 10, 3, 7));
        Assert.Equal(new Int32Rect(3, 7, 1, 1), bounds);
    }

    [Fact]
    public void FullyTransparentImageYieldsEmpty()
    {
        var pixels = new byte[4 * 4 * 4];
        var blank = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
        Assert.Equal(Int32Rect.Empty, OpaquePixelBounds.Compute(blank));
    }

    [Fact]
    public void AlphaBelowThresholdDoesNotCount()
    {
        var pixels = new byte[4 * 4 * 4];
        pixels[3] = 4; // (0,0) 알파 4 — 임계값 8 미만
        var faint = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
        Assert.Equal(Int32Rect.Empty, OpaquePixelBounds.Compute(faint));
    }
}
