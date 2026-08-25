using System.Windows;

namespace Puck.Movement.States;

/// 한 걸음을 딛고 나서 벌어지는 일. Walk와 MoveTo가 글자 그대로 같은 판정을
/// 하고 있었다 — 가두고, 화면 밖이면 서고, 발밑이 사라졌으면 떨어지고,
/// 도착했거나 막혔으면 선다.
///
/// 한곳으로 모으는 이유는 줄 수가 아니다. 착지 판정을 한 번 고칠 때 다른
/// 쪽에 옮겨 적기를 잊으면, 같은 걸음이 상태에 따라 다르게 끝난다.
public static class GroundStep
{
    /// 딛고 난 뒤 무엇을 해야 하는가.
    public enum Outcome
    {
        /// 계속 걷는다.
        Continue,
        /// 화면 밖이라 더 갈 수 없다. 부르는 쪽이 오를 곳을 찾아볼 수 있다.
        OffWorld,
        /// 발밑이 사라졌다.
        Fell,
        /// 목적지에 닿았거나 가장자리에 눌려 더 못 간다.
        Arrived,
    }

    /// `step`이 내놓은 자리로 실제로 옮기고 그 결과를 돌려준다.
    /// **Outcome이 Continue나 Arrived일 때만 몸이 움직인다** — 나머지는
    /// 그 자리에 두고 부르는 쪽이 상태를 갈아탄다.
    public static Outcome Take(MovementSolver.Step step, StateContext context)
    {
        var body = context.Body;
        var next = PetBounds.Contain(step.Position, context.VisualBounds, context.RoamableArea);

        // RoamableArea는 경계 상자라 디스플레이 사이의 빈 공간도 포함한다.
        // 그리로 한 걸음 내디디면 펫이 화면 밖으로 사라진다. 발 한 점이 아니라
        // 그림 좌우 끝을 보는 이유는, 경계에서 절반만 걸친 채 멈추면 그것도
        // "잘려 보이는" 것이기 때문이다.
        if (!context.ArtworkHasGround(next)) return Outcome.OffWorld;

        body.Position = next;

        var surfaceY = context.LandingY(body.Position);
        if (surfaceY > body.Position.Y + WindowSupport.FootTolerance) return Outcome.Fell;

        // 가장자리에 눌려 더 못 가는 경우도 도착으로 친다 — 아니면
        // 벽에 붙어 걷는 클립을 영원히 재생한다.
        var blocked = Math.Abs(body.Position.X - step.Position.X) > 0.001;
        return step.HasArrived || blocked ? Outcome.Arrived : Outcome.Continue;
    }
}
