using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Puck.Avatar;

/// 그려진 것 위를 눌렀는지 판정한다. 스프라이트의 사각형 전체를 쓰면
/// 펫의 머리 위 빈 공간을 눌러도 펫을 잡게 된다.
public sealed class AlphaHitMask
{
    private readonly bool[] _opaque;
    private readonly int _width;
    private readonly int _height;

    private AlphaHitMask(bool[] opaque, int width, int height)
    {
        _opaque = opaque;
        _width = width;
        _height = height;
    }

    public static AlphaHitMask From(BitmapSource source, byte alphaThreshold = 8)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        var opaque = new bool[width * height];
        for (var i = 0; i < opaque.Length; i++)
            opaque[i] = pixels[i * 4 + 3] >= alphaThreshold;

        return new AlphaHitMask(opaque, width, height);
    }

    /// `tolerance` 픽셀 이내에 불투명 픽셀이 있으면 명중.
    public bool Contains(int x, int y, int tolerance)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height) return false;
        if (tolerance <= 0) return _opaque[y * _width + x];

        var minX = Math.Max(0, x - tolerance);
        var maxX = Math.Min(_width - 1, x + tolerance);
        var minY = Math.Max(0, y - tolerance);
        var maxY = Math.Min(_height - 1, y + tolerance);

        for (var yy = minY; yy <= maxY; yy++)
            for (var xx = minX; xx <= maxX; xx++)
                if (_opaque[yy * _width + xx])
                    return true;

        return false;
    }
}
