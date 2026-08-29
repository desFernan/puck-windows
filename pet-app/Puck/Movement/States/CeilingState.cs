using System.Windows;
using Puck.Avatar;

namespace Puck.Movement.States;

/// 천장에 거꾸로 매달려 긴다. 영역의 가로 끝에서는 떨어지지 않고 돌아선다 —
/// 창 윗변에서는 걸어 나가 떨어지는 WalkOnTopState와 다른 점이다. "천장의
/// 끝"이라는 것은 없고 화면 자신의 경계만 있다.
///
/// 전용 클립이 없으므로 walk를 다시 쓴다. WalkOnTop과 같다.
///
/// IsUpsideDown을 Enter가 아니라 매 프레임 세우는 이유는 Enter가
/// StateContext를 받지 못하기 때문이다(IStateHandler를 보라). 몸이 스스로
/// 같은 값 쓰기를 걸러내므로 매 프레임 쓰는 편이 "이미 뒤집었나" 플래그를
/// 따로 두는 것보다 간단하다.
public sealed class CeilingState : IStateHandler
{
    public string Name => "Ceiling";
    public string ClipKey => "walk";
    public bool LoopsClip => true;
    public bool PreservesUpsideDown => true;

    private readonly Func<double> _duration;
    private readonly Func<double> _hang;

    private double _direction = 1;
    private double _elapsed;
    private double _limit;
    /// 노치 아래 매달림이 얼마나 남았는지, 그리고 이번 기어가기가 이미
    /// 한 번 매달렸는지.
    private double _hangRemaining;
    private bool _hasHung;
    /// 첫 프레임이 어느 쪽으로 갈지 정했는가. Enter는 정할 수 없다 —
    /// 컨텍스트를 받지 못해서 펫이 어디 있는지도, 위에 뭐가 있는지도 모른다.
    private bool _headingChosen;

    public CeilingState(Func<double>? duration = null, Func<double>? hang = null)
    {
        _duration = duration ?? (() => 3 + Random.Shared.NextDouble() * 5);
        _hang = hang ?? (() => 1.5 + Random.Shared.NextDouble() * 2);
    }

    public void Enter()
    {
        _elapsed = 0;
        _limit = _duration();
        _direction = 1;
        _headingChosen = false;
        _hangRemaining = 0;
        _hasHung = false;
    }

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;
        body.IsUpsideDown = true;

        // 펫이 있는 **디스플레이**의 영역이지 전부의 경계 상자가 아니다.
        // 더 큰 모니터가 옆에 있으면 그 상자의 윗변은 이 화면 밖 어딘가라,
        // 그걸 겨눈 기어가기는 펫을 지금 화면 위로 내보낸다. 머리 위에
        // 노치가 있는지도 이것이 정한다.
        var area = context.AreaAt(body.Position);
        var notch = context.NotchOver(area);

        _elapsed += dt;
        // 갈 데가 있는 기어가기는 중간에 멈추지 않는다. 오른 벽에서 노치까지
        // 건너가는 데 걷는 속도로 7초쯤 걸리는데 기어가기는 3~8초라, 이게
        // 없으면 펫은 대개 빈 천장에서 시간을 다 쓰고 위에서 볼 만한 단
        // 하나를 못 본 채 끝낸다. 매달려 있는 동안 시간이 다 하는 일도
        // 잦은데, 아무도 못 본 멈춤은 멈춘 것이 아니다.
        var somewhereToBe = _hangRemaining > 0 || (!_hasHung && notch is not null);
        if (_elapsed >= _limit && !somewhereToBe)
        {
            context.RequestTransition(StateKind.Fall);
            return;
        }

        // 노치가 있으면 그쪽으로 출발한다.
        //
        // 단정함의 문제가 아니다. 기어가기는 몇 초이고 천장은 천 픽셀이
        // 넘게 넓으니, 보지 않고 방향을 고른 펫은 대부분의 기어가기를
        // 위에 있는 단 하나에서 멀어지는 데 쓰고 돌아서기 전에 끝낸다.
        // 펫은 벽을 타고 천장에 오르므로 언제나 한쪽 끝에서 시작한다 —
        // 그래서 노치는 언제나 가야 할 그 방향에 있다.
        if (!_headingChosen)
        {
            _headingChosen = true;
            if (notch is { } n)
                _direction = (n.Rect.Left + n.Rect.Width / 2) >= body.Position.X ? 1 : -1;
        }

        // 노치 아래에서 기어가기마다 한 번, 펫은 멈춰서 잠깐 매달린다.
        //
        // 노치는 그것 말고는 아무것도 없는 천장의 유일한 지형지물이고,
        // 위에 있는 단 하나를 그냥 지나치는 펫은 자기가 사는 기계를 못 본
        // 펫이다. 한 번인 이유는, 지날 때마다 멈추면 쉬는 것이 아니라
        // 걸린 것으로 읽히기 때문이다.
        if (_hangRemaining > 0)
        {
            _hangRemaining -= dt;
            return;
        }
        if (!_hasHung && notch is { } housing
            && body.Position.X >= housing.Rect.Left && body.Position.X <= housing.Rect.Right)
        {
            _hasHung = true;
            _hangRemaining = _hang();
            return;
        }

        // 갈 자리와, **거기의** 천장 높이. 노치가 이 방으로 내려와 있고,
        // 화면의 윗변만 계속 겨누는 기어가기는 펫을 그 속으로 몰고 간다 —
        // ScreenNotch를 보라.
        var travelledX = body.Position.X + _direction * context.WalkSpeed * dt;

        // 먼저 가로로 가둔 뒤, 가두기가 실제로 놓은 자리의 천장으로
        // 내린다. 벽에서 고정한 것과 고정 전에 읽은 천장은 한 걸음
        // 어긋나는데, 노치의 모서리에서 그 한 걸음은 딱 붙어 도는 것과
        // 모서리를 스치는 것의 차이다.
        var pinned = PetBounds.Contain(new Point(travelledX, body.Position.Y), context.VisualBounds, area);
        var contained = new Point(pinned.X, context.CeilingAt(pinned.X, area));

        // 영역보다 큰 아바타(설정의 크기 슬라이더)는 실제로 맞는 위치가
        // 없어서 Contain이 입력과 무관하게 늘 왼쪽 한계로 고정한다 —
        // 그걸로 비교하면 방향이 매 프레임 뒤집히기만 하고 정착하지 않는다.
        if (pinned.X != travelledX
            && !PetBounds.IsOversizedHorizontally(context.VisualBounds, area))
            _direction = -_direction;

        body.Facing = _direction > 0 ? AvatarFacing.Right : AvatarFacing.Left;
        body.Position = contained;
    }
}
