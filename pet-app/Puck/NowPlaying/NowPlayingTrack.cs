namespace Puck.NowPlaying;

/// 지금 무엇이 재생 중인가 — 노치 패널이 필요로 하는 형태로.
///
/// mac은 이것을 음악 앱에 직접 물어본다. MediaRemote(모든 노치 유틸리티가
/// 쓰던 비공개 프레임워크)가 최근 macOS에서 서드파티에 답하지 않게 되어,
/// AppleScript로 앱 하나하나를 아는 만큼만 물어보는 길로 물러난 것이다.
///
/// Windows에는 그 자리에 지원되는 시스템 API가 있다.
/// `GlobalSystemMediaTransportControls`는 미디어 키를 받는 앱이라면 무엇이든
/// — 음악 앱이든 브라우저 탭이든 — 같은 방식으로 답한다. 그래서 mac에 있는
/// 경로 셋(음악 앱 이름으로 묻기, 브라우저를 소리로 잡아내기, 시스템에
/// 묻기) 중 여기 남는 것은 시스템 경로 하나뿐이고, mac이 그 경로에만 붙여
/// 둔 성질(어느 앱인지 이름을 밝힐 가치가 있다, 재생 위치를 보고한다)이
/// 여기서는 언제나 참이다.
public sealed record NowPlayingTrack
{
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public required string Album { get; init; }
    public required bool IsPlaying { get; init; }

    /// 트랙의 몇 초 지점인지와 전체 길이. 진행 막대에 둘 다 필요하다.
    public required TimeSpan Position { get; init; }
    public required TimeSpan Duration { get; init; }

    /// 어느 앱이 답했는가. AUMID(예: `Spotify.exe`,
    /// `MSEdge`)와 사람이 읽을 이름.
    ///
    /// mac이 이것을 들고 다니는 이유와 같다 — 시스템 경로의 요점이 "무엇이든
    /// 될 수 있다"는 것이라, 패널이 출처를 밝힐 수 있어야 한다.
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }

    /// 얼마나 지났는가, 0에서 1.
    ///
    /// 길이가 0이면 0을 준다. 나눗셈을 피하려는 것만이 아니라, 길이를
    /// 보고하지 않는 것은 생방송이거나 잘못된 답이고 그때 진행 막대는 가득
    /// 찬 것보다 빈 것이 옳기 때문이다.
    public double Progress => Duration > TimeSpan.Zero
        ? Math.Clamp(Position / Duration, 0, 1)
        : 0;

    /// 바늘이 어디 있는지를 빼고, `other`와 같은 녹음인가.
    ///
    /// 값 전체를 비교하면 위치가 매번 달라서 읽을 때마다 다른 곡이 된다.
    public bool IsSameTrack(NowPlayingTrack? other)
        => other is not null
        && Title == other.Title && Artist == other.Artist && Album == other.Album;

    /// 보여 줄 것이 있는가. 제목이 없는 세션은 세션이 아니다 — 미디어 키를
    /// 잡아 두었을 뿐 아무것도 재생하지 않는 앱이 실제로 이렇게 답한다.
    public bool HasSomethingToShow => !string.IsNullOrWhiteSpace(Title);
}
