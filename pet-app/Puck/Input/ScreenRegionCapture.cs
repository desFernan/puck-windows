using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Puck.Diagnostics;
using Puck.Interop;

namespace Puck.Input;

/// 화면의 한 조각을 찍는다. 에이전트가 그대로 첨부할 PNG로 돌려준다.
///
/// mac의 `CGWindowListCreateImage` 자리. `Windows.Graphics.Capture`는 WinRT
/// 상호운용이 필요해서 미뤄 뒀다 — BitBlt로도 필요한 것은 다 된다.
public static class ScreenRegionCapture
{
    /// 가상 화면 물리 픽셀 사각형을 찍는다. 화면 밖으로 나간 부분은 잘라 낸다.
    /// 찍을 것이 없으면 null.
    public static BitmapSource? Capture(Int32Rect region)
    {
        if (region.Width <= 0 || region.Height <= 0) return null;

        var desktop = Win32.GetDesktopWindow();
        var screenDc = Win32.GetWindowDC(desktop);
        if (screenDc == IntPtr.Zero) return null;

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        try
        {
            memoryDc = Win32.CreateCompatibleDC(screenDc);
            bitmap = Win32.CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero) return null;

            var previous = Win32.SelectObject(memoryDc, bitmap);

            // CAPTUREBLT가 없으면 레이어드 창이 통째로 빠진다 — 펫 자신이
            // 레이어드 창이라, 그게 없으면 "펫이 있는 화면"을 찍을 수가 없다.
            var ok = Win32.BitBlt(memoryDc, 0, 0, region.Width, region.Height,
                                  screenDc, region.X, region.Y,
                                  Win32.SRCCOPY | Win32.CAPTUREBLT);

            Win32.SelectObject(memoryDc, previous);
            if (!ok)
            {
                AppLogger.Warning("capture", "화면을 찍지 못했습니다",
                    new Dictionary<string, object?> { ["region"] = $"{region.X},{region.Y} {region.Width}x{region.Height}" });
                return null;
            }

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) Win32.DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero) Win32.DeleteDC(memoryDc);
            Win32.ReleaseDC(desktop, screenDc);
        }
    }

    /// 에이전트가 첨부할 수 있게 PNG 바이트로.
    public static byte[]? CapturePng(Int32Rect region)
    {
        var source = Capture(region);
        if (source is null) return null;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
