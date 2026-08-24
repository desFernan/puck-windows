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
        Size = new Size(load.Manifest.Hitbox.Width * load.Manifest.Scale,
                        load.Manifest.Hitbox.Height * load.Manifest.Scale);
    }

    public string PackageDirectory { get; }

    /// manifest hitbox × scale — 그려지고 클릭되는 크기, 포인트 단위.
    public Size Size { get; }

    public Point Position { get; private set; }
    public AvatarFacing Facing { get; private set; } = AvatarFacing.Right;
    public bool UpsideDown { get; private set; }
    public double BounceScaleY { get; private set; } = 1.0;

    public BitmapSource? CurrentImage => Image(_currentClip);

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
