using System.Collections.ObjectModel;

namespace Puck.ClientWindow;

/// 대화 한 줄이 누구의 것인가. 색과 들여쓰기가 여기서 갈린다.
public enum TranscriptKind
{
    /// 사람이 한 말.
    User,
    /// 펫이 한 말.
    Pet,
    /// 도구를 쓰고 있다는 표시. 대화가 아니라 진행 상황이다.
    Tool,
    /// 실패. 조용히 넘어가면 사람은 펫이 무시했다고 읽는다.
    Error,
    /// 앱이 하는 안내.
    Notice,
}

public sealed record TranscriptEntry(TranscriptKind Kind, string Text)
{
    /// 스크린리더가 읽을 한 줄. 화면에서 이 줄이 누구 것인지는 **색**이
    /// 말하는데, 색은 읽히지 않는다. 그래서 종류를 글로 앞에 붙인다.
    public string SpokenText
        => Localization.Strings.KindOf(Kind.ToString().ToLowerInvariant()) + ": " + Text;
}

/// 채팅 창에 쌓이는 줄들.
///
/// puck-linux의 `puck-client`는 `TextBuffer`에 줄을 이어 붙였다. 여기서는
/// 줄마다 누구 것인지 표시가 달라야 해서 항목으로 들고 있는다.
public sealed class Transcript
{
    /// 들고 있는 줄 수. 오래 켜 둔 앱에서 대화가 메모리를 먹기만 하는 것을
    /// 막는다 — 지나간 줄은 사람도 스크롤해서 보지 않는다.
    public const int MaxEntries = 500;

    public ObservableCollection<TranscriptEntry> Entries { get; } = [];

    /// 빈 줄은 넣지 않는다. 모델이 생각만 하고 끝낸 턴이 빈 칸으로 남으면
    /// 사람은 펫이 답을 하다 만 것으로 읽는다.
    public void Add(TranscriptKind kind, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        Entries.Add(new TranscriptEntry(kind, text.Trim()));

        while (Entries.Count > MaxEntries) Entries.RemoveAt(0);
    }
}
