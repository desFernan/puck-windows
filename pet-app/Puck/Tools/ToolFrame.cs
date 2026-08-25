using System.Text.Json;
using System.Windows;

namespace Puck.Tools;

/// 화면 위의 한 자리. **모델과 주고받는 모양이 여기 하나뿐이다** —
/// 스키마도, 읽는 법도, 돌려줄 때의 표기도.
///
/// 셋이 흩어져 있었다. find_ui_element가 사각형을 한 가지 표기로 내놓고
/// get_frontmost_window가 다른 표기로 내놓는 동안, point_at의 스키마에는
/// 실제로 받는 것보다 좁게(x/y만) 적혀 있었다. 모델은 이 셋을 이어 붙여
/// 쓰므로 — 찾은 것의 frame을 그대로 누르라고 넘긴다 — 어긋난 만큼
/// 그 자리에서 실수한다.
public static class ToolFrame
{
    /// `{x, y}` 또는 `{left, top, width, height}`. point_at·click_element가
    /// 둘 다 받는다.
    public static JsonElement PointOrRectParam(string description)
        => ToolSpec.ObjectParam(description, new
        {
            x = Number,
            y = Number,
            left = Number,
            top = Number,
            width = Number,
            height = Number,
        });

    /// `{left, top, width, height}`만. capture_screen처럼 넓이가 있어야 뜻이
    /// 서는 도구가 쓴다 — 점을 받는 것처럼 적어 두면 모델이 점을 보내고,
    /// 그건 조용히 "화면 전체"가 된다.
    public static JsonElement RectParam(string description)
        => ToolSpec.ObjectParam(description, new
        {
            left = Number,
            top = Number,
            width = Number,
            height = Number,
        });

    /// 어느 모양으로 와도 한 점으로. 사각형이면 그 한가운데다 —
    /// find_ui_element가 돌려주는 모양이 사각형이고 모델은 그걸 그대로 넘긴다.
    public static Point? PointIn(JsonElement frame)
    {
        if (Field(frame, "x") is { } x && Field(frame, "y") is { } y) return new Point(x, y);

        return RectIn(frame) is { } rect
            ? new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2)
            : null;
    }

    /// 네 값이 다 있어야 사각형이다. 반쯤 온 것을 0으로 채우면 "없음"과
    /// 구분되지 않는다.
    public static Rect? RectIn(JsonElement frame)
        => Field(frame, "left") is { } left && Field(frame, "top") is { } top &&
           Field(frame, "width") is { } width && Field(frame, "height") is { } height
            ? new Rect(left, top, width, height)
            : null;

    /// 사각형을 모델에게 돌려줄 때의 표기. 그대로 다시 넘길 수 있는 모양이다.
    public static string Format(Rect frame)
        => $"frame={{left:{frame.Left:0}, top:{frame.Top:0}, width:{frame.Width:0}, height:{frame.Height:0}}}";

    private static object Number => new { type = "number" };

    private static double? Field(JsonElement element, string name)
        => element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;
}
