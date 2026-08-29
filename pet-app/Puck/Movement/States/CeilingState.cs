using System.Windows;
using Puck.Avatar;

namespace Puck.Movement.States;

/// 천장에 거꾸로 매달려 긴다. 영역의 가로 끝에서는 떨어지지 않고 돌아선다 —
/// 창 윗변에서는 걸어 나가 떨어지는 WalkOnTopState와 다른 점이다. "천장의
/// 끝"이라는 것은 없고 화면 자신의 경계만 있다.
///
/// 전용 클립이 없으므로 walk를 다시 쓴다. WalkOnTop과 같다.
///
/// puck-mac의 이 상태에는 카메라 하우징 아래에서 한 번 멈춰 매달리는
/// 부분이 있는데, 여기에는 없다. Windows에는 하우징이 달린 디스플레이가
/// 없어서 천장에 멈춰 설 지형지물이 하나도 없다.
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

    private double _direction = 1;
    private double _elapsed;
    private double _limit;
    /// 첫 프레임이 어느 쪽으로 갈지 정했는가. Enter는 정할 수 없다 —
    /// 컨텍스트를 받지 못해서 펫이 어디 있는지 모른다.
    private bool _headingChosen;

    public CeilingState(Func<double>? duration = null)
        => _duration = duration ?? (() => 3 + Random.Shared.NextDouble() * 5);

    public void Enter()
    {
        _elapsed = 0;
        _limit = _duration();
        _direction = 1;
        _headingChosen = false;
    }

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;
        body.IsUpsideDown = true;

        // 펫이 있는 **디스플레이**의 영역이지 전부의 경계 상자가 아니다.
        // 더 큰 모니터가 옆에 있으면 그 상자의 윗변은 이 화면 밖 어딘가라,
        // 그걸 겨눈 기어가기는 펫을 지금 화면 위로 내보낸다.
        var area = context.AreaAt(body.Position);

        _elapsed += dt;
        if (_elapsed >= _limit)
        {
            context.RequestTransition(StateKind.Fall);
            return;
        }

        // 넓은 쪽으로 출발한다.
        //
        // 펫은 벽을 타고 천장에 오르므로 언제나 한쪽 끝에서 시작한다.
        // 방향을 고정해 두면 절반의 경우 올라온 그 벽으로 곧장 걸어가
        // 첫 걸음에 돌아서고, 짧은 기어가기가 통째로 제자리 걸음이 된다.
        if (!_headingChosen)
        {
            _headingChosen = true;
            _direction = body.Position.X <= area.Left + area.Width / 2 ? 1 : -1;
        }

        var travelledX = body.Position.X + _direction * context.WalkSpeed * dt;

        // 가로로 가둔 뒤 그 자리의 천장에 붙인다.
        var pinned = PetBounds.Contain(new Point(travelledX, area.Top), context.VisualBounds, area);

        // 영역보다 큰 아바타(설정의 크기 슬라이더)는 실제로 맞는 위치가
        // 없어서 Contain이 입력과 무관하게 늘 왼쪽 한계로 고정한다 —
        // 그걸로 비교하면 방향이 매 프레임 뒤집히기만 하고 정착하지 않는다.
        if (pinned.X != travelledX
            && !PetBounds.IsOversizedHorizontally(context.VisualBounds, area))
            _direction = -_direction;

        body.Facing = _direction > 0 ? AvatarFacing.Right : AvatarFacing.Left;
        body.Position = pinned;
    }
}
