namespace Puck.NowPlaying;

/// 지금 재생 중인 것을 들고 있으면서, 바뀌면 알려 준다.
///
/// 시계를 갖고 있지 않다. `Poll`을 누가 언제 부를지는 패널이 정한다 —
/// 패널이 닫혀 있는 동안 초당 몇 번씩 시스템에 묻는 것은 아무도 보지 않는
/// 것을 위한 순전한 낭비이고, `WindowPollPolicy`가 창 목록에 대해 내린 것과
/// 같은 판단이다.
public sealed class NowPlayingStore(INowPlayingReader reader)
{
    private NowPlayingTrack? _current;

    /// 지금 재생 중인 것. 아무것도 없으면 null.
    public NowPlayingTrack? Current => _current;

    /// 값이 **실제로** 달라졌을 때만 오른다.
    ///
    /// 재생 위치는 읽을 때마다 달라지므로, 이걸 그냥 매번 올리면 패널이
    /// 초당 몇 번씩 통째로 다시 그려진다. 무엇이 달라졌는지는 받는 쪽이
    /// 판단하도록 값을 넘긴다.
    public event Action<NowPlayingTrack?>? Changed;

    /// 곡이 바뀌었을 때만 오른다. 가사나 앨범 아트처럼 트랙 하나에 한 번
    /// 가져오면 되는 것들이 여기 붙는다.
    public event Action<NowPlayingTrack?>? TrackChanged;

    public void Poll()
    {
        var next = reader.Read();

        // 제목이 없는 세션은 세션이 아니다. 미디어 키만 잡아 두고 아무것도
        // 재생하지 않는 앱이 실제로 이렇게 답한다.
        if (next is { HasSomethingToShow: false }) next = null;

        var wasTrack = _current;
        if (Equals(_current, next)) return;

        var trackChanged = next is null
            ? wasTrack is not null
            : !next.IsSameTrack(wasTrack);

        _current = next;
        Changed?.Invoke(next);
        if (trackChanged) TrackChanged?.Invoke(next);
    }
}
