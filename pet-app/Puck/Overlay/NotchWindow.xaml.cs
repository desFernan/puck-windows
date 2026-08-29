using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Puck.Interop;
using Puck.NowPlaying;

namespace Puck.Overlay;

/// 화면 위 한가운데의 노치, 그리고 가리키면 열리는 패널.
///
/// mac에서 이 자리에 있는 것은 진짜 카메라 하우징이라 아무것도 그리지
/// 않아도 되지만(검은 플라스틱이 이미 거기 있다), Windows에는 하우징이
/// 없으므로 닫힌 모습부터 그린다. `ScreenNotch`가 같은 이야기를 한다 —
/// 그리지 않는 노치는 펫이 없는 것을 피해 돌게 만들 뿐이다.
///
/// 창은 언제나 열린 크기다. 호버마다 크기를 바꾸면 줄어드는 프레임에
/// 커서가 창 밖으로 나가고, 그것이 호버 패널이 깜빡이는 경로다. 바뀌는
/// 것은 안에 그려지는 것과, 창이 마우스를 받는지 여부뿐이다.
public partial class NotchWindow : Window
{
    /// 커서가 어디 있는지 보는 주기. 마우스 훅을 걸지 않는 이유는 이 창이
    /// 대부분의 시간 클릭 통과 상태라 자기 위의 움직임을 받지 못하기
    /// 때문이다 — 받으려고 통과를 끄면 노치가 그 아래 창을 가린다.
    private static readonly TimeSpan HoverTick = TimeSpan.FromMilliseconds(120);

    /// 열려 있는 동안 재생 정보를 다시 읽는 주기. 진행 막대가 움직여
    /// 보이기에 충분하고, 닫혀 있는 동안에는 아예 묻지 않는다.
    private static readonly TimeSpan PlaybackTick = TimeSpan.FromMilliseconds(500);

    private readonly NowPlayingStore _store;
    private readonly DispatcherTimer _timer;

    private IntPtr _handle;
    private Rect _notch;
    private bool _isOpen;
    private TimeSpan _sincePlaybackRead;

    public NotchWindow(NowPlayingStore store)
    {
        InitializeComponent();

        _store = store;
        _store.Changed += _ => Render();

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = HoverTick };
        _timer.Tick += (_, _) => Tick();

        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            // 작업 표시줄에도 Alt+Tab에도 없고, 눌러도 포커스를 가져가지
            // 않는다. 펫 오버레이와 같은 이유이고 같은 스타일이다.
            WindowStyles.MakeOverlay(_handle);
            WindowStyles.SetClickThrough(_handle, true);
        };
    }

    /// 이 노치가 어느 화면의 것인가. 화면 구성이 바뀌면 다시 불린다 —
    /// 노치는 하드웨어가 바뀔 때 바뀌는 것이고, 사라진 디스플레이의 노치는
    /// 그냥 없다.
    public void PlaceOn(Rect notch)
    {
        _notch = notch;
        var frame = NotchPanelGeometry.WindowFrame(notch);

        // 물리 픽셀 -> WPF의 논리 단위. 이 창은 화면 좌표로 계산되지만
        // Left/Top/Width/Height는 논리 단위다.
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        Left = frame.Left / scale;
        Top = frame.Top / scale;
        Width = frame.Width / scale;
        Height = frame.Height / scale;

        // 닫힌 껍데기는 노치 그 자체의 크기다. 창이 그보다 넓으므로
        // 가운데에 그 크기로 그린다.
        ClosedShell.Width = notch.Width / scale;
        ClosedShell.Height = notch.Height / scale;

        Show();
        _timer.Start();
        Render();
    }

    public void Retire()
    {
        _timer.Stop();
        Hide();
    }

    private void Tick()
    {
        if (_notch.IsEmpty) return;

        var cursor = CursorPosition();
        var open = NotchPanelGeometry.ShouldBeOpen(cursor, _notch, _isOpen);

        if (open != _isOpen)
        {
            _isOpen = open;
            ClosedShell.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            OpenShell.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            // 닫혀 있는 동안 재생 정보를 물어 둘 이유가 없다. 열리는 순간
            // 한 번 읽어 두어야 첫 프레임이 비어 보이지 않는다.
            if (open)
            {
                _store.Poll();
                _sincePlaybackRead = TimeSpan.Zero;
            }
            Render();
        }

        if (!_isOpen) return;

        _sincePlaybackRead += HoverTick;
        if (_sincePlaybackRead < PlaybackTick) return;
        _sincePlaybackRead = TimeSpan.Zero;
        _store.Poll();
        // 위치는 값이 같아도 흐르므로 Changed가 오르지 않을 수 있다.
        Render();
    }

    private void Render()
    {
        if (!_isOpen) return;

        if (_store.Current is not { } playing)
        {
            TrackTitle.Text = Localization.Strings.NotchNothingPlaying;
            TrackArtist.Text = string.Empty;
            SourceLabel.Text = string.Empty;
            StatusLabel.Text = string.Empty;
            ElapsedText.Text = string.Empty;
            RemainingText.Text = string.Empty;
            ProgressFill.Width = 0;
            return;
        }

        TrackTitle.Text = playing.Title;
        TrackArtist.Text = playing.Artist;
        SourceLabel.Text = playing.SourceName;
        StatusLabel.Text = playing.IsPlaying
            ? Localization.Strings.NotchPlaying
            : Localization.Strings.NotchPaused;

        // 길이를 모르는 것(생방송)은 시계도 막대도 그리지 않는다. 지어낸
        // 진행 막대보다 없는 편이 낫다.
        if (playing.Duration <= TimeSpan.Zero)
        {
            ElapsedText.Text = string.Empty;
            RemainingText.Text = string.Empty;
            ProgressFill.Width = 0;
            return;
        }

        ElapsedText.Text = TrackTime.Text(playing.Position);
        RemainingText.Text = TrackTime.Text(playing.Duration);
        ProgressFill.Width = Math.Max(0, ProgressTrack.ActualWidth * playing.Progress);
    }

    private static Point CursorPosition()
    {
        Win32.GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }
}
