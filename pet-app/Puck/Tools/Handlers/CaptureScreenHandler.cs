using System.IO;
using System.Text.Json;
using System.Windows;
using Puck.Diagnostics;
using Puck.Input;

namespace Puck.Tools.Handlers;

/// 화면 한 조각을 찍어 파일로 남긴다.
///
/// 그림 자체를 대화에 넣는 것은 아직 하지 않는다. 지금은
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
            ["frame"] = ToolFrame.RectParam("찍을 사각형 {left, top, width, height}. 비우면 전체 화면."),
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
        DropOldCaptures(directory);

        return Task.FromResult(
            $"{region.Width}x{region.Height} 영역을 찍어 저장했습니다: {path}");
    }

    /// 남겨 두는 장수. 사람이 지우라고 배운 적 없는 폴더라, 대화 한 번에 몇
    /// 장씩 쌓이면 아무도 모르는 채로 디스크를 먹는다.
    private const int KeepCaptures = 20;

    private static void DropOldCaptures(string directory)
    {
        try
        {
            var old = Directory.EnumerateFiles(directory, "capture-*.png")
                               .OrderByDescending(f => f, StringComparer.Ordinal)
                               .Skip(KeepCaptures)
                               .ToList();

            // 이름이 곧 찍은 시각이라 이름 순서가 시간 순서다 — 파일 정보를
            // 하나씩 물어보지 않아도 된다.
            foreach (var file in old) File.Delete(file);
        }
        catch (Exception ex)
        {
            // 청소에 실패했다고 찍은 것을 못 돌려줄 이유는 없다.
            AppLogger.Warning("capture", "오래된 캡처를 지우지 못했습니다",
                new Dictionary<string, object?> { ["error"] = ex.Message });
        }
    }

    private static Int32Rect Whole(Rect screen)
        => new((int)screen.Left, (int)screen.Top, (int)screen.Width, (int)screen.Height);

    private static Int32Rect? RegionFrom(IReadOnlyDictionary<string, JsonElement> arguments)
    {
        if (Args.RectFrom(arguments, "frame") is not { } frame) return null;
        if (frame.Width <= 0 || frame.Height <= 0) return null;

        return new Int32Rect((int)frame.Left, (int)frame.Top, (int)frame.Width, (int)frame.Height);
    }
}
