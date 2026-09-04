using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarPoseTests
{
    [Fact]
    public void TheTwoWallsAreMirrorImagesRatherThanOnePose()
    {
        // 한쪽에 맞는 그림은 다른 쪽에서 뒤집혀 있다. 둘을 함께 고치면
        // 하나를 고치고 하나를 망가뜨린다.
        Assert.NotEqual(
            AvatarPose.ClimbingLeftWall.Facing(),
            AvatarPose.ClimbingRightWall.Facing());
    }

    [Fact]
    public void OnlyTheCeilingPosesHangUpsideDown()
    {
        foreach (AvatarPose pose in Enum.GetValues<AvatarPose>())
            Assert.Equal(
                pose is AvatarPose.OnTheCeilingFacingLeft or AvatarPose.OnTheCeilingFacingRight,
                pose.IsUpsideDown());
    }

    [Fact]
    public void APreviewWouldShowTheClipThePetActuallyWears()
    {
        Assert.Equal("climb", AvatarPose.ClimbingLeftWall.Clip());
        Assert.Equal("walk", AvatarPose.OnTheCeilingFacingRight.Clip());
    }

    [Theory]
    [InlineData(AvatarFacing.Right, false, "walk", AvatarPose.WalkingRight)]
    [InlineData(AvatarFacing.Left, false, "walk", AvatarPose.WalkingLeft)]
    [InlineData(AvatarFacing.Left, false, "climb", AvatarPose.ClimbingLeftWall)]
    [InlineData(AvatarFacing.Right, true, "walk", AvatarPose.OnTheCeilingFacingRight)]
    public void TheRendererFindsThePoseFromWhatItAlreadyKnows(
        AvatarFacing facing, bool upsideDown, string clip, AvatarPose expected)
    {
        Assert.Equal(expected, AvatarPoseOrientation.PoseOf(facing, upsideDown, clip));
    }
}

public class AvatarPoseOrientationTests
{
    [Fact]
    public void WithNoCorrectionAPoseIsWhatItAlwaysWas()
    {
        var right = AvatarPoseOrientation.Of(AvatarPose.WalkingRight, null);
        var left = AvatarPoseOrientation.Of(AvatarPose.WalkingLeft, null);

        Assert.Equal(1, right.ScaleX);
        Assert.Equal(-1, left.ScaleX);
        Assert.Equal(0, right.Rotation);
    }

    [Fact]
    public void HangingUpsideDownFlipsOnY()
    {
        Assert.Equal(-1, AvatarPoseOrientation.Of(AvatarPose.OnTheCeilingFacingRight, null).ScaleY);
    }

    [Fact]
    public void ACorrectionTurnsTheArtworkTheRightWayRound()
    {
        // 반대로 그려진 그림. 그림판을 열지 않고 고친다.
        var corrected = AvatarPoseOrientation.Of(
            AvatarPose.WalkingRight, new AvatarPoseAdjustment { FlipsHorizontally = true });

        Assert.Equal(-1, corrected.ScaleX);
    }

    [Fact]
    public void TheCorrectionComesBeforeWhatThePetIsDoingWithIt()
    {
        // 보정은 그림이 어떻게 그려졌는지를 고치는 것이고, 회전은 펫이 그것으로
        // 무엇을 하고 있는지다. 음수 배율 뒤의 회전은 반대로 돌아가므로
        // 순서가 곧 결과다.
        var turned = AvatarPoseOrientation.Of(
            AvatarPose.WalkingLeft, new AvatarPoseAdjustment { QuarterTurns = 1 });

        Assert.Equal(Math.PI / 2, turned.Rotation, precision: 9);
        Assert.Equal(-1, turned.ScaleX);
    }

    [Fact]
    public void FourPressesIsWhereYouStarted()
    {
        Assert.Equal(0, new AvatarPoseAdjustment { QuarterTurns = 4 }.Rotation);
        Assert.Equal(Math.PI / 2, new AvatarPoseAdjustment { QuarterTurns = 5 }.Rotation, precision: 9);
    }

    [Fact]
    public void ACorrectionThatDoesNothingIsNoCorrection()
    {
        Assert.True(new AvatarPoseAdjustment().IsIdentity);
        Assert.False(new AvatarPoseAdjustment { FlipsVertically = true }.IsIdentity);
    }

    [Fact]
    public void NothingRotatesWithoutBeingAskedTo()
    {
        // 이 포트의 오르기 클립은 이미 눕혀 그려진 것을 그대로 쓴다.
        foreach (AvatarPose pose in Enum.GetValues<AvatarPose>())
            Assert.Equal(0, AvatarPoseOrientation.Of(pose, null).Rotation);
    }
}
