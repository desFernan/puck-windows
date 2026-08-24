using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Puck.Interop;
using Puck.Localization;

namespace Puck.Input;

/// 펫 옆에 뜨는 입력 버블. 핫키로 열어 한 줄 적어 넘긴다.
///
/// 오버레이와 달리 **입력을 받아야 하므로** 클릭스루를 켜지 않는다. 다만
/// 도구 창으로 두어 Alt+Tab과 작업표시줄에는 나타나지 않게 한다 — 잠깐 떴다
/// 사라지는 것이 창 목록에 끼면 성가시다.
public partial class TextInputBubbleWindow : Window
{
    private IntPtr _handle;

    public TextInputBubbleWindow()
    {
        InitializeComponent();
        Prompt.Text = Strings.BubblePrompt;
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            var style = Win32.GetWindowLong(_handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(_handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_TOOLWINDOW);
        };
    }

    /// 사람이 다 적고 Enter를 눌렀다.
    public event Action<string>? Submitted;

    /// 사람이 그만뒀다 (Esc, 또는 다른 창을 눌러 포커스를 잃음).
    public event Action? Cancelled;

    /// 펫 옆에 띄운다. 좌표는 가상 화면 물리 픽셀.
    public void ShowAt(Point origin)
    {
        // 크기를 재기 전에는 어디 놓을지 알 수 없다. 일단 화면 밖에 띄우고
        // 크기가 정해진 뒤에 옮긴다 — 그래야 잘못된 자리에서 한 프레임
        // 깜빡이지 않는다.
        Left = -10000;
        Top = -10000;
        Show();
        UpdateLayout();

        MoveTo(origin);
        Activate();
        Input.Clear();
        Input.Focus();
        Keyboard.Focus(Input);
    }

    /// 이미 떠 있는 버블을 옮긴다. 펫이 걸으면 따라와야 한다.
    public void MoveTo(Point origin)
    {
        if (_handle == IntPtr.Zero) return;
        Win32.SetWindowPos(_handle, Win32.HWND_TOPMOST,
            (int)Math.Round(origin.X), (int)Math.Round(origin.Y), 0, 0,
            Win32.SWP_NOACTIVATE | Win32.SWP_NOSIZE);
    }

    /// 지금 재어 둔 버블 크기(물리 픽셀). 배치 계산에 넘긴다.
    public Size MeasuredSize
    {
        get
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return new Size(ActualWidth * dpi.DpiScaleX, ActualHeight * dpi.DpiScaleY);
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                var text = Input.Text.Trim();
                Hide();
                if (text.Length > 0) Submitted?.Invoke(text);
                else Cancelled?.Invoke();
                break;

            case Key.Escape:
                e.Handled = true;
                Hide();
                Cancelled?.Invoke();
                break;
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // 다른 창을 누른 것은 그만두겠다는 뜻이다. 떠 있는 채로 남으면
        // 펫 옆에 붙은 빈 상자가 계속 따라다닌다.
        if (!IsVisible) return;
        Hide();
        Cancelled?.Invoke();
    }
}
