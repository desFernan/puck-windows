using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Puck.Interop;

namespace Puck.WindowSensing;

/// 지금 화면에 있는 창들을 앞에서 뒤 순서로 읽어 온다.
///
/// 실제 창에 의존하므로 단위 테스트하지 않는다 — 얇게 두고, 판단은 전부
/// `WindowFilter` / `LandingSurfaceResolver` / `WindowSupport` 쪽 순수
/// 함수에 있다. mac 원본이 `fetchRawWindowList`를 같은 이유로 분리해 뒀다.
public static class WindowListSource
{
    /// `EnumWindows`는 Z 순서로, 맨 앞 창부터 준다. 착지면 판정이 그 순서에
    /// 기대므로 그대로 유지한다.
    public static IReadOnlyList<WindowInfo> Fetch()
    {
        var windows = new List<WindowInfo>(64);

        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;

            var frame = FrameOf(hwnd);
            if (frame.IsEmpty) return true;

            Win32.GetWindowThreadProcessId(hwnd, out var pid);

            windows.Add(new WindowInfo(
                Handle: hwnd,
                ProcessId: (int)pid,
                OwnerName: ProcessNameOf((int)pid),
                Title: TitleOf(hwnd),
                Frame: frame,
                IsCloaked: IsCloaked(hwnd),
                IsToolWindow: (ExStyleOf(hwnd) & Win32.WS_EX_TOOLWINDOW) != 0));

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// 그림자·리사이즈 여백을 뺀 진짜 사각형. DWM이 답하지 못하는 창(콘솔 등)만
    /// GetWindowRect로 떨어진다.
    private static Rect FrameOf(IntPtr hwnd)
    {
        if (Win32.DwmGetWindowAttributeRect(
                hwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out var dwm, Marshal.SizeOf<Win32.RECT>()) == 0)
            return ToRect(dwm);

        return Win32.GetWindowRect(hwnd, out var raw) ? ToRect(raw) : Rect.Empty;
    }

    private static Rect ToRect(Win32.RECT r)
    {
        var width = r.Right - r.Left;
        var height = r.Bottom - r.Top;
        return width <= 0 || height <= 0 ? Rect.Empty : new Rect(r.Left, r.Top, width, height);
    }

    private static bool IsCloaked(IntPtr hwnd)
        => Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0
           && cloaked != 0;

    private static int ExStyleOf(IntPtr hwnd) => Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);

    private static string? TitleOf(IntPtr hwnd)
    {
        var length = Win32.GetWindowTextLength(hwnd);
        if (length <= 0) return null;

        var buffer = new char[length + 1];
        var copied = Win32.GetWindowText(hwnd, buffer, buffer.Length);
        return copied <= 0 ? null : new string(buffer, 0, copied);
    }

    /// 창 제목만으로는 무슨 앱인지 알기 어려울 때가 많다. 프로세스 이름은
    /// 도구 응답과 로그가 사람에게 보여 줄 이름이다.
    private static string? ProcessNameOf(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (Exception)
        {
            // 열거하는 사이에 끝난 프로세스. 이름이 없을 뿐 창은 유효하다.
            return null;
        }
    }
}
