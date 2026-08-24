using System.Windows;

namespace Puck.WindowSensing;

/// 창 안의 UI 요소 하나. UIA가 준 것을 펫과 같은 좌표 공간으로 옮겨 담는다.
public sealed record UIElementInfo(
    string? Name,
    string ControlType,
    Rect Bounds,
    bool IsEnabled,
    bool IsOffscreen,
    /// 눌러 볼 수 있는가(InvokePattern이 있는가). 도구가 "클릭할 수 있는 것"만
    /// 추릴 때 쓴다.
    bool IsInvokable);

/// 요소 검색이 왜 아무것도 못 찾았는가. "없음"과 "볼 수 없음"은 다르다 —
/// 후자는 사람이 할 수 있는 일이 있다.
public enum UIElementSearchStatus
{
    Ok,
    /// 창은 있는데 UIA가 트리를 주지 않았다. Windows에서 이건 거의 항상
    /// UIPI다 — 관리자 권한으로 실행된 창은 일반 권한 프로세스가 들여다볼 수 없다.
    BlockedByPrivilege,
    /// 창 자체가 없다.
    WindowNotFound,
}

public sealed record UIElementSearchResult(
    UIElementSearchStatus Status,
    IReadOnlyList<UIElementInfo> Matches,
    /// 트리를 훑다 상한에 걸려 멈췄는가. 걸렸다면 "없다"가 아니라 "여기까진 없다"다.
    bool Truncated = false);
