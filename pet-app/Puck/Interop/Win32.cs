using System.Runtime.InteropServices;

namespace Puck.Interop;

/// P/Invoke 선언은 전부 여기 산다. 다른 코드는 Win32를 직접 부르지 않는다.
internal static partial class Win32
{
    public const int GWL_EXSTYLE = -20;

    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const int WM_DPICHANGED = 0x02E0;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    public static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                            int X, int Y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    public const int VK_LBUTTON = 0x01;

    // --- 창 열거 (WindowSensing) ---

    public const int GWL_STYLE = -16;
    public const int WS_VISIBLE = unchecked((int)0x10000000);

    /// DWM이 이 창을 "화면에 없음"으로 표시했는가. 0이 아니면 클로킹된 창이다.
    public const int DWMWA_CLOAKED = 14;

    /// 창의 진짜 시각적 사각형. GetWindowRect는 리사이즈용 투명 여백까지
    /// 포함해서, 그 값으로 착지면을 잡으면 펫이 창 위에 붕 떠 보인다.
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    public static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    public static partial int DwmGetWindowAttributeRect(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    // --- 화면 캡처 ---

    public const int SRCCOPY = 0x00CC0020;

    /// 이게 없으면 레이어드 창이 캡처에서 빠진다 — 펫 자신이 레이어드 창이다.
    public const int CAPTUREBLT = 0x40000000;

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetDesktopWindow();

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetWindowDC(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr ho);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy,
                                      IntPtr hdcSrc, int x1, int y1, int rop);

    // --- 전역 핫키 ---

    public const int WM_HOTKEY = 0x0312;

    /// 메시지만 받는 창의 부모. 화면에 뜨지도, 포커스를 갖지도 않는다.
    public static readonly IntPtr HWND_MESSAGE = new(-3);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    // --- 포그라운드 변경 알림 ---

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
                                      int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
                                                WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(IntPtr hWinEventHook);
}
