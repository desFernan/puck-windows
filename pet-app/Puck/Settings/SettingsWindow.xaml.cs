using System.ComponentModel;
using System.Windows;
using Puck.Avatar;
using Puck.ClientWindow;
using Puck.Diagnostics;
using Puck.Localization;

namespace Puck.Settings;

/// 한 번 정해 두고 잊는 설정들의 창.
///
/// mac에서 이것들은 메뉴막대에서 떨어지는 패널 안에 있었는데, 그 패널이
/// 여섯 구획과 스크롤을 갖게 되면서 드롭다운이 폼이 되어 있었다. 패널에
/// 있어야 하는 것은 펫이 **지금** 하고 있는 것 — 어떤 아바타인지, 소리를
/// 껐는지 — 이고, 소리 곡선과 걷는 속도와 시작 프로그램 등록은 한 번
/// 정하고 마는 것이라 창으로 나왔다.
///
/// Windows에는 그 패널에 해당하는 것이 트레이 메뉴다. 그래서 여기 있는
/// 것은 mac의 창과 같고, 트레이는 mac의 패널처럼 지금 상태만 들고 있다.
///
/// 닫아도 버리지 않는다. 패널과 달리 여기에는 창이 닫힌 동안 낡아 갈 살아
/// 있는 상태가 없다.
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly Action _onAvatarChanged;

    /// 값을 화면에 채워 넣는 동안 켜 둔다. 이게 없으면 채우는 것 자체가
    /// 변경 이벤트로 돌아와 방금 읽은 값을 도로 쓴다.
    private bool _loading;

    /// 트레이 → 종료가 세운다. 그때는 숨기지 않고 정말로 닫는다.
    private bool _closingForGood;

    public SettingsWindow(SettingsStore store, Action onAvatarChanged)
    {
        InitializeComponent();

        _store = store;
        _onAvatarChanged = onAvatarChanged;

        Title = Strings.SettingsTitle;
        LaunchCaption.Text = Strings.SettingsLaunchCaption;

        Load();

        AvatarPicker.SelectionChanged += (_, _) => Commit(() =>
        {
            if (AvatarPicker.SelectedItem is not string name) return;
            _store.AvatarName = name;
            _onAvatarChanged();
        });

        SpeedSlider.ValueChanged += (_, _) => Commit(() =>
        {
            _store.MovementSpeedMultiplier = SpeedSlider.Value;
            SpeedValue.Text = $"{SpeedSlider.Value:0.00}×";
        });

        AvoidFocused.Click += (_, _) => Commit(() =>
            _store.AvoidFocusedWindow = AvoidFocused.IsChecked == true);

        ThemePicker.SelectionChanged += (_, _) => Commit(() =>
        {
            if (ThemePicker.SelectedIndex < 0) return;
            var style = ThemePicker.SelectedIndex == 0 ? "dark" : "light";
            _store.ThemeStyle = style;
            ThemeResources.Apply(style);
        });

        LaunchAtLoginBox.Click += (_, _) => Commit(() =>
        {
            var enabled = LaunchAtLoginBox.IsChecked == true;
            _store.LaunchAtLogin = enabled;
            LaunchAtLogin.Apply(enabled);
            // 등록부가 정본이다. 쓰기가 실패했으면 체크가 도로 풀려야
            // 하지, 켜졌다고 거짓말하면 안 된다.
            LaunchAtLoginBox.IsChecked = LaunchAtLogin.IsEnabled;
        });
    }

    /// 아바타 폴더가 바뀌었을 수 있으므로 열 때마다 다시 읽는다. 그 밖의
    /// 값은 이 창만이 바꾸므로 다시 읽어도 같은 값이지만, 한 곳에서
    /// 채우는 편이 두 경로를 두는 것보다 틀릴 자리가 적다.
    public void ShowAndActivate()
    {
        Load();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void CloseForGood()
    {
        _closingForGood = true;
        Close();
    }

    /// X는 닫는 것이 아니라 숨기는 것이다. 트레이에 사는 앱에서 설정 창을
    /// 진짜로 닫아 버리면 다시 열 때 상태를 처음부터 만든다.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_closingForGood)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void Load()
    {
        _loading = true;
        try
        {
            var avatars = AvatarCatalogue.Scan(PuckPaths.Avatars).Select(a => a.Name).ToList();
            AvatarPicker.ItemsSource = avatars;
            AvatarPicker.SelectedItem = avatars.FirstOrDefault(a => a == _store.AvatarName)
                                        ?? avatars.FirstOrDefault();
            AvatarPicker.IsEnabled = avatars.Count > 0;
            AvatarCaption.Text = avatars.Count > 0
                ? Strings.SettingsAvatarCaption
                : Strings.AvatarNoneInstalled;

            SpeedSlider.Value = _store.MovementSpeedMultiplier;
            SpeedValue.Text = $"{SpeedSlider.Value:0.00}×";
            AvoidFocused.IsChecked = _store.AvoidFocusedWindow;

            ThemePicker.ItemsSource = new[] { Strings.SettingsThemeDark, Strings.SettingsThemeLight };
            ThemePicker.SelectedIndex =
                string.Equals(_store.ThemeStyle, "light", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            // 설정 파일이 아니라 등록부가 정본이다 — 사람이 작업 관리자에서
            // 꺼 두었을 수 있고, 그러면 설정 파일 쪽이 틀린 것이다.
            LaunchAtLoginBox.IsChecked = LaunchAtLogin.IsEnabled;
        }
        finally
        {
            _loading = false;
        }
    }

    /// 바꾸고 곧바로 저장한다. 창에 확인 단추가 없으므로 저장의 순간은
    /// 바꾸는 순간뿐이고, 트레이에 사는 앱은 사람이 저장을 누르러 돌아올
    /// 것을 기대할 수 없다.
    private void Commit(Action change)
    {
        if (_loading) return;
        change();
        _store.Save();
    }
}
