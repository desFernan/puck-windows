using System.IO;
using Puck.Diagnostics;

namespace PuckTests.Diagnostics;

public class PuckPathsTests
{
    [Fact]
    public void RootLivesUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(localAppData, "Puck"), PuckPaths.Root);
    }

    [Fact]
    public void KnownSubpathsHangOffRoot()
    {
        Assert.Equal(Path.Combine(PuckPaths.Root, "Avatars"), PuckPaths.Avatars);
        Assert.Equal(Path.Combine(PuckPaths.Root, "Tank"), PuckPaths.Tank);
        Assert.Equal(Path.Combine(PuckPaths.Root, "logs"), PuckPaths.Logs);
        Assert.Equal(Path.Combine(PuckPaths.Root, "settings.json"), PuckPaths.SettingsFile);
        Assert.Equal(Path.Combine(PuckPaths.Root, ".env"), PuckPaths.EnvFile);
    }
}
