namespace Puck.ClientWindow;

/// 색은 ClientPalette, 여기는 타입/간격/모양. puck-mac ClientTheme.swift에서 옮김.
public static class ClientTheme
{
    public static class Typography
    {
        public const double SectionHeader = 13;      // semibold
        public const double WorkspaceName = 14;      // medium
        public const double SessionTitle = 13;
        public const double ToolLabel = 13;          // medium
        public const double Mono = 12.5;             // monospaced
        public const double Caption = 12;
        public const double TranscriptBody = 16;
        public const double TranscriptCode = 13.5;   // monospaced

        /// 본문(16)보다 반드시 커야 한다 — 그게 고정 크기를 쓰는 이유다.
        public static double TranscriptHeading(int level) => level switch
        {
            1 => 23,
            2 => 20,
            _ => 17,
        };
    }

    public static class Metrics
    {
        public const double SpacingSmall = 4;
        public const double SpacingMedium = 8;
        public const double SpacingLarge = 12;
        public const double SectionSpacing = 20;
        public const double WindowEdgePadding = 20;
        public const double TranscriptColumnWidth = 760;
        public const double TranscriptHorizontalPadding = 12;
        public const double PanelCornerRadius = 14;
        public const double PanelInset = 8;
        public const double CardCornerRadius = 6;
        public const double RowCornerRadius = 4;
        public const double WindowMinWidth = 560;
        public const double WindowMinWidthWithCode = 1040;
        public const double EditorWindowMinWidth = 540;
        public const double WindowTint = 0.78;
        public const double WindowMinHeight = 640;
    }
}
