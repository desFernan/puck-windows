# Puck for Windows

> Language: [English](README.md) · **한국어** (here)

> [**desFernan/puck-mac**](https://github.com/desFernan/puck-mac)(Swift/AppKit, macOS)의
> Windows 이식. C# / .NET 8 + WPF.
>
> 플랫폼: [macOS](https://github.com/desFernan/puck-mac) · **Windows** (여기) · [Linux](https://github.com/desFernan/puck-linux)

### 💬 [디스코드 참여하기](https://discord.gg/ePBZVnwSYE)

버그 제보, 기능 요청, 빌드 관련 질문, 아니면 그냥 놀러 오고 싶어도 —
[서포트 서버](https://discord.gg/ePBZVnwSYE)가 가장 빠른 연락 방법입니다. 놀러 오세요!

**지금 상태: Phase 0~4 완료 — 펫이 화면 위를 걷고, 말을 걸 수 있다.**
투명·항상 위·클릭스루 오버레이에 아바타가 뜨고, 물리와 상태기계가 돌고,
클릭·드래그·던지기가 되고, 작업표시줄 위에 착지한다. mac에서 만든 아바타
폴더를 그대로 읽는다. 창과 UI 요소를 감지하고, 채팅 창(트레이 → "대화 열기")
으로 Claude와 이야기한다. 도구는 아홉 개 — 창 목록, UI 요소 찾기, 가리키기,
클릭, 화면 캡처, 프로그램 실행, PowerShell. 무언가를 바꾸는 도구는 실행 전에
사람에게 묻는다. 코드 에디터와 터미널 패널은 Phase 5~6이다.

## 빌드

```powershell
pet-app\scripts\build.ps1                     # Release 빌드
dotnet run --project pet-app\Puck\Puck.csproj # 펫 띄우기
```

.NET 8 SDK(또는 그 이상 + net8.0-windows 타게팅) 가 필요하다.

## 테스트

```powershell
pet-app\scripts\test.ps1   # xUnit, 무인 실행
```

무인 실행이며, 실패가 있으면 nonzero로 종료한다.

## 에이전트 프로바이더

Anthropic. 공식 C# SDK를 쓴다(macOS가 HTTP를 직접 쓰는 이유는 Swift용 공식
SDK가 없어서다). 키는 `%LOCALAPPDATA%\Puck\.env`에 넣는다:

```
ANTHROPIC_API_KEY=sk-ant-...
PUCK_MODEL=claude-opus-5      # 선택
AGENT_PERMISSIONS=tools       # tools | edits | all | auto
```

묻기를 멈추는 것은 `auto` 하나뿐이다 — 펫 자신의 승인 도구(셸 명령, 남의 창에
대한 클릭)가 채팅에 물음을 띄우지 않고 곧장 돈다. 나머지 셋은 코딩 CLI가 혼자
무엇을 하느냐만 정하고 그 게이트는 건드리지 않는다. 없거나 모르는 값은 가장
좁은 `tools`로 떨어진다.

환경 변수가 파일을 이기고, 파일은 요청마다 다시 읽는다 — 키를 넣으려고 앱을
껐다 켤 필요가 없다. macOS의 `run_applescript` 자리는 `run_powershell`이
받는다. ACP 기반 코드 에디터는 아직 Phase 5다.

## 내 것으로 만들기

바꿀 수 있는 모든 것은 폴더 하나에 있다 — macOS와 같은 패키지 포맷이고,
루트 경로만 다르다:

```
%LOCALAPPDATA%\Puck\
    Avatars\<이름>\     캐릭터 하나당 폴더 하나
    Tank\seabed.png     섬을 채우는 그림
    logs\puck-YYYY-MM-DD.jsonl
    settings.json
```

이 폴더들은 첫 실행 시 자동으로 만들어진다.

### 수조 (Tank)

`Tank\`에 `seabed.png`를 넣으면 앱이 기본으로 제공하는 것을 대체한다. 앱
시작 시 한 번만 읽으므로, 바꾼 뒤엔 펫을 재시작한다 — 섬 렌더링 규칙이
이식 과정에서 바뀌지 않았으므로 macOS와 동일하게 동작한다.

### 캐릭터

아바타는 `manifest.json`과 클립별 PNG가 함께 들어 있는 폴더다:

```
Avatars\my-pet\
    manifest.json
    idle.png  walk.png  fall.png  …
    sounds\*.wav
```

패키지 포맷(`schema_version: 1`)은 puck-mac이 정의하고 Windows는 그대로
읽는다 — macOS에서 만든 아바타 폴더가 수정 없이 그대로 들어간다. 필드
전체 설명(`clips`, `emotions`, `sounds`, `hitbox`, `bounce_intensity`,
최소 동작 manifest)은
[puck-mac README](https://github.com/desFernan/puck-mac/blob/main/README.ko.md#캐릭터)에
있으며, 여기서도 변경 없이 그대로 적용된다.

**Windows에서 새/수정된 아바타 불러오기:** 폴더를 `Avatars\`에 넣고 트레이
메뉴의 "아바타 다시 불러오기"를 누른다 — 재시작 불필요, macOS의 다시
불러오기 버튼과 동일하다.

## 커뮤니티

Windows 포팅 계획에 힘을 보태고 싶거나, 그냥 진행 상황이 궁금하다면 —
**[디스코드](https://discord.gg/ePBZVnwSYE)**로 오세요.
