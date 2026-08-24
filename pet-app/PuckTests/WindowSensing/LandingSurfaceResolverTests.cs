using System.Windows;
using Puck.WindowSensing;

namespace PuckTests.WindowSensing;

public class LandingSurfaceResolverTests
{
    private const double ScreenBottom = 1000;

    private static WindowInfo At(double left, double top, double width, double height)
        => new(IntPtr.Zero, 1, "App", "창", new Rect(left, top, width, height), false, false);

    private static double Land(IReadOnlyList<WindowInfo> windows, double x = 500, double from = 0,
                               double roamableTop = double.NegativeInfinity, double avatarHeight = 0)
        => LandingSurfaceResolver.LandingY(x, from, windows, ScreenBottom, roamableTop, avatarHeight);

    [Fact]
    public void WithNoWindowsThePetLandsOnTheScreenFloor()
    {
        Assert.Equal(ScreenBottom, Land([]));
    }

    [Fact]
    public void AWindowUnderfootIsTheLandingSurface()
    {
        Assert.Equal(400, Land([At(200, 400, 600, 300)]));
    }

    [Fact]
    public void AWindowThePetIsNotAboveIsIgnored()
    {
        // x=500은 이 창의 가로 범위(0..100) 밖이다.
        Assert.Equal(ScreenBottom, Land([At(0, 400, 100, 300)]));
    }

    [Fact]
    public void ASurfaceAlreadyPassedIsIgnored()
    {
        // 이미 y=600까지 떨어졌으면 위에 있는 윗변(400)에 다시 올라설 수 없다.
        Assert.Equal(ScreenBottom, Land([At(200, 400, 600, 300)], from: 600));
    }

    [Fact]
    public void TheHighestQualifyingEdgeWinsBecauseItIsHitFirst()
    {
        Assert.Equal(300, Land([At(200, 500, 600, 300), At(200, 300, 600, 600)]));
    }

    [Fact]
    public void AnEdgeHiddenBehindAFrontWindowIsNotASurface()
    {
        // 앞 창(Z 0)이 그 지점의 그 높이를 덮고 있으면, 뒤 창의 윗변은
        // 보이지도 않는 선이다. 거기 세우면 펫이 창 뒤에서 허공에 뜬다.
        var front = At(200, 200, 600, 700);   // 200..900을 덮는다
        var behind = At(200, 400, 600, 300);  // 윗변 400이 front 안에 있다
        Assert.Equal(200, Land([front, behind]));
    }

    [Fact]
    public void AnEdgePokingOutAboveTheFrontWindowStillCounts()
    {
        var front = At(200, 400, 600, 500);
        var behind = At(200, 300, 600, 600);  // 윗변 300은 front 위로 나와 있다
        Assert.Equal(300, Land([front, behind]));
    }

    [Fact]
    public void AWindowWithoutHeadroomIsRefusedEntirely()
    {
        // 윗변이 화면 위쪽에 너무 붙어 있으면(최대화된 창) 거기 서는 순간
        // 펫의 머리가 화면 밖으로 잘린다.
        var maximised = At(0, 10, 1920, 990);
        Assert.Equal(ScreenBottom, Land([maximised], roamableTop: 0, avatarHeight: 133));
    }

    [Fact]
    public void AWindowWithEnoughHeadroomIsStillOffered()
    {
        var window = At(0, 200, 1920, 800);
        Assert.Equal(200, Land([window], roamableTop: 0, avatarHeight: 133));
    }

    [Fact]
    public void TheEdgesOfAWindowCount()
    {
        // 가로 범위는 양 끝을 포함한다 — 창 모서리에 딱 선 펫이 떨어지면 안 된다.
        Assert.Equal(400, Land([At(200, 400, 600, 300)], x: 200));
        Assert.Equal(400, Land([At(200, 400, 600, 300)], x: 800));
    }
}
