using System.IO;
using System.Windows.Media.Imaging;
using Puck.Diagnostics;

namespace Puck.ClientWindow.Island;

/// 펫의 섬을 채우는 그림.
///
/// 고를 수 있는 분위기 일곱 가지가 아니라 하나다. 섬이 무엇으로 만들어졌는지는
/// 앱이 어떻게 생겼는가의 일부이지 취향 설정이 아니고, 그 옆에 그러데이션
/// 여섯 개를 늘어놓는 것은 그것을 나쁘게 만드는 방법 여섯 가지였다.
public static class TankArtwork
{
    /// 커스터마이징 폴더에도, 동봉본에도 같은 이름으로 있다.
    public const string Name = "seabed";

    private static readonly Lazy<BitmapSource?> Held = new(Load);

    /// 한 번 읽고 들고 있는다. 섬이 그리는 모든 프레임이 이것을 묻고, 넓은
    /// PNG를 프레임마다 디코딩하는 것은 할 일이 아니다. 앱이 도는 중에 넣은
    /// 그림은 다음 실행에 잡힌다 — README가 약속하는 그대로다.
    public static BitmapSource? Image => Held.Value;

    /// 커스터마이징 폴더의 것이 있으면 그것, 없으면 앱 자신의 것.
    /// 당신 것이 이긴다 — 그 폴더가 있는 이유가 그것이다.
    public static string? ResolvePath(string? custom = null, string? bundled = null)
    {
        custom ??= Path.Combine(PuckPaths.Tank, $"{Name}.png");
        bundled ??= Path.Combine(AppContext.BaseDirectory, "Resources", "Tank", $"{Name}.png");

        if (File.Exists(custom)) return custom;
        return File.Exists(bundled) ? bundled : null;
    }

    /// 세로 한 점당 가로 몇 점인가. 높이가 0인 그림을 막아 둔다 — 섬의
    /// 배치가 이걸로 나눈다.
    public static double Aspect(BitmapSource image)
        => image.PixelHeight > 0 ? (double)image.PixelWidth / image.PixelHeight : 1;

    /// 그림이 실제로 몇 픽셀인가. 섬이 이 그림으로 자기를 채울 수 있는지를
    /// 정하는 것은 그림이 얼마나 크다고 주장하는지가 아니라 실제로 얼마나
    /// 있는지다. WPF의 `PixelWidth`/`PixelHeight`가 이미 픽셀이라 dpi 태그에
    /// 흔들리지 않는다.
    public static double PixelHeight(BitmapSource image) => image.PixelHeight;

    private static BitmapSource? Load()
    {
        if (ResolvePath() is not { } path) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            // 파일을 잠그지 않는다 — 그림을 바꾼 사람이 앱을 끄고 다시 켤 수
            // 있어야 하고, 잠긴 파일은 덮어쓰지도 못한다.
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            AppLogger.Warning("island", "수조 그림을 읽지 못했습니다",
                new Dictionary<string, object?> { ["path"] = path, ["error"] = ex.Message });
            return null;
        }
    }
}
