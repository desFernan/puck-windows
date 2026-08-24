using System.Windows.Media;

namespace Puck.ClientWindow;

/// 디자인 시스템 v2 (2026-08-14). 값의 정본은 puck-mac의
/// ClientPalette.swift이고 이 파일은 그 값을 그대로 옮긴 것이다.
public sealed record ClientPalette
{
    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceBorder { get; init; }
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color Accent { get; init; }
    public required Color OnAccent { get; init; }
    public required Color StatusSuccess { get; init; }
    public required Color StatusError { get; init; }
    public required Color StatusWarning { get; init; }

    public Color StatusIdle => TextSecondary;
    public Color StatusActive => Accent;

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;

    // 테마와 무관하게 고정.
    private const string Success = "#3fb950";
    private const string Error = "#f85149";
    private const string WarningColour = "#e3b341";
    private const string AccentColour = "#ed8c33";

    public static ClientPalette Light { get; } = new()
    {
        Background = Hex("#fafafa"),
        Surface = Hex("#ffffff"),
        SurfaceBorder = Hex("#e5e5e5"),
        TextPrimary = Hex("#1a1a1a"),
        TextSecondary = Hex("#6b6b6b"),
        Accent = Hex(AccentColour),
        OnAccent = Hex("#ffffff"),
        StatusSuccess = Hex(Success),
        StatusError = Hex(Error),
        StatusWarning = Hex(WarningColour),
    };

    public static ClientPalette Dark { get; } = new()
    {
        Background = Hex("#0a0a0a"),
        Surface = Hex("#131313"),
        SurfaceBorder = Hex("#242424"),
        TextPrimary = Hex("#ededed"),
        TextSecondary = Hex("#7a7a7a"),
        Accent = Hex(AccentColour),
        // 이 팔레트에서는 흰색보다 accent 위 대비/무드가 낫다.
        OnAccent = Hex("#161616"),
        StatusSuccess = Hex(Success),
        StatusError = Hex(Error),
        StatusWarning = Hex(WarningColour),
    };
}
