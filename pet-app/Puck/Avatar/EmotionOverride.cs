namespace Puck.Avatar;

/// 움직임과 상관없이 **잠깐 다른 클립을 보여 준다.**
///
/// 걷기·떨어지기 같은 것은 FSM이 정하지만, "지금 생각하는 중"은 상태가
/// 아니다 — 펫은 생각하면서도 걷는다. 그래서 상태 기계를 건드리는 대신
/// 그 위에 잠깐 덮었다가 시간이 지나면 걷어 낸다.
///
/// puck-linux의 `emotion.rs`를 옮긴 것이다. 저쪽은 16ms 고정 틱이라 틱 수로
/// 셌지만 여기 프레임 루프는 dt(초)를 받으므로 초로 센다 — 프레임이 밀려도
/// 표정이 그만큼 길어지지 않는다.
public sealed class EmotionOverride
{
    /// 한 번 띄우면 보여 주는 시간. puck-linux의 188틱(약 3초)과 같다.
    public const double DefaultSeconds = 3.0;

    private string? _clip;
    private double _remaining;

    public bool IsActive => _remaining > 0;

    /// 이 클립을 지금부터 보여 준다. 이미 보여 주는 중이면 시간을 다시 채운다 —
    /// 도구를 연달아 쓰는 동안 표정이 깜빡이지 않게.
    public void Show(string clip, double seconds = DefaultSeconds)
    {
        _clip = clip;
        _remaining = seconds;
    }

    /// 끝날 때를 모르는 일 동안 계속 짓고 있는다. 에이전트 한 턴이 얼마나
    /// 걸릴지는 아무도 모르므로(도구를 열두 번까지 부른다) 시간을 정해 두면
    /// 생각하다 말고 표정이 풀린다. 끝나면 다른 표정으로 덮거나 걷어 낸다.
    public void Hold(string clip) => Show(clip, double.PositiveInfinity);

    /// 지금 당장 걷어 낸다. 펫이 답을 내놓았으면 더 생각하는 척할 이유가 없다.
    public void Clear()
    {
        _clip = null;
        _remaining = 0;
    }

    /// dt만큼 흘려보내고 지금 보여야 할 클립을 돌려준다. 시간이 다 되면
    /// null — 부르는 쪽은 그때 원래 클립으로 되돌리면 된다.
    public string? Tick(double seconds)
    {
        if (_remaining <= 0) return null;

        _remaining -= seconds;
        if (_remaining > 0) return _clip;

        _clip = null;
        _remaining = 0;
        return null;
    }
}
