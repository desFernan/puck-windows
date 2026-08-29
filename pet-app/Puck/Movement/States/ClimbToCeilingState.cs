using System.Windows;
using Puck.Diagnostics;

namespace Puck.Movement.States;

/// 벽을 타고 천장까지 곧장 오른다.
///
/// ClimbState와 같이 발밑에 진짜 벽을 요구하고, 매 프레임 다시 묻는다 —
/// 오르기는 실제로 화면에 있는 벽으로만 일어나야지 아무 데서나 시작되면
/// 안 된다. 다만 여기서 벽은 창의 옆면만이 아니라 **화면 자신의 옆면**도
/// 된다(WindowSupport.HasWall). 최대화된 창 하나만 떠 있는 바탕화면에서는
/// 오를 수 있는 창이 하나도 없어서, 그게 아니면 천장은 영영 닿을 수 없는
/// 곳이 된다.
///
/// 천장까지 못 미치는 짧은 벽은 그냥 끝까지 오르고 떨어진다. ClimbState가
/// 자기 창을 다 올랐을 때와 같다.
public sealed class ClimbToCeilingState : IStateHandler
{
    public string Name => "ClimbToCeiling";
    public string ClipKey => "climb";
    public bool LoopsClip => true;

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;
        var area = context.AreaAt(body.Position);

        // `UnclimbableWindows`를 보지 않는 이유는 ClimbState와 같다 —
        // 진행 중인 오르기를 "시작해도 되는가"에 대한 설정으로 다시
        // 판단하면, 반쯤 오른 창을 사람이 클릭하는 순간 펫이 손을 놓는다.
        if (!WindowSupport.HasWall(body.Position, context.VisualBounds, context.Windows, area))
        {
            // 여기 벽이 없다(또는 짧은 벽의 끝을 넘었다) — 잡을 것이 없다.
            AppLogger.Warning("movement", "천장으로 오르던 벽을 잃었습니다",
                new Dictionary<string, object?>
                {
                    ["x"] = (int)body.Position.X,
                    ["y"] = (int)body.Position.Y,
                    ["windows"] = context.Windows.Count,
                });
            context.RequestTransition(StateKind.Fall);
            return;
        }

        // 곧장 위로. x는 고정 — 오르는 도중에 방향을 바꾸지 않는다는
        // ClimbState의 규칙 그대로다.
        //
        // 목표는 천장 그 자체가 아니라 천장 + 아바타 높이다. 여기서 위치는
        // 아직 발밑이고 몸은 위로 뻗어 있으므로, 발을 화면 맨 위까지
        // 올리면 "도착" 전에 머리가 화면 밖으로 나간다. 머리가 천장에 막
        // 닿는 데서 멈추면, CeilingState가 뒤집힌 뒤 계산하는 사각형과
        // 정확히 같은 자리에 놓이기도 한다 — 그래서 뒤집힘이 도약이 아니라
        // 제자리에서 도는 것으로 읽힌다.
        //
        // 천장은 화면 윗변 그대로다. mac은 카메라 하우징이 여기 걸릴 수
        // 있어 x의 함수로 묻지만, Windows에는 하우징이 있는 디스플레이가
        // 없다.
        var target = new Point(body.Position.X, area.Top + context.AvatarHeight);
        var step = MovementSolver.StepToward(body.Position, target, dt, context.WalkSpeed);
        body.Position = step.Position;

        if (step.HasArrived) context.RequestTransition(StateKind.Ceiling);
    }
}
