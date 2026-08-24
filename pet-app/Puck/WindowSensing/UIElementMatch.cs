namespace Puck.WindowSensing;

/// "이 요소가 그 질의에 얼마나 맞는가"의 판단. UIA 트리는 테스트할 수 없지만
/// 이 규칙은 테스트할 수 있어서 따로 뺐다.
public static class UIElementMatch
{
    /// 점수가 이보다 낮으면 후보로 치지 않는다.
    public const double Threshold = 0.3;

    /// 0(전혀 아님)에서 1(정확히 그것)까지.
    ///
    /// 사람은 버튼을 부를 때 화면에 적힌 그대로 쓰지 않는다 — "저장"이라고
    /// 말하고 실제 이름은 "저장(&S)"이거나 "파일 저장"이다. 그래서 정확히
    /// 같을 때만 맞다고 하면 거의 언제나 못 찾는다.
    public static double Score(UIElementInfo element, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 0;
        if (string.IsNullOrWhiteSpace(element.Name)) return 0;

        var name = Normalise(element.Name);
        var wanted = Normalise(query);
        if (name.Length == 0 || wanted.Length == 0) return 0;

        var score =
            name == wanted ? 1.0
            : name.StartsWith(wanted, StringComparison.Ordinal) ? 0.8
            : name.Contains(wanted, StringComparison.Ordinal) ? 0.6
            : wanted.Contains(name, StringComparison.Ordinal) ? 0.4
            : 0.0;

        if (score == 0) return 0;

        // 화면에 없거나 꺼져 있는 것은 이름이 맞아도 사람이 누를 수 없다.
        // 후보에서 빼지는 않는다 — "있는데 꺼져 있음"은 "없음"과 다른 답이다.
        if (element.IsOffscreen) score *= 0.3;
        if (!element.IsEnabled) score *= 0.5;

        return score;
    }

    /// 점수순으로 상위 `limit`개. 같은 점수면 원래 순서(트리 순서)를 지킨다 —
    /// 트리 순서가 곧 화면에서 눈이 훑는 순서다.
    public static IReadOnlyList<UIElementInfo> Best(
        IEnumerable<UIElementInfo> elements, string query, int limit)
        => elements
            .Select((element, index) => (element, index, score: Score(element, query)))
            .Where(x => x.score >= Threshold)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Take(limit)
            .Select(x => x.element)
            .ToList();

    /// 대소문자·공백·니모닉 표시(&)·괄호 안 단축키를 지운다. 사람이 부르는
    /// 이름과 화면에 적힌 이름의 차이가 대부분 여기서 사라진다.
    private static string Normalise(string text)
    {
        var buffer = new System.Text.StringBuilder(text.Length);
        var inParens = 0;

        foreach (var c in text)
        {
            if (c is '(' or '（') { inParens++; continue; }
            if (c is ')' or '）') { if (inParens > 0) inParens--; continue; }
            if (inParens > 0) continue;
            if (c is '&' or '_') continue;
            if (char.IsWhiteSpace(c)) continue;
            buffer.Append(char.ToLowerInvariant(c));
        }

        return buffer.ToString();
    }
}
