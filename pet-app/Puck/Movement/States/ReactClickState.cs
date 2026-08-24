namespace Puck.Movement.States;

/// 눌린 것에 대한 반응. 한 번 재생하고 Idle로 돌아간다.
public sealed class ReactClickState : IStateHandler
{
    public const double Duration = 0.4;

    private double _elapsed;

    public string Name => "ReactClick";
    public string ClipKey => "react_click";

    /// 연달아 누르면 연달아 재생돼야 한다.
    public bool RestartsOnReentry => true;

    public void Enter() => _elapsed = 0;

    public void Update(double dt, StateContext context)
    {
        _elapsed += dt;
        if (_elapsed >= Duration)
            context.RequestTransition(StateKind.Idle);
    }
}
