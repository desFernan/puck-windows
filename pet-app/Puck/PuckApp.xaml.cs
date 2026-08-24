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
        _bootstrap = new PetBootstrap(settings);
        _bootstrap.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrap?.Dispose();
        base.OnExit(e);
    }
}
