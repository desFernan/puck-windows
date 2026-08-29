using System.IO;
using System.Windows;
using System.Windows.Media;
using Puck.ClientWindow;
using Puck.Settings;

namespace PuckTests.Settings;

/// 설정이 둘로 갈린 것 — 한 번 정하고 마는 것은 창, 지금 상태는 트레이 —
/// 과, 그 창이 실제로 무언가를 하게 만드는 두 조각(테마, 자동 시작).
public class SettingsSplitTests
{
    private static SettingsStore Fresh()
    {
        var path = Path.Combine(Path.GetTempPath(), $"puck-settings-{Guid.NewGuid():N}.json");
        return SettingsStore.Load(path);
    }

    // --- 새 설정들 ---

    /// 노치는 요청하지 않은 사람의 화면에 없던 물건을 만들면 안 된다.
    [Fact]
    public void 노치는_기본이_꺼짐이다()
    {
        Assert.False(Fresh().NotchEnabled);
    }

    /// 사람이 끈 소리와 집중 지원이 끈 소리는 다른 것이라 따로 저장된다.
    [Fact]
    public void 음소거는_저장되고_다시_읽힌다()
    {
        var path = Path.Combine(Path.GetTempPath(), $"puck-settings-{Guid.NewGuid():N}.json");
        var store = SettingsStore.Load(path);

        Assert.False(store.Muted);
        store.Muted = true;
        store.Save();

        Assert.True(SettingsStore.Load(path).Muted);
    }

    [Fact]
    public void 노치_설정도_저장되고_다시_읽힌다()
    {
        var path = Path.Combine(Path.GetTempPath(), $"puck-settings-{Guid.NewGuid():N}.json");
        var store = SettingsStore.Load(path);

        store.NotchEnabled = true;
        store.Save();

        Assert.True(SettingsStore.Load(path).NotchEnabled);
    }

    // --- 테마 ---

    /// 설정에 값이 있는데 아무 일도 일어나지 않으면 그 설정은 거짓말이다.
    /// 이 앱은 theme_style을 저장만 하고 어디에서도 읽지 않았다.
    [Fact]
    public void 밝은_테마는_밝은_팔레트다()
    {
        Assert.Equal(ClientPalette.Light, ThemeResources.PaletteFor("light"));
        Assert.Equal(ClientPalette.Light, ThemeResources.PaletteFor("Light"));
    }

    [Fact]
    public void 모르는_값은_어두운_쪽이다()
    {
        Assert.Equal(ClientPalette.Dark, ThemeResources.PaletteFor("dark"));
        Assert.Equal(ClientPalette.Dark, ThemeResources.PaletteFor("자몽"));
        Assert.Equal(ClientPalette.Dark, ThemeResources.PaletteFor(""));
    }

    /// 창들이 DynamicResource로 묶여 있으므로, 키를 덮어쓰면 열려 있는
    /// 창도 그 자리에서 바뀐다. 키 이름이 Theme.xaml과 같아야 한다는 것이
    /// 이 조각의 유일한 규칙이라, 그것을 못 박아 둔다.
    [Fact]
    public void 팔레트를_리소스_사전에_밀어_넣는다()
    {
        var resources = new ResourceDictionary();

        ThemeResources.Apply(resources, ClientPalette.Light);

        Assert.Equal(ClientPalette.Light.Background, ((SolidColorBrush)resources["Background"]).Color);
        Assert.Equal(ClientPalette.Light.TextPrimary, ((SolidColorBrush)resources["TextPrimary"]).Color);
        Assert.Equal(ClientPalette.Light.Accent, ((SolidColorBrush)resources["Accent"]).Color);
    }

    /// Theme.xaml에서 온 브러시는 얼어 있어 Color를 바꿀 수 없다. 새로
    /// 만들어 덮는지 확인한다 — 안 그러면 실행할 때만 터진다.
    [Fact]
    public void 얼어_있는_브러시_위에도_덮을_수_있다()
    {
        var frozen = new SolidColorBrush(Colors.Red);
        frozen.Freeze();
        var resources = new ResourceDictionary { ["Background"] = frozen };

        ThemeResources.Apply(resources, ClientPalette.Light);

        Assert.Equal(ClientPalette.Light.Background, ((SolidColorBrush)resources["Background"]).Color);
    }

    [Fact]
    public void 덮어쓴_브러시도_얼려_둔다()
    {
        var resources = new ResourceDictionary();
        ThemeResources.Apply(resources, ClientPalette.Dark);
        Assert.True(((SolidColorBrush)resources["Background"]).IsFrozen);
    }

    /// 두 팔레트가 같은 키를 전부 채워야 한다. 하나라도 빠지면 테마를
    /// 바꿨을 때 그 키만 이전 테마의 색으로 남는다.
    [Fact]
    public void 두_테마가_같은_키를_채운다()
    {
        var light = new ResourceDictionary();
        var dark = new ResourceDictionary();

        ThemeResources.Apply(light, ClientPalette.Light);
        ThemeResources.Apply(dark, ClientPalette.Dark);

        Assert.Equal(light.Keys.Cast<object>().OrderBy(k => k.ToString()),
                     dark.Keys.Cast<object>().OrderBy(k => k.ToString()));
    }
}
