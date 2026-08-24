using System.IO;
using Puck.Settings;

namespace PuckTests.Settings;

public class SettingsStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

    [Fact]
    public void MissingFileYieldsDefaults()
    {
        var store = SettingsStore.Load(TempFile());
        Assert.Null(store.AvatarName);
        Assert.Equal(1.0, store.MovementSpeedMultiplier);
        Assert.Equal("dark", store.ThemeStyle);
        Assert.False(store.LaunchAtLogin);
    }

    [Fact]
    public void CorruptFileYieldsDefaultsInsteadOfThrowing()
    {
        var path = TempFile();
        File.WriteAllText(path, "{ this is not json");
        var store = SettingsStore.Load(path);
        Assert.Equal(1.0, store.MovementSpeedMultiplier);
    }

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        var path = TempFile();
        var store = SettingsStore.Load(path);
        store.AvatarName = "my-pet";
        store.MovementSpeedMultiplier = 1.5;
        store.Save();

        var reloaded = SettingsStore.Load(path);
        Assert.Equal("my-pet", reloaded.AvatarName);
        Assert.Equal(1.5, reloaded.MovementSpeedMultiplier);
    }

    [Fact]
    public void UnknownKeysSurviveARoundTrip()
    {
        var path = TempFile();
        File.WriteAllText(path, """{"avatar_name":"a","future_key":42}""");
        var store = SettingsStore.Load(path);
        store.AvatarName = "b";
        store.Save();

        Assert.Contains("future_key", File.ReadAllText(path));
    }

    [Fact]
    public void ChangedFiresOnPropertySet()
    {
        var store = SettingsStore.Load(TempFile());
        var fired = 0;
        store.Changed += (_, _) => fired++;
        store.MovementSpeedMultiplier = 2.0;
        Assert.Equal(1, fired);
    }
}
