using System.Windows;

namespace Puck.Movement.States;

/// 모니터 경계의 턱을 타고 위 화면으로 올라간다.
///
/// 바닥 높이가 다른 모니터가 붙어 있으면 내려가는 건 떨어지면 되지만
/// 올라오는 힘이 없어서, 낮은 화면에 내려간 펫이 영영 거기 갇힌다.
///
/// 올라가는 방식은 mac의 ClimbState와 같다 — walkSpeed로 수직 이동, climb 클립.
/// 다만 mac이 타는 것은 창의 옆면이고(Phase 2), 이쪽은 화면의 턱이다.
///
/// 두 걸음으로 나뉜다: 먼저 제자리에서 위로, 그다음 위 화면 안쪽으로.
/// 대각선으로 한 번에 가면 올라가는 도중 몸의 절반이 아직 아무 화면도
/// 없는 자리에 걸려 잘려 보인다.
public sealed class ClimbLedgeState : IStateHandler
{
    public string Name => "ClimbLedge";
    public string ClipKey => "climb";
    public bool LoopsClip => true;

    /// 올라선 뒤 설 자리. WalkState가 막힌 그 프레임에 채워 둔다.
    public Point? Target { get; set; }

    private bool _reachedHeight;

    public void Enter() => _reachedHeight = false;

    public void Update(double dt, StateContext context)
    {
        if (Target is not { } target)
        {
            context.RequestTransition(StateKind.Idle);
            return;
        }

        var body = context.Body;

        if (!_reachedHeight)
        {
            // 수직으로만. 좌우를 섞으면 벽을 타는 동안 펫이 뒤집힌다 —
            // 방향도 여기서는 건드리지 않는다.
            var top = new Point(body.Position.X, target.Y);
            var up = MovementSolver.StepToward(body.Position, top, dt, context.WalkSpeed);
            // 도착 반경 안이면 StepToward는 제자리를 돌려준다. 턱은 "대충 그 근처"가
            // 아니라 정확히 그 자리여야 해서, 도착했으면 목표에 맞춰 놓는다.
            body.Position = up.HasArrived ? top : up.Position;
            _reachedHeight = up.HasArrived;
            return;
        }

        var across = MovementSolver.StepToward(body.Position, target, dt, context.WalkSpeed);
        if (MovementSolver.FacingToward(body.Position, target) is { } facing) body.Facing = facing;
        body.Position = across.HasArrived ? target : across.Position;

        if (!across.HasArrived) return;

        Target = null;
        context.RequestTransition(StateKind.Idle);
    }
}
