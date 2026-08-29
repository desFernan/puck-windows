using System.Windows;

namespace Puck.Movement.States;

/// 바탕화면과 수조 사이를 **날아서** 오간다.
///
/// 걷지 않는 이유는 둘 사이에 걸어갈 바닥이 없기 때문이다 — 수조는 창 안에
/// 떠 있는 상자이고, 거기까지 걸어가려면 허공을 딛어야 한다.
///
/// 목적지를 매 프레임 다시 묻는다. 날아가는 동안 사람이 창을 끌면 수조가
/// 함께 움직이고, 그때 처음 잰 자리로 계속 가면 펫이 창이 있던 자리에
/// 내려선다. puck-mac의 "a trip in the air follows the tank it is flying to".
public sealed class TravelState : IStateHandler
{
    /// 얼마나 빨리 나는가. 걷기보다 빠르되, 순간이동으로 읽히지 않을 만큼.
    public const double Speed = 900;

    /// 이만큼 남으면 도착으로 친다.
    public const double ArrivalDistance = 2;

    public string Name => "Travel";
    public string ClipKey => "fall";
    public bool LoopsClip => true;

    /// 어디로 가고 있는가. 매 프레임 다시 읽히므로, 여기 꽂아 두는 쪽이
    /// 목적지가 움직일 때마다 갱신하면 된다.
    public Func<Point?>? Destination { get; set; }

    /// 도착하면 어디로 갈 것인가. 도착 자체는 상태가 아니다.
    public StateKind Then { get; set; } = StateKind.Idle;

    public void Update(double dt, StateContext context)
    {
        if (Destination?.Invoke() is not { } target)
        {
            // 가려던 곳이 사라졌다. 허공에 멈춰 있는 것보다 떨어지는 편이 낫다.
            context.RequestTransition(StateKind.Fall);
            return;
        }

        var body = context.Body;
        var step = MovementSolver.StepToward(body.Position, target, dt, Speed);

        if (MovementSolver.FacingToward(body.Position, target) is { } facing) body.Facing = facing;

        body.Position = step.Position;

        var remaining = (target - body.Position).Length;
        if (step.HasArrived || remaining <= ArrivalDistance)
            context.RequestTransition(Then);
    }
}
