namespace Puck.WindowSensing;

/// 창 목록을 얼마나 자주 물어볼 값어치가 있는가.
///
/// 묻는 것은 공짜가 아니다. mac에서 돌아가는 펫을 샘플링했더니 그 한 번의
/// 호출을 초당 열 번 하는 것이 쉬고 있는 앱이 하는 일의 대부분이었다.
/// Windows의 EnumWindows도 창마다 제목과 사각형을 캐 오는 같은 종류의 일이다.
///
/// 초당 열 번은 **무슨 일이 벌어지는 동안에는** 맞는 답이다. 펫은 창 윗변에
/// 서고, 창은 아무 알림 없이 끌리거나 크기가 바뀔 수 있으므로, 걷는 펫은
/// 정말로 가장자리가 어디인지 계속 들어야 한다. 한동안 가만히 앉아 있던
/// 펫은 그렇지 않다 — 하는 일 중 그 답에 기대는 것이 없고, 다시 움직이는
/// 순간 첫 걸음이 닿기 전에 속도가 돌아온다.
///
/// puck-mac의 `WindowPollPolicy`를 옮긴 것이다. 기다릴 이벤트가 없어서
/// 구독이 아니라 느려지는 심장 박동인 것도 같은 이유다.
public sealed class WindowPollPolicy(double threshold = WindowPollPolicy.DefaultThreshold)
{
    /// 무언가 벌어지는 동안: 걷기, 떨어지기, 오르기.
    public const double ActiveHz = 10;

    /// 앱이 활성화·실행·종료된 직후 — 창 목록이 지금 막 바뀌려는 참이고,
    /// 펫은 0.1초 뒤가 아니라 그때 반응해야 한다.
    public const double BurstHz = 15;

    /// 아무것도 그 답에 기대지 않을 만큼 펫이 오래 가만히 있었을 때.
    /// 0은 아니다 — 쉬고 있는 펫이 딛은 창이 움직이는 것도 알아채야 하고,
    /// 다만 0.1초 안에 알 필요가 없을 뿐이다.
    public const double RestingHz = 2;

    /// 느려지기까지 펫이 가만히 있어야 하는 시간.
    ///
    /// 눈에 보이는 것이 여기 달려 있지 않으므로 짧게 잡는다. 이 값이 정하는
    /// 것은 쉬는 동안 발밑의 창이 움직였을 때 펫이 그것을 얼마나 빨리
    /// 알아채는가이고, 배회 스케줄러가 다음 걸음까지 기다리는 시간(8~15초)에
    /// 견주면 3초는 아무도 느끼지 못한다. 그보다 길게 잡으면 아예 걸리지
    /// 않는 문턱이 되는데, 그게 이 정책이 대신한 상태다.
    public const double DefaultThreshold = 3;

    private double _restingElapsed;

    public double Threshold { get; } = threshold;

    /// 지금 물어볼 주기.
    ///
    /// `resting`은 창 목록을 읽지 않는 상태인가(어딘가에 서서 쉬는 중이거나,
    /// 치워져 있거나). `bursting`은 방금 앱이 바뀌었는가.
    public double Hertz(bool resting, bool bursting, double dt)
    {
        if (!resting)
        {
            // 곧장 올린다. 천천히 올리면 걷기의 첫 걸음이 쉴 때의 목록을
            // 보고 내디디게 되는데, 하필 그 한 경우가 중요한 경우다.
            _restingElapsed = 0;
            return bursting ? BurstHz : ActiveHz;
        }

        // 버스트가 쉬는 것을 이긴다: 창 목록이 지금 바뀌는 중이고, 그게
        // 버스트가 있는 이유 전부다.
        if (bursting) return BurstHz;

        _restingElapsed += dt;
        return _restingElapsed >= Threshold ? RestingHz : ActiveHz;
    }
}
