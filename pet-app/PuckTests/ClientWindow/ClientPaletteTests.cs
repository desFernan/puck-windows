using System.Windows.Media;
using Puck.ClientWindow;

namespace PuckTests.ClientWindow;

public class ClientPaletteTests
{
    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    [Fact]
    public void DarkMatchesTheMacPalette()
    {
        var p = ClientPalette.Dark;
        Assert.Equal("#0a0a0a", Hex(p.Background));
        Assert.Equal("#131313", Hex(p.Surface));
        Assert.Equal("#242424", Hex(p.SurfaceBorder));
        Assert.Equal("#ededed", Hex(p.TextPrimary));
        Assert.Equal("#7a7a7a", Hex(p.TextSecondary));
        Assert.Equal("#ed8c33", Hex(p.Accent));
        Assert.Equal("#161616", Hex(p.OnAccent));
    }

    [Fact]
    public void LightMatchesTheMacPalette()
    {
        var p = ClientPalette.Light;
        Assert.Equal("#fafafa", Hex(p.Background));
        Assert.Equal("#ffffff", Hex(p.Surface));
        Assert.Equal("#e5e5e5", Hex(p.SurfaceBorder));
        Assert.Equal("#1a1a1a", Hex(p.TextPrimary));
        Assert.Equal("#6b6b6b", Hex(p.TextSecondary));
        Assert.Equal("#ed8c33", Hex(p.Accent));
        Assert.Equal("#ffffff", Hex(p.OnAccent));
    }

    [Fact]
    public void StatusColoursAreThemeIndependent()
    {
        foreach (var p in new[] { ClientPalette.Light, ClientPalette.Dark })
        {
            Assert.Equal("#3fb950", Hex(p.StatusSuccess));
            Assert.Equal("#f85149", Hex(p.StatusError));
            Assert.Equal("#e3b341", Hex(p.StatusWarning));
        }
    }

    [Fact]
    public void DerivedStatusColoursReuseTheirSource()
    {
        var p = ClientPalette.Dark;
        Assert.Equal(p.TextSecondary, p.StatusIdle);
        Assert.Equal(p.Accent, p.StatusActive);
    }
}
