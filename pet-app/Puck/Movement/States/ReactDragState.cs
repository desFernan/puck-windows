using System.Windows;

namespace Puck.Movement.States;

/// 끌려다니는 동안. 물리는 없다 — 커서를 그대로 따라간다.
/// 놓이는 순간의 속도는 CharacterBody.LaunchVelocity에 실려 FallState로 넘어간다.
public sealed class ReactDragState : IStateHandler
{
    public string Name => "ReactDrag";
    public string ClipKey => "react_drag";
    public bool LoopsClip => true;

    /// 제스처 인식기가 매 이동마다 갱신한다.
    public Point? DragPosition { get; set; }

    public void Enter() => DragPosition = null;

    public void Update(double dt, StateContext context)
    {
        if (DragPosition is not { } position) return;

        // 끌려가는 중에도 화면 밖으로는 못 나간다.
        context.Body.Position = PetBounds.Contain(position, context.VisualBounds, context.RoamableArea);
    }
}
