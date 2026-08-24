# puck-windows 포팅 기획

> 2026-08-24. `desFernan/puck-mac`(Swift/AppKit/SwiftUI, macOS)를 Windows로 1:1
> 이식하기 위한 설계 문서. 이 문서는 "무엇을 어떤 순서로 만드는가"의 정본이고,
> 값이나 동작의 정본은 언제나 puck-mac 쪽 소스다.

## 0. 전제

- **범위: 완전 1:1 포팅.** 펫, 에이전트, 채팅 창, 워크스페이스, git 상태, 코드
  에디터, 터미널 패널까지 전부 포함한다. 기능을 덜어내는 결정은 이 문서의
  §5(결정 사항)에 명시된 것들뿐이다.
- **puck-linux와 완전 독립.** 공유 코어를 뽑거나 크로스플랫폼 추상화를 두지
  않는다. 리눅스 포팅은 별개 프로젝트로 별도 시점에 시작한다.
- **1:1은 기능의 1:1이지 코드 구조의 1:1이 아니다.** macOS 제약 때문에 존재하는
  구조(§5-①의 프로세스 분리 등)는 Windows에서 그 제약이 없으면 따라가지 않는다.

### 이식 대상 규모 (puck-mac, 2026-08-24 기준)

앱 코드 Swift 약 34k줄 + 테스트 포함 약 63.5k줄.

| 모듈 | 줄 수 | 성격 |
|---|---:|---|
| `Puck/ClientWindow` | 10,131 | UI — 채팅 3.6k, 에디터 3.9k, git 0.5k, 테마/셸 나머지 |
| `Puck/Agent` | 6,509 | 순수 로직 — ACP 2.0k, MCP 0.8k, 클라이언트/세션/프롬프트 |
| `Puck/Movement` | 4,304 | 순수 로직 — 물리, 상태기계 1.3k, 장난감 1.2k |
| `Puck/App` | 3,017 | 앱 셸, 메뉴바, 아이콘 |
| `Puck/Bridge` | 1,998 | 프로세스 간 소켓 (→ §5-①에서 대부분 소멸) |
| `Puck/Avatar` | 1,701 | 매니페스트 파싱, 스프라이트 로딩 |
| `Puck/Tools` | 1,253 | 에이전트 도구 9종 |
| `Puck/Localization` | 1,170 | 한국어 UI 문자열 |
| `Puck/Settings` | 1,115 | 설정 저장/창 |
| `Puck/Input` | 769 | 전역 핫키, 화면 캡처, 입력 버블 |
| `Puck/Overlay` | 725 | 투명 오버레이 창, 클릭스루, 스프라이트 레이어 |
| `Puck/WindowSensing` | 461 | 창 열거, Accessibility 조회 |
| `Puck/Workspaces` | 403 | 워크스페이스 목록 |
| `Puck/Audio` | 295 | 효과음 |
| `Puck/Voice` | 278 | 음성 인식 |
| `Puck/Diagnostics` | 266 | 로그 |
| `Puck/Pointing` | 229 | 합성 클릭, 클릭 감지 |
| `Puck/Customisation` | 54 | 커스터마이징 폴더 열기 |

---

## 1. 기술 스택: C# / .NET 8 + WPF

### 결론

- **언어/런타임: C# / .NET 8 (LTS)**
- **UI: WPF**
- **배포: `dotnet publish -r win-x64 --self-contained` + Inno Setup 인스톨러**
- **테스트: xUnit**

### WPF를 고른 이유

Puck의 절반은 UI 앱이 아니라 OS 후킹 덩어리다. 투명·클릭스루·항상 위·비활성
오버레이 창(`Overlay`), 전역 핫키와 화면 캡처(`Input`), 창 열거와 Accessibility
트리 탐색(`WindowSensing`), 합성 클릭과 저수준 마우스 훅(`Pointing`), UI 요소
검색/클릭 도구(`Tools/Handlers`). 이건 전부 Win32 + UI Automation 직결이고,
.NET에서는 P/Invoke 없이도 거의 1급 API로 존재한다.

