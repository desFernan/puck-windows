using System.Windows;

namespace Puck.WindowSensing;

/// 화면에 있는 창 하나. 좌표는 펫과 같은 공간이다 — 가상 화면 물리 픽셀,
/// 좌상단 원점, Y는 아래로. 그래서 창의 윗변은 그냥 `Frame.Top`이다.
///
/// mac 원본은 여기서 AppKit ↔ Quartz 좌표를 정규화해야 했지만
/// (`GlobalScreenSpace`), Win32는 이미 그 공간이라 옮길 것이 없다.
public sealed record WindowInfo(
    IntPtr Handle,
    int ProcessId,
    string? OwnerName,
    string? Title,
    Rect Frame,
    /// DWM이 "화면에 없음"으로 표시한 창. UWP/스토어 앱은 종료해도 이런 창을
    /// 남긴다 — 보이지 않으므로 발판이 될 수 없다.
    bool IsCloaked,
    /// WS_EX_TOOLWINDOW. 작업표시줄에 뜨지 않는 보조 창이고, 우리 오버레이도
    /// 이 스타일이다.
    bool IsToolWindow);
