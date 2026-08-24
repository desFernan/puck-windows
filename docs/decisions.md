# 결정 기록 — puck-windows

Windows에서 puck-mac과 다르게 간 것만 적는다. mac 쪽 `docs/decisions.md`는
macOS 맥락의 기록이므로 복사하지 않는다.

`porting-design.md` §5의 네 항목(단일 프로세스 / `run_powershell` / 음성 입력
1차 제외 / 터미널 패널 Phase 6)이 이 문서의 전제이고, 아래는 구현하면서
실제로 갈라진 지점들이다.

## 2026-08-24 — 앱 진입점 클래스 이름은 `PuckApp`

`App/` 폴더의 네임스페이스가 `Puck.App`인데 WPF 진입점 클래스도 관례상 `App`이라
`Puck.App`이 타입이자 네임스페이스가 되어 CS0101로 컴파일되지 않는다.

→ 진입점을 `Puck.PuckApp` (`PuckApp.xaml`)로 한다. puck-mac도 `App/PuckApp.swift`라
이름이 오히려 원본에 가깝다. 파일 이름이 `App.xaml`이 아니게 되므로 SDK의 기본
글롭이 `ApplicationDefinition`으로 잡아 주지 않고, `Puck.csproj`가 직접 지정한다.

## 2026-08-24 — 펫의 시작 위치는 커서가 있는 디스플레이의 바닥

Phase 0+1 플랜은 펫을 `RoamableArea`(모든 작업 영역의 **경계 상자**)의
바닥 한가운데에 세운다. 디스플레이가 계단처럼 놓이면 그 지점이 어느
디스플레이에도 속하지 않는다 — 예를 들어 1920×1080 주 모니터 오른쪽에
세로 1080×1920 모니터가 y=-407에 붙어 있으면 경계 상자의 바닥은 y=1465이고,
그건 주 모니터 아래쪽 빈 공간이다. 거기 세운 펫은 실행하자마자 화면 밖이라
보이지 않는다.

→ `PetBootstrap.StartPosition`이 커서가 있는 디스플레이를 골라 그 작업 영역의
바닥 한가운데를 쓴다.

## 2026-08-24 — 걷기는 디스플레이 사이의 빈 공간으로 나가지 않는다

같은 경계 상자 문제가 걷기에도 있다. 위 배치에서 펫이 오른쪽으로 걸어
세로 모니터에 올라타면(정상) 그 바닥 y=1465에서 다시 왼쪽으로 걸어
x<1920인 빈 공간에 들어가고, 거기엔 디스플레이가 없어서 다시는 보이지 않는다.
플랜의 `LandingY`는 "가장 가까운 디스플레이의 바닥"을 주므로 그 자리를
멀쩡한 바닥으로 판정해 떨어지지도 않는다.

→ `ScreenSpace.HasGroundUnder(point)`와 `StateContext.HasGroundUnder`를 더한다.
`WalkState`는 발밑에 디스플레이가 없는 한 걸음을 거부하고 Idle로 돌아가며,
`IdleState`는 이미 그런 자리에 있으면(던져진 경우) Fall을 요청해
`FallState`가 가장 가까운 실제 바닥으로 되돌린다.

플랜의 모델(빈 공간을 모르는 경계 상자)을 통째로 갈아엎지는 않았다. 창 윗면이
착지면으로 끼어드는 Phase 2의 `LandingSurfaceResolver`가 어차피 이 계층을
다시 쓰므로, 그때 디스플레이별 영역으로 정리하는 편이 낫다.

## 2026-08-24 — 동봉 아바타의 `idle.png`는 mac dummy의 `starry-eyed.png`

플랜은 `Resources/Avatars/dummy/`에 `clips: {"idle": "idle"}` 매니페스트와
puck-mac에서 복사한 `idle.png`를 두라고 한다. mac의 dummy 패키지에는
`idle.png`라는 파일이 없다 — 그 매니페스트의 idle 클립이 `starry-eyed`를
가리킨다. 그 그림을 `idle.png`로 복사했다.
