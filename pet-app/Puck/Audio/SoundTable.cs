using Puck.Avatar;

namespace Puck.Audio;

/// 매니페스트의 `sounds` 표를 키로 찾는다.
///
/// 같은 표가 두 종류의 방아쇠를 받는다: FSM의 클립 키(walk, land, react_click…)와
/// 앱 이벤트 이름(app_launch, task_success…). **매핑되지 않은 키는 무음**이다 —
/// 오류가 아니라 설계다. 아바타를 만든 사람이 그 소리를 안 넣었을 뿐이다.
public sealed record SoundTable(string AvatarDirectory, IReadOnlyDictionary<string, string> Sounds)
{
    public static SoundTable From(AvatarManifest manifest, string avatarDirectory)
        => new(avatarDirectory, manifest.Sounds);

    /// 그 키의 소리 파일 경로. 없으면 null.
    ///
    /// 경로는 반드시 `AvatarPackagePath`를 거친다 — 표는 패키지가 들고 온
    /// 데이터이고, 패키지 밖 파일을 재생하게 두면 아바타 하나로 아무 파일이나
    /// 열게 된다.
    public string? FilePath(string key)
    {
        if (!Sounds.TryGetValue(key, out var relative)) return null;
        return AvatarPackagePath.ResolveFile(AvatarDirectory, relative);
    }

    /// 그 접두사로 시작하는 키들, 이름순. 어떤 혼잣말을 이 아바타가 들고
    /// 있는지 앱이 이름을 정하는 대신 패키지에게 묻는 방법이다.
    public IReadOnlyList<string> KeysWithPrefix(string prefix)
        => Sounds.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                      .OrderBy(k => k, StringComparer.Ordinal)
                      .ToList();
}
