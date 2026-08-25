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

        var timer = new CancellationTokenSource(timeout);
        // 토큰은 미리 떠 둔다. 아래 continuation이 시간을 넘긴 일을 뒤에 남긴 채
        // 원본을 버리므로, 그 뒤에 `timer.Token`을 읽으면 이미 없는 것을 읽는다.
        var timedOutToken = timer.Token;
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timer.Token);

        ToolOutcome TimedOut()
        {
            AppLogger.Warning("tool", "도구가 시간을 넘겼습니다",
                new Dictionary<string, object?> { ["tool"] = name, ["seconds"] = timeout.TotalSeconds });
            return new ToolOutcome(toolUseId,
                $"{name}이(가) {timeout.TotalSeconds:0}초 안에 끝나지 않아 중단했습니다.", IsError: true);
        }

        // 핸들러는 거의 다 동기다 — UIA 트리를 걷고, 화면을 찍고, 프로세스를
        // 기다린다. 부른 자리에서 그대로 await 하면 그 일이 UI 스레드에서
        // 벌어져 도구가 도는 동안 펫이 얼어붙는다. 스레드 풀로 옮긴다.
        var work = Task.Run(() => handler.ExecuteAsync(arguments, linked.Token), linked.Token);

        // 토큰을 안 보는 동기 핸들러 앞에서는 위의 타임아웃이 뜻이 없다.
        // 그래서 기다리는 쪽에도 상한을 둔다 — 넘긴 일은 백그라운드에 남지만,
        // 대화는 멈춘 도구 하나에 붙잡히지 않는다.
        _ = work.ContinueWith(t =>
        {
            _ = t.Exception;
            timer.Dispose();
            linked.Dispose();
        }, TaskScheduler.Default);

        try
        {
            if (await Task.WhenAny(work, Task.Delay(timeout, CancellationToken.None)) != work)
            {
                // 사람이 취소한 것이 먼저다. 취소를 시간 초과로 보고하면
                // 사람은 자기가 누른 것이 먹었는지 알 수 없다.
                cancellation.ThrowIfCancellationRequested();
                return TimedOut();
            }

            var content = await work;
            AppLogger.Log(LogLevel.Info, "tool", "도구를 실행했습니다",
                new Dictionary<string, object?>
                {
                    ["tool"] = name,
                    ["ms"] = (int)stopwatch.ElapsedMilliseconds,
                    ["chars"] = content.Length,
                });
            return new ToolOutcome(toolUseId, content, IsError: false);
        }
        catch (OperationCanceledException) when (timedOutToken.IsCancellationRequested &&
                                                 !cancellation.IsCancellationRequested)
        {
            return TimedOut();
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
