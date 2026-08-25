using System.Text.Json;
using Puck.WindowSensing;

namespace Puck.Tools.Handlers;

/// 지금 창을 가진 프로세스들. mac의 `list_running_apps`.
public sealed class ListRunningAppsHandler(Func<IReadOnlyList<WindowInfo>> windows) : IToolHandler
{
    public string Name => "list_running_apps";

    public static ToolSpec Spec => new()
    {
        Name = "list_running_apps",
        Description = "지금 화면에 창을 띄우고 있는 프로그램들의 이름과 창 제목을 돌려준다.",
        Properties = new Dictionary<string, JsonElement>(),
        Approval = ToolApproval.NotRequired,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        var apps = windows()
            .GroupBy(w => w.OwnerName ?? "(알 수 없음)")
            .Select(g => $"{g.Key}: " + string.Join(" | ", g.Select(w => w.Title ?? "(제목 없음)").Take(4)))
            .ToList();

        return Task.FromResult(apps.Count == 0 ? "창을 가진 프로그램이 없습니다." : string.Join("\n", apps));
    }
}
