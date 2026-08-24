using System.Windows;
using Puck.WindowSensing;

namespace PuckTests.WindowSensing;

public class WindowFilterTests
{
    private static readonly Size Minimum = new(40, 40);

    private static WindowInfo Window(
        int handle, int pid = 1234, double width = 800, double height = 600,
        bool cloaked = false, bool toolWindow = false, string? title = "창")
        => new(new IntPtr(handle), pid, "Some App", title,
               new Rect(0, 0, width, height), cloaked, toolWindow);

    [Fact]
    public void OrdinaryWindowsSurvive()
    {
        var kept = WindowFilter.Keep([Window(1), Window(2)], selfProcessId: 99, Minimum);
        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void OurOwnWindowsAreNotObstacles()
    {
        // 펫의 오버레이 창을 착지면으로 삼으면 자기 머리 위에 선다.
        var kept = WindowFilter.Keep([Window(1, pid: 99), Window(2, pid: 1234)], selfProcessId: 99, Minimum);
        Assert.Equal(new IntPtr(2), Assert.Single(kept).Handle);
    }

    [Fact]
    public void WindowsSmallerThanTheMinimumAreIgnored()
    {
        // 펫보다 작은 자투리 창 위에 서게 하면 발판이 아니라 방해물이 된다.
        var kept = WindowFilter.Keep([Window(1, width: 20, height: 20)], selfProcessId: 99, Minimum);
        Assert.Empty(kept);
    }

    [Fact]
    public void CloakedWindowsAreIgnored()
    {
        // UWP/스토어 앱은 닫아도 클로킹된 창을 남긴다. 화면에 없는 창을
        // 남기면 펫이 아무것도 없는 허공에 선다.
        var kept = WindowFilter.Keep([Window(1, cloaked: true), Window(2)], selfProcessId: 99, Minimum);
        Assert.Equal(new IntPtr(2), Assert.Single(kept).Handle);
    }

    [Fact]
    public void ToolWindowsAreIgnored()
    {
        var kept = WindowFilter.Keep([Window(1, toolWindow: true), Window(2)], selfProcessId: 99, Minimum);
        Assert.Equal(new IntPtr(2), Assert.Single(kept).Handle);
    }

    [Fact]
    public void EmptyFramesAreIgnored()
    {
        var kept = WindowFilter.Keep([Window(1, width: 0, height: 0)], selfProcessId: 99, Minimum);
        Assert.Empty(kept);
    }

    [Fact]
    public void ZOrderIsPreserved()
    {
        // 착지면 판정이 "앞 창이 가리고 있나"를 Z 순서로 본다. 순서가 흐트러지면
        // 뒤 창의 보이지도 않는 윗변에 펫이 선다.
        var input = new[] { Window(1), Window(2, cloaked: true), Window(3), Window(4) };
        var kept = WindowFilter.Keep(input, selfProcessId: 99, Minimum);
        Assert.Equal([new IntPtr(1), new IntPtr(3), new IntPtr(4)], kept.Select(w => w.Handle));
    }
}
