using System.IO;

namespace Puck.Diagnostics;

/// mac의 ~/Library/Application Support/Puck/ 에 해당하는 한 곳.
public static class PuckPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Puck");

    public static string Avatars => Path.Combine(Root, "Avatars");
    public static string Tank => Path.Combine(Root, "Tank");
    public static string Logs => Path.Combine(Root, "logs");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string EnvFile => Path.Combine(Root, ".env");

    /// 설정의 "커스터마이징 폴더 열기"가 폴더를 만들어 주는 것과 같은 동작.
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Avatars);
        Directory.CreateDirectory(Tank);
        Directory.CreateDirectory(Logs);
    }
}
