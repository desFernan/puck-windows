# Puck for Windows

> Language: [English](README.md) · **한국어** (here)

> [**desFernan/puck-mac**](https://github.com/desFernan/puck-mac)(Swift/AppKit,
> macOS)의 Windows 포팅입니다. C# / .NET 8 + WPF.
>
> 플랫폼: [macOS](https://github.com/desFernan/puck-mac) · **Windows** (여기) · [Linux](https://github.com/desFernan/puck-linux)

### 💬 [디스코드 참여하기](https://discord.gg/nGqtBGP857)

버그 제보, 기능 요청, 빌드 관련 질문, 아니면 그냥 놀러 오고 싶어도 —
[서포트 서버](https://discord.gg/nGqtBGP857)가 가장 빠른 연락 방법입니다. 놀러 오세요!

AI 에이전트이기도 한 Windows 데스크톱 펫입니다. .NET 8 앱 하나로 구성돼 있어요:

- **펫 본체** — 투명·항상 위·클릭스루 캐릭터가 화면을 걸어 다니고, 창이나 화면
  가장자리를 타고 천장까지 올라가 거꾸로 매달려 기어다니고, 작업표시줄 위에
  착지합니다. 클릭·드래그·던지기가 됩니다.
- **채팅 창** — 트레이 → **대화 열기**. 도구 아홉 개로 Windows를 조작합니다:
  창 목록, 최상단 창, UI 요소 찾기, 가리키기, 클릭, 화면 캡처, 프로그램 실행,
  `run_shell`, `run_powershell`. 무언가를 바꾸는 도구는 실행 전에 사람에게 묻습니다.
- **섬** — 채팅 창 맨 위의 그림 패널. 접으면 그 그림의 색을 딴 띠가 됩니다.
  창을 열면 펫이 날아와 그 안에 살고, 닫으면 바탕화면으로 돌아갑니다.
  `Tank\seabed.png`를 넣으면 그 그림으로 채워집니다.

트레이에는 **설정…**(아바타·이동 속도·테마·자동 시작), **커스터마이징 폴더 열기**,
**아바타 다시 불러오기**도 있습니다. 에이전트 코어(채팅, 도구, 승인)는
`pet-app/Puck/Agent`에 있습니다.

아직 포팅되지 않은 것: puck-mac의 별도 앱 `PuckClient`에 있는 코드 에디터,
터미널 패널, 워크스페이스.

## 빌드

```powershell
pet-app\scripts\build.ps1                     # Release 빌드
dotnet run --project pet-app\Puck\Puck.csproj # 펫 실행
```

.NET 8 SDK(또는 그 이상, `net8.0-windows` 타깃)가 필요합니다.

## 테스트

```powershell
pet-app\scripts\test.ps1   # xUnit
```

무인 실행이며, 실패가 있으면 nonzero로 종료합니다.

## 에이전트 프로바이더

Anthropic 공식 C# SDK를 씁니다 (macOS는 공식 Swift SDK가 없어서 HTTP API를
직접 호출합니다). 키는 `%LOCALAPPDATA%\Puck\.env`에 넣습니다:

```
ANTHROPIC_API_KEY=sk-ant-...
PUCK_MODEL=claude-opus-5      # 선택
AGENT_PERMISSIONS=tools       # tools | edits | all | auto
```

묻기를 그만두는 모드는 `auto` 하나뿐입니다: 승인이 걸린 Puck 자신의 도구 —
셸 명령, 남의 창 클릭 — 가 채팅에 확인을 띄우지 않고 바로 실행됩니다. 나머지
셋은 코딩 CLI가 혼자 할 수 있는 범위만 정할 뿐, 그 게이트는 건드리지 않습니다.
없거나 모르는 값은 가장 좁은 `tools`로 떨어집니다.

환경 변수가 파일을 이기고, 파일은 요청마다 다시 읽습니다 — 키를 넣으려고 앱을
껐다 켤 필요가 없습니다. macOS의 `run_applescript` 자리는 `run_powershell`이
받습니다. ACP 기반 코드 에디터는 아직 포팅되지 않았습니다.

## 내 것으로 만들기

바꿀 수 있는 모든 것은 폴더 하나에 있습니다 — 패키지 형식은 macOS와 같고,
루트만 다릅니다:

```
%LOCALAPPDATA%\Puck\
    Avatars\<name>\                  캐릭터 하나당 폴더 하나
    Tank\seabed.png                  섬을 채우는 그림
    settings.json
    .env
    logs\puck-YYYY-MM-DD.jsonl
```

트레이의 **커스터마이징 폴더 열기**가 이 폴더를 열어 주고, 없으면 만들어 줍니다.

### 캐릭터

아바타는 `manifest.json` 하나와 클립별 PNG가 든 폴더입니다:

```
Avatars\my-pet\
    manifest.json
    idle.png  walk.png  fall.png  …
    sounds\*.wav
```

#### 처음부터 끝까지 추가하기

1. **폴더 열기.** 트레이 → **커스터마이징 폴더 열기**. `Avatars\`가 없으면
   만들어 주니, 폴더가 있다는 것도 이걸로 확인됩니다.
2. **`Avatars\` 안에 캐릭터 폴더를 만듭니다.** 폴더 이름이 그대로 목록에 뜨는
   이름입니다: `Avatars\my-pet\`은 `my-pet`으로 보입니다.
3. **PNG 하나와 `manifest.json`을 넣습니다.** 그림 한 장이면 동작하는
   캐릭터입니다 — 반드시 있어야 하는 클립은 `idle` 하나뿐이고 나머지 상태는
   전부 여기로 떨어지니, 한 장으로 시작해서 걷기·오르기 같은 걸 나중에 더해도
   됩니다. 배경은 투명하게, 오른쪽을 보게 그리세요 (왼쪽으로 걸을 땐 좌우로
   뒤집힙니다). 동작하는 가장 작은 manifest:

   ```json
   {
     "schema_version": 1,
     "name": "my-pet",
     "type": "sprites",
     "hitbox": { "width": 130, "height": 133 },
     "clips": { "idle": "idle" }
   }
   ```

   `hitbox`는 그려지고 클릭되는 크기입니다 — 그림 비율과 맞추지 않으면
   찌그러져 보입니다.
4. **불러오기.** 트레이 → **아바타 다시 불러오기**, 그다음 **설정…**에서
   선택합니다. 재시작은 필요 없습니다: 다시 불러오기가 디스크에 있는 것으로
   실행 중인 펫을 다시 만들기 때문에, 그림을 고치거나 manifest를 바꾼 것도
   앱을 끄지 않고 확인할 수 있습니다.

패키지에 문제가 있으면 펫은 바뀌지 않고, 이유는 로그
(`%LOCALAPPDATA%\Puck\logs\`)에 남습니다 — `idle` 파일이 없거나, manifest가
파싱되지 않거나, 이 빌드가 모르는 `schema_version`이거나.

패키지 형식(`schema_version: 1`)은 puck-mac이 정의하고 여기서는 그대로
읽습니다. macOS에서 만든 아바타 폴더가 그대로 들어옵니다. 전체 필드 설명 —
`clips`, `emotions`, `sounds`, `hitbox`, `bounce_intensity`와 각각의 기본값 —
은 [puck-mac README](https://github.com/desFernan/puck-mac#a-character)에 있고,
여기에도 그대로 적용됩니다.

## 커뮤니티

질문, 버그 제보, 기능 아이디어, 아니면 그냥 직접 만든 아바타를 자랑하고 싶어도 —
**[디스코드](https://discord.gg/nGqtBGP857)**에서 만나요.
