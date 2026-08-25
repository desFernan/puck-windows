using System.Text.Json;
using System.Windows;

namespace Puck.Tools.Handlers;

/// 인자 꺼내기의 잔손. 모델이 보내는 JSON은 스키마를 대체로 지키지만
/// 언제나는 아니다 — 없으면 없다고 말하지 터지지 않는다.
///
/// 창 도구 옆에 얹혀 있었는데 실제로 쓰는 곳은 세 파일이라 제 파일로 냈다.
/// frame의 뜻 자체는 <see cref="ToolFrame"/>가 안다 — 여기는 사전에서
/// 꺼내는 일만 한다.
internal static class Args
{
    public static string? String(IReadOnlyDictionary<string, JsonElement> args, string key)
        => args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static double? Number(IReadOnlyDictionary<string, JsonElement> args, string key)
        => args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    public static int? Int(IReadOnlyDictionary<string, JsonElement> args, string key)
        => (int?)Number(args, key);

    public static Point? PointFrom(IReadOnlyDictionary<string, JsonElement> args, string key)
        => Object(args, key) is { } frame ? ToolFrame.PointIn(frame) : null;

    public static Rect? RectFrom(IReadOnlyDictionary<string, JsonElement> args, string key)
        => Object(args, key) is { } frame ? ToolFrame.RectIn(frame) : null;

    private static JsonElement? Object(IReadOnlyDictionary<string, JsonElement> args, string key)
        => args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;
}
