namespace Puck.Avatar;

/// 아무것도 움직이기 전에 자세가 어떻게 놓여 있는가.
///
/// **한 곳인 이유는 두 곳이었기 때문이다.** puck-mac에서는 렌더러가 뒤집기와
/// 보정과 오르기의 90도를 한 순서로 합치고 설정 창의 미리보기가 다른 순서로
/// 합쳤다. 음수 배율 뒤의 회전은 반대로 돌아가므로, 보정이 둘 다 쓰기 전까지는
/// 둘이 일치했다 — 그리고 그 뒤로는 창 안의 그림이 화면 위의 펫이 아니게
/// 됐는데, 그것이야말로 미리보기가 절대 하면 안 되는 한 가지다.
///
/// 행렬이 아니라 부분들을 돌려준다. 렌더러가 클립의 스쿼시를 배율에 곱해
/// 넣기 때문이다.
public readonly record struct AvatarPoseOrientation(double ScaleX, double ScaleY, double Rotation)
{
    public static AvatarPoseOrientation Of(AvatarPose? pose, AvatarPoseAdjustment? adjustment)
    {
        var correction = adjustment ?? AvatarPoseAdjustment.None;

        var facingX = pose?.Facing() == AvatarFacing.Left ? -1.0 : 1.0;
        var upsideDownY = pose?.IsUpsideDown() == true ? -1.0 : 1.0;
        var quarterTurn = pose?.RotatesQuarterTurn() == true ? Math.PI / 2 : 0;

        return new AvatarPoseOrientation(
            facingX * (correction.FlipsHorizontally ? -1 : 1),
            upsideDownY * (correction.FlipsVertically ? -1 : 1),
            // 보정이 먼저, 오르기가 얹는 회전이 그 위에: 보정은 그림이 어떻게
            // 그려졌는지를 고치는 것이고, 회전은 펫이 그것으로 무엇을 하고
            // 있는지다.
            correction.Rotation + quarterTurn);
    }

    /// 지금 펫이 놓인 자세. 렌더러가 아는 것(방향, 뒤집힘, 클립)에서 고른다.
    public static AvatarPose? PoseOf(AvatarFacing facing, bool upsideDown, string clip)
    {
        if (upsideDown)
            return facing == AvatarFacing.Left
                ? AvatarPose.OnTheCeilingFacingLeft
                : AvatarPose.OnTheCeilingFacingRight;

        if (clip == "climb")
            return facing == AvatarFacing.Left
                ? AvatarPose.ClimbingLeftWall
                : AvatarPose.ClimbingRightWall;

        return facing == AvatarFacing.Left ? AvatarPose.WalkingLeft : AvatarPose.WalkingRight;
    }
}
