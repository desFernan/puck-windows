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

        Handler.Enter();
        PlayClipFor(Handler);
    }

    public StateKind Current { get; private set; }

    public event Action<StateKind, StateKind>? Transitioned;

    private IStateHandler Handler => _states[Current];

    /// 이 프레임이 끝난 뒤에 `kind`로 가 달라는 요청. 즉시가 아닌 이유는
    /// 어떤 상태도 자기 update 도중에 교체되면 안 되기 때문이다.
    public void Request(StateKind kind) => _pending = kind;

    /// 팩토리가 준 컨텍스트를 그대로 쓰지 않고 다시 만드는 이유는
    /// RequestTransition을 컨트롤러 자신의 Request로 바꿔 끼우기
    /// 위해서다 — 팩토리는 그 구멍을 채울 방법이 없다.
    public void Advance(double dt)
    {
        var source = _contextFactory();
        var frameContext = new StateContext
        {
            Body = source.Body,
            RoamableArea = source.RoamableArea,
            AvatarHeight = source.AvatarHeight,
            VisualBounds = source.VisualBounds,
            WalkSpeed = source.WalkSpeed,
            LandingY = source.LandingY,
            HasGroundUnder = source.HasGroundUnder,
            SnapToGround = source.SnapToGround,
            LedgeBeyond = source.LedgeBeyond,
            Windows = source.Windows,
            UnclimbableWindows = source.UnclimbableWindows,
            RequestTransition = Request,
        };

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
        handler.Enter();
        PlayClipFor(handler);
        Transitioned?.Invoke(previous, next);
    }

    private void PlayClipFor(IStateHandler handler)
        => _body.Play(handler.ClipKey, handler.LoopsClip);
}
