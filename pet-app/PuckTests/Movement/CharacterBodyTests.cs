using System.Windows;
using Puck.Avatar;
using Puck.Movement;

namespace PuckTests.Movement;

/// 무엇이 전달됐는지만 기록하는 아바타.
internal sealed class FakeAvatar : IAvatarPlayable
{
    public List<Point> Positions { get; } = [];
    public List<AvatarFacing> Facings { get; } = [];
    public List<bool> UpsideDowns { get; } = [];
    public List<(string Clip, bool Loop)> Played { get; } = [];
    public List<(string Clip, TimeSpan Elapsed, double Intensity)> Bounces { get; } = [];
    public int Jumps { get; private set; }

    public Rect VisualBounds { get; set; } = new(-50, -100, 100, 100);

    public void SetScreenPosition(Point position) => Positions.Add(position);
    public void SetFacing(AvatarFacing facing) => Facings.Add(facing);
    public void SetUpsideDown(bool upsideDown) => UpsideDowns.Add(upsideDown);
    public bool HitTest(Point relative, double tolerance) => VisualBounds.Contains(relative);
    public void Play(string clip, bool loop) => Played.Add((clip, loop));
    public void Stop() { }
    public void UpdateBounce(string clip, TimeSpan elapsed, double intensity)
        => Bounces.Add((clip, elapsed, intensity));
    public void TriggerJump() => Jumps++;
}

public class CharacterBodyTests
{
    [Fact]
    public void ConstructionPushesTheInitialPositionOntoTheAvatar()
    {
        var avatar = new FakeAvatar();
        _ = new CharacterBody(avatar, new Point(10, 20));
        Assert.Equal(new Point(10, 20), Assert.Single(avatar.Positions));
    }

    [Fact]
    public void MovingPushesTheNewPosition()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0));
        body.Position = new Point(5, 5);
        Assert.Equal(new Point(5, 5), avatar.Positions[^1]);
    }

    [Fact]
    public void WritingTheSameFacingIsANoOp()
    {
        // FSM은 걷는 동안 매 프레임 이걸 쓴다. 초당 60번 같은 변환을
        // 다시 적용하는 건 의미 없는 일이다.
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0)) { Facing = AvatarFacing.Right };
        avatar.Facings.Clear();

        body.Facing = AvatarFacing.Right;
        Assert.Empty(avatar.Facings);

        body.Facing = AvatarFacing.Left;
        Assert.Equal(AvatarFacing.Left, Assert.Single(avatar.Facings));
    }

    [Fact]
    public void UpsideDownHasTheSameNoOpGuard()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0));
        avatar.UpsideDowns.Clear();

        body.IsUpsideDown = false;
        Assert.Empty(avatar.UpsideDowns);

        body.IsUpsideDown = true;
        Assert.True(Assert.Single(avatar.UpsideDowns));
    }

    [Fact]
    public void LaunchVelocityStartsAtZero()
    {
        var body = new CharacterBody(new FakeAvatar(), new Point(0, 0));
        Assert.Equal(new Vector(0, 0), body.LaunchVelocity);
    }

    [Fact]
    public void BounceIntensityDefaultsToTheAppsOwnValue()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0));
        body.UpdateBounce("idle", TimeSpan.FromSeconds(1));
        Assert.Equal(CharacterBody.DefaultBounceIntensity, avatar.Bounces[^1].Intensity);
    }

    [Fact]
    public void ManifestBounceIntensityWins()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0), bounceIntensity: 0.2);
        body.UpdateBounce("idle", TimeSpan.FromSeconds(1));
        Assert.Equal(0.2, avatar.Bounces[^1].Intensity);
    }

    [Fact]
    public void VisualBoundsComesStraightFromTheAvatar()
    {
        var avatar = new FakeAvatar { VisualBounds = new Rect(-1, -2, 3, 4) };
        var body = new CharacterBody(avatar, new Point(0, 0));
        Assert.Equal(new Rect(-1, -2, 3, 4), body.VisualBounds);
    }
}
