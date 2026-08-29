using System.Windows;
using Puck.Movement;

namespace Puck.App;

/// 움직임을 줄여 달라는 시스템 설정.
///
/// macOS의 "동작 줄이기"에 해당하는 것은 Windows에서 설정 →
/// 접근성 → 시각 효과의 "애니메이션 효과"다. WPF는 그것을
/// `SystemParameters.ClientAreaAnimation`으로 노출한다.
///
/// 이 앱이야말로 그것을 지켜야 한다. 화면 위를 스스로 돌아다니는 것이
/// 이 앱이 하는 일 전부이기 때문이다.
public static class ReducedMotion
{
    /// 지금 켜져 있는가. 물어볼 수 없으면 꺼진 것으로 본다 — 켜져 있다고
    /// 잘못 보면 아무도 요청하지 않은 채로 펫이 가만히 있게 된다.
    public static bool IsOn
    {
        get
        {
            try
            {
                return !SystemParameters.ClientAreaAnimation;
            }
            catch
            {
                return false;
            }
        }
    }

    /// 이번 배회를 무엇으로 할 것인가.
    ///
    /// 배회는 이 앱에서 아무도 요청하지 않은 유일한 움직임이다 — 타이머로
    /// 시작해서, 사람이 실제로 읽고 있는 것 옆에서 일어난다. 설정이 켜져
    /// 있으면 펫이 어디에 있든 무엇을 뽑았든 "가만히"로 나온다.
    ///
    /// 사람이나 에이전트가 요청한 것은 그대로 움직인다 — 드래그, 던지기,
    /// 도구, 불러서 오는 것. 그것들은 이 함수를 지나가지 않는다.
    public static WanderOutcome Apply(WanderOutcome outcome, bool reduceMotion)
        => reduceMotion ? WanderOutcome.Stay : outcome;
}
