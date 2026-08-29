using System.Windows;
using Puck.App;
using Puck.Diagnostics;
using Puck.Settings;

namespace Puck;

/// 앱 진입점. 이름이 App이 아닌 이유는 Puck.App이 이미 네임스페이스(App/ 폴더)이기
/// 때문이다 — puck-mac의 App/PuckApp.swift와 같은 이름을 쓴다.
public partial class PuckApp : Application
{
    private PetBootstrap? _bootstrap;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = SettingsStore.Load(PuckPaths.SettingsFile);

        // 첫 창이 뜨기 전에. Theme.xaml은 어두운 값을 적어 둔 것이라,
        // 밝은 테마를 골라 둔 사람은 이게 없으면 창이 한 번 어둡게 떴다가
        // 바뀌는 것을 본다.
        ClientWindow.ThemeResources.Apply(settings.ThemeStyle);

        // 앱을 다른 폴더로 옮겼으면 등록부의 명령줄이 낡았다. 켜 둔 사람의
        // 자동 시작이 조용히 멈춰 있지 않도록 여기서 맞춘다.
        Settings.LaunchAtLogin.Reconcile(settings.LaunchAtLogin);

        _bootstrap = new PetBootstrap(settings);
        _bootstrap.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrap?.Dispose();
        base.OnExit(e);
    }
}
