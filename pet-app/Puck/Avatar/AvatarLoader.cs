using System.IO;
using System.Text.Json;

namespace Puck.Avatar;

public enum AvatarLoaderError
{
    AvatarNotFound,
    ManifestNotDecodable,
    /// idle에는 폴백 대상이 없다 — 권장 클립과 달리, 없으면 "성공적으로"
    /// 로드해서 나중에 눈치채게 두는 대신 그 자리에서 거절한다.
    MissingRequiredClips,
    /// 미래 스키마 버전은 디코딩에 성공하면서 의미가 다를 수 있다.
    /// 조용히 v1로 믿으면 오류 없이 잘못 로드된다.
    UnsupportedSchemaVersion,
}

public sealed class AvatarLoaderException(AvatarLoaderError error, string message)
    : Exception(message)
{
    public AvatarLoaderError Error { get; } = error;
}

public sealed record AvatarLoadResult(
    AvatarManifest Manifest,
    IReadOnlyList<string> MissingRecommendedClips);

public static class AvatarLoader
{
    public const int SupportedSchemaVersion = 1;

    public static IReadOnlyList<string> RequiredClips { get; } = ["idle"];

    public static IReadOnlyList<string> RecommendedClips { get; } =
    [
        "walk", "climb", "fall", "land", "point", "type", "listen",
        "react_click", "react_drag", "kick",
    ];

    public static AvatarLoadResult Load(string avatarDirectory)
    {
        var manifestPath = Path.Combine(avatarDirectory, "manifest.json");
        byte[] data;
        try
        {
            data = File.ReadAllBytes(manifestPath);
        }
        catch (Exception ex)
        {
            throw new AvatarLoaderException(AvatarLoaderError.AvatarNotFound,
                $"{Path.GetFileName(avatarDirectory)}의 manifest.json을 읽지 못했습니다: {ex.Message}");
        }
        return Load(data);
    }

    public static AvatarLoadResult Load(ReadOnlySpan<byte> manifestData)
    {
        AvatarManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<AvatarManifest>(manifestData, AvatarManifest.JsonOptions);
        }
        catch (Exception ex)
        {
            throw new AvatarLoaderException(AvatarLoaderError.ManifestNotDecodable, ex.Message);
        }

        if (manifest is null)
            throw new AvatarLoaderException(AvatarLoaderError.ManifestNotDecodable, "manifest.json이 비어 있습니다");

        if (manifest.SchemaVersion != SupportedSchemaVersion)
            throw new AvatarLoaderException(AvatarLoaderError.UnsupportedSchemaVersion,
                $"이 빌드가 모르는 schema_version입니다: {manifest.SchemaVersion}");

        var missingRequired = RequiredClips.Where(c => !manifest.Clips.ContainsKey(c)).ToList();
        if (missingRequired.Count > 0)
            throw new AvatarLoaderException(AvatarLoaderError.MissingRequiredClips,
                $"필수 클립이 없습니다: {string.Join(", ", missingRequired)}");

        var missingRecommended = RecommendedClips.Where(c => !manifest.Clips.ContainsKey(c)).ToList();
        return new AvatarLoadResult(manifest, missingRecommended);
    }

    /// 요청한 클립의 파일 스템. 없으면 idle의 것으로 떨어지고, idle 자체가
    /// 스템이 아니면(video 아바타) null.
    public static string? ResolveClipStem(string clip, AvatarLoadResult result)
    {
        if (result.Manifest.Clips.TryGetValue(clip, out var reference) &&
            reference is ClipReference.Stem named)
            return named.Value;

        if (result.Manifest.Clips.TryGetValue("idle", out var idle) &&
            idle is ClipReference.Stem idleNamed)
            return idleNamed.Value;

        return null;
    }
}
