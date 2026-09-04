using System.Windows;
using System.Windows.Media;
using Puck.Avatar;

namespace Puck.Overlay;

/// 지금 그려야 할 변환. 자세의 방향(`AvatarPoseOrientation`)에 스쿼시&스트레치를
/// 곱해 넣는다.
///
/// 방향 자체는 여기서 정하지 않는다 — 그건 미리보기도 물어야 하는 것이고,
/// 두 곳에서 각자 합치면 보정이 뒤집기와 회전을 함께 쓰는 순간 어긋난다.
public static class SpriteTransform
{
    /// 스쿼시&스트레치가 그림을 뒤집는 일이 없게 하는 하한.
    public const double MinimumBounceScale = 0.05;

    public static (double ScaleX, double ScaleY, double Rotation) For(
        AvatarFacing facing, bool upsideDown, double bounceScaleY,
        string clip = "walk", AvatarPoseAdjustment? adjustment = null)
    {
        var orientation = AvatarPoseOrientation.Of(
            AvatarPoseOrientation.PoseOf(facing, upsideDown, clip), adjustment);

        var bounce = Math.Max(MinimumBounceScale, bounceScaleY);
        return (orientation.ScaleX, orientation.ScaleY * bounce, orientation.Rotation);
    }
}

/// 스프라이트 하나를 그리는 것 말고는 아무것도 하지 않는다.
public sealed class SpriteView : FrameworkElement
{
    public SpriteView()
    {
        // 스프라이트는 원본보다 한참 작게 그려진다(1200px 그림을 130px로).
        // WPF 기본값인 이중선형 보간은 축소할 때 2×2 이웃만 보기 때문에
        // 가장자리가 계단처럼 서고 움직일 때 반짝인다. Fant는 줄어드는 만큼의
        // 면적을 평균 내므로 축소에 맞는 방식이다.
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    public SpriteAvatar? Avatar { get; set; }

    /// 이 뷰가 올라가 있는 오버레이 창의 좌상단이 가상 화면 어디인가.
    public Point OriginInVirtualScreen { get; set; }

    /// 물리 픽셀 → DIP 환산 배율. 창이 다른 배율의 모니터로 넘어가면 바뀐다.
    public double DpiScale { get; set; } = 1.0;

    /// 지금 자세에 걸린 보정. 없으면 보정 없음.
    public Func<SpriteAvatar, AvatarPoseAdjustment?>? Adjustment { get; set; }

    public void Invalidate() => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        var avatar = Avatar;
        var image = avatar?.CurrentImage;
        if (avatar is null || image is null) return;

        // 펫의 절대 좌표를 창 안의 좌표로 옮기고, 물리 픽셀을 DIP로 바꾼다.
        var localX = (avatar.Position.X - OriginInVirtualScreen.X) / DpiScale;
        var localY = (avatar.Position.Y - OriginInVirtualScreen.Y) / DpiScale;

        var width = avatar.Size.Width / DpiScale;
        var height = avatar.Size.Height / DpiScale;

        var (scaleX, scaleY, rotation) = SpriteTransform.For(
            avatar.Facing, avatar.UpsideDown, avatar.BounceScaleY,
            avatar.CurrentClipKey, Adjustment?.Invoke(avatar));

        // 반전과 스쿼시의 기준점은 접지점(발밑) — 여기를 중심으로 잡지 않으면
        // 뒤집을 때 펫이 옆으로 튀고, 스쿼시할 때 바닥에서 뜬다.
        drawingContext.PushTransform(new TranslateTransform(localX, localY));
        drawingContext.PushTransform(new ScaleTransform(scaleX, scaleY));

        var turned = rotation != 0;
        if (turned) drawingContext.PushTransform(new RotateTransform(rotation * 180 / Math.PI));

        drawingContext.DrawImage(image, new Rect(-width / 2, -height, width, height));

        if (turned) drawingContext.Pop();
        drawingContext.Pop();
        drawingContext.Pop();
    }
}
