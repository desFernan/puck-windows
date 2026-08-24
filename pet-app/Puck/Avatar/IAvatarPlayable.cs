using System.Windows;

namespace Puck.Avatar;

public enum AvatarFacing { Right, Left }

/// FSM이 보는 아바타. 어떤 상태도 렌더러와 직접 말하지 않는다는 게
/// 원본의 규칙이고(F2: FSM은 아바타 타입을 몰라야 한다), 이 인터페이스가
/// 그 경계다.
public interface IAvatarPlayable
{
    void SetScreenPosition(Point position);
    void SetFacing(AvatarFacing facing);
    void SetUpsideDown(bool upsideDown);

    /// 접지점(발밑) 기준 상대 사각형. 좌우 대칭 그림이면 X = -너비/2.
    Rect VisualBounds { get; }

    bool HitTest(Point relativeToPosition, double tolerance);

    void Play(string clip, bool loop);
    void Stop();
    void UpdateBounce(string clip, TimeSpan elapsed, double intensity);
    void TriggerJump();
}
