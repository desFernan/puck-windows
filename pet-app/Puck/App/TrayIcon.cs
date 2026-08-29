using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Puck.Localization;

namespace Puck.App;

/// mac의 메뉴막대 항목에 해당하는 것. 창이 하나도 없어도 앱이
/// 살아 있다는 걸 보여 주는 유일한 표시이기도 하다.
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    /// 음소거는 창이 아니라 여기 있다. mac에서 이것은 메뉴막대 패널의
    /// 몫인데, 지금 소리가 나는지는 **살아 있는 상태**라 한 번 정하고 마는
    /// 설정들과 성질이 다르다. Windows에서 그 패널에 해당하는 것이 이 메뉴다.
    public TrayIcon(Action onOpenChat, Action onToggleVisible, Action onOpenCustomisationFolder,
                    Action onReloadAvatar, Action onOpenSettings,
                    bool initiallyMuted, Action<bool> onMutedChanged, Action onQuit)
    {
        var menu = new ContextMenuStrip();
        // 맨 위. 펫에게 말을 거는 것이 이 앱이 하는 일이다.
        menu.Items.Add(Strings.TrayOpenChat, null, (_, _) => onOpenChat());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TrayToggleVisible, null, (_, _) => onToggleVisible());
        menu.Items.Add(Strings.TrayOpenCustomisationFolder, null, (_, _) => onOpenCustomisationFolder());
        menu.Items.Add(Strings.TrayReloadAvatar, null, (_, _) => onReloadAvatar());

        var mute = new ToolStripMenuItem(Strings.TrayMute) { CheckOnClick = true, Checked = initiallyMuted };
        mute.CheckedChanged += (_, _) => onMutedChanged(mute.Checked);
        menu.Items.Add(mute);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TraySettings, null, (_, _) => onOpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TrayQuit, null, (_, _) => onQuit());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Puck",
            Visible = true,
            ContextMenuStrip = menu,
        };
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "puck.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
