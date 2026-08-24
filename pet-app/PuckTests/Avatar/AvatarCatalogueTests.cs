using System.IO;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarCatalogueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AvatarCatalogueTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void MakeAvatar(string name, string manifest = """
        {"schema_version":1,"name":"x","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"idle"}}
        """)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifest);
    }

    [Fact]
    public void FolderNameIsTheDisplayedName()
    {
        MakeAvatar("my-pet");
        var entry = Assert.Single(AvatarCatalogue.Scan(_root));
        Assert.Equal("my-pet", entry.Name);
        Assert.Equal(Path.Combine(_root, "my-pet"), entry.Directory);
    }

    [Fact]
    public void FolderWithoutAManifestIsNotAnAvatar()
    {
        Directory.CreateDirectory(Path.Combine(_root, "not-an-avatar"));
        Assert.Empty(AvatarCatalogue.Scan(_root));
    }

    [Fact]
    public void BrokenManifestIsSkippedRatherThanFailingTheWholeScan()
    {
        MakeAvatar("good");
        MakeAvatar("broken", "{ not json");
        var names = AvatarCatalogue.Scan(_root).Select(e => e.Name).ToList();
        Assert.Equal(["good"], names);
    }

    [Fact]
    public void MissingRootIsEmptyNotAnError()
    {
        Assert.Empty(AvatarCatalogue.Scan(Path.Combine(_root, "nope")));
    }

    [Fact]
    public void ResultIsSortedByName()
    {
        MakeAvatar("zebra");
        MakeAvatar("apple");
        Assert.Equal(["apple", "zebra"], AvatarCatalogue.Scan(_root).Select(e => e.Name));
    }
}
