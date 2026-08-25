namespace Puck.Localization;

/// UI 문자열. 하드코딩된 사용자 노출 문자열은 없다 — 전부 여기를 거친다.
/// Phase 1이 쓰는 것만 있고, 이후 Phase가 자기 몫을 더한다.
public static class Strings
{
    private static readonly Dictionary<string, string> Table = new(StringComparer.Ordinal)
    {
        ["tray.toggleVisible"] = "펫 보이기/숨기기",
        ["tray.openCustomisationFolder"] = "커스터마이징 폴더 열기",
        ["tray.reloadAvatar"] = "아바타 다시 불러오기",
        ["tray.quit"] = "종료",
        ["avatar.loadFailed"] = "아바타를 불러오지 못했습니다",
        ["avatar.noneInstalled"] = "설치된 아바타가 없습니다",
        ["bubble.prompt"] = "무엇을 시킬까요? (Enter로 보내기, Esc로 닫기)",
        ["tray.openChat"] = "대화 열기",
        ["chat.title"] = "Puck",
        ["chat.prompt"] = "무엇이든 시켜 보세요. Enter로 보냅니다.",
        ["chat.allow"] = "허용",
        ["chat.deny"] = "거절",
        ["chat.approvalQuestion"] = "{0}을(를) 실행할까요?",
        ["chat.failed"] = "대화가 실패했습니다",
        ["chat.usingTool"] = "· {0}",
        ["chat.toolFailed"] = "· {0} — 실패",
        ["chat.toolRefused"] = "· {0} — 거절함",
    };

    /// 없는 키는 키 자체를 돌려준다 — 문자열 하나가 빠졌다고 UI가
    /// 죽지 않고, 무엇이 빠졌는지도 화면에 보인다.
    public static string Get(string key) => Table.TryGetValue(key, out var value) ? value : key;

    public static string TrayToggleVisible => Get("tray.toggleVisible");
    public static string TrayOpenCustomisationFolder => Get("tray.openCustomisationFolder");
    public static string TrayReloadAvatar => Get("tray.reloadAvatar");
    public static string TrayQuit => Get("tray.quit");
    public static string AvatarLoadFailed => Get("avatar.loadFailed");
    public static string AvatarNoneInstalled => Get("avatar.noneInstalled");
    public static string BubblePrompt => Get("bubble.prompt");
    public static string TrayOpenChat => Get("tray.openChat");
    public static string ChatTitle => Get("chat.title");
    public static string ChatPrompt => Get("chat.prompt");
    public static string ChatAllow => Get("chat.allow");
    public static string ChatDeny => Get("chat.deny");

    /// `{0}`은 도구 이름.
    public static string ChatApprovalQuestion => Get("chat.approvalQuestion");
    public static string ChatFailed => Get("chat.failed");
    public static string ChatUsingTool => Get("chat.usingTool");
    public static string ChatToolFailed => Get("chat.toolFailed");
    public static string ChatToolRefused => Get("chat.toolRefused");
}
