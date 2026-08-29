using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Puck.Diagnostics;

namespace Puck.Settings;

/// settings.json. 모르는 키는 보존한다 — 구버전이 신버전 설정을 날리지
/// 않게 하는 유일한 방법이고, mac의 UserDefaults가 공짜로 주던 성질이다.
public sealed class SettingsStore
{
    private readonly string _path;
    private readonly JsonObject _raw;

    private SettingsStore(string path, JsonObject raw)
    {
        _path = path;
        _raw = raw;
    }

    public event EventHandler? Changed;

    public static SettingsStore Load(string path)
    {
        JsonObject raw;
        try
        {
            raw = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch (Exception ex)
        {
            AppLogger.Warning("settings", "settings.json을 읽지 못해 기본값으로 시작합니다",
                new Dictionary<string, object?> { ["error"] = ex.Message });
            raw = new JsonObject();
        }
        return new SettingsStore(path, raw);
    }

    public string? AvatarName
    {
        get => GetString("avatar_name", null);
        set => Set("avatar_name", value);
    }

    public double MovementSpeedMultiplier
    {
        get => GetDouble("movement_speed_multiplier", 1.0);
        set => Set("movement_speed_multiplier", value);
    }

    public bool LaunchAtLogin
    {
        get => GetBool("launch_at_login", false);
        set => Set("launch_at_login", value);
    }

    public string ThemeStyle
    {
        get => GetString("theme_style", "dark")!;
        set => Set("theme_style", value);
    }

    public bool AvoidFocusedWindow
    {
        get => GetBool("avoid_focused_window", false);
        set => Set("avoid_focused_window", value);
    }

    /// 사람이 직접 끈 소리. 집중 지원이 조용히 시킨 것과는 다른 것이라
    /// 따로 둔다 — 집중 지원이 풀렸다고 사람이 끈 소리가 돌아오면 안 된다.
    public bool Muted
    {
        get => GetBool("muted", false);
        set => Set("muted", value);
    }

    /// 임시 파일에 쓰고 갈아끼운다 — 저장 중에 죽어도 반쯤 쓰인
    /// settings.json이 남지 않는다.
    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = _path + ".tmp";
        File.WriteAllText(temp, _raw.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, overwrite: true);
    }

    private string? GetString(string key, string? fallback)
        => _raw[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;

    private double GetDouble(string key, double fallback)
        => _raw[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : fallback;

    private bool GetBool(string key, bool fallback)
        => _raw[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    // 제네릭 JsonValue.Create<T>는 T를 런타임에야 알기 때문에 커스텀 노드를 만들고,
    // 그 노드는 직렬화할 때 TypeInfoResolver를 요구한다. 원시 타입 오버로드로 내려
    // 보내면 그냥 값 노드가 나온다.
    private void Set<T>(string key, T value)
    {
        _raw[key] = value switch
        {
            null => null,
            string s => JsonValue.Create(s),
            double d => JsonValue.Create(d),
            bool b => JsonValue.Create(b),
            _ => JsonSerializer.SerializeToNode(value),
        };
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
