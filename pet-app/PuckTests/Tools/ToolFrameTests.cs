using System.Text.Json;
using System.Windows;
using Puck.Tools;

namespace PuckTests.Tools;

/// 도구 세 개가 같은 "frame" 모양을 주고받는다. 그 해석과 표기가 하나뿐이어야
/// 모델이 find_ui_element의 결과를 point_at에 그대로 갈아 끼울 수 있다.
public class ToolFrameTests
{
    private static JsonElement Frame(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public void ARectangleGoesOutAndComesBackAsTheSameNumbers()
    {
        // 이 표기가 곧 도구 사이의 계약이다. 바뀌면 모델이 find_ui_element의
        // 결과를 point_at에 갈아 끼우던 습관이 그 자리에서 깨진다.
        Assert.Equal("frame={left:10, top:20, width:30, height:40}",
            ToolFrame.Format(new Rect(10, 20, 30, 40)));
    }

    [Fact]
    public void ARectangleIsPointedAtInItsMiddle()
    {
        // 모델은 find_ui_element가 준 사각형을 point_at에 그대로 넘긴다.
        var point = ToolFrame.PointIn(Frame(new { left = 100, top = 200, width = 40, height = 20 }));

        Assert.Equal(new Point(120, 210), point);
    }

    [Fact]
    public void APointIsTakenAsGiven()
    {
        Assert.Equal(new Point(7, 9), ToolFrame.PointIn(Frame(new { x = 7, y = 9 })));
    }

    [Fact]
    public void HalfARectangleIsNoRectangle()
    {
        // 빠진 값을 0으로 채우면 "없음"과 구분되지 않는다 — capture_screen이
        // 그 차이로 "전체 화면"을 정한다.
        Assert.Null(ToolFrame.RectIn(Frame(new { left = 10, top = 20 })));
        Assert.Null(ToolFrame.PointIn(Frame(new { left = 10, top = 20 })));
    }

    [Fact]
    public void EveryFrameToolAdvertisesTheShapeItActuallyAccepts()
    {
        // point_at은 사각형도 받는다. 스키마에 x/y만 적어 두면 모델은
        // 설명에 적힌 것을 덜 믿는다.
        var properties = ToolFrame.PointOrRectParam("어디를").GetProperty("properties");

        foreach (var field in new[] { "x", "y", "left", "top", "width", "height" })
            Assert.True(properties.TryGetProperty(field, out _), $"{field}가 스키마에 없다");
    }

    [Fact]
    public void ACaptureFrameDoesNotAdvertiseAPoint()
    {
        // capture_screen은 넓이가 있어야 뜻이 선다. {x,y}를 받는 것처럼
        // 적으면 모델이 점을 보내고, 그건 조용히 전체 화면이 된다.
        var properties = ToolFrame.RectParam("무엇을").GetProperty("properties");

        Assert.False(properties.TryGetProperty("x", out _));
        Assert.True(properties.TryGetProperty("width", out _));
    }
}
