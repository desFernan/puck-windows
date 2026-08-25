using System.Text.Json;
using Puck.ClientWindow;

namespace PuckTests.ClientWindow;

public class TranscriptTests
{
    [Fact]
    public void LinesKeepTheirOrderAndSpeaker()
    {
        var transcript = new Transcript();
        transcript.Add(TranscriptKind.User, "안녕");
        transcript.Add(TranscriptKind.Pet, "안녕!");

        Assert.Collection(transcript.Entries,
            e => Assert.Equal(new TranscriptEntry(TranscriptKind.User, "안녕"), e),
            e => Assert.Equal(new TranscriptEntry(TranscriptKind.Pet, "안녕!"), e));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void EmptyLinesAreNotShown(string text)
    {
        // 생각만 하고 끝낸 턴이 빈 칸으로 남으면 사람은 펫이 답을 하다 만
        // 것으로 읽는다.
        var transcript = new Transcript();
        transcript.Add(TranscriptKind.Pet, text);

        Assert.Empty(transcript.Entries);
    }

    [Fact]
    public void ALongConversationDropsItsOldestLines()
    {
        var transcript = new Transcript();
        for (var i = 0; i < Transcript.MaxEntries + 10; i++)
            transcript.Add(TranscriptKind.User, $"줄 {i}");

        Assert.Equal(Transcript.MaxEntries, transcript.Entries.Count);
        Assert.Equal("줄 10", transcript.Entries[0].Text);
        Assert.Equal($"줄 {Transcript.MaxEntries + 9}", transcript.Entries[^1].Text);
    }
}

public class ChatApprovalPromptTests
{
    private static IReadOnlyDictionary<string, JsonElement> Args(object value)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(value))!;

    [Fact]
    public void ASingleArgumentIsShownAsJustItsValue()
    {
        // {"command": "git status"}보다 git status가 낫다 — 사람이 승인할지
        // 정하려고 읽는 것은 명령이지 JSON이 아니다.
        Assert.Equal("git status", ChatApprovalPrompt.Describe(Args(new { command = "git status" })));
    }

    [Fact]
    public void SeveralArgumentsKeepTheirNames()
    {
        var described = ChatApprovalPrompt.Describe(Args(new { query = "저장", window_title = "메모장" }));

        Assert.Contains("query: 저장", described);
        Assert.Contains("window_title: 메모장", described);
    }

    [Fact]
    public void AToolWithNoArgumentsDescribesAsNothing()
    {
        Assert.Equal("", ChatApprovalPrompt.Describe(Args(new { })));
    }

    [Fact]
    public void AVeryLongArgumentIsCutSoTheButtonsStayOnScreen()
    {
        var described = ChatApprovalPrompt.Describe(Args(new { script = new string('x', 5000) }));

        Assert.True(described.Length < 1000);
        Assert.EndsWith("…", described);
    }
}
