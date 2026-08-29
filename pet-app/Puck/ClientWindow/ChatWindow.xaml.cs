using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using Puck.ClientWindow.Island;
using Puck.Localization;

namespace Puck.ClientWindow;

/// 펫과 글로 말하는 창. puck-linux의 `puck-client`를 옮긴 것이다.
///
/// 저쪽은 창이 **별도 프로세스**라 소켓으로 펫과 이야기하지만, 여기는 한
/// 프로세스이므로 창이 `AgentRunner`를 직접 부른다 — 옮길 것은 소켓이 아니라
/// 화면과 승인 흐름이다.
public partial class ChatWindow : Window
{
    private readonly Transcript _transcript = new();

    /// 트레이 → 종료가 세운다. 그때는 숨기지 않고 정말로 닫는다.
    private bool _closingForGood;

    /// 답을 기다리는 승인. 도구는 한 번에 하나씩 돌므로 언제나 하나뿐이다.
    private TaskCompletionSource<bool>? _pendingApproval;
    private CancellationTokenRegistration _approvalCancellation;

    public ChatWindow()
    {
        InitializeComponent();

        Title = Strings.ChatTitle;
        AllowButton.Content = Strings.ChatAllow;
        DenyButton.Content = Strings.ChatDeny;
        Lines.ItemsSource = _transcript.Entries;

        // 화면에서는 줄 옆의 이름표가, 여기서는 이것이 무엇이 무엇인지
        // 말한다. 컨트롤 자체에 붙지 않은 이름은 스크린리더에 닿지 않아서,
        // 창 전체가 이름 없는 목록과 이름 없는 입력란으로 읽힌다.
        AutomationProperties.SetName(Lines, Strings.A11yTranscript);
        AutomationProperties.SetName(Input, Strings.A11yInput);
        AutomationProperties.SetName(ApprovalPanel, Strings.A11yApproval);

        UpdateFoldButton();

        // 창이 움직이거나 크기가 바뀌면 섬도 함께 움직인다. 펫이 날아가는
        // 중이면 그 목적지가 따라 움직여야 한다.
        LocationChanged += (_, _) => ReportIslandFrame();
        SizeChanged += (_, _) => ReportIslandFrame();
        IsVisibleChanged += (_, _) => ReportIslandFrame();
        Island.LayoutUpdated += (_, _) => ReportIslandFrame();

        Append(TranscriptKind.Notice, Strings.ChatPrompt);
    }

    /// 섬을 접었다 폈다. 접힌 것은 띠 하나이고, 그 색은 펼친 섬을 채우는
    /// 그림에서 읽어 온다 — 1초 차이의 같은 곳이어야 하므로.
    private void OnFoldClicked(object sender, RoutedEventArgs e)
    {
        Island.IsFolded = !Island.IsFolded;
        UpdateFoldButton();
        ReportIslandFrame();
    }

    private void UpdateFoldButton()
    {
        FoldButton.Content = Island.IsFolded ? Strings.IslandUnfold : Strings.IslandFold;
        AutomationProperties.SetName(FoldButton, (string)FoldButton.Content);
        AutomationProperties.SetName(Island, Strings.A11yIsland);
    }

    /// 사람이 한 줄 적어 보냈다.
    public event Action<string>? Submitted;

    /// 섬의 바닥이 화면 어디에 있는가 — 가상 화면 물리 픽셀. 없으면 갈 곳이
    /// 없다는 뜻이다(창이 숨겨졌거나, 아직 재어지지 않았거나).
    ///
    /// **외곽선이 아니라 바닥에서** 잰다. 펫의 세계는 그것이 설 수 있는
    /// 부분이고, 단추가 떠 있는 위쪽은 모양이지 방이 아니다.
    public event Action<Rect?>? IslandFrameChanged;

    /// 창이 움직이거나 크기가 바뀌거나 접혔을 때마다 다시 알린다.
    private void ReportIslandFrame()
    {
        IslandFrameChanged?.Invoke(IslandFrame());
    }

    private Rect? IslandFrame()
    {
        if (!IsVisible || Island.ActualWidth <= 0 || Island.ActualHeight <= 0) return null;

        try
        {
            // 화면 좌표로. 펫이 사는 좌표계가 가상 화면 물리 픽셀이므로
            // DPI 배율을 다시 곱해 준다 — WPF가 주는 것은 장치 독립 점이다.
            var topLeft = Island.PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(this);

            return new Rect(
                topLeft.X, topLeft.Y,
                Island.ActualWidth * dpi.DpiScaleX,
                Island.ActualHeight * dpi.DpiScaleY);
        }
        catch (InvalidOperationException)
        {
            // 아직 창이 만들어지지 않았다. 다음 배치에서 다시 묻는다.
            return null;
        }
    }