- **WinUI 3을 뺀 이유.** 투명 배경 + 테두리 없음 + topmost + no-activate 창이
  WinUI 3에서 여전히 고질적으로 불안정하다. Puck 오버레이의 핵심 요구가 정확히
  그것이다. WPF는 `AllowsTransparency=true` + `WS_EX_LAYERED` /
  `WS_EX_TRANSPARENT` / `WS_EX_NOACTIVATE` 토글로 오늘 바로 된다.
- **WPF의 기본 룩이 문제가 안 되는 이유.** Puck은 이미 시스템 색/폰트를 쓰지
  않는다. `pet-app/design.md`의 제1원칙 그대로, 모든 색·타이포·간격이
  `ClientPalette` / `ClientTheme` 토큰에서 나온다. 그 값들을 `ResourceDictionary`
  두 벌(light/dark)로 옮기면 디자인 시스템이 수치 그대로 이식된다. 프레임워크
  기본 컨트롤 룩을 볼 일이 없다.
- **Electron / Tauri를 뺀 이유.** (1) 펫 절반은 어차피 네이티브 사이드카를 따로
  짜게 되고, 그러면 언어 두 개와 브리지 하나가 공짜로 늘어난다. (2) puck-mac의
  `docs/decisions.md`가 같은 실수의 기록이다 — chat-web(React/Tailwind/shadcn)을
  넣었다가 이틀 만에 지웠고(2026-08-13 → 2026-08-15), workspace 웹 레이어도
  지웠다. "웹 UI + JS↔네이티브 브리지"는 이 프로젝트가 이미 두 번 버린 길이다.
- **Swift on Windows를 뺀 이유.** 그대로 재사용 가능한 건 `Agent` + `Movement`
  약 10k줄로 전체의 30% 미만인데, 대가로 아무도 디버깅 못 하는 툴체인과 UI
  프레임워크 부재를 산다. 수지가 맞지 않는다.

---

## 2. 아키텍처

### 프로세스 구성

**단일 프로세스, 두 윈도우.** (근거는 §5-①)

```
Puck.exe
├─ 트레이 아이콘 (NotifyIcon)              ← mac의 NSStatusItem 메뉴바
├─ PetOverlayWindow  (모니터당 1개, 투명·topmost·클릭스루)
├─ ClientWindow      (일반 창: 채팅 + 에디터 + 상태바)
├─ SettingsWindow
└─ TextInputBubbleWindow (펫 옆 입력 버블)
```

### 프로젝트 레이아웃

puck-mac의 `pet-app/` 안 폴더 구조를 그대로 따른다. 파일 단위 대응을 유지해야
"mac 쪽 어디를 보면 되는가"가 항상 자명하다.

```
pet-app/
  Puck.sln
  Puck/                       (WPF 앱, net8.0-windows)
    App/  Overlay/  Movement/  Avatar/  Input/  WindowSensing/
    Pointing/  Tools/  Agent/  ClientWindow/  Settings/  Audio/
    Voice/  Workspaces/  Diagnostics/  Localization/  Interop/
    Resources/
  PuckTests/                  (xUnit, Puck/ 폴더 구조 미러)
  scripts/
    build.ps1  test.ps1  package.ps1  check-resources.ps1
```

`Interop/`만 mac에 없는 새 폴더다 — P/Invoke 선언, 윈도우 스타일 플래그,
UI Automation 래퍼를 여기 모아 두고 나머지 코드에서 Win32를 직접 부르지 않는다.

### 레이어 원칙

1. **플랫폼 무관 로직**(`Movement`, `Avatar`, `Agent`, `Workspaces`,
   `Localization`)은 Win32를 모른다. 기계적 번역 대상이고, 테스트가 붙는 곳이다.
2. **플랫폼 계층**(`Overlay`, `Input`, `WindowSensing`, `Pointing`, `Audio`,
   `Voice`, `Tools/Handlers`)은 `Interop/`을 통해서만 OS와 대화한다.
3. **UI**(`ClientWindow`, `Settings`)는 토큰만 보고 그린다. 시스템 색/폰트 금지.

---

## 3. 모듈별 이식 지도

