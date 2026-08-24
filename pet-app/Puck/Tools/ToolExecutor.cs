using System.Diagnostics;
using System.Text.Json;
using Puck.Diagnostics;

namespace Puck.Tools;

/// 도구 하나를 실제로 돌린다. 타임아웃·취소·예외·로그를 여기 한 곳에서만
/// 처리하고, 핸들러는 자기 일만 한다.
public sealed class ToolExecutor(IReadOnlyDictionary<string, IToolHandler> handlers)
{
    /// 도구 하나가 붙잡을 수 있는 최대 시간. 없으면 멈춘 명령 하나가 대화를
    /// 통째로 세운다 — 사람은 펫이 고장 났다고 생각한다.
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// 셸은 더 오래 걸릴 수 있다(빌드, 설치). 그래도 상한은 있어야 한다.
    public static readonly TimeSpan ShellTimeout = TimeSpan.FromMinutes(2);

    public static TimeSpan TimeoutFor(string toolName) => toolName switch
    {
        "run_shell" or "run_powershell" => ShellTimeout,
        _ => DefaultTimeout,
    };

    public async Task<ToolOutcome> ExecuteAsync(
        string toolUseId, string name, IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellation = default)
    {
        if (!handlers.TryGetValue(name, out var handler))
        {
            // 모델이 없는 도구를 부르는 일은 실제로 있다. 오류로 돌려주면
            // 다음 턴에 스스로 고친다.
            return new ToolOutcome(toolUseId, $"그런 도구는 없습니다: {name}", IsError: true);
        }

        var timeout = TimeoutFor(name);
        var stopwatch = Stopwatch.StartNew();

        using var timer = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timer.Token);

        try
        {
            var content = await handler.ExecuteAsync(arguments, linked.Token);
            AppLogger.Log(LogLevel.Info, "tool", "도구를 실행했습니다",
                new Dictionary<string, object?>
                {
                    ["tool"] = name,
                    ["ms"] = (int)stopwatch.ElapsedMilliseconds,
                    ["chars"] = content.Length,
                });
            return new ToolOutcome(toolUseId, content, IsError: false);
        }
        catch (OperationCanceledException) when (timer.IsCancellationRequested)
        {
            var message = $"{name}이(가) {timeout.TotalSeconds:0}초 안에 끝나지 않아 중단했습니다.";
            AppLogger.Warning("tool", "도구가 시간을 넘겼습니다",
                new Dictionary<string, object?> { ["tool"] = name, ["seconds"] = timeout.TotalSeconds });
            return new ToolOutcome(toolUseId, message, IsError: true);
        }
        catch (OperationCanceledException)
        {
            // 사람이 턴을 취소했다. 이건 오류가 아니라 위로 올려 보낸다.
            throw;
        }
        catch (Exception ex)
        {
            // 도구 하나가 던졌다고 대화가 끝나면 안 된다. 모델은 실패를 읽고
            // 다음 수를 정할 수 있다.
            AppLogger.Warning("tool", "도구가 실패했습니다",
                new Dictionary<string, object?> { ["tool"] = name, ["error"] = ex.Message });
            return new ToolOutcome(toolUseId, $"{name} 실행이 실패했습니다: {ex.Message}", IsError: true);
        }
    }
}
