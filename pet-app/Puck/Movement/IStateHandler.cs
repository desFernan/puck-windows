namespace Puck.Movement;

/// FSM 상태 하나. name/clipKey는 아바타 매니페스트의 clips 키와 대응한다.
/// 전이할 때 CharacterController가 Play를 부르고 효과음도 트리거하는데,
/// 그때 쓰는 건 Name이 아니라 ClipKey다 — 매니페스트의 sounds 테이블은
/// 대문자로 시작하는 상태 이름이 아니라 clips와 같은 소문자 클립 이름으로
/// 키가 매겨져 있다.
///
/// 상태기계 전체가 프레임 루프에서 돌고, 프레임 루프는 UI 스레드다.
/// 다른 곳에서 도는 상태는 WPF가 그리고 있는 캐릭터를 움직이게 된다.
public interface IStateHandler
{
    /// 표시/디버깅용 이름 ("Idle", "Walk").
    string Name { get; }

    /// 아바타 매니페스트 clips의 조회 키. 전용 클립이 없는 상태는 "walk"를 재사용한다.
    string ClipKey { get; }

    /// 클립이 반복 가능한가 (시작 자세와 끝 자세가 맞는가).
    bool LoopsClip => false;

    /// 매니페스트 sounds의 조회 키. 기본은 ClipKey.
    string SoundKey => ClipKey;

    /// 효과음이 반복되는가. 기본 false: 지금 매핑된 소리는 전부 한 번 하는
    /// 말이고, 말에는 끝이 있다. LoopsClip을 따라가게 두면 목소리가 있는
    /// 모든 반복 상태가 문장 중간에 스스로를 영원히 되감는다.
    bool LoopsSound => false;

    /// 이미 이 상태인데 또 이 상태로 전이하라는 요청이 오면 다시 시작할
    /// 것인가(Exit + Enter). 반응성 일회성 상태(ReactClick)는 반복 트리거가
    /// 처음부터 다시 재생되기를 원하고, Idle 같은 상시 상태는 절대 재시작하면
    /// 안 된다 — 같은 종류의 이벤트가 반복될 때마다 타이머가 초기화된다.
    bool RestartsOnReentry => false;

    void Enter() { }
    void Update(double dt, StateContext context) { }
    void Exit() { }
}
