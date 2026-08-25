using System.Text.Json;
using Puck.ClientWindow;

namespace PuckTests.ClientWindow;

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
