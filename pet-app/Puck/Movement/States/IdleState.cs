using Puck.WindowSensing;

namespace Puck.Movement.States;

/// 가만히 있는 펫이 다음에 무엇을 할지, 창 목록을 아는 쪽이 정할 수 있게 하는 구멍.
public interface IWanderDelegate
{
    /// 배회 타이머가 찼다. 이번에 뽑힌 것이 `outcome`이다.
    void WanderRequested(WanderOutcome outcome);

    /// 발밑이 사라진 게 아니라 창 **뒤로** 갔다 — 사람이 그 창을 앞으로 가져왔다.
    /// 떨어지는 것은 답이 아니라서(아래 호출부 주석) 판단을 위로 넘긴다.
    void LostFootingBehind(WindowInfo window);
}

/// 서 있기. 타이머가 차면 다음 행동을 요청하고, 발밑이 사라지면 떨어진다.
public sealed class IdleState(WanderScheduler scheduler) : IStateHandler
{
    /// 발밑이 이 정도 어긋나는 건 서 있는 것으로 친다 — 반올림과
    /// 픽셀 경계 때문에 정확히 같은 값이 나오지 않는다.
    public const double FootTolerance = 4;

    public string Name => "Idle";
    public string ClipKey => "idle";
    public bool LoopsClip => true;

    /// 창 목록을 아는 쪽. 없으면 무작위로 걷기만 한다.
    public IWanderDelegate? Wander { get; init; }

    public void Enter() => scheduler.Reset();

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;

        // WalkOnTop은 매 프레임 발밑을 다시 확인하는데 Idle은 그러지 않아서,
        // 창 윗면에 착지해 쉬던 펫이 그 창이 닫히면 영원히 공중에 떠 있었다.
        // landingY가 지금 위치보다 아래면 밑에 틈이 생긴 것이다.
        var surfaceY = context.LandingY(body.Position);
        if (surfaceY > body.Position.Y + FootTolerance)
        {
            // 받치던 것이 아예 사라졌으면 떨어지는 게 맞고, 창 **뒤로** 갔을
            // 뿐이면 아니다. 펫은 모든 창 위에 그려지므로, 그 창이 덮고 있는
            // 바닥으로 떨어뜨리면 숨어 있던 자리에서 사람이 지금 쓰는 창
            // 한가운데로 옮겨 놓는 셈이 된다.
            var landing = new System.Windows.Point(body.Position.X, surfaceY);
            var covering = WindowSupport.CoveringWindow(landing, context.AvatarHeight, context.Windows);
            if (covering is not null)
            {
                Wander?.LostFootingBehind(covering);
                return;
            }

            context.RequestTransition(StateKind.Fall);
            return;
        }

        // 디스플레이 사이의 빈 공간에 던져졌다면 발밑에 화면이 없다.
        // 떨어뜨리면 FallState가 가장 가까운 바닥으로 되돌린다.
        if (!context.HasGroundUnder(body.Position))
        {
            context.RequestTransition(StateKind.Fall);
            return;
        }

        if (scheduler.Tick(dt) is not { } outcome) return;

        if (Wander is not null)
        {
            Wander.WanderRequested(outcome);
            return;
        }

        // 창을 아는 쪽이 없으면 걷는 것 말고는 할 수 있는 게 없다.
        if (outcome != WanderOutcome.Stay)
            context.RequestTransition(StateKind.Walk);
    }
}
