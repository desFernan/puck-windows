using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Puck.Diagnostics;

namespace Puck.Avatar;

/// 클립 하나 = PNG 하나. 실제 그리기는 Overlay/SpriteView가 하고,
/// 여기는 "지금 무엇을 어떤 크기로 어느 방향으로 그려야 하는가"만 갖는다.
public sealed class SpriteAvatar : IAvatarPlayable
{
    private readonly AvatarLoadResult _load;
    private readonly Dictionary<string, BitmapSource> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AlphaHitMask> _masks = new(StringComparer.Ordinal);

    private string _currentClip = "idle";

    public SpriteAvatar(AvatarLoadResult load, string packageDirectory)
    {
        _load = load;
        PackageDirectory = packageDirectory;
        DesktopSize = SizeFor(load.Manifest.Hitbox);
    }

    /// 그려지는 크기의 몇 배로 디코딩할지. 1배로 딱 맞추면 스쿼시&스트레치로
    /// 늘어나는 프레임과 나중의 확대에서 부족해진다. 2배면 원본 픽셀의 5%만
    /// 남으면서도 여유가 있다.
    private const double Supersample = 2.0;

    public string PackageDirectory { get; }

    /// 이 아바타의 매니페스트. 사운드 표처럼 같은 패키지를 보는 것들이 쓴다.
    /// (이름이 Load가 아닌 이유는 아래 정적 팩토리와 겹치기 때문이다.)
    public AvatarManifest Manifest => _load.Manifest;

    /// 디코딩할 가로 픽셀 수. 원본이 이보다 작으면 WIC이 늘려서 디코딩하는데,
    /// 그 경우 메모리 몇백 KB를 더 쓸 뿐이라 원본 크기를 미리 읽어 보는
    /// (파일을 한 번 더 여는) 비용을 치를 값어치가 없다.
    /// 바탕화면 크기를 기준으로 디코딩한다 — 수조에서 작아진 크기로 재면,
    /// 그 그림이 캐시에 남은 채로 펫이 다시 커졌을 때 흐려진다.
    private int DecodeWidth => Math.Max(1, (int)Math.Ceiling(DesktopSize.Width * Supersample));

    /// 바탕화면에서 펫이 서는 키. **앱이 정한다.**
    ///
    /// 매니페스트의 hitbox를 그대로 화면 크기로 쓰면, 펫이 얼마나 크게
    /// 나오는지를 패키지가 저 좋을 대로 고른 숫자가 정하게 된다 — 130x133로
    /// 그린 것과 251x300으로 그린 것이 같은 설정에서 머리 하나만큼 차이가
    /// 난다. 패키지의 무엇도 펫의 크기를 정해서는 안 된다. 그건 앱과
    /// 사람의 일이다.
    public const double DefaultHeight = 133;

    /// 바탕화면에서의 크기.
    public Size DesktopSize { get; }

    /// hitbox는 이름 그대로 **모양**으로만 쓴다 — 비율을 주고, 크기는 앱이 준다.
    ///
    /// 긴 쪽을 맞춘다. 높이만 맞추면 10:1로 적어 낸 패키지가 바탕화면을
    /// 가로지르는 현수막이 된다.
    public static Size SizeFor(Hitbox hitbox)
    {
        var longest = Math.Max(hitbox.Width, hitbox.Height);
        if (longest <= 0) return new Size(DefaultHeight, DefaultHeight);

        var scale = DefaultHeight / longest;
        return new Size(hitbox.Width * scale, hitbox.Height * scale);
    }

    /// 지금 크기를 바탕화면 크기의 몇 배로 그릴 것인가.
    ///
    /// 섬은 90점짜리 상자라 바탕화면의 펫이 그대로 들어가지 않는다. 매니페스트를
    /// 다시 읽는 대신 여기서 줄인다 — 그림은 이미 그려지는 크기의 두 배로
    /// 디코딩해 두므로(`Supersample`), 작아지는 쪽으로는 다시 읽을 것이 없다.
    public double RuntimeScale
    {
        get => _runtimeScale;
        set => _runtimeScale = value > 0 ? value : 1;
    }

    private double _runtimeScale = 1;

    /// 실제로 그려지고 클릭되는 크기.
    public Size Size => new(DesktopSize.Width * RuntimeScale, DesktopSize.Height * RuntimeScale);

    public Point Position { get; private set; }
    public AvatarFacing Facing { get; private set; } = AvatarFacing.Right;
    public bool UpsideDown { get; private set; }
    public double BounceScaleY { get; private set; } = 1.0;

    public BitmapSource? CurrentImage => Image(_currentClip);

