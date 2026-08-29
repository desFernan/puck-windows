using Puck.Diagnostics;

namespace Puck.Movement;

/// 프레임마다 현재 상태를 돌리고, 요청된 전이를 그 프레임이 끝난 뒤에
/// 적용한다. 상태 전이가 곧 클립 전환이기도 하다.
public sealed class CharacterController
{
    private readonly CharacterBody _body;
    private readonly IReadOnlyDictionary<StateKind, IStateHandler> _states;
    private readonly Func<StateContext> _contextFactory;

    private StateKind? _pending;

    public CharacterController(
        CharacterBody body,
        IReadOnlyDictionary<StateKind, IStateHandler> states,
        StateKind initial,
        Func<StateContext> contextFactory)
    {
        if (!states.ContainsKey(initial))
            throw new ArgumentException($"초기 상태 {initial}이 등록되지 않았습니다", nameof(initial));

        _body = body;
        _states = states;
        _contextFactory = contextFactory;
        Current = initial;

        EnterState(Handler);
    }

    public StateKind Current { get; private set; }

    public event Action<StateKind, StateKind>? Transitioned;

    private IStateHandler Handler => _states[Current];

    /// 이 프레임이 끝난 뒤에 `kind`로 가 달라는 요청. 즉시가 아닌 이유는
    /// 어떤 상태도 자기 update 도중에 교체되면 안 되기 때문이다.
    public void Request(StateKind kind) => _pending = kind;

    /// 팩토리가 준 컨텍스트를 그대로 쓰지 않는 이유는 RequestTransition을
    /// 컨트롤러 자신의 Request로 바꿔 끼우기 위해서다 — 팩토리는 그 구멍을
    /// 채울 방법이 없다. `with` 하나로 바꾸는 것은 필드를 손으로 옮겨 담으면
    /// StateContext에 필드가 늘 때마다 여기 적기를 잊는 쪽으로 조용히 틀리기
    /// 때문이다(그 필드는 상태에 도달하지 않는다).
    public void Advance(double dt)
    {
        var source = _contextFactory();
        var frameContext = source with { RequestTransition = Request };

        Handler.Update(dt, frameContext);

        // 어떤 상태가 어디에 놓았든 프레임이 끝날 때 펫은 실제 화면 위에 있어야 한다.
        // 상태마다 막으면 하나를 빠뜨리는 순간(던지기, 드래그, 모니터 착탈, 앞으로
        // 늘어날 상태들) 펫이 디스플레이 사이 빈 공간에 갇혀 영영 보이지 않는다.
        if (!frameContext.ArtworkHasGround(_body.Position))
            _body.Position = frameContext.SnapToGround(_body.Position, frameContext.VisualBounds);

        if (_pending is not { } next) return;
        _pending = null;
        ApplyTransition(next);
    }

    private void ApplyTransition(StateKind next)
    {
        if (!_states.TryGetValue(next, out var handler))
        {
            // 등록을 빠뜨린 상태 하나가 프레임 루프를 죽이면 앱 전체가 얼어붙는다.
            AppLogger.Error("movement", "등록되지 않은 상태로의 전이를 무시합니다",
                new Dictionary<string, object?> { ["from"] = Current.ToString(), ["to"] = next.ToString() });
            return;
        }

        if (next == Current && !handler.RestartsOnReentry) return;

        var previous = Current;
        Handler.Exit();
        Current = next;
        EnterState(handler);
        Transitioned?.Invoke(previous, next);
    }

    /// 상태에 들어선다. 클립을 걸기 **전에** 자세를 바로 세운다 — 거꾸로
    /// 매달린 채로 그 상태의 그림을 한 프레임 그리면 눈에 띈다.
    ///
    /// 되돌리기가 Ceiling의 Exit이 아니라 여기 있는 이유는, 어떤 상태든
    /// 다른 어떤 상태를 가로챌 수 있기 때문이다. 들어서는 쪽에서 한 번에
    /// 처리해야 Ceiling에서 곧장 넘어간 상태까지 빠짐없이 바로 선다.
    private void EnterState(IStateHandler handler)
    {
        if (!handler.PreservesUpsideDown) _body.IsUpsideDown = false;
        handler.Enter();
        PlayClipFor(handler);
    }

    /// 지금 상태의 클립을 다시 건다. 표정 같은 것이 잠깐 다른 그림을 덮었다가
    /// 물러날 때, 상태를 건드리지 않고 원래 그림으로 돌아오는 길이다.
    public void ReplayCurrentClip() => PlayClipFor(Handler);

    private void PlayClipFor(IStateHandler handler)
        => _body.Play(handler.ClipKey, handler.LoopsClip);
}
