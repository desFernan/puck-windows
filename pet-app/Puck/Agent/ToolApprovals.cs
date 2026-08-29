using System.Text.Json;
using Puck.Tools;

namespace Puck.Agent;

/// 사람에게 물어야 하는 도구를 어떻게 물을 것인가.
/// 답하는 쪽을 갈아 끼울 수 있게 인터페이스로 둔다.
public interface IApprovalPrompt
{
    /// 이 도구를 이 인자로 실행해도 되는가.
    Task<bool> RequestAsync(string toolName, IReadOnlyDictionary<string, JsonElement> arguments,
                            CancellationToken cancellation);
}

/// UI가 아직 없을 때. 묻지 않고 거절한다 — 물어볼 수 없는 상황에서 "예"로
/// 치는 것은 사람이 안 보는 사이에 명령을 실행하는 것이다.
public sealed class DenyingApprovalPrompt : IApprovalPrompt
{
    public Task<bool> RequestAsync(string toolName, IReadOnlyDictionary<string, JsonElement> arguments,
                                   CancellationToken cancellation) => Task.FromResult(false);
}

/// 도구를 실행해도 되는지 정한다.
public sealed class ToolApprovals(IApprovalPrompt prompt)
{
    /// 물어보지 않고 지나가는 셸 명령들. 읽기만 하고, 되돌릴 것이 없고,
    /// 사람이 매번 "예"를 누르게 하면 승인 자체가 의미를 잃는 것들.
    ///
    /// **거의 첫 낱말만 본다.** 인자까지 판단하려 들면 `git log; rm -rf` 같은
    /// 것을 놓치는 쪽으로 틀리게 되므로, 아래 검사가 이어붙인 명령을 통째로 막는다.
    /// 예외는 `git` 하나다 — 아래 `ReadOnlyGitVerbs` 참고.
    public static IReadOnlySet<string> ShellAllowlist { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "echo", "whoami", "hostname", "date", "pwd", "ver",
        "dir", "ls", "cat", "type", "get-location", "get-date", "get-childitem",
        "tasklist", "get-process", "ipconfig", "systeminfo",
        "git",
    };

    /// `git`은 읽기도 쓰기도 하는 한 낱말이다 — `git push`, `git reset --hard`,
    /// `git clean -fdx`의 첫 낱말도 `git`이다. 그래서 여기만 두 번째 낱말까지
    /// 본다. 어떤 깃발을 붙여도 저장소를 바꾸지 못하는 것만 남긴다.
    public static IReadOnlySet<string> ReadOnlyGitVerbs { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "status", "log", "diff", "show", "blame", "shortlog",
        "describe", "rev-parse", "ls-files", "grep", "whatchanged",
    };

    /// 명령을 이어붙이거나, 방향을 바꾸거나, 다른 명령을 그 자리에서 부르는
    /// 기호. 하나라도 있으면 허용 목록을 적용하지 않는다 — `git log && del *`
    /// 의 첫 낱말도 `git`이다.
    ///
    /// `$`와 괄호가 여기 있는 이유는 `echo $(del *)`처럼 부분식이 첫 낱말과
    /// 상관없이 실행되기 때문이다.
    private static readonly char[] Chaining = [';', '|', '&', '>', '<', '`', '$', '(', ')'];

    /// 줄바꿈도 PowerShell에게는 명령 구분자다.
    private static readonly char[] Newlines = ['\n', '\r'];

    public static bool IsAllowlistedCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;

        // `run_shell`은 **한 줄**이다. 첫 줄만 보고 통과시키면 그 아래에
        // 무엇이든 붙는다 — "git log\nRemove-Item -Recurse ..." 의 첫 낱말도 git이다.
        if (command.IndexOfAny(Newlines) >= 0) return false;
        if (command.IndexOfAny(Chaining) >= 0) return false;

        var words = command.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || !ShellAllowlist.Contains(words[0])) return false;

        if (words[0].Equals("git", StringComparison.OrdinalIgnoreCase))
            return words.Length >= 2 && ReadOnlyGitVerbs.Contains(words[1]);

        return true;
    }

    /// 이 도구를 지금 실행해도 되는가. 필요하면 사람에게 묻는다.
    public async Task<bool> IsAllowedAsync(ToolSpec spec, IReadOnlyDictionary<string, JsonElement> arguments,
                                           AgentPermissionMode mode, CancellationToken cancellation)
    {
        switch (spec.Approval)
        {
            case ToolApproval.NotRequired:
                return true;

            case ToolApproval.RequiredUnlessAllowlisted:
                var command = arguments.TryGetValue("command", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString()
                    : null;
                if (IsAllowlistedCommand(command)) return true;
                break;
        }

        // `Auto`는 사람이 "그만 물어라"라고 답해 둔 것이다. 물음을 띄웠다가
        // 대신 답해 주는 것이 아니라 **아예 띄우지 않는다** — 떴다가 저절로
        // 닫히는 카드는 아무도 누를 수 없는 단추 두 개를 남긴다.
        //
        // `Everything`은 여기 없다. 그것이 정하는 것은 코딩 CLI가 혼자 무엇을
        // 하느냐이고, 모델이 펫에게 시킨 셸 명령은 다른 물음이다.
        if (mode.ApprovesWithoutAsking()) return true;

        return await prompt.RequestAsync(spec.Name, arguments, cancellation);
    }
}
