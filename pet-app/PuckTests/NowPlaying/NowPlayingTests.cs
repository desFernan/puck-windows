using Puck.NowPlaying;

namespace PuckTests.NowPlaying;

public class TrackTimeTests
{
    [Fact]
    public void 분과_초로_읽는다()
    {
        Assert.Equal("0:00", TrackTime.Text(TimeSpan.Zero));
        Assert.Equal("0:07", TrackTime.Text(TimeSpan.FromSeconds(7)));
        Assert.Equal("3:04", TrackTime.Text(TimeSpan.FromSeconds(184)));
    }

    [Fact]
    public void 한_시간이_넘으면_시간을_붙인다()
    {
        Assert.Equal("1:00:00", TrackTime.Text(TimeSpan.FromHours(1)));
        Assert.Equal("2:03:04", TrackTime.Text(new TimeSpan(2, 3, 4)));
    }

    /// 미디어 앱은 탐색하는 한 프레임 동안 음수 위치를 보고할 수 있다.
    /// 진행 막대 밑에서 "-1:-3"을 발견하는 대신 여기서 못 박는다.
    [Fact]
    public void 음수는_0으로_읽는다()
    {
        Assert.Equal("0:00", TrackTime.Text(TimeSpan.FromSeconds(-5)));
    }

    /// 내림이다 — 반올림하면 1.9초가 0:02로 보이고, 그러면 시계가 트랙보다
    /// 앞서간다.
    [Fact]
    public void 초는_내림이다()
    {
        Assert.Equal("0:01", TrackTime.Text(TimeSpan.FromSeconds(1.9)));
    }
}

public class NowPlayingModelTests
{
    private static NowPlayingTrack Track(string title = "곡", string artist = "가수",
                                    string album = "앨범",
                                    double position = 30, double duration = 180)
        => new()
        {
            Title = title,
            Artist = artist,
            Album = album,
            IsPlaying = true,
            Position = TimeSpan.FromSeconds(position),
            Duration = TimeSpan.FromSeconds(duration),
            SourceId = "Spotify.exe",
            SourceName = "Spotify",
        };

    [Fact]
    public void 진행률은_0에서_1이다()
    {
        Assert.Equal(1.0 / 6, Track().Progress, 6);
    }

    /// 길이를 보고하지 않는 것은 생방송이거나 잘못된 답이고, 그때 진행
    /// 막대는 가득 찬 것보다 빈 것이 옳다.
    [Fact]
    public void 길이가_0이면_진행률도_0이다()
    {
        Assert.Equal(0, Track(duration: 0).Progress);
    }

    [Fact]
    public void 진행률은_범위를_벗어나지_않는다()
    {
        Assert.Equal(1, Track(position: 500, duration: 180).Progress);
        Assert.Equal(0, Track(position: -5, duration: 180).Progress);
    }

    /// 값 전체를 비교하면 위치가 매번 달라 읽을 때마다 다른 곡이 된다.
    [Fact]
    public void 위치가_달라도_같은_곡이다()
    {
        Assert.True(Track(position: 10).IsSameTrack(Track(position: 90)));
    }

    [Fact]
    public void 제목이_다르면_다른_곡이다()
    {
        Assert.False(Track().IsSameTrack(Track(title: "다른 곡")));
        Assert.False(Track().IsSameTrack(null));
    }

    /// 미디어 키만 잡아 두고 아무것도 재생하지 않는 앱이 실제로 이렇게 답한다.
    [Fact]
    public void 제목이_없으면_보여_줄_것이_없다()
    {
        Assert.False(Track(title: "").HasSomethingToShow);
        Assert.False(Track(title: "   ").HasSomethingToShow);
        Assert.True(Track().HasSomethingToShow);
    }
}

public class NowPlayingSourceNameTests
{
    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("chrome.exe", "chrome")]
    [InlineData("MSEdge", "MSEdge")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "ZuneMusic")]
    [InlineData("", "")]
    public void AUMID에서_읽을_이름을_만든다(string id, string expected)
    {
        Assert.Equal(expected, SystemNowPlayingReader.FriendlyName(id));
    }
}

