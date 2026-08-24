using System.IO;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarPackagePathTests
{
    private static readonly string Package =
        Path.Combine(Path.GetTempPath(), "puck-test-avatars", "my-pet");

    [Theory]
    [InlineData("idle.png")]
    [InlineData("sounds/waah.wav")]
    [InlineData("sounds\\waah.wav")]
    [InlineData("./idle.png")]
    public void NamesInsideThePackageResolve(string relative)
    {
        var resolved = AvatarPackagePath.ResolveFile(Package, relative);
        Assert.NotNull(resolved);
        Assert.StartsWith(Path.GetFullPath(Package), resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../other/idle.png")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("/Windows/System32")]
    [InlineData("\\\\server\\share\\idle.png")]
    [InlineData("idle.png:hidden")]
    [InlineData("sounds/../../escape.wav")]
    public void NamesOutsideThePackageAreRefused(string relative)
    {
        Assert.Null(AvatarPackagePath.ResolveFile(Package, relative));
    }

    [Fact]
    public void SiblingDirectoryWithTheSamePrefixIsNotInside()
    {
        // "my-pet-evil"은 "my-pet"으로 시작하지만 안이 아니다 —
        // 접두사 비교에 구분자를 빼먹으면 통과해 버리는 고전적인 구멍.
        Assert.Null(AvatarPackagePath.ResolveFile(Package, "../my-pet-evil/idle.png"));
    }

    [Fact]
    public void DirectoryNeedNotExistYet()
    {
        var nonexistent = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "pet");
        Assert.NotNull(AvatarPackagePath.ResolveFile(nonexistent, "idle.png"));
    }
}
