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

    public TrayIcon(Action onToggleVisible, Action onOpenCustomisationFolder,
                    Action onReloadAvatar, Action onQuit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.TrayToggleVisible, null, (_, _) => onToggleVisible());
        menu.Items.Add(Strings.TrayOpenCustomisationFolder, null, (_, _) => onOpenCustomisationFolder());
        menu.Items.Add(Strings.TrayReloadAvatar, null, (_, _) => onReloadAvatar());
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
