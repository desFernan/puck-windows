using System.Windows;
using Puck.Diagnostics;

namespace Puck.Movement.States;

/// 창의 윗변을 따라 거닌다. 그 창이 없어질 때까지 — 닫혔든, 최소화됐든,
/// 끝까지 걸어 나갔든. 셋 다 같은 상황이다: 발밑에 아무것도 없다.
///
/// 전용 클립이 없으므로 walk를 다시 쓴다.
public sealed class WalkOnTopState : IStateHandler
{
    public string Name => "WalkOnTop";
    public string ClipKey => "walk";
    public bool LoopsClip => true;

    /// Enter가 아니라 첫 update에서 정한다. Climb은 접근한 쪽에 가까운 모서리로
    /// 올라오므로, 방향을 고정해 두면 방금 올라온 그 모서리로 도로 걸어 나간다.
    private double? _direction;

    public void Enter() => _direction = null;

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;

        var window = WindowSupport.SupportingWindow(body.Position, context.Windows);
        if (window is null)
        {
            // 이 전이만은 밖에서 원인이 보이지 않는다 — 펫이 그냥 떨어질 뿐,
            // "창이 닫혔다"(정상)인지 "서 있는 창을 목록이 놓쳤다"(버그)인지
            // 구분할 수 없다. 그래서 그때의 실제 데이터를 남긴다.
            AppLogger.Warning("movement", "창 위를 걷다 발밑을 잃었습니다",
                new Dictionary<string, object?>
                {
                    ["x"] = (int)body.Position.X,
                    ["y"] = (int)body.Position.Y,
                    ["windows"] = context.Windows.Count,
                });
            context.RequestTransition(StateKind.Fall);
            return;
        }

        var direction = _direction ?? InitialDirection(body.Position, window.Frame);
        var nextX = body.Position.X + direction * context.WalkSpeed * dt;

        var (low, high) = WalkableX(context.RoamableArea, context.VisualBounds);

        // 화면 가장자리에서는 돌아선다. 화면 끝까지 닿은 창에는 걸어 나갈
        // 끝이 없어서, 이게 없으면 펫이 가장자리에 박힌 채 걷는 클립만
        // 계속 재생한다 — 화면을 떠나려고 애쓰는 것처럼 보인다.
        //
        // 펫이 영역보다 넓으면 설 수 있는 x가 하나뿐이라 매 프레임이 "범위 밖"이
        // 되고, 그러면 방향만 프레임률로 뒤집힌다.
        var fits = !PetBounds.IsOversizedHorizontally(context.VisualBounds, context.RoamableArea);
        if (fits && (nextX < low || nextX > high))
        {
            direction = -direction;
            nextX = body.Position.X + direction * context.WalkSpeed * dt;
        }

        _direction = direction;
        body.Facing = direction > 0 ? Avatar.AvatarFacing.Right : Avatar.AvatarFacing.Left;
        body.Position = new Point(Math.Clamp(nextX, low, Math.Max(low, high)), window.Frame.Top);
    }

    /// 몸 전체가 화면 안에 있는 x의 범위. 펫보다 좁은 영역이면 한 점으로 접힌다.
    private static (double Low, double High) WalkableX(Rect area, Rect visualBounds)
    {
        var low = area.Left - visualBounds.Left;
        var high = area.Right - visualBounds.Right;
        return (low, Math.Max(low, high));
    }

    /// 방금 올라온 모서리 쪽이 아니라 창 안쪽으로 걸어 들어간다.
    private static double InitialDirection(Point position, Rect frame)
        => Math.Abs(position.X - frame.Left) <= Math.Abs(position.X - frame.Right) ? 1 : -1;
}
