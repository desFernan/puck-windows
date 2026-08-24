using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using Puck.Pointing;

namespace Puck.Tools.Handlers;

/// 펫이 그 자리를 가리킨다. 사람에게 "여기요"라고 말하는 방법이고,
/// 클릭할 수 없는 창(권한이 더 높은 창)에서 할 수 있는 유일한 일이기도 하다.
public sealed class PointAtHandler(Action<Point> pointAt) : IToolHandler
{
    public string Name => "point_at";

    public static ToolSpec Spec => new()
    {
        Name = "point_at",
        Description =
            "펫을 그 자리로 보내 가리킨다. 사람이 직접 눌러야 하는 것을 알려 줄 때 쓴다. " +
            "find_ui_element가 준 frame을 그대로 넘겨도 되고 {x, y}를 줘도 된다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["frame"] = ToolSpec.ObjectParam("가리킬 곳. {x,y} 또는 {left,top,width,height}.",
                new { x = new { type = "number" }, y = new { type = "number" } }),
        },
        Required = ["frame"],
        Approval = ToolApproval.NotRequired,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        if (Args.PointFrom(arguments, "frame") is not { } target)
            return Task.FromResult("가리킬 곳(frame)이 필요합니다. {x,y} 또는 {left,top,width,height}.");

        pointAt(target);
        return Task.FromResult($"({target.X:0}, {target.Y:0})을(를) 가리켰습니다.");
    }
}

/// 실제로 누른다. 승인이 필요한 도구다.
public sealed class ClickElementHandler(Func<Rect> virtualScreen) : IToolHandler
{
    public string Name => "click_element";

    public static ToolSpec Spec => new()
    {
        Name = "click_element",
        Description =
            "그 자리를 실제로 왼쪽 클릭한다. find_ui_element가 준 frame을 그대로 넘겨도 된다. " +
            "사람 대신 누르는 것이므로 되돌릴 수 없는 일에는 쓰지 말고, point_at으로 알려 주는 편이 낫다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["frame"] = ToolSpec.ObjectParam("누를 곳. {x,y} 또는 {left,top,width,height}.",
                new { x = new { type = "number" }, y = new { type = "number" } }),
        },
        Required = ["frame"],
        Approval = ToolApproval.Required,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        if (Args.PointFrom(arguments, "frame") is not { } target)
            return Task.FromResult("누를 곳(frame)이 필요합니다.");

        var ok = SyntheticClick.Click(target, virtualScreen());

        // 실패는 조용하다. 눌렀는데 아무 일도 없는 것과 구분되지 않으므로
        // 가능성을 말해 준다.
        return Task.FromResult(ok
            ? $"({target.X:0}, {target.Y:0})을(를) 클릭했습니다."
            : $"({target.X:0}, {target.Y:0}) 클릭이 전달되지 않았습니다. " +
              "권한이 더 높은 창이면 입력이 차단됩니다(UIPI).");
    }
}

/// 프로그램을 띄운다.
public sealed class LaunchAppHandler : IToolHandler
{
    public string Name => "launch_app";

    public static ToolSpec Spec => new()
    {
        Name = "launch_app",
        Description = "프로그램을 실행한다. 실행 파일 이름이나 전체 경로(예: notepad, ms-settings:)를 준다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["app_name"] = ToolSpec.Param("string", "실행 파일 이름, 경로, 또는 프로토콜 URI."),
        },
        Required = ["app_name"],
        Approval = ToolApproval.NotRequired,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        var app = Args.String(arguments, "app_name");
        if (string.IsNullOrWhiteSpace(app)) return Task.FromResult("실행할 프로그램 이름(app_name)이 필요합니다.");

        // UseShellExecute가 켜져 있어야 이름만으로도, 프로토콜 URI로도 열린다.
        var process = Process.Start(new ProcessStartInfo(app) { UseShellExecute = true });
        return Task.FromResult(process is null
            ? $"{app}을(를) 실행했습니다(프로세스 정보 없음)."
            : $"{app}을(를) 실행했습니다.");
    }
}

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

    /// 모델의 창을 명령 하나의 출력으로 채우지 않는다.
    private static string Truncate(string text, int limit = 8000)
        => text.Length <= limit ? text : text[..limit] + $"\n… ({text.Length - limit}자 잘림)";
}
