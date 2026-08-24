using System.Text.Json;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarManifestTests
{
    private static AvatarManifest Parse(string json) =>
        JsonSerializer.Deserialize<AvatarManifest>(json, AvatarManifest.JsonOptions)!;

    [Fact]
    public void SmallestWorkingManifestParses()
    {
        var m = Parse("""
        {
          "schema_version": 1,
          "name": "my-pet",
          "type": "sprites",
          "hitbox": { "width": 130, "height": 133 },
          "clips": { "idle": "idle" }
        }
        """);

        Assert.Equal(1, m.SchemaVersion);
        Assert.Equal("my-pet", m.Name);
        Assert.Equal(AvatarType.Sprites, m.Type);
        Assert.Equal(130, m.Hitbox.Width);
        Assert.Equal(133, m.Hitbox.Height);
        Assert.Equal(new ClipReference.Stem("idle"), m.Clips["idle"]);
    }

    [Fact]
    public void AbsentScaleSoundsAndEmotionsGetTheirDefaults()
    {
        var m = Parse("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"idle"}}
        """);

        Assert.Equal(1.0, m.Scale);
        Assert.Empty(m.Sounds);
        Assert.Null(m.Emotions);
        Assert.Null(m.BounceIntensity);
    }

    [Fact]
    public void FullManifestParses()
    {
        var m = Parse("""
        {
          "schema_version": 1, "name": "my-pet", "type": "sprites",
          "scale": 1.5, "bounce_intensity": 0.6,
          "hitbox": { "width": 130, "height": 133 },
          "clips": { "idle": "starry-eyed", "walk": "walk" },
          "emotions": { "happy": "beaming" },
          "sounds": { "land": "sounds/waah.wav" }
        }
        """);

        Assert.Equal(1.5, m.Scale);
        Assert.Equal(0.6, m.BounceIntensity);
        Assert.Equal(new ClipReference.Stem("starry-eyed"), m.Clips["idle"]);
        Assert.Equal(new ClipReference.Stem("beaming"), m.Emotions!["happy"]);
        Assert.Equal("sounds/waah.wav", m.Sounds["land"]);
    }

    [Fact]
    public void ClipMayBeATimeRangeForVideoAvatars()
    {
        var m = Parse("""
        {"schema_version":1,"name":"v","type":"video",
         "hitbox":{"width":1,"height":1},
         "clips":{"idle":{"in":0.5,"out":2.25}}}
        """);

        Assert.Equal(new ClipReference.TimeRange(0.5, 2.25), m.Clips["idle"]);
    }

    [Fact]
    public void RoundTripsThroughSerialisation()
    {
        var json = """
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":10,"height":20},
         "clips":{"idle":"idle","walk":{"in":0.0,"out":1.0}}}
        """;
        var once = Parse(json);
        var twice = Parse(JsonSerializer.Serialize(once, AvatarManifest.JsonOptions));

        // record의 값 동등성은 사전 필드에선 참조 동등이라, 사전은 내용으로 따로 본다.
        Assert.Equal(once.Clips, twice.Clips);
        Assert.Equal(once.Sounds, twice.Sounds);
        Assert.Equal(once with { Clips = twice.Clips, Sounds = twice.Sounds }, twice);
    }
}
