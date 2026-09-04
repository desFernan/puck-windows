using System.Windows;
using Puck.Avatar;
using Puck.Movement.States;

namespace Puck.Movement;

/// 펫이 어디 사는가 — 바탕화면인가, 채팅 창의 섬인가.
///
/// 수조가 어디 있는지(`TankResidency`), 언제 옮겨야 하는지(`PetHomeDecider`),
/// 어떻게 건너가는지(`TravelState`) 셋이 한 생각이라 함께 둔다. 셋을 배선하는
/// 파일에 흩어 두면, 하나만 고쳤을 때 나머지 둘이 그것을 모르는 상태가 된다.
public sealed class PetHome(Func<double> now)
{
    private readonly TankResidency _residency = new();
    private readonly PetHomeDecider _decider = new(now);

    /// 둘 사이를 나는 상태. 컨트롤러에 등록해야 해서 밖에서 볼 수 있다.
    public TravelState Travel { get; } = new();

    /// 지금 갈 수 있는 수조. 없으면 갈 곳이 없다.
    public Rect? Area => _residency.Area;

    /// 펫이 그 안에 사는가.
    public bool IsHome => _residency.IsHome;

    /// 창이 섬 자리를 알려 왔다. 담을 수 없는 수조는 여기서 거절되고,
    /// 그러면 갈 곳이 없는 것과 같다.
    ///
    /// 이미 그 안에 사는 펫은 새 모양에 맞춰 다시 앉힌다 — 창을 끌거나
    /// 섬을 접으면 발밑이 통째로 옮겨 간다.
    public void Report(Rect? frame, Rect screen, SpriteAvatar avatar, CharacterBody body)
    {
        var scale = _residency.ScaleFor(avatar.DesktopSize);
        var scaled = new Size(avatar.DesktopSize.Width * scale, avatar.DesktopSize.Height * scale);

        _residency.Report(frame, screen, scaled);

        if (!_residency.IsHome || _residency.Area is not { } tank) return;

        avatar.RuntimeScale = _residency.ScaleFor(avatar.DesktopSize);
        body.Position = new Point(Math.Clamp(body.Position.X, tank.Left, tank.Right), tank.Bottom);
    }

    /// 이 프레임에 옮겨야 하는가. 옮긴다면 요청할 상태를 돌려준다.
    ///
    /// 숨겨진 펫은 어디에도 있지 않으므로 그 사실이 먼저다.
    public StateKind? Decide(bool petIsHidden, SpriteAvatar avatar, CharacterBody body, ScreenSpace? screens)
    {
        _decider.IsPetHidden = petIsHidden;

        if (_decider.Decide(_residency.Area is not null) is not { } move) return null;

        if (move == PetHomeDecider.Move.Home && !_residency.IsHome)
        {
            _residency.IsHome = true;

            // 떠날 때 바로 작아진다. 상자에 들어가는 크기가 되는 것이 출발의
            // 일부로 읽히고, 도착에 맞춰 줄이려면 도착을 알려 주는 자리가
            // 하나 더 필요하다.
            avatar.RuntimeScale = _residency.ScaleFor(avatar.DesktopSize);

            // 목적지를 매 프레임 다시 묻는다 — 나는 중에 창을 끌면 수조가
            // 함께 움직인다.
            Travel.Destination = _residency.StandingPoint;
            Travel.Then = StateKind.Idle;
            return StateKind.Travel;
        }

        if (move == PetHomeDecider.Move.Desktop && _residency.IsHome)
        {
            _residency.IsHome = false;
            avatar.RuntimeScale = 1;

            var floor = screens is { } space
                ? new Point(body.Position.X, space.FloorY(body.Position))
                : body.Position;

            Travel.Destination = () => floor;
            Travel.Then = StateKind.Idle;
            return StateKind.Travel;
        }

        return null;
    }
}
