namespace Puck.WindowSensing;

/// 곧장 아래로 떨어지는 펫이 무엇에 닿는가. 착지면은 창의 윗변이거나,
/// 아무것도 없으면 화면 바닥이다.
///
/// puck-mac의 `LandingSurfaceResolver`를 그대로 옮긴 것이다. 좌표계가 같아서
/// (좌상단 원점, Y 아래로) 창의 윗변은 양쪽 다 `frame.Top`이다.
public static class LandingSurfaceResolver
{
    /// <param name="x">펫의 X. 가상 화면 물리 픽셀.</param>
    /// <param name="fallingFromY">펫의 현재 Y. 이보다 아래에 있는 면만 후보다 —
    /// 이미 지나친 윗변으로 다시 올라설 수는 없다.</param>
    /// <param name="windows">앞에서 뒤 순서(0번이 맨 앞).</param>
    /// <param name="screenBottomY">아무 창도 걸리지 않을 때의 바닥.</param>
    /// <param name="roamableTop">화면 위쪽 한계. avatarHeight와 함께 쓰인다.</param>
    /// <param name="avatarHeight">그려진 펫의 높이.</param>
    public static double LandingY(
        double x,
        double fallingFromY,
        IReadOnlyList<WindowInfo> windows,
        double screenBottomY,
        double roamableTop = double.NegativeInfinity,
        double avatarHeight = 0)
    {
        var best = double.PositiveInfinity;

        for (var i = 0; i < windows.Count; i++)
        {
            var frame = windows[i].Frame;

            if (x < frame.Left || x > frame.Right) continue;

            // 윗변이 화면 위쪽에 너무 붙어 있는 창(최대화/전체화면에 가까운 창)은
            // 통째로 제외한다. 거기 서는 순간 펫의 머리가 화면 밖으로 잘린다.
            if (frame.Top - roamableTop < avatarHeight) continue;

            var topEdge = frame.Top;
            if (topEdge < fallingFromY) continue;

            // 앞 창이 이 지점의 이 높이를 덮고 있으면 그 윗변은 보이지 않는다.
            // 보이지도 않는 선 위에 펫을 세우면 창 뒤에서 허공에 뜬 것으로 보인다.
            var occluded = false;
            for (var front = 0; front < i; front++)
            {
                var f = windows[front].Frame;
                if (f.Left <= x && x <= f.Right && f.Top <= topEdge && topEdge <= f.Bottom)
                {
                    occluded = true;
                    break;
                }
            }
            if (occluded) continue;

            if (topEdge < best) best = topEdge;
        }

        return double.IsPositiveInfinity(best) ? screenBottomY : best;
    }
}
