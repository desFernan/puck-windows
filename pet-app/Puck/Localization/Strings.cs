namespace Puck.Localization;

/// UI 문자열. 하드코딩된 사용자 노출 문자열은 없다 — 전부 여기를 거친다.
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
        ["tray.settings"] = "설정…",
        ["tray.mute"] = "소리 끄기",
        ["settings.title"] = "Puck 설정",
        ["settings.avatarCaption"] = "커스터마이징 폴더의 Avatars에 든 것들입니다.",
        ["settings.launchCaption"] = "이 사용자 계정에만 등록합니다.",
        ["settings.themeDark"] = "어둡게",
        ["settings.themeLight"] = "밝게",
        ["a11y.transcript"] = "대화 기록",
        ["a11y.input"] = "펫에게 할 말",
        ["a11y.approval"] = "실행 허락",
        ["a11y.avatar"] = "아바타",
        ["a11y.speed"] = "이동 속도",
        ["a11y.theme"] = "테마",
        ["a11y.bubble"] = "펫에게 할 말",
        ["a11y.kind.pet"] = "펫",
        ["a11y.kind.user"] = "나",
        ["a11y.kind.tool"] = "진행",
        ["a11y.kind.error"] = "오류",
        ["a11y.kind.notice"] = "안내",
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
    public static string TraySettings => Get("tray.settings");
    public static string TrayMute => Get("tray.mute");
    public static string SettingsTitle => Get("settings.title");
    public static string SettingsAvatarCaption => Get("settings.avatarCaption");
    public static string SettingsLaunchCaption => Get("settings.launchCaption");
    public static string SettingsThemeDark => Get("settings.themeDark");
    public static string SettingsThemeLight => Get("settings.themeLight");
    public static string A11yTranscript => Get("a11y.transcript");
    public static string A11yInput => Get("a11y.input");
    public static string A11yApproval => Get("a11y.approval");
    public static string A11yAvatar => Get("a11y.avatar");
    public static string A11ySpeed => Get("a11y.speed");
    public static string A11yTheme => Get("a11y.theme");
    public static string A11yBubble => Get("a11y.bubble");

    /// 줄 하나가 누구 것인지. 화면에서는 색이 말해 주지만 색은 읽히지 않는다.
    public static string KindOf(string kind) => Get("a11y.kind." + kind);

    /// `{0}`은 도구 이름.
    public static string ChatApprovalQuestion => Get("chat.approvalQuestion");
    public static string ChatFailed => Get("chat.failed");
    public static string ChatUsingTool => Get("chat.usingTool");
    public static string ChatToolFailed => Get("chat.toolFailed");
    public static string ChatToolRefused => Get("chat.toolRefused");
}
