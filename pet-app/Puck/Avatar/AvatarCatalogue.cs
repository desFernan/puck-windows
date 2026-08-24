using System.IO;
using Puck.Diagnostics;

namespace Puck.Avatar;

public sealed record AvatarEntry(string Name, string Directory);

/// Avatars\ 아래 한 폴더 = 한 캐릭터. 폴더 이름이 그대로 선택 목록에 뜨는 이름이다.
public static class AvatarCatalogue
{
    public static IReadOnlyList<AvatarEntry> Scan(string avatarsRoot)
    {
        if (!Directory.Exists(avatarsRoot)) return [];

        var entries = new List<AvatarEntry>();
        foreach (var dir in Directory.EnumerateDirectories(avatarsRoot))
        {
            try
            {
                AvatarLoader.Load(dir);
            }
            catch (AvatarLoaderException ex)
            {
                // 하나가 깨졌다고 나머지가 안 보이면 안 된다. 이유는 로그에 남는다.
                AppLogger.Warning("avatar", "아바타 패키지를 건너뜁니다",
                    new Dictionary<string, object?>
                    {
                        ["directory"] = Path.GetFileName(dir),
                        ["reason"] = ex.Error.ToString(),
                        ["detail"] = ex.Message,
                    });
                continue;
            }
            entries.Add(new AvatarEntry(Path.GetFileName(dir), dir));
        }

        return entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
