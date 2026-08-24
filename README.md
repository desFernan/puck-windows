# Puck for Windows

[**desFernan/puck-mac**](https://github.com/desFernan/puck-mac)(Swift/AppKit, macOS)의
Windows 이식. C# / .NET 8 + WPF.

**지금 상태: Phase 0 + Phase 1 완료 — 펫이 화면 위를 걷는다.**
투명·항상 위·클릭스루 오버레이에 아바타가 뜨고, 물리와 상태기계가 돌고,
클릭·드래그·던지기가 되고, 작업표시줄 위에 착지한다. mac에서 만든 아바타
폴더를 그대로 읽는다. 에이전트·채팅 창·에디터·터미널은 Phase 2~6이다.

## 빌드와 실행

```powershell
pet-app\scripts\build.ps1                     # Release 빌드
pet-app\scripts\test.ps1                      # xUnit, 무인 실행
dotnet run --project pet-app\Puck\Puck.csproj # 펫 띄우기
```

.NET 8 SDK(또는 그 이상 + net8.0-windows 타게팅) 가 필요하다.

## 아바타

사용자 데이터는 `%LOCALAPPDATA%\Puck\` 아래에 있다.

```
%LOCALAPPDATA%\Puck\
  Avatars\<이름>\manifest.json   한 폴더 = 한 캐릭터
  logs\puck-YYYY-MM-DD.jsonl     한 줄 = 한 이벤트
  settings.json
```

`Avatars\`가 비어 있으면 동봉된 `dummy` 아바타로 뜬다. 폴더를 넣거나 고친 뒤
트레이 메뉴의 "아바타 다시 불러오기"를 누르면 앱을 끄지 않고 반영된다.
패키지 포맷(`schema_version: 1`)의 정본은 puck-mac이고 Windows는 그것을 읽기만 한다.

## 문서

- [`docs/porting-design.md`](docs/porting-design.md) — 스택 선택, 모듈별 이식 지도,
  단계 계획, Windows가 일부러 다르게 가는 네 가지
- [`docs/plans/`](docs/plans) — 단계별 구현 플랜
- [`docs/decisions.md`](docs/decisions.md) — 구현하면서 플랜과 갈라진 지점
- [`docs/verification.md`](docs/verification.md) — 자동화하지 않는 수동 검증 항목
