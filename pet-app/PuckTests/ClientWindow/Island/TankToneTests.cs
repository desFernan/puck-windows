using Puck.ClientWindow.Island;

namespace PuckTests.ClientWindow.Island;

/// 결정이 든 쪽만 시험한다 — 그림을 몇 픽셀로 줄이는 일이 아니라,
/// 그 픽셀들을 띠의 색으로 바꾸는 규칙.
public class TankToneTests
{
    private static IReadOnlyList<IReadOnlyList<TankSample>> Grid(
        TankSample water, TankSample seabed, int rows = TankToneReader.Rows)
        => Enumerable.Range(0, rows)
            .Select(row => (IReadOnlyList<TankSample>)Enumerable.Repeat(
                row < TankToneReader.WaterRows ? water : seabed, TankToneReader.Columns).ToList())
            .ToList();

    [Fact]
    public void TheSeabedIsNotAveragedIntoTheWater()
    {
        // 띠는 물을 보여 준다. 바닥의 모래는 아예 다른 색 계열이다.
        var blue = new TankSample(0.2, 0.6, 0.9);
        var sand = new TankSample(0.9, 0.8, 0.5);

        var tone = TankToneReader.Tone(Grid(blue, sand));

        Assert.All(tone.Currents, sample =>
        {
            Assert.Equal(blue.Red, sample.Red, precision: 6);
            Assert.Equal(blue.Blue, sample.Blue, precision: 6);
        });
    }

    [Fact]
    public void TheBandHasATopAndABottom()
    {
        // 그린 장면의 물은 아래까지 거의 같은 파랑이라, 그림의 세 줄을 그대로
        // 쓰면 납작한 막대가 된다. 깊이는 찾는 것이 아니라 얹는 것이다.
        var blue = new TankSample(0.2, 0.6, 0.9);

        var tone = TankToneReader.Tone(Grid(blue, blue));

        Assert.Equal(3, tone.Depth.Count);
        Assert.True(tone.Depth[0].Blue > tone.Depth[1].Blue);
        Assert.True(tone.Depth[1].Blue > tone.Depth[2].Blue);
    }

    [Fact]
    public void TheColourItIsPutOnComesFromThePicture()
    {
        var blue = new TankSample(0.2, 0.6, 0.9);

        var tone = TankToneReader.Tone(Grid(blue, blue));

        Assert.Equal(blue, tone.Depth[1]);
    }

    [Fact]
    public void ThereIsOneCurrentPerColumnSoTheBandVariesAlongIt()
    {
        // 없으면 창 너비만 한 한 가지 색이 된다.
        var tone = TankToneReader.Tone(Grid(new TankSample(0.2, 0.6, 0.9), new TankSample(0, 0, 0)));

        Assert.Equal(TankToneReader.Columns, tone.Currents.Count);
    }

    [Fact]
    public void APictureThatSamplesToNothingKeepsTheIslandLookingLikeItself()
    {
        // 검은 띠가 아니라 fallback. 색이 안 나오는 그림은 누군가 바꿀 그림이다.
        Assert.Equal(TankTone.Fallback, TankToneReader.Tone([]));
        Assert.Equal(TankTone.Fallback, TankToneReader.Tone([[], []]));
    }

    [Fact]
    public void LightAndShadeMoveTowardWhiteAndBlack()
    {
        var mid = new TankSample(0.5, 0.5, 0.5);

        Assert.Equal(0.75, mid.Lightened(0.5).Red, precision: 6);
        Assert.Equal(0.25, mid.Darkened(0.5).Red, precision: 6);
    }

    [Fact]
    public void ASampleOfNothingIsBlackRatherThanAThrow()
    {
        Assert.Equal(new TankSample(0, 0, 0), TankToneReader.Average([]));
    }
}
