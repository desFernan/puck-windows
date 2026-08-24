using System.Windows;
using Puck.Diagnostics;

namespace Puck.Movement.States;

/// 창의 옆면을 타고 그 윗변까지 올라간 뒤, 그 위를 걷는다.
///
/// 매달린 창을 매 프레임 다시 찾는다 — 위치를 기억해 두지 않는 이유는 창이
/// 오르는 도중에 닫히거나 움직이기 때문이다. 잡을 것이 없어지면 떨어진다.
public sealed class ClimbState : IStateHandler
{
    public string Name => "Climb";
    public string ClipKey => "climb";
    public bool LoopsClip => true;

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;

        // `UnclimbableWindows`를 여기서는 보지 않는다. 그 목록은 "오르기를
        // *시작*하면 안 되는 창"이고, 그 판단은 한 전이 전에 WalkState가 이미
        // 했다. 여기서 다시 보면, 반쯤 올라간 창을 사람이 클릭하는 순간
        // 펫이 손을 놓는다.
        var window = WindowSupport.WindowBeingClimbed(body.Position, context.Windows);
        if (window is null)
        {
            // WalkOnTop과 같은 이유로 남긴다: 밖에서 보면 펫이 그냥 떨어질 뿐,
            // "창이 닫혔다"(정상)인지 "잡고 있던 벽을 목록이 놓쳤다"(버그)인지
            // 구분할 수 없다.
            AppLogger.Warning("movement", "오르던 벽을 잃었습니다",
                new Dictionary<string, object?>
                {
                    ["x"] = (int)body.Position.X,
                    ["y"] = (int)body.Position.Y,
                    ["windows"] = context.Windows.Count,
                    ["near"] = string.Join(" ", context.Windows.Take(4).Select(w =>
                        $"[{(int)w.Frame.Left}..{(int)w.Frame.Right} y{(int)w.Frame.Top}..{(int)w.Frame.Bottom} {w.OwnerName}]")),
                });
            context.RequestTransition(StateKind.Fall);
            return;
        }

        // 곧장 위로. 좌우를 섞으면 올라가는 도중 펫이 뒤집힌다.
        var top = new Point(body.Position.X, window.Frame.Top);
        var step = MovementSolver.StepToward(body.Position, top, dt, context.WalkSpeed);

        if (!step.HasArrived)
        {
            body.Position = step.Position;
            return;
        }

        // 올라선 자리를 창 안으로 당긴다. 모서리는 EdgeTolerance만큼 밖에서도
        // 잡히므로 매달린 X는 프레임 밖일 수 있는데(실측: 창 오른쪽 끝 1845에
        // 펫이 1846.5), 발판 판정은 X에 여유가 없어서 올라서자마자 발밑을
        // 잃는다. 몸을 끌어올려 모서리 위로 넘기는 마지막 한 걸음이다.
        body.Position = new Point(
            Math.Clamp(body.Position.X, window.Frame.Left, window.Frame.Right),
            window.Frame.Top);

        context.RequestTransition(StateKind.WalkOnTop);
    }
}
