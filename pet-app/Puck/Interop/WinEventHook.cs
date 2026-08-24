using System.Runtime.InteropServices;

namespace Puck.Interop;

/// 포그라운드 창이 바뀔 때 알려 준다. mac이 `NSWorkspace`의 앱 활성/실행/종료
/// 알림으로 하던 자리다 — 창 목록이 가장 많이 흔들리는 순간을 폴링이 우연히
/// 만나기를 기다리지 않고 직접 듣는다.
public sealed class WinEventHook : IDisposable
{
    private readonly Win32.WinEventProc _callback;
    private IntPtr _hook;

    public WinEventHook(Action onForegroundChanged)
    {
        // 델리게이트를 필드로 붙잡아 둔다. 넘긴 뒤 GC가 걷어 가면 콜백이
        // 조용히 죽고, 훅은 등록된 채로 아무 일도 하지 않는다.
        _callback = (_, _, _, _, _, _, _) => onForegroundChanged();

        _hook = Win32.SetWinEventHook(
            Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback, 0, 0,
            Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
    }

    /// 훅이 걸렸는가. 실패해도 앱은 돌아간다 — 폴링만으로도 목록은 갱신된다.
    public bool IsInstalled => _hook != IntPtr.Zero;

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        Win32.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
        GC.KeepAlive(_callback);
    }
}
