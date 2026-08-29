using System.IO;
using System.Windows.Automation;
using System.Windows.Controls;
using Puck.ClientWindow;
using Puck.Localization;

namespace PuckTests.ClientWindow;

/// 접근성 이름은 코드에 적혀 있는 것만으로는 아무 일도 하지 않는다 —
/// 실제로 그 컨트롤에 실려야 한다. 그래서 창을 만들어 트리에서 도로 읽는다.
///
/// WPF 창은 STA 스레드에서만 만들 수 있어서 이 파일에만 그 장치가 있다.
/// 창을 띄우지는 않는다: 이름은 `InitializeComponent` 다음에 붙으므로
/// 만들기만 해도 확인할 수 있고, 무인 테스트가 화면에 창을 띄우면 안 된다.
public class ChatWindowAccessibilityTests
{
    /// STA 스레드에서 `work`를 돌리고 예외는 호출한 쪽으로 넘긴다.
    private static void OnStaThread(Action work)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { work(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static string? NameOf(System.Windows.DependencyObject element)
        => AutomationProperties.GetName(element);

    [Fact]
    public void 대화_기록과_입력란과_승인_영역에_이름이_붙는다()
    {
        OnStaThread(() =>
        {
            var window = new ChatWindow();

            var lines = (ItemsControl)window.FindName("Lines")!;
            var input = (TextBox)window.FindName("Input")!;
            var approval = (Border)window.FindName("ApprovalPanel")!;

            Assert.Equal(Strings.A11yTranscript, NameOf(lines));
            Assert.Equal(Strings.A11yInput, NameOf(input));
            Assert.Equal(Strings.A11yApproval, NameOf(approval));
        });
    }

    /// 이름이 키 그대로 나오면 표에 넣는 것을 잊은 것이고, 스크린리더는
    /// "a11y.input"이라고 읽는다.
    [Fact]
    public void 붙은_이름이_키가_아니라_사람의_말이다()
    {
        foreach (var name in new[] { Strings.A11yTranscript, Strings.A11yInput, Strings.A11yApproval })
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.DoesNotContain("a11y.", name);
        }
    }
}

/// 설정 창. mac 커밋이 실제로 겨눈 곳이 여기다 — 모든 컨트롤이 빈 이름표로
/// 만들어져 있고 이름은 옆의 줄이 그리므로, 붙여 주지 않으면 창 전체가
/// 이름 없는 콤보 상자와 이름 없는 슬라이더로 읽힌다.
public class SettingsWindowAccessibilityTests
{
    private static void OnStaThread(Action work)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { work(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void 아바타와_속도와_테마_컨트롤에_이름이_붙는다()
    {
        OnStaThread(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"puck-a11y-{Guid.NewGuid():N}.json");
            var window = new Puck.Settings.SettingsWindow(
                Puck.Settings.SettingsStore.Load(path), () => { });

            var avatar = (ComboBox)window.FindName("AvatarPicker")!;
            var speed = (Slider)window.FindName("SpeedSlider")!;
            var theme = (ComboBox)window.FindName("ThemePicker")!;

            Assert.Equal(Strings.A11yAvatar, AutomationProperties.GetName(avatar));
            Assert.Equal(Strings.A11ySpeed, AutomationProperties.GetName(speed));
            Assert.Equal(Strings.A11yTheme, AutomationProperties.GetName(theme));
        });
    }

    /// 체크 상자는 Content가 곧 이름이라 따로 붙이지 않는다. 비어 있으면
    /// 그것도 이름 없는 컨트롤이므로 확인해 둔다.
    [Fact]
    public void 체크_상자는_자기_글이_이름_노릇을_한다()
    {
        OnStaThread(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"puck-a11y-{Guid.NewGuid():N}.json");
            var window = new Puck.Settings.SettingsWindow(
                Puck.Settings.SettingsStore.Load(path), () => { });

            foreach (var name in new[] { "AvoidFocused", "LaunchAtLoginBox" })
            {
                var box = (CheckBox)window.FindName(name)!;
                Assert.False(string.IsNullOrWhiteSpace(box.Content as string), $"{name}에 글이 없습니다");
            }
        });
    }
}
