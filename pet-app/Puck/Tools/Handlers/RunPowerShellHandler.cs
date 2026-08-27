using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Puck.Tools.Handlers;

/// PowerShell을 돌린다. `run_shell`은 명령 한 줄, `run_powershell`은 스크립트다.
/// 둘로 나뉜 이유는 승인 등급이 다르기 때문이다.
public sealed class RunPowerShellHandler(string toolName, string argumentName) : IToolHandler
{
    public string Name => toolName;

    public static ToolSpec ShellSpec => new()
    {
        Name = "run_shell",
        Description =
            "PowerShell 명령 한 줄을 실행하고 출력을 돌려준다. 읽기만 하는 흔한 명령" +
            "(git status, dir, whoami 등)은 묻지 않고 실행되고, 그 밖은 사람에게 확인을 받는다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["command"] = ToolSpec.Param("string", "실행할 명령 한 줄."),
        },
        Required = ["command"],
        Approval = ToolApproval.RequiredUnlessAllowlisted,
    };

    public static ToolSpec ScriptSpec => new()
    {
        Name = "run_powershell",
        Description =
            "여러 줄 PowerShell 스크립트를 실행한다. COM 자동화(New-Object -ComObject ...)처럼 " +
            "명령 한 줄로는 안 되는 일에 쓴다. 언제나 사람에게 확인을 받는다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["script"] = ToolSpec.Param("string", "실행할 스크립트."),
        },
        Required = ["script"],
        Approval = ToolApproval.Required,
    };

    public async Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        var script = Args.String(arguments, argumentName);
        if (string.IsNullOrWhiteSpace(script))
            return $"실행할 내용({argumentName})이 필요합니다.";

        var start = new ProcessStartInfo("powershell.exe")
        {
            // -NoProfile: 사람의 프로필이 출력이나 동작을 바꾸면 결과를 믿을 수 없다.
            // -NonInteractive: 프롬프트가 뜨면 아무도 답할 수 없어 타임아웃까지 멈춘다.
            // -EncodedCommand: 명령을 UTF-16LE base64로 넘긴다. 표준 입력이나
            //   -Command 문자열로 주면 Windows PowerShell이 콘솔 코드페이지로
            //   읽어서 한글이 깨진다("안녕"이 "?�녕"으로 갔다).
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Encode(script),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("powershell.exe를 띄우지 못했습니다.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
        var stderr = process.StandardError.ReadToEndAsync(cancellation);

        try
        {
            await process.WaitForExitAsync(cancellation);
        }
        catch (OperationCanceledException)
        {
            // 취소를 약속했으면 실제로 멈춰야 한다. 안 그러면 "취소됨"이라고
            // 답한 뒤에도 명령이 계속 돈다.
            try { process.Kill(entireProcessTree: true); } catch (Exception) { /* 이미 끝났다 */ }
            throw;
        }

        var output = (await stdout).TrimEnd();
        var error = PowerShellErrorStream.Clean(await stderr);

        var report = new StringBuilder();
        report.Append("종료 코드 ").Append(process.ExitCode);
        if (output.Length > 0) report.Append("\n--- 출력 ---\n").Append(Truncate(output));
        if (error.Length > 0) report.Append("\n--- 오류 ---\n").Append(Truncate(error));
        if (output.Length == 0 && error.Length == 0) report.Append("\n(출력 없음)");

        return report.ToString();
    }

    /// 나가는 쪽은 -EncodedCommand로, 돌아오는 쪽은 앞머리에서 출력 인코딩을
    /// UTF-8로 바꿔서 맞춘다. 둘 중 하나만 하면 여전히 깨진다.
    private static string Encode(string script)
    {
        // $ProgressPreference: stderr가 리다이렉트돼 있으면 Windows PowerShell이
        // 진행률 레코드를 CLIXML로 직렬화해 stderr에 쏟는다. 그걸 그대로 두면
        // 성공한 명령마다 "오류" 덩어리가 모델에게 간다.
        const string preamble =
            "$ProgressPreference = 'SilentlyContinue'\n" +
            "$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)\n";
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(preamble + script));
    }

    /// 명령 하나의 출력으로 모델의 창을 채우지 않는다. 잘린 쪽은 **앞머리를
    /// 남긴다** — 오류 메시지도, 목록의 첫 줄도 앞에 있다.
    ///
    /// 대화 기록은 마흔 장까지 쌓이고(AgentConversation.MaxMessages) 그
    /// 전부가 매 요청에 다시 실려 나가므로, 한 번의 출력이 그 예산의 한
    /// 자리를 넘게 먹으면 안 된다. 한글은 토큰당 글자 수가 영어의 절반쯤이라
    /// 6000자면 대략 4천~6천 토큰이다.
    private const int OutputLimit = 6000;

    private static string Truncate(string text, int limit = OutputLimit)
        => text.Length <= limit ? text : text[..limit] + $"\n… (뒤 {text.Length - limit}자 잘림)";
}
