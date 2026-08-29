namespace Puck.Movement;

/// 펫이 수조에 있어야 하는가, 바탕화면에 있어야 하는가.
///
/// 한 곳에서만 정한다. 채팅 창은 자기 창만 알고, 펫이 숨겨졌는지는 여기서만
/// 아는데, 두 곳에 나뉜 결정은 서로 어긋날 수 있는 결정이다.
public sealed class PetHomeDecider(Func<double> now)
{
    public enum Move { Home, Desktop }

    /// 어떤 상태가 얼마나 유지되어야 펫이 그것에 따라 움직이는가.
    /// 없으면 창을 스쳐 지나가는 Alt+Tab에도 펫이 갔다 온다.
    ///
    /// 펫이 나중에 알아챈 것이 아니라 창에 답한 것으로 읽힐 만큼은 짧아야
    /// 한다. 견뎌야 하는 것은 포커스를 스쳐 가는 창이고, 그건 한순간이지
    /// 1초 가까이가 아니다.
    public const double HoldSeconds = 0.25;

    private bool _wanted;
    private double? _wantedSince;

    /// 트레이의 숨기기. 다른 무엇보다 세다 — 숨겨진 펫은 어딘가에 있는 것이
    /// 아니라 아무 데도 없는 것이다.
    public bool IsPetHidden { get; set; }

    /// 지금 있어야 할 곳. 아직 충분히 유지되지 않았으면 null이고, 그때는
    /// 부르는 쪽이 하던 것을 계속한다.
    ///
    /// `tankIsAvailable`은 갈 수 있는 수조가 지금 있는가 — 창이 떠 있고,
    /// 그 섬이 펫을 담을 만한가.
    public Move? Decide(bool tankIsAvailable)
    {
        var wanted = tankIsAvailable && !IsPetHidden;

        if (wanted != _wanted)
        {
            _wanted = wanted;
            _wantedSince = now();
            return null;
        }

        if (_wantedSince is not { } since) return null;
        if (now() - since < HoldSeconds) return null;

        _wantedSince = null;
        return wanted ? Move.Home : Move.Desktop;
    }
}
