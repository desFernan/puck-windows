using Puck.Diagnostics;
using Windows.Media.Control;

namespace Puck.NowPlaying;

/// 이 기계가 무엇을 재생 중인지 한 번 묻는 것.
///
/// 인터페이스인 이유는 저장소를 재생 중인 것 없이도 시험하기 위해서다 —
/// 실제 답은 이 기계에서 지금 무엇이 열려 있느냐에 달려 있어 테스트가
/// 붙잡을 수 있는 것이 아니다.
public interface INowPlayingReader
{
    /// 지금 재생 중인 것, 아무것도 없으면 null.
    NowPlayingTrack? Read();
}

/// `GlobalSystemMediaTransportControls`에 묻는 판.
///
/// mac이 MediaRemote를 쓰던 자리다. 다만 이쪽은 지원되는 공개 API이고,
/// 미디어 키를 받는 앱이라면 무엇이든 — 음악 앱이든 브라우저 탭이든 —
/// 같은 방식으로 답한다. 그래서 mac이 앱 이름을 하나하나 알아야 했던 것과
/// 달리 여기에는 아는 앱 목록이 없다.
///
/// 세션 관리자를 한 번 얻어 들고 있는다. 매번 새로 얻으면 WinRT 쪽에서
/// 비동기 왕복이 한 번씩 더 생기는데, 이걸 부르는 것은 패널이 열려 있는
/// 동안의 타이머라 초당 여러 번이다.
public sealed class SystemNowPlayingReader : INowPlayingReader
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private bool _unavailable;

    public NowPlayingTrack? Read()
    {
        var session = CurrentSession();
        if (session is null) return null;

        try
        {
            var media = session.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();
            if (media is null) return null;

            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            // 끝에서 시작을 뺀다. 0에서 시작한다고 가정하면 안 된다 —
            // 방송이나 챕터가 있는 것은 시작이 0이 아닌 구간을 보고한다.
            var duration = timeline.EndTime - timeline.StartTime;
            var position = timeline.Position - timeline.StartTime;

            var source = session.SourceAppUserModelId ?? string.Empty;
            return new NowPlayingTrack
            {
                Title = media.Title ?? string.Empty,
                Artist = media.Artist ?? string.Empty,
                Album = media.AlbumTitle ?? string.Empty,
                IsPlaying = playback.PlaybackStatus
                    == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                // 음수 위치는 탐색 중인 한 프레임의 답이다. TrackTime이
                // 0으로 읽지만, 진행 막대까지 음수를 보게 두지는 않는다.
                Position = position < TimeSpan.Zero ? TimeSpan.Zero : position,
                Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration,
                SourceId = source,
                SourceName = FriendlyName(source),
            };
        }
        catch (Exception ex)
        {
            // 세션은 읽는 도중에 사라질 수 있다 — 곡을 끄면 그렇다. 그건
            // 고장이 아니라 "지금 재생 중인 것이 없다"이다.
            AppLogger.Log(LogLevel.Debug, "nowplaying", "재생 정보를 읽지 못했습니다",
                new Dictionary<string, object?> { ["error"] = ex.Message });
            return null;
        }
    }

    private GlobalSystemMediaTransportControlsSession? CurrentSession()
    {
        if (_unavailable) return null;

        try
        {
            _manager ??= GlobalSystemMediaTransportControlsSessionManager
                .RequestAsync().GetAwaiter().GetResult();
            return _manager?.GetCurrentSession();
        }
        catch (Exception ex)
        {
            // 이 기계에서는 쓸 수 없다(정책으로 껐거나, 너무 낮은 버전이거나).
            // 한 번만 적고 다시는 시도하지 않는다 — 패널이 열려 있는 동안
            // 초당 여러 번 실패를 기록하게 두면 안 된다.
            _unavailable = true;
            AppLogger.Warning("nowplaying", "시스템 재생 정보를 쓸 수 없습니다",
                new Dictionary<string, object?> { ["error"] = ex.Message });
            return null;
        }
    }

    /// AUMID에서 사람이 읽을 만한 이름을 만든다.
    ///
    /// 제대로 하려면 셸에 등록된 앱 이름을 물어야 하지만, 그 왕복을 타이머
    /// 안에서 하기에는 비싸다. 실제로 보이는 것은 `Spotify.exe`,
    /// `MSEdge`, `Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic`
    /// 같은 것들이라, 확장자와 패키지 꼬리를 떼는 것만으로 대부분 읽힌다.
    /// 못 알아보겠으면 원래 문자열을 그대로 둔다 — 이름을 지어내는 것보다
    /// 낫다.
    public static string FriendlyName(string appUserModelId)
    {
        if (string.IsNullOrWhiteSpace(appUserModelId)) return string.Empty;

        // 패키지 AUMID는 `PackageFamily!AppId` 꼴이다. 뒤쪽 AppId가
        // 앱 이름에 더 가깝다.
        var bang = appUserModelId.LastIndexOf('!');
        var name = bang >= 0 && bang < appUserModelId.Length - 1
            ? appUserModelId[(bang + 1)..]
            : appUserModelId;

        // 확장자를 먼저 뗀다. 마지막 점으로 먼저 자르면 `Spotify.exe`가
        // `exe`가 된다.
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];

        // `Microsoft.ZuneMusic` -> `ZuneMusic`.
        var dot = name.LastIndexOf('.');
        if (dot >= 0 && dot < name.Length - 1) name = name[(dot + 1)..];

        return name;
    }
}
