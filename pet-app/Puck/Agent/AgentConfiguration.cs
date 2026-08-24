using Puck.Diagnostics;

namespace Puck.Agent;

/// 코딩 CLI가 한 턴 동안 스스로 할 수 있는 일의 범위. mac의
/// `AgentPermissionMode` 그대로다.
public enum AgentPermissionMode
{
    /// 펫 자신의 도구만. 승인이 필요한 도구는 사람에게 묻는다.
    ToolsOnly,
    /// ...그리고 파일을 고쳐도 된다.
    Edits,
    /// ...명령을 실행해도 된다.
    Everything,
}

/// 에이전트가 한 요청을 보낼 때 필요한 것들.
///
/// **매 요청마다 다시 만든다.** 한 번 잡아 두면 설정에 키를 넣은 사람이 앱을
/// 껐다 켜야 한다 — mac이 같은 이유로 이걸 클로저로 들고 다닌다.
public sealed record AgentConfiguration
{
    /// 기본 모델. 도구를 고르고 여러 걸음을 잇는 일이라 가장 좋은 모델을 쓴다.
    public const string DefaultModel = "claude-opus-5";

    /// 생각 깊이. 도구 루프는 high가 품질과 토큰의 균형점이다.
    public const string DefaultEffort = "high";

    public string? ApiKey { get; init; }
    public string Model { get; init; } = DefaultModel;
    public string Effort { get; init; } = DefaultEffort;
    public AgentPermissionMode Permissions { get; init; } = AgentPermissionMode.ToolsOnly;

    /// 키가 없으면 아무것도 할 수 없다. 조용히 실패하는 대신 이걸 물어본다.
    public bool IsUsable => !string.IsNullOrWhiteSpace(ApiKey);

    /// 프로세스 환경 변수 > `.env` > 기본값.
    ///
    /// 환경 변수를 먼저 보는 이유는, 터미널에서 키를 export 하고 띄운 사람의
    /// 의도가 파일에 적힌 것보다 명확하기 때문이다.
    public static AgentConfiguration Resolve(IReadOnlyDictionary<string, string> dotEnv,
                                             Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;

        string? Value(string key)
        {
            var fromEnvironment = environment(key);
            if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;
            return dotEnv.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
        }

        return new AgentConfiguration
        {
            ApiKey = Value("ANTHROPIC_API_KEY"),
            Model = Value("PUCK_MODEL") ?? DefaultModel,
            Effort = Value("PUCK_EFFORT") ?? DefaultEffort,
            Permissions = ParsePermissions(Value("AGENT_PERMISSIONS")),
        };
    }

    /// 파일에서 읽어 지금 값을 만든다.
    public static AgentConfiguration FromDisk()
        => Resolve(DotEnv.Load(PuckPaths.EnvFile));

    /// 모르는 값은 가장 좁은 것으로 떨어진다 — 설정 파일의 오타가 권한을
    /// 넓히는 방향으로 작용하면 안 된다.
    public static AgentPermissionMode ParsePermissions(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "edits" => AgentPermissionMode.Edits,
        "all" or "everything" => AgentPermissionMode.Everything,
        _ => AgentPermissionMode.ToolsOnly,
    };

    /// 로그에 남겨도 되는 모양. **키는 절대 넣지 않는다** — 있는지 없는지만.
    public IReadOnlyDictionary<string, object?> ToLogFields() => new Dictionary<string, object?>
    {
        ["model"] = Model,
        ["effort"] = Effort,
        ["permissions"] = Permissions.ToString(),
        ["hasKey"] = IsUsable,
    };
}
