using System.Diagnostics;
using System.Text.Json;

namespace Puck.Tools.Handlers;

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
        // 이름만 받는 것처럼 보이지만 전체 경로도, 프로토콜 URI도 받는다 —
        // 즉 아무 실행 파일이나 띄울 수 있다. 이걸 묻지 않고 지나가게 두면
        // 셸을 승인 뒤에 두는 일이 뜻을 잃는다(그냥 띄우면 되니까).
        Approval = ToolApproval.Required,
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
