using Puck.Avatar;
using Puck.Overlay;

namespace PuckTests.Overlay;

public class SpriteTransformTests
{
    [Fact]
    public void FacingRightIsTheDrawnOrientation()
    {
        var (sx, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: false, bounceScaleY: 1.0);
        Assert.Equal(1, sx);
        Assert.Equal(1, sy);
    }

    [Fact]
    public void FacingLeftMirrorsHorizontally()
    {
        // 그림은 오른쪽을 보게 그려지고, 반대로 걸을 때 뒤집힌다.
        var (sx, _) = SpriteTransform.For(AvatarFacing.Left, upsideDown: false, bounceScaleY: 1.0);
        Assert.Equal(-1, sx);
    }

    [Fact]
    public void UpsideDownMirrorsVertically()
    {
        var (_, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: true, bounceScaleY: 1.0);
        Assert.Equal(-1, sy);
    }

    [Fact]
    public void BounceMultipliesTheVerticalScale()
    {
        var (_, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: false, bounceScaleY: 1.06);
        Assert.Equal(1.06, sy, precision: 9);
    }

    [Fact]
    public void BounceAndUpsideDownCompose()
    {
        var (_, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: true, bounceScaleY: 1.06);
        Assert.Equal(-1.06, sy, precision: 9);
    }

    [Fact]
    public void ANonPositiveBounceIsClampedSoTheSpriteNeverInverts()
    {
        // 매니페스트의 bounce_intensity가 이상해도 그림이 뒤집히면 안 된다.
        var (_, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: false, bounceScaleY: -0.5);
        Assert.True(sy > 0);
    }
}
