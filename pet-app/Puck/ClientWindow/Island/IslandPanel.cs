using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Puck.ClientWindow.Island;

/// 채팅 창 안의 섬. 그림으로 채워지고, 접으면 그 그림의 색을 띤 띠가 된다.
///
/// 직접 그리는 이유는 타일 때문이다. PNG를 제 뷰로 얹으면 렌더러가 프레임마다
/// 다시 배율을 맞추고, 이어 붙인 자리마다 그 결과가 조금씩 어긋난다. 여기서는
/// 그림이 한 번 해석되고 타일은 그것을 같은 배율로 놓는다.
public sealed class IslandPanel : FrameworkElement
{
    public static readonly DependencyProperty IsFoldedProperty = DependencyProperty.Register(
        nameof(IsFolded), typeof(bool), typeof(IslandPanel),
        new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// 펼쳤을 때의 높이. 그림이 버티는 것보다 높이 끌 수는 없다 —
    /// `IslandLayout.MaximumHeight`를 보라.
    public static readonly DependencyProperty OpenHeightProperty = DependencyProperty.Register(
        nameof(OpenHeight), typeof(double), typeof(IslandPanel),
        new FrameworkPropertyMetadata(90.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public bool IsFolded
    {
        get => (bool)GetValue(IsFoldedProperty);
        set => SetValue(IsFoldedProperty, value);
    }

    public double OpenHeight
    {
        get => (double)GetValue(OpenHeightProperty);
        set => SetValue(OpenHeightProperty, value);
    }

    /// 지금 서 있어야 할 높이. 접혀 있으면 띠, 아니면 그림이 감당하는 만큼.
    public double CurrentHeight => IsFolded
        ? IslandLayout.CollapsedHeight
        : Math.Min(OpenHeight, IslandLayout.MaximumHeight(ArtworkPixelHeight, DisplayScale));

    private static double ArtworkPixelHeight
        => TankArtwork.Image is { } image ? TankArtwork.PixelHeight(image) : 0;

    private double DisplayScale
    {
        get
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(width, CurrentHeight);
    }

    protected override void OnRender(DrawingContext drawing)
    {
        var area = new Rect(0, 0, ActualWidth, ActualHeight);
        if (area.Width <= 0 || area.Height <= 0) return;

        var corner = IsFolded ? IslandLayout.CollapsedHeight / 2 : 14;
        var clip = new RectangleGeometry(area, corner, corner);
        clip.Freeze();

        drawing.PushClip(clip);

        // 그림이 없으면 색조만으로 채운다. 그래도 같은 섬이다.
        if (!IsFolded && TankArtwork.Image is { } artwork)
            DrawTiles(drawing, area, artwork);
        else
            DrawTone(drawing, area);

        drawing.Pop();
    }

    private static void DrawTiles(DrawingContext drawing, Rect area, BitmapSource artwork)
    {
        var aspect = TankArtwork.Aspect(artwork);
        foreach (var tile in IslandLayout.Tiles(area.Size, aspect))
            drawing.DrawImage(artwork, tile);
    }

    /// 그림에서 읽은 색으로 그린 물. 위에서 아래로 깊이가 있고, 가로로는
    /// 결이 있다 — 그게 없으면 창 너비만 한 한 가지 색이 된다.
    private static void DrawTone(DrawingContext drawing, Rect area)
    {
        var tone = TankToneReader.Current;

        var depth = new LinearGradientBrush(
            Stops(tone.Depth), new Point(0, 0), new Point(0, 1));
        depth.Freeze();
        drawing.DrawRectangle(depth, null, area);

        if (tone.Currents.Count < 2) return;

        var currents = new LinearGradientBrush(
            Stops(tone.Currents), new Point(0, 0), new Point(1, 0));
        currents.Opacity = 0.35;
        currents.Freeze();
        drawing.DrawRectangle(currents, null, area);
    }

    /// 표본들을 끝에서 끝까지 고르게 벌려 놓는다.
    private static GradientStopCollection Stops(IReadOnlyList<TankSample> samples)
    {
        var stops = new GradientStopCollection();
        for (var index = 0; index < samples.Count; index++)
            stops.Add(new GradientStop(
                samples[index].Color, samples.Count == 1 ? 0 : (double)index / (samples.Count - 1)));

        return stops;
    }
}
