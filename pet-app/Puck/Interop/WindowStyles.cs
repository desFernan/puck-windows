namespace Puck.Interop;

/// 오버레이 창의 확장 스타일. mac의 NSWindow 설정(레벨, ignoresMouseEvents,
/// collectionBehavior)에 해당하는 것들이 여기 모여 있다.
public static class WindowStyles
{
    /// 레이어드(픽셀 단위 투명) + 도구 창(Alt+Tab과 작업표시줄에 안 뜸)
    /// + 활성화되지 않음(클릭해도 지금 작업 중인 창의 포커스를 빼앗지 않음).
    public static void MakeOverlay(IntPtr hwnd)
    {
        var style = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        style |= Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, style);
    }

    /// 켜면 마우스 이벤트가 창을 그대로 통과해 아래 창으로 간다.
    /// 펫은 대부분의 시간을 이 상태로 보낸다 — 그림 밖의 여백에서
    /// 클릭이 막히면 아래 앱을 쓸 수 없다.
    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        var style = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        style = clickThrough
            ? style | Win32.WS_EX_TRANSPARENT
            : style & ~Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, style);
    }
}
