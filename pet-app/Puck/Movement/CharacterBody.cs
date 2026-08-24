using System.Windows;
using Puck.Avatar;

namespace Puck.Movement;

/// 펫의 위치와 방향, 그리고 그 둘을 아바타로 밀어 넣는 유일한 곳.
///
/// 어떤 상태도 렌더러와 직접 말하지 않는다는 규칙(FSM은 아바타 타입을
/// 몰라야 한다)이 여기서 지켜진다.
public sealed class CharacterBody
{
    /// 매니페스트에 bounce_intensity가 없을 때 pet-app 자신의 기본값.
    public const double DefaultBounceIntensity = 0.6;

    private readonly IAvatarPlayable _avatar;
    private readonly double _bounceIntensity;

    private Point _position;
    private AvatarFacing _facing;
    private bool _isUpsideDown;

    public CharacterBody(IAvatarPlayable avatar, Point position,
                         AvatarFacing facing = AvatarFacing.Right,
                         double bounceIntensity = DefaultBounceIntensity)
    {
        _avatar = avatar;
        _position = position;
        _facing = facing;
        _bounceIntensity = bounceIntensity;
        avatar.SetScreenPosition(position);
    }

    public Point Position
    {
        get => _position;
        set { _position = value; _avatar.SetScreenPosition(value); }
    }

    /// 같은 방향을 다시 쓰는 건 아무 일도 하지 않는다 — FSM이 걷는 동안
    /// 매 프레임 이걸 쓰기 때문이다.
    public AvatarFacing Facing
    {
        get => _facing;
        set
        {
            if (_facing == value) return;
            _facing = value;
            _avatar.SetFacing(value);
        }
    }

    /// 다음에 이걸 소화할 수 있는 상태(오늘은 FallState뿐)가 첫 프레임에
    /// 읽고 지우는 일회성 발사 충격량, px/sec. 던져진 속도는 그걸 측정한
    /// 드래그 상태보다 오래 살아야 한다. 0이면 그냥 떨어뜨린 것이라,
    /// Fall로 들어오는 다른 모든 경로는 영향을 받지 않는다.
    public Vector LaunchVelocity { get; set; }

    public bool IsUpsideDown
    {
        get => _isUpsideDown;
        set
        {
            if (_isUpsideDown == value) return;
            _isUpsideDown = value;
            _avatar.SetUpsideDown(value);
        }
    }

    /// 아바타에서 그대로 전달 — FSM이 렌더러와 말하지 않게 하는 규칙 그대로.
    public Rect VisualBounds => _avatar.VisualBounds;

    public bool HitTest(Point relativeToPosition, double tolerance)
        => _avatar.HitTest(relativeToPosition, tolerance);

    public void Play(string clip, bool loop) => _avatar.Play(clip, loop);
    public void Stop() => _avatar.Stop();

    public void UpdateBounce(string clip, TimeSpan elapsed)
        => _avatar.UpdateBounce(clip, elapsed, _bounceIntensity);

    public void TriggerJump() => _avatar.TriggerJump();
}
