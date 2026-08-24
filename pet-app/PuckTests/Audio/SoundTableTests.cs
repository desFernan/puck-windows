using System.IO;
using Puck.Audio;

namespace PuckTests.Audio;

public class SoundTableTests
{
    private static readonly string Package = Path.Combine(Path.GetTempPath(), "puck-sounds", "my-pet");

    private static SoundTable Table(params (string Key, string Path)[] entries)
        => new(Package, entries.ToDictionary(e => e.Key, e => e.Path));

    [Fact]
    public void AMappedKeyResolvesInsideThePackage()
    {
        var path = Table(("land", "sounds/waah.wav")).FilePath("land");
        Assert.NotNull(path);
        Assert.StartsWith(Path.GetFullPath(Package), path);
    }

    [Fact]
    public void AnUnmappedKeyIsSilenceNotAnError()
    {
        // 아바타를 만든 사람이 그 소리를 안 넣었을 뿐이다.
        Assert.Null(Table(("land", "sounds/waah.wav")).FilePath("kick"));
    }

    [Fact]
    public void APathEscapingThePackageIsRefused()
    {
        // 표는 패키지가 들고 온 데이터다. 밖으로 나가게 두면 아바타 하나로
        // 아무 파일이나 열게 된다.
        Assert.Null(Table(("land", "../../../Windows/System32/config/SAM")).FilePath("land"));
        Assert.Null(Table(("land", "C:/Windows/win.ini")).FilePath("land"));
    }

    [Fact]
    public void KeysWithAPrefixComeBackSortedSoTheSetIsStable()
    {
        var table = Table(
            ("chatter_yay", "a.wav"),
            ("chatter_mopping", "b.wav"),
            ("land", "c.wav"));

        Assert.Equal(["chatter_mopping", "chatter_yay"], table.KeysWithPrefix("chatter_"));
    }

    [Fact]
    public void APrefixNothingUsesIsEmpty()
    {
        Assert.Empty(Table(("land", "c.wav")).KeysWithPrefix("chatter_"));
    }
}
