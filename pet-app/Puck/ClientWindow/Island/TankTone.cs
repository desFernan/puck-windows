using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Puck.ClientWindow.Island;

/// 그림에서 평균 낸 색 하나.
public readonly record struct TankSample(double Red, double Green, double Blue)
{
    public Color Color => Color.FromRgb(Byte(Red), Byte(Green), Byte(Blue));

    /// 같은 색에 빛을 더한 것 — 흰색 쪽으로.
    public TankSample Lightened(double amount) => new(
        Red + (1 - Red) * amount,
        Green + (1 - Green) * amount,
        Blue + (1 - Blue) * amount);

    /// 덜한 것 — 검은색 쪽으로.
    public TankSample Darkened(double amount)
        => new(Red * (1 - amount), Green * (1 - amount), Blue * (1 - amount));

    private static byte Byte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}

/// 접힌 섬을 그리는 색들. 펼친 섬을 채우는 그림에서 읽어 온다.
///
/// 띠는 찍은 것이 아니라 그린 것이다 — 그림은 장면이고 띠는 장면을 담기엔
/// 너무 낮다. 하지만 둘은 1초 차이의 같은 섬이라, 자기 색을 따로 가진 띠는
/// 다른 곳이 된다. 게다가 그림은 바꿀 수 있는 것이다: 누구든 자기 seabed.png를
/// 커스터마이징 폴더에 넣을 수 있고, 동봉본에 맞춰 색을 박아 둔 띠는 그 순간
/// 틀린다. 그래서 실제로 거기 있는 그림에서 읽는다.
public sealed record TankTone(IReadOnlyList<TankSample> Depth, IReadOnlyList<TankSample> Currents)
{
    /// 그림이 아예 없을 때 — 동봉본의 색을 한 번 재어 적어 둔 것이라,
    /// 아무것도 안 든 섬도 여전히 같은 섬이다.
    public static TankTone Fallback { get; } = new(
        [
            new TankSample(0.42, 0.82, 0.95),
            new TankSample(0.22, 0.61, 0.88),
            new TankSample(0.10, 0.38, 0.70),
        ],
        [
            new TankSample(0.30, 0.70, 0.92),
            new TankSample(0.24, 0.64, 0.90),
            new TankSample(0.34, 0.74, 0.93),
            new TankSample(0.26, 0.66, 0.91),
        ]);
}

public static class TankToneReader
{
    /// 그림을 몇 칸으로 줄여 읽는가. 일부러 작다 — 이건 색조이지
    /// 썸네일이 아니다. 띠가 길이를 따라 변할 만큼의 칸과, 밝은 물과 어두운
    /// 물을 가릴 만큼의 줄.
    public const int Columns = 12;
    public const int Rows = 10;

    /// 어디까지가 아직 물인가. 띠는 물을 보여 주므로 바닥의 모래와 돌 —
    /// 이 그림에서는 아예 다른 색 계열이다 — 이 섞이면 안 된다. 절반은
    /// 동봉본처럼 놓인 장면에 안전하고, 끝까지 물인 그림에도 해가 없다.
    public const int WaterRows = 5;

    /// 띠의 표면에 얼마나 빛이 있고 바닥에 얼마나 못 닿는가. 띠에 위아래가
    /// 생길 만큼이되, 그림의 색이기를 그만둘 만큼은 아니게.
    public const double SurfaceLight = 0.30;
    public const double DepthShade = 0.34;

    /// 격자에서 색조를 뽑는다. 읽어 낼 수 없는 것은 검은 띠가 아니라
    /// fallback으로 돌려준다 — 색이 안 나오는 그림은 누군가 바꿀 그림이고,
    /// 바꾸기 전까지 섬은 자기처럼 보여야 한다.
    public static TankTone Tone(IReadOnlyList<IReadOnlyList<TankSample>> grid)
    {
        var water = grid.Take(WaterRows).Where(row => row.Count > 0).ToList();
        if (water.Count == 0) return TankTone.Fallback;

        var columns = water[0].Count;
        if (columns <= 0) return TankTone.Fallback;

        // 그림의 세 줄이 아니라, 위가 밝고 아래가 어두운 색 하나. 그린 장면의
        // 물은 아래까지 거의 같은 파랑이라 세 줄을 재면 100분의 1 안에서
        // 같은 값이 나오고, 그 사이의 그러데이션은 납작한 막대가 된다.
        // 깊이는 찾는 것이 아니라 얹는 것이고, 얹는 바탕색이 그림에서 오는
        // 것이 둘을 같은 곳으로 만든다.
        var basis = Average(water.SelectMany(row => row));
        var depth = new[] { basis.Lightened(SurfaceLight), basis, basis.Darkened(DepthShade) };

        var currents = Enumerable.Range(0, columns)
            .Select(column => Average(water.Select(row => row[column])))
            .ToList();

        return new TankTone(depth, currents);
    }

    public static TankSample Average(IEnumerable<TankSample> samples)
    {
        var list = samples as IReadOnlyList<TankSample> ?? samples.ToList();
        if (list.Count == 0) return new TankSample(0, 0, 0);

        return new TankSample(
            list.Sum(s => s.Red) / list.Count,
            list.Sum(s => s.Green) / list.Count,
            list.Sum(s => s.Blue) / list.Count);
    }

    /// 그림을 Columns x Rows 비트맵으로 다시 그린 것. 영역을 평균 내는 일이
    /// 실제로 그것이다 — 400만 픽셀을 도는 반복문이 아니라 보간이 대신 더한다.
    public static IReadOnlyList<IReadOnlyList<TankSample>>? Grid(BitmapSource image)
    {
        try
        {
            var scaled = new TransformedBitmap(
                new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0),
                new System.Windows.Media.ScaleTransform(
                    (double)Columns / image.PixelWidth, (double)Rows / image.PixelHeight));

            var pixels = new byte[Columns * Rows * 4];
            scaled.CopyPixels(pixels, Columns * 4, 0);

            return Enumerable.Range(0, Rows).Select(row =>
                (IReadOnlyList<TankSample>)Enumerable.Range(0, Columns).Select(column =>
                {
                    // BGRA. 0번 줄이 그림의 위쪽 — 밝은 수면이라는 말이 그 뜻이다.
                    var offset = (row * Columns + column) * 4;
                    return new TankSample(
                        pixels[offset + 2] / 255.0,
                        pixels[offset + 1] / 255.0,
                        pixels[offset] / 255.0);
                }).ToList()).ToList();
        }
        catch (Exception)
        {
            // 읽을 수 없는 그림은 없는 것과 같다. 여기서 던지면 창이 안 뜬다.
            return null;
        }
    }

    private static readonly Lazy<TankTone> Held = new(() =>
        TankArtwork.Image is { } image && Grid(image) is { } grid ? Tone(grid) : TankTone.Fallback);

    /// 섬이 실제로 채워져 있는 그림의 색조. 다시 계산하지 않고 들고 있는다 —
    /// 띠는 펫이 그 위를 걷는 모든 프레임에 다시 그려지고, 이건 비트맵을 읽는다.
    public static TankTone Current => Held.Value;
}
