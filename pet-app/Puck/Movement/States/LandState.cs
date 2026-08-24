namespace Puck.Movement.States;

/// 착지 자세를 한 번 재생하고 Idle로 돌아간다.
public sealed class LandState : IStateHandler
{
    /// land 클립의 길이. 정지 그림 아바타에도 착지가 한 박자 보이도록
    /// 하는 최소 시간이다.
    public const double Duration = 0.35;

    private double _elapsed;

    public string Name => "Land";
    public string ClipKey => "land";

    /// 튕겨서 두 번 착지하면 두 번 재생돼야 한다.
    public bool RestartsOnReentry => true;

    public void Enter() => _elapsed = 0;

    public void Update(double dt, StateContext context)
    {
        _elapsed += dt;
        if (_elapsed >= Duration)
            context.RequestTransition(StateKind.Idle);
    }
}
