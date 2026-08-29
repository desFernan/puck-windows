using System.Windows;
using System.Windows.Media;

namespace Puck.ClientWindow;

/// 팔레트를 앱의 리소스 사전에 밀어 넣는 유일한 곳.
///
/// Theme.xaml은 어두운 팔레트의 값을 그대로 적어 둔 것이라, 그것만으로는
/// 설정의 테마가 아무 일도 하지 않는다. 여기가 같은 키를 덮어써서 밝은
/// 팔레트로 바꾼다 — 창들이 전부 `DynamicResource`로 묶여 있으므로 열려
/// 있는 창도 그 자리에서 바뀐다.
///
/// 키 이름이 Theme.xaml과 같아야 한다는 것이 이 파일의 유일한 규칙이다.
/// ClientPalette의 속성 이름을 그대로 쓰는 것이 그 규칙을 지키는 방법이다.
public static class ThemeResources
{
    /// 설정의 `theme_style` 값이 뜻하는 팔레트. 모르는 값은 어두운 쪽 —
    /// 이 앱이 그렇게 생겼고, 설정 파일의 오타가 화면을 바꾸면 안 된다.
    public static ClientPalette PaletteFor(string style)
        => string.Equals(style, "light", StringComparison.OrdinalIgnoreCase)
            ? ClientPalette.Light
            : ClientPalette.Dark;

    public static void Apply(string style)
    {
        if (Application.Current is not { } app) return;
        Apply(app.Resources, PaletteFor(style));
    }

    /// 사전을 직접 받는 판. 앱 없이도 시험할 수 있게 갈라 두었다.
    public static void Apply(ResourceDictionary resources, ClientPalette palette)
    {
        Set(resources, "Background", palette.Background);
        Set(resources, "Surface", palette.Surface);
        Set(resources, "SurfaceBorder", palette.SurfaceBorder);
        Set(resources, "TextPrimary", palette.TextPrimary);
        Set(resources, "TextSecondary", palette.TextSecondary);
        Set(resources, "Accent", palette.Accent);
        Set(resources, "OnAccent", palette.OnAccent);
        Set(resources, "StatusSuccess", palette.StatusSuccess);
        Set(resources, "StatusError", palette.StatusError);
        Set(resources, "StatusWarning", palette.StatusWarning);
    }

    /// 브러시를 새로 만들어 덮는다. 있는 브러시의 Color를 바꾸는 쪽이
    /// 빠르겠지만, Theme.xaml에서 온 것은 얼어 있어(Freezable) 쓸 수 없다.
    private static void Set(ResourceDictionary resources, string key, Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        resources[key] = brush;
    }
}
