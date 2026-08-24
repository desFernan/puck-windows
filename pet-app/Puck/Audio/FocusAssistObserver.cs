using Microsoft.Win32;
using Puck.Diagnostics;

namespace Puck.Audio;

/// 집중 지원(Focus Assist)이 켜져 있으면 소리를 내지 않는다. mac의
/// `FocusModeObserver`와 같은 예의다 — 사람이 방해받지 않겠다고 해 둔 동안
/// 펫이 혼자 떠드는 것은 그 설정을 무시하는 것이다.
///
/// 공개 API가 없어서 셸의 알림 설정 레지스트리를 읽는다. 값이 없거나 읽지
/// 못하면 "켜져 있지 않다"로 본다 — 확실하지 않을 때 조용히 만들면, 소리가
/// 왜 안 나는지 아무도 알 수 없다.
public sealed class FocusAssistObserver
{
    private const string QuietHoursKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.QuietHours";

    /// 알림이 꺼져 있는가 = 집중 지원 중인가.
    public bool IsQuiet()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(QuietHoursKey);
            if (key?.GetValue("Enabled") is int enabled) return enabled == 0;
            return false;
        }
        catch (Exception ex)
        {
            AppLogger.Warning("audio", "집중 지원 상태를 읽지 못했습니다",
                new Dictionary<string, object?> { ["error"] = ex.Message });
            return false;
        }
    }
}
