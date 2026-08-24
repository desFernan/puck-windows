using Puck.Tools.Handlers;

namespace PuckTests.Tools;

public class PowerShellErrorStreamTests
{
    [Fact]
    public void PlainErrorTextIsLeftAlone()
    {
        Assert.Equal("무언가 잘못됐습니다", PowerShellErrorStream.Clean("무언가 잘못됐습니다\n"));
    }

    [Fact]
    public void NothingStaysNothing()
    {
        Assert.Equal("", PowerShellErrorStream.Clean(""));
        Assert.Equal("", PowerShellErrorStream.Clean("   \n "));
    }

    [Fact]
    public void TheErrorTextIsPulledOutOfTheClixmlWrapper()
    {
        // 그대로 두면 사람이 읽을 글 대신 XML 덩어리가 모델에게 간다.
        const string clixml =
            "#< CLIXML\n<Objs Version=\"1.1.0.1\" xmlns=\"http://schemas.microsoft.com/powershell/2004/04\">" +
            "<S S=\"Error\">Get-Item : 'C:\\없는파일.txt' 경로는 존재하지 않으므로 찾을 수 없습니다._x000D__x000A_</S>" +
            "</Objs>";

        var cleaned = PowerShellErrorStream.Clean(clixml);

        Assert.StartsWith("Get-Item :", cleaned);
        Assert.Contains("없는파일.txt", cleaned);
        Assert.DoesNotContain("CLIXML", cleaned);
        Assert.DoesNotContain("_x000D_", cleaned);
    }

    [Fact]
    public void SeveralRecordsAreJoined()
    {
        const string clixml =
            "#< CLIXML\n<Objs><S S=\"Error\">첫 줄_x000D__x000A_</S><S S=\"Error\">둘째 줄</S></Objs>";

        var cleaned = PowerShellErrorStream.Clean(clixml);
        Assert.Contains("첫 줄", cleaned);
        Assert.Contains("둘째 줄", cleaned);
    }

    [Fact]
    public void EntitiesComeBackAsTheirCharacters()
    {
        const string clixml = "#< CLIXML\n<Objs><S S=\"Error\">a &lt;b&gt; &amp; &quot;c&quot;</S></Objs>";
        Assert.Equal("a <b> & \"c\"", PowerShellErrorStream.Clean(clixml));
    }

    [Fact]
    public void WarningsCountToo()
    {
        const string clixml = "#< CLIXML\n<Objs><S S=\"Warning\">조심하세요</S></Objs>";
        Assert.Equal("조심하세요", PowerShellErrorStream.Clean(clixml));
    }

    [Fact]
    public void ATruncatedStreamStillGivesUpWhatItCan()
    {
        // 이 스트림은 잘려서 오는 일이 흔하다. XML 파서를 안 쓰는 이유가 그것이다.
        const string clixml = "#< CLIXML\n<Objs><S S=\"Error\">반쯤 온 오류</S><S S=\"Error\">잘린 부";
        Assert.Equal("반쯤 온 오류", PowerShellErrorStream.Clean(clixml));
    }

    [Fact]
    public void AWrapperWithNothingUsableFallsBackToTheRawText()
    {
        // XML을 보여 주는 편이 침묵보다 낫다.
        const string clixml = "#< CLIXML\n<Objs><Obj S=\"progress\" /></Objs>";
        Assert.Contains("CLIXML", PowerShellErrorStream.Clean(clixml));
    }
}
