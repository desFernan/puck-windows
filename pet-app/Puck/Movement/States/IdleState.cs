namespace Puck.Movement.States;

/// 서 있기. 타이머가 차면 걷겠다고 요청하고, 발밑이 사라지면 떨어진다.
public sealed class IdleState(WanderScheduler scheduler) : IStateHandler
{
    /// 발밑이 이 정도 어긋나는 건 서 있는 것으로 친다 — 반올림과
    /// 픽셀 경계 때문에 정확히 같은 값이 나오지 않는다.
    public const double FootTolerance = 2;

    public string Name => "Idle";
    public string ClipKey => "idle";
    public bool LoopsClip => true;

    public void Enter() => scheduler.Reset();

    public void Update(double dt, StateContext context)
    {
        // WalkState는 매 프레임 발밑을 다시 확인하는데 Idle은 그러지 않아서,
        // 창 윗면에 착지해 쉬던 펫이 그 창이 닫히면 영원히 공중에 떠 있었다.
        // landingY가 지금 위치보다 아래면 밑에 틈이 생긴 것이다.
        var surfaceY = context.LandingY(context.Body.Position);
        if (surfaceY > context.Body.Position.Y + FootTolerance)
        {
            context.RequestTransition(StateKind.Fall);
            return;
        }

        if (scheduler.Tick(dt))
            context.RequestTransition(StateKind.Walk);
    }
}