| puck-mac | macOS API | Windows 대응 | 난이도 |
|---|---|---|---|
| `Overlay/OverlayWindow`, `ClickThroughController` | NSWindow, CALayer | WPF 투명창 + `WS_EX_LAYERED/TRANSPARENT/NOACTIVATE`, `HWND_TOPMOST` | 쉬움 |
| `Overlay/SpriteLayerView` | CALayer | WPF `Canvas` + `Image` (`CompositionTarget.Rendering`) | 쉬움 |
| `Overlay/ScreenManager`, `DockInset` | NSScreen, Dock | `Screen.AllScreens`, 작업표시줄 `WorkingArea`, per-monitor DPI v2 | 쉬움 |
| `Overlay/PetGestureRecognizer` | NSGestureRecognizer | WPF 마우스 이벤트 | 쉬움 |
| `Movement/*` (4.3k줄) | 순수 로직 | 그대로 번역 | 기계적 |
| `Avatar/*` | 순수 로직 + ImageIO | 그대로 번역, 디코딩만 WIC | 기계적 |
| `Input/GlobalHotkeyManager` | Carbon `RegisterEventHotKey` | `RegisterHotKey` | 쉬움 |
| `Input/ScreenRegionCapture` | `CGWindowListCreateImage` | `Windows.Graphics.Capture` (폴백: `BitBlt`) | 쉬움 |
| `Input/TextInputBubbleWindow` | NSPanel | WPF 무테 topmost 창 | 쉬움 |
| `WindowSensing/WindowListWatcher`, `WindowInfo` | `CGWindowListCopyWindowInfo` | `EnumWindows` + `GetWindowRect` + `DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)` | 중간 |
| `WindowSensing/UIElementInspector`, `UIElementSearch` | AXUIElement | `System.Windows.Automation` (UIA) | 중간 |
| `WindowSensing/AccessibilityPermission` | TCC 권한 | **불필요** — Windows엔 UIA 권한 프롬프트가 없다 | 삭제 |
| `WindowSensing/LandingSurfaceResolver` | 순수 로직 + 창 정보 | 그대로, 입력만 교체 | 기계적 |
| `Pointing/SyntheticClick` | `CGEventPost` | `SendInput` | 쉬움 |
| `Pointing/ClickDetector` | CGEventTap | `SetWindowsHookEx(WH_MOUSE_LL)` | 중간 |
| `Tools/run_shell` | `/bin/sh` | `powershell.exe -NoProfile -Command` | 쉬움 |
| `Tools/run_applescript` | `osascript` | → `run_powershell`로 교체 (§5-②) | 결정 |
| `Tools/launch_app`, `list_running_apps` | NSWorkspace | `Process.Start`, `Process.GetProcesses` + 시작 메뉴 인덱스 | 중간 |
| `Tools/find_ui_element`, `click_element` | Accessibility | UIA `TreeWalker` + `InvokePattern` | 중간 |
| `Tools/get_frontmost_window` | NSWorkspace | `GetForegroundWindow` | 쉬움 |
| `Tools/point_at` | 순수 로직 | 그대로 | 기계적 |
| `Agent/*` (6.5k줄) | Foundation, URLSession | `HttpClient` + `System.Text.Json` | 기계적 |
| `Agent/ACP` (node 하위프로세스) | Process + 파이프 | `Process` + 리다이렉트 (node.exe) | 쉬움 |
| `Audio/*` | AVAudioEngine | NAudio | 쉬움 |
| `Audio/FocusModeObserver` | 집중 모드 | 집중 지원(Focus Assist) 쿼리 | 쉬움 |
| `Voice/*` | SFSpeechRecognizer | 1차 제외 → whisper.cpp (§5-③) | 결정 |
| `App/MenuBarController` | NSStatusItem | 트레이 `NotifyIcon` + 컨텍스트 메뉴 | 쉬움 |
| `App/CompanionAppLauncher` | 앱 실행 | **불필요** (단일 프로세스) | 삭제 |
| `Bridge/*` | 유닉스 소켓 | **대부분 삭제** (§5-①) | 삭제 |
| `Settings/*` | UserDefaults | `%LOCALAPPDATA%\Puck\settings.json` | 쉬움 |
| `Diagnostics/*` | 파일 로그 | 그대로 (경로만 교체) | 기계적 |
| `ClientWindow/Chat` (3.6k줄) | SwiftUI | WPF + 토큰 리소스 | 큼(양) |
| `ClientWindow/Editor` (3.9k줄) | CodeEditSourceEditor | **AvalonEdit** + TextMateSharp | 중간 |
| `ClientWindow/Git` | 프로세스 호출 | 그대로 (`git.exe`) | 기계적 |
| 터미널 패널 | SwiftTerm | ConPTY + 위젯 미정 (§5-④) | 결정 |

