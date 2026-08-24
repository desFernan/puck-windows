using System.IO;
using System.Text;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarLoaderTests
{
    private static AvatarLoadResult Load(string json) =>
        AvatarLoader.Load(Encoding.UTF8.GetBytes(json));

    private static AvatarLoaderException LoadFailure(string json) =>
        Assert.Throws<AvatarLoaderException>(() => AvatarLoader.Load(Encoding.UTF8.GetBytes(json)));

    private const string Minimal = """
    {"schema_version":1,"name":"a","type":"sprites",
     "hitbox":{"width":1,"height":1},"clips":{"idle":"idle"}}
    """;

    [Fact]
    public void IdleAloneIsAValidAvatar()
    {
        var result = Load(Minimal);
        Assert.Equal("a", result.Manifest.Name);
    }

    [Fact]
    public void MissingIdleIsRejected()
    {
        var e = LoadFailure("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"walk":"walk"}}
        """);
        Assert.Equal(AvatarLoaderError.MissingRequiredClips, e.Error);
    }

    [Fact]
    public void FutureSchemaVersionIsRejectedRatherThanTrusted()
    {
        var e = LoadFailure(Minimal.Replace("\"schema_version\":1", "\"schema_version\":2"));
        Assert.Equal(AvatarLoaderError.UnsupportedSchemaVersion, e.Error);
    }

    [Fact]
    public void UnparseableManifestIsRejected()
    {
        var e = LoadFailure("{ not json");
        Assert.Equal(AvatarLoaderError.ManifestNotDecodable, e.Error);
    }

    [Fact]
    public void MissingRecommendedClipsAreReportedNotFatal()
    {
        var result = Load(Minimal);
        Assert.Equal(AvatarLoader.RecommendedClips, result.MissingRecommendedClips);
    }

    [Fact]
    public void PresentRecommendedClipIsNotReportedMissing()
    {
        var result = Load("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"idle","walk":"w"}}
        """);
        Assert.DoesNotContain("walk", result.MissingRecommendedClips);
    }

    [Fact]
    public void MissingClipFallsBackToIdlesStem()
    {
        var result = Load("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"starry-eyed"}}
        """);
        Assert.Equal("starry-eyed", AvatarLoader.ResolveClipStem("walk", result));
        Assert.Equal("starry-eyed", AvatarLoader.ResolveClipStem("idle", result));
    }

    [Fact]
    public void PresentClipUsesItsOwnStem()
    {
        var result = Load("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"i","walk":"w"}}
        """);
        Assert.Equal("w", AvatarLoader.ResolveClipStem("walk", result));
    }

    [Fact]
    public void MissingDirectoryReportsAvatarNotFound()
    {
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var e = Assert.Throws<AvatarLoaderException>(() => AvatarLoader.Load(missing));
        Assert.Equal(AvatarLoaderError.AvatarNotFound, e.Error);
    }
}
