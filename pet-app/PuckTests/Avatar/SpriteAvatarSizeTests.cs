using System.Windows;
using Puck.Avatar;

namespace PuckTests.Avatar;

/// 패키지의 무엇도 펫이 얼마나 크게 나오는지를 정하지 않는다.
public class SpriteAvatarSizeTests
{
    [Fact]
    public void TwoAvatarsDrawnAtDifferentNumbersComeOutTheSameHeight()
    {
        // 130x133로 그린 것과 251x300으로 그린 것이 같은 설정에서 머리
        // 하나만큼 차이가 나던 것이 이것이다.
        var small = SpriteAvatar.SizeFor(new Hitbox(130, 133));
        var large = SpriteAvatar.SizeFor(new Hitbox(251, 300));

        Assert.Equal(small.Height, large.Height, precision: 6);
    }

    [Fact]
    public void TheShapeThePackageDrewIsKept()
    {
        var size = SpriteAvatar.SizeFor(new Hitbox(100, 200));

        Assert.Equal(0.5, size.Width / size.Height, precision: 6);
    }

    [Fact]
    public void AWideClaimDoesNotBecomeABannerAcrossTheDesktop()
    {
        // 높이만 맞추면 10:1이 화면을 가로지른다. 긴 쪽을 맞춘다.
        var size = SpriteAvatar.SizeFor(new Hitbox(1000, 100));

        Assert.Equal(SpriteAvatar.DefaultHeight, size.Width, precision: 6);
        Assert.Equal(SpriteAvatar.DefaultHeight / 10, size.Height, precision: 6);
    }

    [Fact]
    public void ATallClaimIsMatchedOnItsHeight()
    {
        var size = SpriteAvatar.SizeFor(new Hitbox(100, 1000));

        Assert.Equal(SpriteAvatar.DefaultHeight, size.Height, precision: 6);
    }

    [Fact]
    public void AHitboxOfNothingStillGivesAPetToLookAt()
    {
        // 0으로 나누는 대신 앱의 키를 그대로 쓴다.
        var size = SpriteAvatar.SizeFor(new Hitbox(0, 0));

        Assert.Equal(SpriteAvatar.DefaultHeight, size.Height);
        Assert.Equal(SpriteAvatar.DefaultHeight, size.Width);
    }
}