public class NowPlayingStoreTests
{
    private sealed class FakeReader : INowPlayingReader
    {
        public NowPlayingTrack? Next { get; set; }
        public int Reads { get; private set; }

        public NowPlayingTrack? Read()
        {
            Reads++;
            return Next;
        }
    }

    private static NowPlayingTrack Track(string title, double position = 0)
        => new()
        {
            Title = title,
            Artist = "가수",
            Album = "앨범",
            IsPlaying = true,
            Position = TimeSpan.FromSeconds(position),
            Duration = TimeSpan.FromSeconds(180),
            SourceId = "Spotify.exe",
            SourceName = "Spotify",
        };

    [Fact]
    public void 시계를_갖고_있지_않다()
    {
        var reader = new FakeReader();
        _ = new NowPlayingStore(reader);

        // 만들기만 해서는 아무것도 묻지 않는다 — 언제 물을지는 패널이 정한다.
        Assert.Equal(0, reader.Reads);
    }

    [Fact]
    public void 값이_바뀌면_알린다()
    {
        var reader = new FakeReader { Next = Track("곡") };
        var store = new NowPlayingStore(reader);
        var changes = new List<NowPlayingTrack?>();
        store.Changed += changes.Add;

        store.Poll();

        Assert.Single(changes);
        Assert.Equal("곡", store.Current!.Title);
    }

    /// 위치는 읽을 때마다 달라진다. 그냥 매번 올리면 패널이 초당 몇 번씩
    /// 통째로 다시 그려진다.
    [Fact]
    public void 같은_값이면_알리지_않는다()
    {
        var reader = new FakeReader { Next = Track("곡") };
        var store = new NowPlayingStore(reader);
        store.Poll();

        var changes = 0;
        store.Changed += _ => changes++;
        store.Poll();
        store.Poll();

        Assert.Equal(0, changes);
    }

    [Fact]
    public void 위치만_달라져도_알린다()
    {
        var reader = new FakeReader { Next = Track("곡", position: 0) };
        var store = new NowPlayingStore(reader);
        store.Poll();

        var changes = 0;
        store.Changed += _ => changes++;
        reader.Next = Track("곡", position: 5);
        store.Poll();

        Assert.Equal(1, changes);
    }

    /// 가사나 앨범 아트처럼 트랙 하나에 한 번 가져오면 되는 것들이 여기
    /// 붙는다. 위치가 흐를 때마다 다시 가져오면 안 된다.
    [Fact]
    public void 곡이_바뀔_때만_트랙_변경을_알린다()
    {
        var reader = new FakeReader { Next = Track("곡", position: 0) };
        var store = new NowPlayingStore(reader);
        var tracks = new List<NowPlayingTrack?>();
        store.TrackChanged += tracks.Add;

        store.Poll();                                   // 첫 곡
        reader.Next = Track("곡", position: 5);
        store.Poll();                                   // 위치만 흘렀다
        reader.Next = Track("다음 곡");
        store.Poll();                                   // 곡이 바뀌었다

        Assert.Equal(2, tracks.Count);
        Assert.Equal("곡", tracks[0]!.Title);
        Assert.Equal("다음 곡", tracks[1]!.Title);
    }

    [Fact]
    public void 재생이_끝나면_null이_된다()
    {
        var reader = new FakeReader { Next = Track("곡") };
        var store = new NowPlayingStore(reader);
        store.Poll();

        var tracks = new List<NowPlayingTrack?>();
        store.TrackChanged += tracks.Add;
        reader.Next = null;
        store.Poll();

        Assert.Null(store.Current);
        Assert.Single(tracks);
        Assert.Null(tracks[0]);
    }

    /// 제목이 없는 세션은 세션이 아니다.
    [Fact]
    public void 제목이_없는_세션은_없는_것으로_친다()
    {
        var reader = new FakeReader { Next = Track("") };
        var store = new NowPlayingStore(reader);

        store.Poll();

        Assert.Null(store.Current);
    }
}
