using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Puck.Diagnostics;
using Puck.Interop;

namespace Puck.Pointing;

/// 사람이 실제로 마우스를 어떻게 쓰는지 듣는다. mac의 `CGEventTap` 자리를
/// `SetWindowsHookEx(WH_MOUSE_LL)`가 대신한다.
///
/// 이걸 쓰는 이유는 오버레이가 대부분의 시간 `WS_EX_TRANSPARENT`라 마우스
/// 이벤트를 아예 못 받기 때문이다. Phase 1은 프레임마다 커서와 버튼 상태를
/// 물어봤는데(폴링), 그러면 프레임 사이에 일어난 클릭을 통째로 놓친다.
public sealed class ClickDetector : IDisposable
{
    private readonly Win32.HookProc _callback;
    private readonly Dispatcher _dispatcher;
    private IntPtr _hook;
    private bool _disposed;

    public ClickDetector(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        // 델리게이트를 필드로 붙잡아 둔다. GC가 걷어 가면 훅이 걸린 채로
        // 콜백만 사라져서, 시스템 전체의 마우스가 이상해진다.
        _callback = HookCallback;
        _hook = Win32.SetWindowsHookExW(Win32.WH_MOUSE_LL, _callback, IntPtr.Zero, 0);

        if (_hook == IntPtr.Zero)
            AppLogger.Error("pointing", "마우스 훅을 걸지 못했습니다 — 클릭을 놓칠 수 있습니다");
    }

    public bool IsInstalled => _hook != IntPtr.Zero;

    /// 눌림/이동/뗌. 좌표는 가상 화면 물리 픽셀, 시각은 초.
    public event Action<Point, double>? Pressed;
    public event Action<Point, double>? Moved;
    public event Action<Point, double>? Released;

    /// 훅 콜백이 시각을 물어볼 시계. 프레임 루프와 같은 것을 써야 제스처
    /// 인식기가 보는 시간이 한 가지다.
    public Func<double>? Clock { get; set; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        GC.KeepAlive(_callback);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        // 훅 콜백은 **반드시 빨리 끝나야 한다.** 여기서 무거운 일을 하면
        // 시스템 전체의 마우스가 끊긴다. 좌표만 읽어 UI 스레드로 넘기고,
        // 판단은 거기서 한다.
        if (code >= 0 && !_disposed)
        {
            var message = wParam.ToInt32();
            if (message is Win32.WM_LBUTTONDOWN or Win32.WM_LBUTTONUP or Win32.WM_MOUSEMOVE)
            {
                var data = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                var point = new Point(data.pt.X, data.pt.Y);
                var now = Clock?.Invoke() ?? 0;

                _dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                {
                    switch (message)
                    {
                        case Win32.WM_LBUTTONDOWN: Pressed?.Invoke(point, now); break;
                        case Win32.WM_LBUTTONUP: Released?.Invoke(point, now); break;
                        default: Moved?.Invoke(point, now); break;
                    }
                });
            }
        }

        // 삼키지 않는다. 펫이 클릭을 가로채면 그 아래 앱을 쓸 수 없다.
        return Win32.CallNextHookEx(_hook, code, wParam, lParam);
    }
}