    /// 어느 스레드에서 불러도 된다 — 에이전트 루프는 스레드 풀을 오간다.
    public void Append(TranscriptKind kind, string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => Append(kind, text));
            return;
        }

        _transcript.Add(kind, text);

        // 펫이 한 말은 소리 내어 전한다. 이 창은 포커스를 가져가지 않고
        // 닫혀 있을 수도 있어서, 목록에 줄이 하나 붙는 것만으로는
        // 스크린리더에게 읽을 이유가 생기지 않는다. 읽던 것을 끊지는
        // 않는다 — 지나가는 말이다.
        if (kind == TranscriptKind.Pet) App.ScreenReaderAnnouncer.Announce(this, text);
        // 새 줄은 언제나 보여야 한다. 사람이 위로 올려 읽고 있어도 마찬가지다 —
        // 답이 왔는데 화면이 그대로면 펫이 무시한 것으로 읽힌다.
        Scroller.ScrollToEnd();
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Input.Focus();
    }

    /// 이 도구를 실행해도 되는지 사람에게 묻고 기다린다. **UI 스레드에서만**
    /// 부른다 — 부르는 쪽은 `ChatApprovalPrompt`가 맞춰 준다.
    ///
    /// 창을 띄우고 활성화하는 이유는, 묻는 것이 보이지 않으면 사람은 펫이
    /// 멈춘 줄 알기 때문이다.
    public Task<bool> RequestApprovalAsync(string toolName, string arguments, CancellationToken cancellation)
    {
        // 앞의 물음이 아직 남아 있으면 그것은 거절로 닫는다. 답을 못 받은
        // 물음을 화면에 겹쳐 두면 어느 것에 답하는지 알 수 없다.
        Resolve(false);

        ShowAndActivate();

        ApprovalQuestion.Text = string.Format(Strings.ChatApprovalQuestion, toolName);
        ApprovalArguments.Text = arguments;
        ApprovalPanel.Visibility = Visibility.Visible;

        // 물음은 사람이 아직 입력란에 커서를 둔 채로 도착한다. 포커스가
        // 움직이지 않으므로 스크린리더에게는 읽을 이유가 없고, 그러면
        // 실행은 아무 설명 없이 멈춘 뒤 아무도 묻는 줄 모르는 답을 기다린다.
        App.ScreenReaderAnnouncer.Announce(this, ApprovalQuestion.Text, interrupting: true);

        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingApproval = pending;

        // 턴이 취소되면 물음도 사라져야 한다. 남겨 두면 이미 끝난 일에
        // 사람이 "허용"을 누르게 된다.
        _approvalCancellation = cancellation.Register(
            () => Dispatcher.InvokeAsync(() => Resolve(false)));

        return pending.Task;
    }

    private void OnAllowClicked(object sender, RoutedEventArgs e) => Resolve(true);

    private void OnDenyClicked(object sender, RoutedEventArgs e) => Resolve(false);

    private void Resolve(bool allowed)
    {
        var pending = _pendingApproval;
        _pendingApproval = null;

        _approvalCancellation.Dispose();
        _approvalCancellation = default;

        ApprovalPanel.Visibility = Visibility.Collapsed;
        pending?.TrySetResult(allowed);
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        var text = Input.Text.Trim();
        if (text.Length == 0) return;

        Input.Clear();
        Submitted?.Invoke(text);
    }

    /// 앱이 끝날 때. 이때만 창이 진짜로 닫힌다.
    public void CloseForGood()
    {
        // 두 번 부를 수 있는 자리다(트레이 종료 → OnExit → Dispose).
        if (_closingForGood) return;

        _closingForGood = true;
        Close();
    }

    /// 창을 닫는 것은 대화를 끝내는 것이지 앱을 끄는 것이 아니다 — 트레이
    /// 앱이므로 펫은 계속 산다. 다시 열면 지난 대화가 그대로 있다.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        // 물어 둔 것이 있으면 거절로 닫는다. 아무도 못 보는 물음을 답을
        // 기다린 채로 두면 턴이 영영 끝나지 않는다.
        Resolve(false);

        if (_closingForGood) return;

        e.Cancel = true;
        Hide();
    }
}
