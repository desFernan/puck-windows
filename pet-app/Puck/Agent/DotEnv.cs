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

    public static IReadOnlyDictionary<string, string> Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? Parse(File.ReadAllText(path))
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // 읽을 수 없는 .env는 없는 것과 같다. 여기서 던지면 앱이 안 뜬다.
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
