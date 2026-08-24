using System.Windows.Interop;
using Puck.Diagnostics;
using Puck.Interop;

namespace Puck.Input;

/// 전역 핫키. `RegisterHotKey`는 HWND와 메시지 루프를 요구하므로 보이지 않는
/// 창을 하나 만들어 `WM_HOTKEY`를 받는다. mac의 Carbon `RegisterEventHotKey` 자리.
public sealed class GlobalHotkeyManager : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, (string Name, Action Handler)> _handlers = new();
    private int _nextId = 1;
    private bool _disposed;

    public GlobalHotkeyManager()
    {
        // 메시지만 받는 창(HWND_MESSAGE). 화면에 나타나지도, 작업표시줄에
        // 뜨지도, 포커스를 가져가지도 않는다.
        _source = new HwndSource(new HwndSourceParameters("Puck.Hotkeys")
        {
            ParentWindow = Win32.HWND_MESSAGE,
            WindowStyle = 0,
        });
        _source.AddHook(WndProc);
    }

    /// 등록하지 못한 핫키들. `RegisterHotKey`는 다른 앱이 이미 잡은 조합에
    /// 대해 그냥 false를 준다 — 조용히 넘기면 사람은 자기 키가 왜 안 먹는지
    /// 알 수 없다. 설정 UI가 이 목록을 보여 줘야 한다.
    public IReadOnlyList<string> Unavailable => _unavailable;
    private readonly List<string> _unavailable = [];

    public void Register(string name, HotkeyBinding binding, Action handler)
    {
        if (_disposed) return;

        var id = _nextId++;
        if (!Win32.RegisterHotKey(_source.Handle, id, (uint)binding.Modifiers, binding.VirtualKey))
        {
            _unavailable.Add(name);
            AppLogger.Warning("hotkey", "핫키를 등록하지 못했습니다 — 다른 프로그램이 쓰는 중입니다",
                new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["key"] = binding.VirtualKey,
                    ["modifiers"] = binding.Modifiers.ToString(),
                });
            return;
        }

        _handlers[id] = (name, handler);
    }

    public void RegisterAll(HotkeyBindings bindings, IReadOnlyDictionary<string, Action> handlers)
    {
        foreach (var (name, binding) in bindings.All)
            if (handlers.TryGetValue(name, out var handler))
                Register(name, binding, handler);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var id in _handlers.Keys) Win32.UnregisterHotKey(_source.Handle, id);
        _handlers.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != Win32.WM_HOTKEY) return IntPtr.Zero;
        if (!_handlers.TryGetValue(wParam.ToInt32(), out var entry)) return IntPtr.Zero;

        handled = true;
        try
        {
            entry.Handler();
        }
        catch (Exception ex)
        {
            // 핸들러 하나가 던진 예외로 메시지 루프를 죽이면 앱 전체가 얼어붙는다.
            AppLogger.Error("hotkey", "핫키 처리 중 예외",
                new Dictionary<string, object?> { ["name"] = entry.Name, ["error"] = ex.Message });
        }
        return IntPtr.Zero;
    }
}
