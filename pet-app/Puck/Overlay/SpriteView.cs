using System.Windows;
using System.Windows.Media;
using Puck.Avatar;

namespace Puck.Overlay;

/// 지금 그려야 할 배율. 좌우 반전, 상하 반전, 스쿼시&스트레치가 여기서 합쳐진다.
public static class SpriteTransform
{
    /// 스쿼시&스트레치가 그림을 뒤집는 일이 없게 하는 하한.
    public const double MinimumBounceScale = 0.05;

    public static (double ScaleX, double ScaleY) For(AvatarFacing facing, bool upsideDown, double bounceScaleY)
    {
        var bounce = Math.Max(MinimumBounceScale, bounceScaleY);
        var scaleX = facing == AvatarFacing.Left ? -1.0 : 1.0;
        var scaleY = upsideDown ? -bounce : bounce;
        return (scaleX, scaleY);
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

        var (scaleX, scaleY) = SpriteTransform.For(avatar.Facing, avatar.UpsideDown, avatar.BounceScaleY);

        // 반전과 스쿼시의 기준점은 접지점(발밑) — 여기를 중심으로 잡지 않으면
        // 뒤집을 때 펫이 옆으로 튀고, 스쿼시할 때 바닥에서 뜬다.
        drawingContext.PushTransform(new TranslateTransform(localX, localY));
        drawingContext.PushTransform(new ScaleTransform(scaleX, scaleY));
        drawingContext.DrawImage(image, new Rect(-width / 2, -height, width, height));
        drawingContext.Pop();
        drawingContext.Pop();
    }
}
