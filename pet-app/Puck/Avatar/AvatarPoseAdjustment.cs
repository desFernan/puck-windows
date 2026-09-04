using System.Text.Json.Serialization;

namespace Puck.Avatar;

/// 자세가 이미 하는 것 위에 얹는 보정.
///
/// 일부러 뒤집기와 90도 회전만 있다. 그보다 미세한 것은 캐릭터를 다시 그려
/// 달라는 요청이고, 자기 펫을 기울일 수 있게 해 주는 앱은 아무도 버그
/// 보고서에 적을 수 없는 방식으로 펫을 망가뜨릴 수 있게 해 주는 앱이다.
public sealed record AvatarPoseAdjustment
{
    [JsonPropertyName("flips_horizontally")]
    public bool FlipsHorizontally { get; init; }

    [JsonPropertyName("flips_vertically")]
    public bool FlipsVertically { get; init; }

    /// 0~3. 라디안이 아니라 횟수로 두는 이유는 직각이 아닌 곳에 멈출 수
    /// 없게 하기 위해서다.
    [JsonPropertyName("quarter_turns")]
    public int QuarterTurns { get; init; }

    public static AvatarPoseAdjustment None { get; } = new();

    [JsonIgnore]
    public bool IsIdentity => Equals(None);

    /// 라디안. 배율 뒤에 적용된다.
    [JsonIgnore]
    public double Rotation => Normalised * Math.PI / 2;

    private int Normalised => ((QuarterTurns % 4) + 4) % 4;
}
