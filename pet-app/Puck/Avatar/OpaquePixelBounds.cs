using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Puck.Avatar;

/// 그림이 실제로 차지하는 사각형. 스프라이트 PNG는 보통 여백을 두고
/// 그려지므로, 이미지 크기를 그대로 쓰면 펫이 화면 가장자리에서
/// 눈에 보이는 것보다 일찍 멈춘다.
public static class OpaquePixelBounds
{
    public static Int32Rect Compute(BitmapSource source, byte alphaThreshold = 8)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * stride + x * 4 + 3] < alphaThreshold) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        return maxX < 0
            ? Int32Rect.Empty
            : new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
