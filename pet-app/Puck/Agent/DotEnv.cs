using System.IO;

namespace Puck.Agent;

/// `.env` 한 장을 읽는다. 라이브러리를 받지 않는 이유는 필요한 문법이
/// `KEY=VALUE` 한 줄뿐이기 때문이다.
///
/// mac의 `DotEnv.swift` 자리. 파일이 없으면 빈 것으로 친다 — 키를 아직 안 넣은
/// 사람에게 오류를 보여 줄 이유가 없다.
public static class DotEnv
{
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // 셸에서 그대로 복사해 붙이는 사람이 많다.
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            var split = line.IndexOf('=');
            if (split <= 0) continue;

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim();

            // 따옴표는 벗긴다. 키를 따옴표째 보내면 서버가 401로 답하고,
            // 그 이유는 화면 어디에도 안 나온다.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];

            if (key.Length > 0) values[key] = value;
        }

        return values;
    }

    private static readonly Dictionary<string, string> Empty = new(StringComparer.OrdinalIgnoreCase);

    private static readonly object CacheLock = new();
    private static (string Path, DateTime Written, long Length) _cachedFrom;
    private static IReadOnlyDictionary<string, string>? _cached;

    /// 설정은 **요청마다** 다시 읽는다 — 키를 넣은 사람이 앱을 끄지 않아도
    /// 되게 하려는 것이다. 다만 한 턴에 도구 왕복이 열두 번까지 있고 그때마다
    /// 디스크를 두드릴 이유는 없으므로, 파일이 그대로면 지난번 것을 준다.
    /// 고쳐 쓴 파일은 시각과 길이가 달라져 그 자리에서 다시 읽힌다.
    public static IReadOnlyDictionary<string, string> Load(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists) return Empty;

            var stamp = (path, file.LastWriteTimeUtc, file.Length);
            lock (CacheLock)
            {
                if (_cached is not null && _cachedFrom == stamp) return _cached;
            }

            var parsed = Parse(File.ReadAllText(path));
            lock (CacheLock)
            {
                _cachedFrom = stamp;
                _cached = parsed;
            }

            return parsed;
        }
        catch (Exception)
        {
            // 읽을 수 없는 .env는 없는 것과 같다. 여기서 던지면 앱이 안 뜬다.
            return Empty;
        }
    }
}
