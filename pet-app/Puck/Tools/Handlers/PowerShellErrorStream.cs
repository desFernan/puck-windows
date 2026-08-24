using System.Text;
using System.Text.RegularExpressions;

namespace Puck.Tools.Handlers;

/// Windows PowerShell은 stderr가 리다이렉트돼 있으면 오류 레코드를 CLIXML로
/// 직렬화해서 내보낸다. 그대로 모델에게 주면 사람이 읽을 글 대신 XML 덩어리가 간다.
///
/// 여기서 원래 글만 꺼낸다. XML 파서를 쓰지 않는 이유는 이 스트림이 잘려서
/// 오는 일이 흔하기 때문이다 — 반쯤 온 XML도 읽을 수 있는 만큼은 읽어야 한다.
public static partial class PowerShellErrorStream
{
    [GeneratedRegex("<S S=\"(?:Error|Warning)\">(.*?)</S>", RegexOptions.Singleline)]
    private static partial Regex StringElement();

    /// `_x000D_` 같은 이스케이프. CLIXML은 제어문자를 이렇게 싣는다.
    [GeneratedRegex("_x([0-9A-Fa-f]{4})_")]
    private static partial Regex CharEscape();

    public static string Clean(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr)) return "";
        if (!stderr.TrimStart().StartsWith("#< CLIXML", StringComparison.Ordinal)) return stderr.TrimEnd();

        var text = new StringBuilder();
        foreach (Match match in StringElement().Matches(stderr))
            text.Append(Unescape(match.Groups[1].Value));

        // 아무것도 못 꺼냈으면 원문을 준다. XML을 보여 주는 편이 침묵보다 낫다.
        var cleaned = text.ToString().Trim();
        return cleaned.Length > 0 ? cleaned : stderr.TrimEnd();
    }

    private static string Unescape(string value)
    {
        var decoded = CharEscape().Replace(value,
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

        return decoded
            .Replace("&lt;", "<").Replace("&gt;", ">")
            .Replace("&quot;", "\"").Replace("&apos;", "'")
            .Replace("&amp;", "&");
    }
}
