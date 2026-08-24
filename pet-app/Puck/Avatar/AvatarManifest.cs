using System.Text.Json;
using System.Text.Json.Serialization;

namespace Puck.Avatar;

public enum AvatarType { Usdz, Video, Sprites }

public sealed record Hitbox(double Width, double Height);

public sealed record AvatarManifest
{
    public required int SchemaVersion { get; init; }
    public required string Name { get; init; }
    public required AvatarType Type { get; init; }
    public double Scale { get; init; } = 1.0;
    public double? BounceIntensity { get; init; }
    public required Hitbox Hitbox { get; init; }
    public required IReadOnlyDictionary<string, ClipReference> Clips { get; init; }
    public IReadOnlyDictionary<string, ClipReference>? Emotions { get; init; }
    public IReadOnlyDictionary<string, string> Sounds { get; init; } =
        new Dictionary<string, string>();

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        Converters =
        {
            new ClipReferenceConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        },
    };
}
