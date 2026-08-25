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
    public void AnEmotionResolvesLikeAnyOtherClip()
    {
        // 표정은 아바타 만드는 사람이 emotions에 적는다. 펫은 그것을 움직임
        // 클립과 똑같이 한 장의 그림으로 쓴다.
        var result = Load("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"i"},
         "emotions":{"happy":"beaming"}}
        """);
        Assert.Equal("beaming", AvatarLoader.ResolveClipStem("happy", result));
    }

    [Fact]
    public void AnUnknownEmotionStillFallsBackToIdle()
    {
        // 아바타에 그 표정이 없다고 펫이 사라지면 안 된다.
        var result = Load("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"i"},
         "emotions":{"happy":"beaming"}}
        """);
        Assert.Equal("i", AvatarLoader.ResolveClipStem("type", result));
    }

    [Fact]
    public void AManifestWithAUtf8BomStillLoads()
    {
        // Windows에서 메모장·PowerShell·VS Code로 매니페스트를 고치면 BOM이 붙는다.
        // 그걸 "깨진 아바타"로 거절하면 사람이 고칠 수 있는 게 없다.
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes(Minimal)).ToArray();
        var result = AvatarLoader.Load(withBom);
        Assert.Equal("a", result.Manifest.Name);
    }

    [Fact]
    public void MissingDirectoryReportsAvatarNotFound()
    {
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var e = Assert.Throws<AvatarLoaderException>(() => AvatarLoader.Load(missing));
        Assert.Equal(AvatarLoaderError.AvatarNotFound, e.Error);
    }
}
