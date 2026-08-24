# Puck for Windows — Phase 2 Implementation Plan (감각 기관)

**Goal:** 펫이 화면만 아는 상태에서 벗어나 **바깥을 감지**하게 만든다 — 실제 창의
타이틀바 위에 서고, 핫키로 부르면 오고, 가리킨 곳을 클릭하고, 효과음이 난다.

**Spec:** [`docs/porting-design.md`](../porting-design.md) §4 Phase 2.
**앞 단계:** [`2026-08-24-phase-0-1-pet-walks.md`](2026-08-24-phase-0-1-pet-walks.md) — 그 플랜이
"Phase 2에서 채운다"고 남긴 구멍들이 여기서 채워진다.

**Tech Stack:** Phase 0+1 그대로 (C# / .NET 8 / WPF / xUnit) + 아래 둘.

- **UI Automation** — `UIAutomationClient` / `UIAutomationTypes` 어셈블리 참조.
  BCL에 있으므로 NuGet이 아니다.
- **NAudio** — 유일하게 새로 받는 NuGet 패키지. `porting-design.md` §3 표의 결정이다.
  BCL의 `System.Media.SoundPlayer`로는 겹쳐 재생·볼륨·비동기가 안 되는데, mac의
  `SFXPlayer`는 그 셋을 다 한다(플레이어 노드 풀).

---

## Global Constraints

Phase 0+1의 전역 규칙을 그대로 잇고, 아래를 더한다.

- **좌표계는 그대로.** 가상 화면 물리 픽셀, 좌상단 원점, Y는 아래로. 창 정보도
  같은 공간으로 정규화해서 들어온다 — mac이 `GlobalScreenSpace`로 하던 일인데,
  Win32는 이미 그 공간이라 변환이 없다.
- **`Interop/` 밖에서 Win32를 직접 부르지 않는다.** P/Invoke 선언은 전부
  `Puck/Interop/Win32.cs`에.
- **순수 로직과 OS 접촉을 가른다.** 창 목록을 *가져오는* 코드는 테스트하지 않고
  얇게 유지하고, 그 목록으로 *판단하는* 코드(필터, 착지면, 지지 창)는 전부
  순수 함수로 빼서 테스트한다. mac의 `WindowListWatcher.filter` /
  `LandingSurfaceResolver` / `WindowSupport`가 이미 그 구조다.
- **권한 프롬프트는 없다.** mac의 `AccessibilityPermission`(TCC)에 해당하는 것이
  Windows에 없다 — 이식하지 않는다. 대신 **UIPI**가 새로 생긴다: 관리자 권한으로
  실행된 창은 조작할 수 없고 조용히 실패한다. 도구 응답과 로그가 그 사실을
  말해야 한다.
- **UI 문자열은 한국어.** 새로 생기는 것도 전부 `Localization/Strings`를 거친다.

### 이 플랜에서 macOS 원본과 의도적으로 다르게 가는 것

| 항목 | puck-mac | puck-windows | 이유 |
|---|---|---|---|
| 창 목록 | `CGWindowListCopyWindowInfo` | `EnumWindows` + `DwmGetWindowAttribute` | Win32에 대응물이 그것뿐 |
| 창 사각형 | `kCGWindowBounds` | `EXTENDED_FRAME_BOUNDS` (`GetWindowRect` 아님) | `GetWindowRect`는 보이지 않는 그림자 여백을 포함한다. 그 값으로 착지면을 잡으면 펫이 창에서 몇 픽셀 떠서 선다 |
| 창 필터 | `layer == 0` | 보이는 최상위 창 + 클로킹 제외 + `WS_EX_TOOLWINDOW` 제외 | Windows에는 layer가 없다. UWP는 실행 중이지만 **클로킹된** 유령 창을 남기므로 `DWMWA_CLOAKED`를 반드시 본다 |
| 목록 갱신 | 폴링 + `NSWorkspace` 알림 | 폴링 + `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` | 같은 모양. 알림 소스만 다르다 |
| 접근성 권한 | TCC 프롬프트 + 온보딩 | 없음 | Windows엔 UIA 권한 프롬프트가 없다 |
| 화면 캡처 | `CGWindowListCreateImage` | `BitBlt` + **`CAPTUREBLT`** | `Windows.Graphics.Capture`는 WinRT 상호운용이 필요해 Phase 6으로 미룬다. `CAPTUREBLT`가 없으면 레이어드 창(=우리 펫)이 캡처에 잡히지 않는다 |
| 마우스 감지 | `CGEventTap` | `SetWindowsHookEx(WH_MOUSE_LL)` | 대응물 |
| 집중 모드 | 집중 모드 | 집중 지원(Focus Assist) | 대응물 |

---

## File Structure (이 플랜이 더하는 것)

```
pet-app/Puck/
  Interop/
    Win32.cs                       (기존 파일에 추가)
    WinEventHook.cs                포그라운드 변경 알림
    LowLevelMouseHook.cs           WH_MOUSE_LL 래퍼
    MessageWindow.cs               핫키를 받을 숨은 창
  WindowSensing/
    WindowInfo.cs                  정규화된 창 하나
    WindowListSource.cs            EnumWindows -> WindowInfo (얇음, 테스트 없음)
    WindowFilter.cs                순수 필터 (테스트)
    WindowListWatcher.cs           폴링 + 버스트
    LandingSurfaceResolver.cs      순수 (테스트)
    UIElementInspector.cs          UIA 조회
    UIElementSearch.cs             UIA 트리 검색
  Movement/
    WindowSupport.cs               순수 창 조회 (테스트)
    States/
      ClimbState.cs  WalkOnTopState.cs  CeilingState.cs  MoveToState.cs  PointState.cs
  Input/
    HotkeyBindings.cs              바인딩 + 충돌 검사 (순수, 테스트)
    GlobalHotkeyManager.cs         RegisterHotKey
    ScreenRegionCapture.cs         BitBlt + CAPTUREBLT
    SpeechBubblePlacement.cs       버블 위치 계산 (순수, 테스트)
    TextInputBubbleWindow.xaml(.cs)
  Pointing/
    SyntheticClick.cs              SendInput
    ClickDetector.cs               저수준 마우스 훅
    PendingPointTracker.cs         가리킨 뒤 클릭 대기 (순수, 테스트)
    PointingController.cs          위 셋을 엮음
  Audio/
    SoundTable.cs                  매니페스트 sounds 조회 (순수, 테스트)
    SfxPlayer.cs                   NAudio, 겹쳐 재생
    FocusAssistObserver.cs         집중 지원 중엔 소리를 줄인다
```

---

## Task 1: 창 하나를 표현하고, 실제 창 목록을 가져온다

**Files:** `WindowSensing/WindowInfo.cs`, `WindowListSource.cs`, `WindowFilter.cs`,
`Interop/Win32.cs`(추가) / Test: `PuckTests/WindowSensing/WindowFilterTests.cs`

**원본:** `WindowSensing/WindowInfo.swift`, `WindowListWatcher.swift`의 `filter`/`fetchRawWindowList`

**Produces:**
- `sealed record WindowInfo(IntPtr Handle, int ProcessId, string? OwnerName, string? Title, Rect Frame, bool IsCloaked, bool IsToolWindow)`
- `static IReadOnlyList<WindowInfo> WindowListSource.Fetch()` — Z 순서(앞→뒤) 그대로
- `static IReadOnlyList<WindowInfo> WindowFilter.Keep(IReadOnlyList<WindowInfo>, int selfProcessId, Size minimumSize)`

**핵심 결정**

- **`GetWindowRect`가 아니라 `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`.**
  Windows 10 이후 창은 리사이즈용 투명 여백을 좌우/아래로 몇 픽셀 두르고 있고,
  `GetWindowRect`는 그것을 포함한다. 그 값을 착지면으로 쓰면 펫이 창 위에
  붕 떠 보인다. DWM 호출이 실패하면(예: 콘솔 창) `GetWindowRect`로 떨어진다.
- **클로킹 검사는 선택이 아니다.** UWP/스토어 앱은 종료해도 클로킹된 창을 남긴다.
  `DWMWA_CLOAKED`가 0이 아니면 화면에 없는 창이다 — 그걸 남기면 펫이 아무것도
  없는 허공에 선다.
- `WS_EX_TOOLWINDOW`는 제외한다. 우리 오버레이도 그 스타일이라 자기 자신도 같이
  걸러진다(그래도 PID로 한 번 더 거른다).

**Steps**
- [ ] `WindowFilterTests` 먼저: 자기 프로세스 창 제외 / 최소 크기 미만 제외 /
      클로킹 제외 / 도구 창 제외 / **Z 순서 보존**(이게 깨지면 착지면이 틀린다)
- [ ] `WindowInfo`, `WindowFilter` 구현 → 통과
- [ ] `Win32`에 `EnumWindows`, `IsWindowVisible`, `GetWindowRect`,
      `DwmGetWindowAttribute`, `GetWindowTextW`, `GetWindowThreadProcessId`,
      `GetWindowLongW` 추가
- [ ] `WindowListSource` 구현 (테스트 없음 — 실제 창에 의존한다. 얇게 유지)
- [ ] 커밋: `feat: enumerate real windows into a normalized list`

---

## Task 2: 창 목록을 계속 최신으로 (WindowListWatcher)

**Files:** `WindowSensing/WindowListWatcher.cs`, `Interop/WinEventHook.cs` /
Test: `PuckTests/WindowSensing/WindowListWatcherTests.cs`

**원본:** `WindowSensing/WindowListWatcher.swift`

**Produces:** `sealed class WindowListWatcher : IDisposable` — `IReadOnlyList<WindowInfo> Windows`,
`Start()`, `Stop()`, `const double IdlePollHz = 10`, `BurstPollHz = 15`, `BurstSeconds = 3`

**핵심 결정**

- **폴링 주기는 mac 값 그대로(10Hz / 버스트 15Hz / 3초).** 프레임 루프(60Hz)와
  분리한다 — 창 목록은 프레임마다 바뀌지 않고, `EnumWindows`를 60Hz로 도는 것은
  낭비다.
- **버스트 방아쇠는 `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`.** mac이
  `NSWorkspace`의 앱 활성/실행/종료 알림으로 하던 것. 사람이 창을 바꾼 직후가
  목록이 가장 많이 흔들리는 때다.
- **경과 시간은 `Stopwatch`(단조 증가)로.** `DateTime.Now`는 절전 복귀나 시간
  동기화로 뛴다 — mac이 `systemUptime`을 쓴 이유와 같다.
- 테스트는 시계와 목록 소스를 주입해서 돈다: `WindowListWatcher(Func<IReadOnlyList<WindowInfo>> source, ...)`

**Steps**
- [ ] 실패 테스트: 시작하면 즉시 한 번 읽는다 / 버스트가 주기를 올린다 /
      버스트가 끝나면 원래 주기로 돌아온다 / Dispose 후에는 더 읽지 않는다
- [ ] `WinEventHook` (콜백 델리게이트를 필드로 잡아 둔다 — GC되면 조용히 죽는다)
- [ ] `WindowListWatcher` 구현 → 통과
- [ ] 커밋: `feat: keep the window list fresh with a polled watcher`

---

## Task 3: 창 윗면이 착지면이 된다 (LandingSurfaceResolver)

**Files:** `WindowSensing/LandingSurfaceResolver.cs` /
Test: `PuckTests/WindowSensing/LandingSurfaceResolverTests.cs`

**원본:** `WindowSensing/LandingSurfaceResolver.swift` — **로직을 그대로 옮긴다.**

**Produces:**
`static double LandingSurfaceResolver.LandingY(double x, double fallingFromY, IReadOnlyList<WindowInfo> windows, double screenBottomY, double roamableTop, double avatarHeight)`

**핵심 결정 (원본의 것을 그대로)**

- 후보는 **x가 창의 가로 범위 안**이고 **윗변이 지금 위치보다 아래**인 창의 윗변.
- **가려진 창은 후보가 아니다.** 앞쪽(Z가 더 위) 창이 그 지점의 그 높이를 덮고
  있으면 뒤 창의 윗변에는 설 수 없다 — 보이지도 않는 선 위에 서게 된다.
- **머리 여유가 없는 창은 통째로 제외.** 윗변이 화면 위쪽에 너무 붙어 있으면
  (전체화면/최대화) 거기 서는 순간 펫의 머리가 화면 밖으로 잘린다.
- 후보가 없으면 화면 바닥.

**Phase 1과의 접합** — `StateContext.LandingY`가 클로저인 덕분에 상태 코드는 한 줄도
바뀌지 않는다. `PetBootstrap`이 `_screens.FloorY` 대신 이 함수를 물리면 끝이다.
Phase 1의 `HasGroundUnder`/`SnapToGround`(디스플레이 사이 빈 공간 방어)는 그대로 둔다 —
그건 창이 아니라 화면에 대한 규칙이다.

**Steps**
- [ ] 실패 테스트: 창 없으면 화면 바닥 / 가로 범위 밖 창은 무시 / 이미 지나친
      윗변은 무시 / 여럿이면 가장 위(가장 먼저 닿는 것) / **가려진 창 제외** /
      머리 여유 없는 창 제외
- [ ] 구현 → 통과
- [ ] `PetBootstrap`이 `LandingY`를 이걸로 교체, 눈으로 확인(창 위에 착지)
- [ ] 커밋: `feat: land on window top edges, not just the screen floor`

---

## Task 4: 어떤 창 위에 서 있고 어떤 창에 막혔나 (WindowSupport)

**Files:** `Movement/WindowSupport.cs` / Test: `PuckTests/Movement/WindowSupportTests.cs`

**원본:** `Movement/WindowSupport.swift` — 순수 조회. 그대로 옮긴다.

**Produces:** `CoveringWindow`, `PerchTarget`, `SupportingWindow`, `WindowBeingClimbed`,
`NearestClimbTarget`, 상수 `FootTolerance = 4`, `EdgeTolerance = 4`

**핵심 결정 (원본의 주석이 이미 이유를 적어 둔 것들)**

- `CoveringWindow`는 **발이 아니라 몸통 가운데**로 판정한다. 펫은 모든 창 위에
  그려지므로 중요한 건 몸이 창 내용 위에 있는가다.
- `PerchTarget`은 **몸 전체가 윗변 위에 올라오도록** 반 폭만큼 안쪽으로 민다.
- `WindowBeingClimbed`는 **그 높이에서 실제로 보이는 옆면**만 벽으로 친다.
  안 그러면 뒤에 겹친 창들의 안 보이는 모서리를 타고 허공을 오른다.

**Steps**
- [ ] 실패 테스트 (원본의 각 규칙마다 하나씩) → 구현 → 통과
- [ ] 커밋: `feat: which window the pet stands on, and which one it bumps into`

---

## Task 5: 창을 타고 오르고, 그 위를 걷는다

**Files:** `Movement/States/ClimbState.cs`, `WalkOnTopState.cs`, `CeilingState.cs`,
`StateKind`에 추가 / Test: `PuckTests/Movement/WindowStatesTests.cs`

**원본:** `Movement/States/{Climb,WalkOnTop,Ceiling,ClimbToCeiling}State.swift`

**핵심 결정**

- `StateKind`에 `Climb`, `WalkOnTop`, `Ceiling`을 더한다. Phase 1이 만든
  **`ClimbLedge`는 그대로 둔다** — 그건 화면 턱이고 이건 창 옆면이라 다른 것이다
  (`docs/decisions.md` 2026-08-25 항목).
- `WalkState`가 창 옆면에 막히면 `Climb`을 요청한다. 화면 턱과 창이 동시에
  후보면 **창이 먼저다** — 눈앞에 있는 것이 그것이다.
- `ClimbState`는 매 프레임 "타고 있던 창이 아직 있는가"를 다시 묻고, 없으면
  `Fall`로 간다. 창은 사람이 언제든 닫는다.

**Steps**
- [x] 실패 테스트: 오르다 창이 사라지면 Fall / 윗변에 닿으면 WalkOnTop /
      끝까지 걸으면 Fall / 화면 끝에 닿은 창에서는 돌아선다
- [x] 구현 → 통과 → 커밋: `feat: climb windows, walk on their top edges`

**천장은 뺐다.** `CeilingState`/`ClimbToCeilingState`는 완료 조건에 없고 같은
기계 위의 장식이라 미룬다 — 이유는 `docs/decisions.md`에.

---

## Task 6: 어디로 갈지 창을 아는 쪽이 정한다

**Files:** `Movement/States/IdleState.cs`(수정), `App/PetBootstrap.cs`(수정) /
Test: `PuckTests/Movement/StatesTests.cs`(추가)

**원본:** `IdleState.wanderDelegate`, `idleStateDidLoseFootingBehind`

**핵심 결정**

- `IdleState`에 `IWanderDelegate? Wander`를 둔다. 없으면 지금처럼 무작위 X.
  있으면 창 목록을 아는 쪽이 "저 창 위로 가 보자"를 고를 수 있다.
- **"발밑이 사라졌다"와 "발밑이 창 뒤로 갔다"를 가른다.** 서 있던 창이 다른 창에
  가려졌을 뿐이면 떨어지는 게 아니라 그 창 뒤로 숨는 것이다 — 원본이 이 둘을
  구분하는 이유이고, 구분하지 않으면 창을 하나 띄울 때마다 펫이 바닥으로 떨어진다.

**Steps**
- [ ] 실패 테스트 두 개(가려짐 vs 사라짐) → 구현 → 통과
- [ ] 커밋: `feat: window-aware wandering, and telling occluded from gone`

---

## Task 7: UI 요소를 들여다본다 (UIA)

**Files:** `WindowSensing/UIElementInspector.cs`, `UIElementSearch.cs`,
`Puck.csproj`(UIA 참조) / Test: `PuckTests/WindowSensing/UIElementSearchTests.cs`

**원본:** `WindowSensing/{UIElementInspector,UIElementSearch}.swift` (AXUIElement)

**Produces:** `sealed record UIElementInfo(string? Name, string? ControlType, Rect Bounds, bool IsEnabled, bool IsOffscreen)`,
`UIElementInspector.Describe(IntPtr hwnd)`, `UIElementSearch.Find(IntPtr hwnd, string query, int limit)`

**핵심 결정**

- `System.Windows.Automation`(UIA)로 `AXUIElement`를 대신한다. `TreeWalker`로
  훑고 `NameProperty`/`ControlTypeProperty`/`BoundingRectangleProperty`를 읽는다.
- **점수 매기기(어느 요소가 질의에 맞는가)는 순수 함수로 뺀다.** UIA 트리는
  테스트할 수 없지만 "이름이 얼마나 맞는가"는 테스트할 수 있다.
- **UIPI를 명시적으로 보고한다.** 관리자 권한 창은 UIA 조회가 빈 결과로
  돌아온다. 그걸 "요소 없음"이라고 하면 사람이 영원히 헤맨다 — 권한 때문임을
  구분해 돌려준다.
- 트리 순회에 **깊이/개수 상한**을 둔다. Electron 앱 하나가 수만 개 노드를 낸다.

**Steps**
- [ ] `UIElementSearch`의 점수 함수 테스트 먼저 → 구현
- [ ] 인스펙터 구현 (얇게) → 실제 창으로 눈 검증
- [ ] 커밋: `feat: inspect and search UI elements through UI Automation`

---

## Task 8: 전역 핫키

**Files:** `Input/HotkeyBindings.cs`, `GlobalHotkeyManager.cs`, `Interop/MessageWindow.cs` /
Test: `PuckTests/Input/HotkeyBindingsTests.cs`

**원본:** `Input/{HotkeyBindings,GlobalHotkeyManager}.swift`

**핵심 결정**

- `RegisterHotKey`는 **HWND와 메시지 루프**를 요구한다. 보이지 않는
  `HwndSource`를 하나 만들어 `WM_HOTKEY`를 받는다.
- **기본 조합은 mac 것을 옮기되 키 코드는 Windows 것으로.** mac의 Option은
  Windows의 Alt다: PTT = `Alt+Space`, 텍스트 입력 = `Alt+Shift+Space`,
  펫 부르기 = `Alt+Ctrl+Space`(mac의 Cmd 자리), 장난감 = `Alt+Shift+1/2`.
- **등록 실패를 삼키지 않는다.** `RegisterHotKey`는 다른 앱이 이미 잡은 조합에
  대해 그냥 false를 준다. 조용히 실패하면 사람은 자기 키가 왜 안 먹는지 모른다 —
  로그와 설정 UI에 "이미 다른 프로그램이 쓰는 중"으로 보여야 한다.
- 충돌 검사(`conflicts()`)는 순수 함수라 그대로 옮기고 테스트한다.

**Steps**
- [ ] `HotkeyBindingsTests`(기본값/충돌 검사) → 구현
- [ ] `MessageWindow` + `GlobalHotkeyManager` → 실제로 눌러 확인
- [ ] 커밋: `feat: global hotkeys on a hidden message window`

---

## Task 9: 부르면 온다 (MoveTo)

**Files:** `Movement/States/MoveToState.cs`, `StateKind` 추가 /
Test: `PuckTests/Movement/MoveToStateTests.cs`

**원본:** `Movement/States/MoveToState.swift`

- 목적지까지 걸어가고 도착하면 Idle. 도중에 발밑이 사라지면 Fall.
- 펫 부르기 핫키가 **커서 근처의 설 수 있는 자리**를 목적지로 준다.
  커서가 창 위면 그 창의 `PerchTarget`, 아니면 그 아래 착지면.

**Steps** — 실패 테스트 → 구현 → 핫키 연결 → 커밋: `feat: summon the pet with a hotkey`

---

## Task 10: 화면 조각을 캡처한다

**Files:** `Input/ScreenRegionCapture.cs` / Test: 없음(실제 화면 의존) — 눈 검증

**원본:** `Input/ScreenRegionCapture.swift`

**핵심 결정**

- **`BitBlt`에 `CAPTUREBLT`(0x40000000)를 반드시 준다.** 없으면 레이어드 창이
  캡처에 빠진다 — 우리 펫 자신이 레이어드 창이다. (Phase 1 검증 중에
  `Graphics.CopyFromScreen`으로 펫이 안 찍혀서 실제로 겪은 것이다.)
- 결과는 PNG 바이트로. 에이전트(Phase 3)가 그대로 첨부한다.
- `Windows.Graphics.Capture`는 WinRT 상호운용이 필요하니 Phase 6에서 다시 본다.

**Steps** — 구현 → 눈 검증 → 커밋: `feat: capture a screen region (CAPTUREBLT)`

---

## Task 11: 펫 옆 입력 버블

**Files:** `Input/SpeechBubblePlacement.cs`, `TextInputBubbleWindow.xaml(.cs)` /
Test: `PuckTests/Input/SpeechBubblePlacementTests.cs`

**원본:** `Input/{SpeechBubblePlacement,TextInputBubbleWindow,TextInputBubbleView}.swift`

- **위치 계산은 순수 함수.** 펫 위/옆 어디에 붙일지, 화면 밖으로 나가면 어느 쪽으로
  접을지 — 전부 테스트한다. 창 자체는 눈으로 본다.
- 버블 창은 오버레이와 달리 **입력을 받아야 하므로** `WS_EX_TRANSPARENT`를 켜지
  않는다. 다만 `WS_EX_NOACTIVATE`는 유지해서 뒤 창의 포커스를 뺏지 않는다.
- 색·타이포는 Phase 0의 `ClientPalette`/`ClientTheme` 토큰에서만 가져온다.

**Steps** — 배치 테스트 → 구현 → 커밋: `feat: text input bubble beside the pet`

---

## Task 12: 가리키고, 클릭한다

**Files:** `Pointing/{SyntheticClick,ClickDetector,PendingPointTracker,PointingController}.cs`,
`Interop/LowLevelMouseHook.cs`, `Movement/States/PointState.cs` /
Test: `PuckTests/Pointing/PendingPointTrackerTests.cs`

**원본:** `Pointing/*.swift`, `Movement/States/PointState.swift`

**핵심 결정**

- `SendInput`으로 클릭을 합성한다(`CGEventPost` 대응). **절대 좌표는 0..65535로
  정규화**해야 하고 가상 화면 전체를 기준으로 잡아야 한다 — 이걸 주 모니터
  기준으로 계산하면 보조 모니터에서 엉뚱한 곳을 누른다.
- `SetWindowsHookEx(WH_MOUSE_LL)`로 사람이 실제로 클릭했는지 감지한다.
  훅 콜백은 **반드시 빨리 끝나야 한다** — 여기서 무거운 일을 하면 시스템 전체의
  마우스가 끊긴다. 큐에 넣고 UI 스레드에서 처리한다.
- **UIPI:** 관리자 권한 창에는 합성 입력이 가지 않는다. 조용히 실패하므로
  클릭 후 확인이 불가능하다 — 도구 응답이 "권한이 더 높은 창이라 조작할 수
  없음"을 명시해야 한다.
- Phase 1의 **마우스 폴링을 이 훅으로 교체**한다. 폴링은 프레임 사이에 일어난
  클릭을 놓친다.

**Steps**
- [ ] `PendingPointTracker` 테스트(가리킨 뒤 N초 안의 클릭만 그 대상으로 친다) → 구현
- [ ] 훅/합성 클릭 구현 → 눈 검증
- [ ] `PetBootstrap.PollMouse` 제거하고 훅으로 교체 → 클릭/드래그/던지기 재검증
- [ ] 커밋: `feat: point at things, click them, and stop polling the mouse`

---

## Task 13: 소리

**Files:** `Audio/{SoundTable,SfxPlayer,FocusAssistObserver}.cs`, `Puck.csproj`(NAudio) /
Test: `PuckTests/Audio/SoundTableTests.cs`

**원본:** `Audio/{SoundTable,SFXPlayer,PlayerNodePool,FocusModeObserver}.swift`

**핵심 결정**

- `SoundTable`은 순수하다 — 매니페스트의 `sounds`를 키로 찾고, **없는 키는 무음**.
  경로는 `AvatarPackagePath`를 거친다(패키지 밖 파일 재생 금지).
- 겹쳐 재생이 필요하다(착지하며 말하고 동시에 클릭 반응). NAudio의
  `MixingSampleProvider` 하나에 얹는다 — mac의 플레이어 노드 풀에 해당.
- **집중 지원(Focus Assist) 중에는 소리를 내지 않는다.** mac의 `FocusModeObserver`와
  같은 예의다.

**Steps** — `SoundTableTests` → 구현 → 눈/귀 검증 → 커밋: `feat: sound effects`

---

## Task 14: 조립과 검증

**Files:** `App/PetBootstrap.cs`, `docs/verification.md`

- 워처를 띄우고, `LandingY`를 `LandingSurfaceResolver`로 물리고, 핫키/훅/오디오를
  붙이고, 마우스 폴링을 걷어낸다.
- `docs/verification.md`에 Phase 2 항목을 더한다: 창 위 착지, 창을 닫았을 때,
  최대화된 창, 관리자 권한 창(UIPI), 다중 모니터에서의 합성 클릭 좌표,
  집중 지원 중 무음, 핫키 충돌.
- 커밋: `feat: wire the senses into the pet`

---

# 진행 상태 (2026-08-25)

Task 1~14 전부 끝. 천장 기어다니기만 의도적으로 뺐다(위 Task 5).

# Phase 2 완료 조건

- `pet-app/scripts/test.ps1`이 실패 0.
- 펫이 **실제 창의 타이틀바 위에 선다**. 그 창을 닫으면 떨어진다.
- **핫키로 부르면** 커서 쪽으로 온다.
- **가리킨 곳을 클릭**할 수 있고, 관리자 권한 창에서는 그렇게 말한다.
- 착지·클릭 반응에 **효과음**이 난다. 집중 지원 중에는 조용하다.
- 마우스 폴링이 사라지고 저수준 훅이 그 자리를 대신한다.

# 다음 플랜

Phase 3(에이전트 코어 — Claude/GPT 클라이언트, 세션, 프롬프트, 승인, ACP, MCP,
도구 9종). 이 플랜이 만든 감각 위에 도구가 올라간다: `find_ui_element`와
`click_element`는 Task 7과 12를, `run_shell`/`run_powershell`은 Phase 3이 새로.
