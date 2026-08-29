using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace Puck.App;

/// 화면에서만 일어나던 두 가지를 소리 내어 말한다.
///
/// 펫은 자기 머리 위 말풍선에서 이야기하고, 허락이 필요한 실행은 대화창에
/// 물음을 띄운다. 둘 다 포커스를 가져가지 않는다 — 오버레이는 설계상
/// 클릭 통과이고, 물음은 사람이 아직 입력란에 커서를 둔 채로 도착한다.
/// 그래서 스크린리더에게는 읽을 이유가 없다. 스크린리더 뒤에서 보면 펫은
/// 말이 없고, 실행은 아무 설명 없이 멈춘 뒤 아무도 묻는 줄 모르는 답을
/// 기다린다.
///
/// UI Automation의 알림(notification)이 정확히 그 모양이다: 포커스를
/// 옮기지도, 활성 창을 빼앗지도 않고 사람이 이미 있는 자리에서 한 번
/// 읽히는 글.
///
/// mac은 `NSAccessibility.post(.announcementRequested)`를 쓴다. 여기서
/// 대응하는 것이 `AutomationPeer.RaiseNotificationEvent`이고, 이쪽은 창
/// 하나에 딸린 peer를 통해 올려야 해서 말할 창을 받는다.
public static class ScreenReaderAnnouncer
{
    /// 듣는 것이 있으면 `text`를 말한다.
    ///
    /// 대부분의 시간에는 아무도 듣지 않고, 그래도 괜찮다: 보조 기술이
    /// 붙어 있지 않으면 알림은 아무에게도 전달되지 않고 값 몇 개를
    /// 만드는 값만 든다. "내레이터가 켜져 있는가"를 묻지 않는 것은
    /// 의도적이다 — 그 질문은 내레이터 하나에만 답하고, 다른 스크린리더나
    /// 점자 단말을 쓰는 사람에게는 아니라고 답한다.
    ///
    /// <param name="from">알림을 올릴 창. 살아 있고 화면에 있어야 한다.</param>
    /// <param name="interrupting">읽고 있던 것을 끊을 것인가. 허락을 묻는
    /// 물음에는 맞다(답할 때까지 실행이 멈춰 있다). 지나가는 말에는 틀리다.</param>
    public static void Announce(UIElement? from, string text, bool interrupting = false)
    {
        var trimmed = text?.Trim();
        if (from is null || string.IsNullOrEmpty(trimmed)) return;

        var peer = UIElementAutomationPeer.FromElement(from)
                   ?? UIElementAutomationPeer.CreatePeerForElement(from);
        if (peer is null) return;

        peer.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            interrupting
                ? AutomationNotificationProcessing.ImportantAll
                : AutomationNotificationProcessing.All,
            trimmed,
            // 같은 알림을 묶어 내는 데 쓰는 식별자. 지나가는 말과 허락을
            // 나눠 두면, 답을 기다리는 물음이 그 사이에 온 잡담에 밀려
            // 사라지지 않는다.
            interrupting ? "puck.approval" : "puck.chatter");
    }
}