    /// 매니페스트에 bounce_intensity가 있으면 그 값, 없으면 앱의 기본값.
    public double BounceIntensityOrDefault =>
        _load.Manifest.BounceIntensity ?? Movement.CharacterBody.DefaultBounceIntensity;

    /// 지금 재생 중인 클립 키. UpdateBounce에 넘길 값이다.
    public string CurrentClipKey => _currentClip;

    /// 접지점 기준. 대칭을 가정하지 않고 hitbox 폭의 절반만 왼쪽으로 민다.
    public Rect VisualBounds => new(-Size.Width / 2, -Size.Height, Size.Width, Size.Height);

    public static SpriteAvatar Load(string avatarDirectory)
        => new(AvatarLoader.Load(avatarDirectory), avatarDirectory);

    public void SetScreenPosition(Point position) => Position = position;
    public void SetFacing(AvatarFacing facing) => Facing = facing;
    public void SetUpsideDown(bool upsideDown) => UpsideDown = upsideDown;

    public void Play(string clip, bool loop) => _currentClip = clip;
    public void Stop() { }

    /// 정지 그림에 얹는 스쿼시&스트레치. 애니메이션 프레임이 없는 아바타에
    /// pet-app이 직접 주는 움직임이고, intensity 0이면 아무 일도 없다.
    public void UpdateBounce(string clip, TimeSpan elapsed, double intensity)
    {
        if (intensity <= 0) { BounceScaleY = 1.0; return; }
        const double frequency = 3.2; // Hz
        var phase = Math.Sin(elapsed.TotalSeconds * frequency * 2 * Math.PI);
        BounceScaleY = 1.0 + phase * 0.06 * intensity;
    }

    public void TriggerJump() { /* Overlay/SpriteView가 소비하는 일회성 신호 — Task 16 */ }

    public bool HitTest(Point relativeToPosition, double tolerance)
    {
        var image = Image(_currentClip);
        if (image is null) return false;

        var bounds = VisualBounds;
        if (!bounds.Contains(relativeToPosition) &&
            !Rect.Inflate(bounds, tolerance, tolerance).Contains(relativeToPosition))
            return false;

        // 그린 크기(Size)에서 이미지 픽셀 좌표로 되돌린다. 좌우 반전이면
        // X를 접어야 마스크가 실제로 보이는 실루엣과 맞는다.
        var localX = relativeToPosition.X - bounds.X;
        if (Facing == AvatarFacing.Left) localX = bounds.Width - localX;
        var localY = relativeToPosition.Y - bounds.Y;
        if (UpsideDown) localY = bounds.Height - localY;

        var px = (int)(localX / bounds.Width * image.PixelWidth);
        var py = (int)(localY / bounds.Height * image.PixelHeight);
        var pixelTolerance = (int)Math.Ceiling(tolerance / bounds.Width * image.PixelWidth);

        return Mask(_currentClip)?.Contains(px, py, pixelTolerance) ?? false;
    }

    private BitmapSource? Image(string clip)
    {
        if (_images.TryGetValue(clip, out var cached)) return cached;

        var stem = AvatarLoader.ResolveClipStem(clip, _load);
        if (stem is null) return null;

        var path = AvatarPackagePath.ResolveFile(PackageDirectory, stem + ".png");
        if (path is null || !File.Exists(path))
        {
            AppLogger.Warning("avatar", "클립 이미지를 찾지 못했습니다",
                new Dictionary<string, object?> { ["clip"] = clip, ["stem"] = stem });
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path);
        // 파일을 잠그지 않는다 — 그림을 다시 그린 사람이 앱을 끄지 않고도
        // "아바타 다시 불러오기"를 누를 수 있어야 한다.
        image.CacheOption = BitmapCacheOption.OnLoad;
        // 원본은 그려지는 크기의 열 배쯤 된다(1200px 그림을 130px로). 클립 하나를
        // 원본 크기로 들고 있으면 5.9MB이고, 여기 캐시에 클립이 쌓일수록 그만큼
        // 늘어난다 — 늘 떠 있는 트레이 앱이 낼 값이 아니다. 디코딩할 때 한 번
        // 줄여서 들고 있는다. 프레임당 CPU는 재 봤을 때 차이가 없었으니(20초에
        // 1.5초 대 2.0초, 측정 편차 안) 이건 순전히 메모리를 위한 것이다.
        image.DecodePixelWidth = DecodeWidth;
        image.EndInit();
        image.Freeze();

        _images[clip] = image;
        return image;
    }

    private AlphaHitMask? Mask(string clip)
    {
        if (_masks.TryGetValue(clip, out var cached)) return cached;
        var image = Image(clip);
        if (image is null) return null;
        var mask = AlphaHitMask.From(image);
        _masks[clip] = mask;
        return mask;
    }
}
