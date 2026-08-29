namespace Puck.NowPlaying;

/// 초를 시계가 읽는 대로.
///
/// 뷰 안의 포맷터가 아니라 자기 파일인 이유는, 곤란한 경우들 — 길이를
/// 보고하지 않는 스트림, 앱이 탐색 중에 잠깐 음수로 보고하는 위치 — 을
/// 진행 막대 밑에서 "-1:-3"으로 발견하는 대신 테스트로 못 박아 두는 편이
/// 낫기 때문이다.
public static class TrackTime
{
    /// `m:ss`, 한 시간이 넘으면 `h:mm:ss`.
    ///
    /// 0보다 작은 것은 0으로 읽는다. 미디어 앱은 탐색하는 한 프레임 동안
    /// 음수 위치를 보고할 수 있고, 그것은 보여 줄 가치가 없다.
    public static string Text(TimeSpan time)
    {
        if (time <= TimeSpan.Zero) return "0:00";

        var total = (int)Math.Floor(time.TotalSeconds);
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }
}
