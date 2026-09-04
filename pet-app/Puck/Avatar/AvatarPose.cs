namespace Puck.Avatar;

/// 펫이 실제로 시간을 보내는, 그리고 아바타가 잘못 그려질 수 있는 자세들.
///
/// 어느 쪽을 보고 어느 쪽이 위인지는 그린 사람이 정한다. 앱은 한 가지 답을
/// 가정하고, 다른 답으로 그려진 아바타는 평생 뒤로 걷거나 머리부터 벽을
/// 오른다 — 지금까지 고치는 방법은 그림판을 열어 그림 자체를 고쳐 다시
/// 넣는 것뿐이었다.
public enum AvatarPose
{
    WalkingRight,
    WalkingLeft,

    /// 오르기는 한 자세가 아니라 둘이다. 펫은 붙을 때의 방향을 끝까지 지키므로
    /// 왼쪽 벽과 오른쪽 벽은 서로의 거울상이고, 한쪽에 맞는 그림은 다른 쪽에서
    /// 뒤집혀 있다. 둘을 함께 고치면 하나를 고치고 하나를 망가뜨린다.
    ClimbingRightWall,
    ClimbingLeftWall,

    /// 천장이 둘인 이유도 벽과 같다. 펫은 양쪽으로 기어가고, 거꾸로 매달린
    /// 채로는 한쪽을 보게 그린 그림이 반대쪽에서 틀리는데 둘을 묶어 뒤집는
    /// 것으로는 고칠 수 없다.
    OnTheCeilingFacingRight,
    OnTheCeilingFacingLeft,
}

public static class AvatarPoses
{
    /// 이 자세일 때 펫이 재생하는 클립. 미리보기가 실제로 입을 그림을 보여
    /// 주게 하는 값이다.
    public static string Clip(this AvatarPose pose) => pose switch
    {
        AvatarPose.ClimbingRightWall or AvatarPose.ClimbingLeftWall => "climb",
        _ => "walk",
    };

    /// 어느 쪽을 보고 있는가 — 렌더러가 이걸로 좌우를 뒤집는다.
    public static AvatarFacing Facing(this AvatarPose pose) => pose switch
    {
        AvatarPose.WalkingLeft or AvatarPose.ClimbingLeftWall or AvatarPose.OnTheCeilingFacingLeft
            => AvatarFacing.Left,
        _ => AvatarFacing.Right,
    };

    /// 거꾸로 매달려 있는가 — Y로 뒤집힌다.
    public static bool IsUpsideDown(this AvatarPose pose)
        => pose is AvatarPose.OnTheCeilingFacingRight or AvatarPose.OnTheCeilingFacingLeft;

    /// 스프라이트가 90도 돌아가 있는가.
    ///
    /// **이 포트에서는 언제나 아니다.** puck-mac은 오르기 클립을 눕혀서 그리고
    /// 렌더러가 90도 돌리지만, 여기 오르기 클립은 이미 그렇게 그려진 것을
    /// 그대로 쓴다. 자리를 남겨 두는 이유는 아래 `AvatarPoseOrientation`이
    /// 회전을 합치는 순서를 갖고 있어서다 — 언젠가 돌리게 되면 그 한 곳만
    /// 바뀐다.
    public static bool RotatesQuarterTurn(this AvatarPose pose) => false;
}
