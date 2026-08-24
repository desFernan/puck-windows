using System.Windows;
using System.Windows.Interop;
using Puck.Interop;

namespace Puck.Overlay;

/// 펫이 그려지는 창. 화면 전체가 아니라 펫만 하고, SetWindowPos로
/// 펫을 따라다닌다.
public partial class PetOverlayWindow : Window
{
    private IntPtr _handle;
    private bool _clickThrough = true;

    public PetOverlayWindow()
    {
        InitializeComponent();
        Root.Children.Add(Sprite);
        SourceInitialized += OnSourceInitialized;
        DpiScaleChanged += scale =>
        {
            Sprite.DpiScale = scale;
            Sprite.Invalidate();
        };
    }

    /// 이 창이 그리는 유일한 것.
    public SpriteView Sprite { get; } = new();

    /// 지금 이 창이 올라가 있는 모니터의 배율 (1.0 = 96 DPI).
    public double DpiScale { get; private set; } = 1.0;

    public bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            if (_clickThrough == value) return;
            _clickThrough = value;
            if (_handle != IntPtr.Zero) WindowStyles.SetClickThrough(_handle, value);
        }
    }

    /// 이 창의 좌상단이 가상 화면 어디에 있는가. SpriteView가 펫의
    /// 절대 좌표를 창 안의 상대 좌표로 바꿀 때 쓴다.
    public Point OriginInVirtualScreen { get; private set; }

    public event Action<double>? DpiScaleChanged;

    /// 창을 펫에 맞춰 옮긴다. 좌표는 가상 화면 물리 픽셀이므로
    /// WPF의 Left/Top(DIP)이 아니라 SetWindowPos를 쓴다.
    public void MoveTo(Point petPosition, Rect visualBounds)
    {
        if (_handle == IntPtr.Zero) return;

        var frame = OverlayPositioner.FrameFor(petPosition, visualBounds);
        Win32.SetWindowPos(_handle, Win32.HWND_TOPMOST,
            frame.X, frame.Y, frame.Width, frame.Height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);

        // 창 안의 그리기는 이 프레임의 좌상단을 원점으로 한다.
        OriginInVirtualScreen = new Point(frame.X, frame.Y);
        Sprite.OriginInVirtualScreen = OriginInVirtualScreen;
        Sprite.DpiScale = DpiScale;
        Sprite.Width = frame.Width / DpiScale;
        Sprite.Height = frame.Height / DpiScale;
        Sprite.Invalidate();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        _handle = source.Handle;

        WindowStyles.MakeOverlay(_handle);
        WindowStyles.SetClickThrough(_handle, _clickThrough);

        DpiScale = Win32.GetDpiForWindow(_handle) / 96.0;
        source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != Win32.WM_DPICHANGED) return IntPtr.Zero;

        // wParam의 하위 워드가 새 DPI. 창이 다른 배율의 모니터로 넘어갔다.
        DpiScale = (wParam.ToInt32() & 0xFFFF) / 96.0;
        DpiScaleChanged?.Invoke(DpiScale);

        // 크기/위치는 우리가 SetWindowPos로 직접 정하므로, Windows가
        // 제안하는 사각형(lParam)은 쓰지 않고 처리했다고만 알린다.
        handled = true;
        return IntPtr.Zero;
    }
}