### 권한 모델의 차이

macOS는 Accessibility 권한(TCC)이 코드 서명에 묶여 있어서, 재빌드마다 권한이
날아가지 않게 안정적인 서명이 필요했다 — `pet-app/scripts/install.sh`가 존재하는
이유가 그것이다. **Windows에는 이 제약이 통째로 없다.** UIA 조회, 입력 훅, 창
열거 모두 권한 프롬프트가 없다. 대신 두 가지가 새로 생긴다:

- **UIPI(권한 격리).** 일반 권한으로 실행 중인 Puck은 관리자 권한으로 실행된
  창에 입력을 보내거나 UIA로 조작할 수 없다. 조용히 실패하므로 도구 응답에
  "권한이 더 높은 창이라 조작할 수 없음"을 명시적으로 반환해야 한다.
- **SmartScreen.** 서명되지 않은 인스톨러는 첫 실행 시 경고가 뜬다. 코드 서명
  인증서는 선택 사항이고, 없으면 README에 우회 방법을 적는다.

### 경로 매핑

| macOS | Windows |
|---|---|
| `~/Library/Application Support/Puck/` | `%LOCALAPPDATA%\Puck\` |
| `.../Puck/Avatars/<name>/` | `%LOCALAPPDATA%\Puck\Avatars\<name>\` |
| `.../Puck/Tank/seabed.png` | `%LOCALAPPDATA%\Puck\Tank\seabed.png` |
| `.../Puck/logs/` | `%LOCALAPPDATA%\Puck\logs\` |
| Puck의 `.env` | 같은 폴더의 `.env` |

아바타 패키지 포맷(`manifest.json`, `schema_version: 1`, 클립/감정/사운드 규칙)은
**바꾸지 않는다.** mac에서 만든 아바타 폴더가 Windows에서 그대로 동작해야 한다.
매니페스트 경로 검증(패키지 밖으로 나가는 경로 거부)도 그대로 가져간다 —
Windows에서는 `..` 외에 드라이브 접두사(`C:`), UNC(`\\`), 대체 데이터 스트림(`:`)
까지 막아야 하므로 검증 로직은 더 빡빡해진다.

---

## 4. 단계별 계획

각 단계는 "돌아가는 무언가"로 끝난다. 앞 단계가 안 끝나면 뒤가 못 나가는 순서다.

### Phase 0 — 골격 (기반)
솔루션/프로젝트 생성, `Interop/` P/Invoke 기반, `Diagnostics` 로거,
`Settings` 저장소, `Localization` 문자열, 디자인 토큰 `ResourceDictionary`
(light/dark, `design.md`의 값 그대로), `build.ps1` / `test.ps1`, CI 워크플로.
**끝나는 조건:** 빈 창이 뜨고 `test.ps1`이 초록.

### Phase 1 — 펫이 산다 ⭐ 첫 데모
`Overlay` + `Avatar` + `Movement` + 트레이 아이콘.
투명 클릭스루 오버레이가 모니터마다 뜨고, 아바타 패키지를 읽고, 물리/상태기계가
돌고, 클릭·드래그·던지기가 되고, 작업표시줄 위에 착지한다.
**끝나는 조건:** 화면에서 펫이 걷는다. mac에서 만든 아바타 폴더가 그대로 로드된다.
**의존:** Phase 0. **가장 위험한 미지수는 여기 있다** — 투명 오버레이의 60fps
성능과 멀티모니터/혼합 DPI 좌표계. 나머지 계획이 이 위에 서므로 먼저 뚫는다.

### Phase 2 — 감각 기관
`WindowSensing`(창 열거 + UIA), `Input`(전역 핫키, 화면 캡처, 입력 버블),
`Pointing`(합성 클릭, 마우스 훅), `Audio`.
**끝나는 조건:** 펫이 실제 창의 타이틀바 위에 서고, 핫키로 부르면 오고,
가리킨 곳을 클릭할 수 있고, 효과음이 난다.
**의존:** Phase 1.

### Phase 3 — 에이전트 코어
`Agent`(Claude/GPT 클라이언트, 세션, 프롬프트, 승인, ACP, MCP, 스킬) +
`Tools`(도구 9종, `run_applescript` → `run_powershell` 교체 포함).
UI 없이 콘솔/테스트로 검증한다.
**끝나는 조건:** 테스트에서 대화가 돌고, 도구 호출이 실제로 창을 조작한다.
**의존:** Phase 2 (도구가 Phase 2의 능력을 쓴다). Phase 1과 병행 가능한 부분이
많다 — 순수 로직이라 실제로는 Phase 0 직후 시작해도 된다.

### Phase 4 — 클라이언트 창
`ClientWindow` 셸 + `Chat` + `ClientStatusBarView` + `Git` + `Settings` 창 +
`Workspaces`.
**끝나는 조건:** 채팅으로 펫에게 시키고 결과를 본다. 앱이 통째로 쓸 만해지는
지점.
**의존:** Phase 3.

### Phase 5 — 코드 에디터
`ClientWindow/Editor` — AvalonEdit + TextMateSharp, 파일 탐색기, 충돌 배너,
`code_editor` / `open_in_editor` 도구 연결.
**의존:** Phase 4.

### Phase 6 — 남은 것
터미널 패널(§5-④), 음성 입력(§5-③), 아이콘/인스톨러 다듬기, 패키징.

**병행 가능성:** Phase 3은 순수 로직이라 Phase 1/2와 독립적으로 진행할 수 있다.
Phase 4의 UI 껍데기(테마·레이아웃)도 Phase 3과 병행 가능하다. Phase 1 → 2는
반드시 순차.

---

## 5. 결정 사항

### ① 프로세스 분리를 하지 않는다 — 단일 프로세스, 두 윈도우

mac은 `Puck.app`(펫) + `PuckClient.app`(창)이 유닉스 소켓 브리지로 대화한다.
그 구조의 비용이 `Bridge/` 2,000줄이고, 부작용도 문서에 남아 있다 —
`design.md` §1: "PuckClient는 별도 프로세스라 UserDefaults를 못 읽는다"는
이유로 테마 값을 알림 `userInfo`에 실어 보내는 레이스 회피 코드가 있다.

Windows에서는 한 프로세스가 topmost 레이어드 창과 일반 창을 동시에 들고 있는 데
아무 문제가 없다. 분리를 유지하면 이득 없이 IPC 계층과 그 레이스만 그대로
수입하게 된다.

→ **단일 프로세스.** `Bridge/`는 인프로세스 호출로 대체되어 대부분 사라지고,
`App/CompanionAppLauncher`도 사라진다. `Bridge/JSONValue.swift`처럼 브리지가
아니라 값 타입인 파일은 남긴다. 기능은 1:1 그대로다.

*나중에:* 에이전트 호스트가 독립적으로 죽어야 할 진짜 이유가 생기면 그때 쪼갠다.

### ② `run_applescript` → `run_powershell`

AppleScript는 Windows에 대응물이 없다. AppleScript가 맡던 앱 자동화 역할은
Windows에서 COM(`New-Object -ComObject ...`)이 맡고, 그건 PowerShell에서 나온다.

→ 도구 이름을 `run_powershell`로 바꾸고, `AgentPrompts`의 시스템 프롬프트에서
AppleScript를 설명하던 문단도 같이 교체한다. `run_shell` 역시 `/bin/sh`가 아니라
`powershell.exe`를 쓰므로 두 도구의 경계를 프롬프트에서 다시 정의해야 한다 —
`run_shell`은 명령 한 줄, `run_powershell`은 스크립트/COM 자동화.

### ③ 음성 입력은 1차 범위에서 제외

`SFSpeechRecognizer`의 무료·온디바이스 대응이 Windows엔 사실상 없다.
`Windows.Media.SpeechRecognition`의 받아쓰기는 온라인 의존에 품질이 낮고,
Azure Speech는 유료 키를 요구한다.

→ **1차에는 텍스트 입력만.** `Voice/` 자리는 인터페이스만 남겨 두고, Phase 6에서
whisper.cpp를 번들해 채운다. 음성은 펫의 정체성이 아니고, 빠졌을 때 가장 티가
안 나는 조각이다.

### ④ 터미널 패널은 마지막에 붙인다

여기가 이 포팅에서 유일하게 진짜 불확실한 부분이다. ConPTY(`CreatePseudoConsole`,
Windows 10 1809+)는 표준 API라 문제없지만, SwiftTerm에 해당하는 정착된 WPF용 VT
렌더러가 없다. 후보는 셋이다:

1. `Microsoft.Terminal.Wpf` — Windows Terminal 저장소의 WPF 컨트롤. 공식 배포
   패키지가 아니라서 직접 빌드/벤더링해야 한다.
2. WebView2 + xterm.js — 확실히 동작하지만 이 한 곳에만 웹 스택이 들어온다.
3. 직접 구현 — VT 파서 + 렌더러. 범위가 크다.

→ **터미널 패널을 Phase 6으로 미룬다.** 여기서 막혀서 나머지 33k줄이 멈추면
안 된다. 선택은 Phase 5가 끝난 뒤 각 후보로 스파이크를 하루씩 태워 보고 정한다.

---

## 6. 테스트와 검증

- **단위 테스트(xUnit).** `PuckTests/`가 `Puck/`의 폴더 구조를 그대로 미러한다.
  puck-mac의 `PuckTests`가 이미 같은 구조이므로 테스트 케이스도 함께 번역한다 —
  특히 `Movement`, `Avatar`, `Agent`, `Localization`은 플랫폼 무관이라 mac 쪽
  테스트가 거의 그대로 넘어온다. `scripts/test.ps1`은 무인 실행이고 실패 시
  0이 아닌 코드로 종료한다. `node`나 `claude`/`codex` CLI가 없는 머신에서는
  실패가 아니라 skip — mac 쪽 규약 그대로.
- **CI.** `.github/workflows/windows-tests.yml`, `windows-latest` 러너에서
  `test.ps1` 실행. mac의 `macos-tests.yml`과 대칭.
- **수동 검증 문서.** `docs/verification.md`를 puck-mac에서 옮겨 오되 Windows
  항목으로 다시 쓴다. 자동화가 어려운 것들이 여기 들어간다: 멀티모니터 + 혼합
  DPI에서의 펫 좌표, 작업표시줄 자동 숨김 상태에서의 착지, 전체화면 앱 위에서의
  오버레이 동작, UIPI 차단 시 도구의 오류 메시지, 절전/화면 잠금 복귀.
- **결정 기록.** `docs/decisions.md`를 새로 시작한다. mac 것을 복사하지 않는다 —
  거기 적힌 결정은 macOS 맥락의 것이고, Windows에서 다르게 간 결정만 이 저장소가
  기록한다. §5의 네 항목이 첫 엔트리다.

## 7. 배포

- `dotnet publish -c Release -r win-x64 --self-contained` — .NET 런타임 설치를
  사용자에게 요구하지 않는다.
- **Inno Setup** 인스톨러. MSIX는 쓰지 않는다 — 컨테이너화가 전역 훅·UIA·임의
  경로 접근과 싸우고, Puck은 그 셋을 전부 한다.
- 시작 프로그램 등록은 설정의 토글로, `HKCU\...\Run` 레지스트리 값을 쓴다.
- 코드 서명은 선택 사항. 없으면 첫 실행에 SmartScreen 경고가 뜨고, README에
  우회 방법을 적는다.

## 8. 이 문서가 다루지 않는 것

- puck-linux. 완전 독립이므로 어떤 추상화도 리눅스를 위해 만들지 않는다.
- mac과의 설정/세션 동기화. 두 앱은 서로를 모른다.
- 아바타 패키지 포맷 변경. 포맷은 mac이 정본이고 Windows는 그것을 읽을 뿐이다.
