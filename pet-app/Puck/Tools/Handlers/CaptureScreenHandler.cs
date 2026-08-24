using System.IO;
using System.Text.Json;
using System.Windows;
using Puck.Diagnostics;
using Puck.Input;

namespace Puck.Tools.Handlers;

/// 화면 한 조각을 찍어 파일로 남긴다.
///
/// 그림 자체를 대화에 넣는 것은 Phase 4(채팅 창)가 붙을 때 할 일이다. 지금은
/// 어디에 저장했는지를 돌려준다 — 모델이 "봤다"고 꾸며 내는 것보다, 사람이
/// 열어 볼 수 있는 파일 하나가 정직하다.
public sealed class CaptureScreenHandler(Func<Rect> virtualScreen) : IToolHandler
{
    public string Name => "capture_screen";

    public static ToolSpec Spec => new()
    {
        Name = "capture_screen",
        Description =
            "화면의 한 부분을 PNG로 저장하고 그 경로를 돌려준다. 사각형을 주지 않으면 화면 전체. " +
            "지금 화면에 무엇이 있는지 사람이 확인해야 할 때 쓴다.",
        Properties = new Dictionary<string, JsonElement>
        {
            ["frame"] = ToolSpec.ObjectParam("찍을 사각형 {left, top, width, height}. 비우면 전체 화면.",
                new
                {
                    left = new { type = "number" },
                    top = new { type = "number" },
                    width = new { type = "number" },
                    height = new { type = "number" },
                }),
        },
        Approval = ToolApproval.NotRequired,
    };

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellation)
    {
        var region = RegionFrom(arguments) ?? Whole(virtualScreen());

        var png = ScreenRegionCapture.CapturePng(region);
        if (png is null) return Task.FromResult("화면을 찍지 못했습니다.");

        var directory = Path.Combine(PuckPaths.Root, "captures");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"capture-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
        File.WriteAllBytes(path, png);

        return Task.FromResult(
            $"{region.Width}x{region.Height} 영역을 찍어 저장했습니다: {path}");
    }

    private static Int32Rect Whole(Rect screen)
        => new((int)screen.Left, (int)screen.Top, (int)screen.Width, (int)screen.Height);

    private static Int32Rect? RegionFrom(IReadOnlyDictionary<string, JsonElement> arguments)
    {
        if (!arguments.TryGetValue("frame", out var frame) || frame.ValueKind != JsonValueKind.Object) return null;

        double? Get(string name) => frame.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetDouble() : null;

        if (Get("left") is not { } left || Get("top") is not { } top ||
            Get("width") is not { } width || Get("height") is not { } height) return null;
        if (width <= 0 || height <= 0) return null;

        return new Int32Rect((int)left, (int)top, (int)width, (int)height);
    }
}
