using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Puck.Avatar;
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
    /// 자세별 보정 — 반대로 그려진 그림을 앱 밖에서 고치지 않아도 되게.
    ///
    /// 자세마다 키를 두지 않고 JSON 한 덩이로 읽고 쓴다. 통째로 읽고 통째로
    /// 쓰는 값이고, 자세마다 키를 두면 어긋날 자리가 여섯 배가 된다.
    ///
    /// 이 포트에는 아직 이걸 만지는 화면이 없다. 값은 settings.json에서
    /// 손으로 적는다 — 커스터마이징 폴더에 있는 그 파일이다.
    public IReadOnlyDictionary<string, AvatarPoseAdjustment> AvatarPoseAdjustments
    {
        get
        {
            if (_raw["avatar_pose_adjustments"] is not JsonObject stored) return Empty;

            var byPose = new Dictionary<string, AvatarPoseAdjustment>(StringComparer.OrdinalIgnoreCase);
            foreach (var (pose, value) in stored)
            {
                try
                {
                    if (value.Deserialize<AvatarPoseAdjustment>() is { } adjustment)
                        byPose[pose] = adjustment;
                }
                catch (JsonException)
                {
                    // 손으로 적는 값이라 틀릴 수 있다. 하나가 틀렸다고
                    // 나머지 다섯까지 버릴 이유는 없다.
                    AppLogger.Warning("settings", "자세 보정 하나를 읽지 못했습니다",
                        new Dictionary<string, object?> { ["pose"] = pose });
                }
            }

            return byPose;
        }
    }

    private static readonly Dictionary<string, AvatarPoseAdjustment> Empty = new();

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
