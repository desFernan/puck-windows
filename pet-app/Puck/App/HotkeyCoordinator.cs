using Puck.Diagnostics;
using Puck.Input;

namespace Puck.App;

/// 전역 핫키를 등록하고 그 결과를 책임진다.
///
/// 등록은 실패할 수 있고(다른 프로그램이 이미 그 조합을 잡고 있다) 그때
/// 앱이 서면 안 된다 — 못 잡은 것만 로그로 남기고 나머지는 그대로 쓴다.
/// 그 판단과 해제까지가 한 덩어리라 따로 두었다.
public sealed class HotkeyCoordinator : IDisposable
{
    private readonly GlobalHotkeyManager _hotkeys = new();

    public HotkeyCoordinator(HotkeyBindings bindings, IReadOnlyDictionary<string, Action> actions)
    {
        _hotkeys.RegisterAll(bindings, actions);

        if (_hotkeys.Unavailable.Count == 0) return;

        AppLogger.Warning("hotkey", "다른 프로그램이 이미 쓰고 있어 등록하지 못한 핫키가 있습니다",
            new Dictionary<string, object?> { ["names"] = string.Join(", ", _hotkeys.Unavailable) });
    }

    public void Dispose() => _hotkeys.Dispose();
}
