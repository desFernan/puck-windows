# Puck for Windows — Phase 0+1 Implementation Plan (골격 + 펫이 걷는다)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 빈 저장소에서 시작해, Windows 화면 위를 걷고 떨어지고 던져지는 Puck 펫을 띄운다 — mac에서 만든 아바타 폴더를 그대로 읽으면서.

**Architecture:** `Puck.exe` 단일 WPF 프로세스. 순수 로직(아바타 파싱, 물리, 상태기계)은 Win32를 모르는 테스트 가능한 계층이고, OS와의 접촉은 전부 `Interop/` 뒤에 있다. 펫은 화면 전체를 덮는 오버레이가 아니라 **펫 크기의 레이어드 창 하나**가 `SetWindowPos`로 따라다니는 방식으로 그린다.

**Tech Stack:** C# / .NET 8 (`net8.0-windows`), WPF, xUnit, PowerShell 빌드 스크립트, GitHub Actions (`windows-latest`).

**Spec:** [`docs/porting-design.md`](../porting-design.md) — Phase 0과 Phase 1만 이 플랜의 범위다. Phase 2~6은 각자 별도 플랜을 받는다.

---

## Global Constraints

프로젝트 전역 규칙. 모든 태스크의 요구사항에 암묵적으로 포함된다.

- **런타임:** .NET 8 (LTS). TFM은 `net8.0-windows`. `<UseWPF>true</UseWPF>`, `<Nullable>enable</Nullable>`, `<LangVersion>12</LangVersion>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- **의존성 추가 금지 원칙.** Phase 0+1에서 허용된 NuGet 패키지는 xUnit 관련 셋(`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`)뿐이다. 그 외에는 BCL + Win32 P/Invoke만 쓴다.
- **좌표계:** 모든 펫 좌표는 **가상 화면 물리 픽셀**이다. 원점은 가상 화면 좌상단, Y는 아래로 증가. macOS 원본의 `GlobalScreenSpace`(AppKit 좌하단 원점 ↔ Quartz 좌상단 원점 변환)는 Windows에서 **불필요하다** — Win32가 이미 좌상단 원점 / Y-down이다. 뒤집기 코드를 옮기지 않는다.
- **DPI:** 프로세스는 Per-Monitor V2 DPI 인식(`app.manifest`). 창 배치는 WPF의 `Window.Left/Top`(DIP)이 아니라 `SetWindowPos`(물리 픽셀)로 한다. 창 내부 렌더링은 `WM_DPICHANGED`에 반응하는 `ScaleTransform`으로 DIP↔물리 픽셀을 맞춘다.
- **경로:** 사용자 데이터 루트는 `%LOCALAPPDATA%\Puck\`. 하위는 `Avatars\`, `Tank\`, `logs\`, `settings.json`, `.env`.
- **아바타 패키지 포맷은 정본이 puck-mac이다.** `schema_version: 1`, 클립 파일 스템 규칙, 필수 클립은 `idle` 하나, 권장 클립 10종. 포맷을 바꾸지 않는다. mac에서 만든 폴더가 그대로 로드돼야 한다.
- **UI 문자열은 한국어.** puck-mac `Puck/Localization`의 문자열을 그대로 가져온다. 하드코딩된 사용자 노출 문자열 금지 — 전부 `Strings` 클래스를 거친다.
- **테스트:** xUnit. `PuckTests/`가 `Puck/`의 폴더 구조를 그대로 미러한다. `scripts/test.ps1`은 무인 실행이고 실패 시 0이 아닌 코드로 종료한다.
- **커밋:** 태스크마다 최소 1회. 커밋 메시지는 `feat:` / `test:` / `chore:` 접두사.
- **참조 원본:** 값이나 동작이 애매하면 `desFernan/puck-mac`의 해당 파일이 정본이다. 각 태스크에 원본 경로를 적어 뒀다.

### 이 플랜에서 macOS 원본과 의도적으로 다르게 가는 것

| 항목 | puck-mac | puck-windows | 이유 |
|---|---|---|---|
| 오버레이 창 | 디스플레이마다 전체화면 투명 `NSWindow` (`ScreenManager`, `OverlayWindowController`) | **펫 크기 레이어드 창 1개**가 `SetWindowPos`로 따라다님 | WPF `AllowsTransparency`는 레이어드 창이라 전체화면 크기로 60fps를 돌리면 비싸다. 200×200이면 공짜. 모니터 간 이동은 창이 그냥 넘어가면 되고 `WM_DPICHANGED`가 배율을 알려준다. **업그레이드 경로:** 탱크/장난감/말풍선이 펫 밖 영역을 요구하면 그때 가상 화면 전체 크기 창으로 넓힌다 (Phase 2+). |
| 좌표 변환 | `GlobalScreenSpace` Y 뒤집기 | 없음 (항등) | Win32가 이미 좌상단 원점 |
| Accessibility 권한 | `AccessibilityPermission` + 온보딩 | 없음 | Windows엔 UIA 권한 프롬프트가 없음 |

---

## File Structure

```
pet-app/
  Puck.sln
  Directory.Build.props                  공통 TFM/Nullable/경고 설정
  Puck/
    Puck.csproj
    app.manifest                         PerMonitorV2 DPI 선언
    App.xaml / App.xaml.cs               앱 진입점, 부트스트랩
    Interop/
      Win32.cs                           P/Invoke 선언 한 곳
      WindowStyles.cs                    WS_EX_* 플래그 상수와 토글 헬퍼
      DpiWatcher.cs                      WM_DPICHANGED → 배율 변경 통지
    Diagnostics/
      PuckPaths.cs                       %LOCALAPPDATA%\Puck 하위 경로들
      AppLogger.cs                       레벨/카테고리 로깅 진입점
      JsonLinesFileAppender.cs           logs\*.jsonl 한 줄 = 한 이벤트
    Settings/
      SettingsStore.cs                   settings.json 로드/원자적 저장
    Localization/
      Strings.cs                          한국어 문자열 테이블
    ClientWindow/
      ClientPalette.cs                   색 토큰 light/dark
      ClientTheme.cs                     타이포/간격/모양 토큰
      Theme.xaml                          위 값들을 WPF 리소스로 노출
    Avatar/
      AvatarManifest.cs                  manifest.json 모델
      ClipReferenceConverter.cs          클립 값 JSON 컨버터
      AvatarLoader.cs                    파싱 + 검증 + 클립 폴백
      AvatarPackagePath.cs               패키지 밖 경로 거부
      AvatarCatalogue.cs                 Avatars\ 스캔
      SpriteAvatar.cs                    PNG 로딩 + 클립 전환 (IAvatarPlayable 구현)
      IAvatarPlayable.cs                 FSM이 보는 아바타 인터페이스
      OpaquePixelBounds.cs               알파 기준 실제 그림 경계
      AlphaHitMask.cs                    투명 픽셀 클릭 무시
    Movement/
      ScreenSpace.cs                     가상 화면 / 작업 영역 조회
      MovementSolver.cs                  순수 운동 계산
      ScreenBounds.cs                    화면 안 가두기 + 튕기기
      CharacterBody.cs                   위치/방향, 아바타로 전달
      CharacterController.cs             프레임 루프 + 상태 전이
      StateContext.cs                    한 프레임 동안 상태가 보는 것
      IStateHandler.cs                   enter/update/exit 계약
      StateKind.cs                        상태 식별자
      FrameClock.cs                       CompositionTarget.Rendering 래퍼
      WanderScheduler.cs                 idle에서 다음 행동까지의 시간
      CursorVelocityTracker.cs           던지기 속도 측정
      States/
        IdleState.cs  WalkState.cs  FallState.cs  LandState.cs
        ReactClickState.cs  ReactDragState.cs
    Overlay/
      PetOverlayWindow.xaml(.cs)         투명·topmost·클릭스루 창
      OverlayPositioner.cs               펫 좌표 → SetWindowPos
      SpriteView.cs                       스프라이트 그리기
      PetGestureRecognizer.cs            클릭/드래그/던지기 인식
    App/
      TrayIcon.cs                        트레이 아이콘 + 메뉴
      PetBootstrap.cs                    전부를 엮는 조립 지점
    Resources/
      Avatars/dummy/                     기본 동봉 아바타
  PuckTests/
    PuckTests.csproj
    Diagnostics/  Settings/  ClientWindow/  Avatar/  Movement/  Overlay/
  scripts/
    build.ps1  test.ps1  check-resources.ps1
.github/workflows/windows-tests.yml
```

---

# Phase 0 — 골격

> 스펙의 Phase 0은 `Interop/`와 `Localization/`도 여기 둔다. 이 플랜은
> 둘을 각각 Task 15(오버레이 창)와 Task 18(트레이 메뉴)로 미룬다 —
> 쓰는 곳 없이 먼저 만들면 무엇이 필요한지 모르는 채로 모양을 정하게
> 되고, 그건 되돌리는 비용이 만드는 비용보다 크다.

## Task 1: 솔루션 골격과 테스트 하네스

**Files:**
- Create: `pet-app/Puck.sln`
- Create: `pet-app/Directory.Build.props`
- Create: `pet-app/Puck/Puck.csproj`
- Create: `pet-app/Puck/app.manifest`
- Create: `pet-app/Puck/App.xaml`, `pet-app/Puck/App.xaml.cs`
- Create: `pet-app/PuckTests/PuckTests.csproj`
- Create: `pet-app/PuckTests/SmokeTests.cs`
- Create: `pet-app/scripts/build.ps1`, `pet-app/scripts/test.ps1`
- Create: `.github/workflows/windows-tests.yml`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: 없음 (첫 태스크)
- Produces: `dotnet test`가 도는 솔루션. 이후 모든 태스크가 `pet-app/PuckTests/<Area>/<Name>Tests.cs`에 테스트를 추가한다.

- [ ] **Step 1: `.gitignore` 작성**

```gitignore
bin/
obj/
*.user
.vs/
publish/
```

- [ ] **Step 2: `Directory.Build.props` 작성**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>12</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: `Puck/Puck.csproj` 작성**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>Puck</AssemblyName>
    <RootNamespace>Puck</RootNamespace>
  </PropertyGroup>
</Project>
```

`UseWindowsForms`는 트레이 아이콘(`NotifyIcon`)과 `Screen.AllScreens` 때문에 켠다 — 이 둘 때문에 별도 패키지를 받지 않아도 된다.

- [ ] **Step 4: `Puck/app.manifest` 작성**

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

- [ ] **Step 5: `Puck/App.xaml` + `App.xaml.cs` 작성**

`App.xaml`:
```xml
<Application x:Class="Puck.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown" />
```

`App.xaml.cs`:
```csharp
using System.Windows;

namespace Puck;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Task 18(PetBootstrap)에서 채운다.
    }
}
```

`ShutdownMode="OnExplicitShutdown"`이 중요하다 — Puck은 창이 하나도 없어도(펫만 떠 있어도, 혹은 전부 숨겨도) 살아 있어야 하는 트레이 앱이다. 기본값인 `OnLastWindowClose`면 창을 닫는 순간 프로세스가 죽는다.

- [ ] **Step 6: `PuckTests/PuckTests.csproj` 작성**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Puck\Puck.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 7: 실패하는 스모크 테스트 작성**

`PuckTests/SmokeTests.cs`:
```csharp
namespace PuckTests;

public class SmokeTests
{
    [Fact]
    public void TestHarnessRuns()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

- [ ] **Step 8: 솔루션 생성 후 테스트 실행**

```powershell
cd pet-app
dotnet new sln -n Puck
dotnet sln add Puck\Puck.csproj PuckTests\PuckTests.csproj
dotnet test
```
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 9: `scripts/build.ps1` 작성**

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet build "$root\Puck.sln" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

- [ ] **Step 10: `scripts/test.ps1` 작성**

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet test "$root\Puck.sln" -c Release --nologo
exit $LASTEXITCODE
```

- [ ] **Step 11: CI 워크플로 작성**

`.github/workflows/windows-tests.yml`:
```yaml
name: windows-tests
on:
  push:
    branches: [main]
  pull_request:
jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: pwsh pet-app/scripts/test.ps1
```

- [ ] **Step 12: 커밋**

```bash
git add -A
git commit -m "chore: solution scaffold, xUnit harness, build/test scripts, CI"
```

---

## Task 2: 경로와 로거

**Files:**
- Create: `pet-app/Puck/Diagnostics/PuckPaths.cs`
- Create: `pet-app/Puck/Diagnostics/AppLogger.cs`
- Create: `pet-app/Puck/Diagnostics/JsonLinesFileAppender.cs`
- Test: `pet-app/PuckTests/Diagnostics/PuckPathsTests.cs`, `pet-app/PuckTests/Diagnostics/JsonLinesFileAppenderTests.cs`

**원본:** `puck-mac/pet-app/Puck/Diagnostics/AppLogger.swift`, `JSONLinesFileAppender.swift`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `PuckPaths.Root` / `.Avatars` / `.Tank` / `.Logs` / `.SettingsFile` / `.EnvFile` — 모두 `string` 절대 경로
  - `PuckPaths.EnsureCreated()` — 루트/Avatars/Tank/logs를 만든다
  - `AppLogger.Log(LogLevel level, string category, string message, IReadOnlyDictionary<string, object?>? fields = null)`
  - `AppLogger.Configure(IJsonLinesSink sink)` — 테스트에서 파일 대신 메모리 싱크를 꽂는 지점

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Diagnostics/PuckPathsTests.cs`:
```csharp
using Puck.Diagnostics;

namespace PuckTests.Diagnostics;

public class PuckPathsTests
{
    [Fact]
    public void RootLivesUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(localAppData, "Puck"), PuckPaths.Root);
    }

    [Fact]
    public void KnownSubpathsHangOffRoot()
    {
        Assert.Equal(Path.Combine(PuckPaths.Root, "Avatars"), PuckPaths.Avatars);
        Assert.Equal(Path.Combine(PuckPaths.Root, "Tank"), PuckPaths.Tank);
        Assert.Equal(Path.Combine(PuckPaths.Root, "logs"), PuckPaths.Logs);
        Assert.Equal(Path.Combine(PuckPaths.Root, "settings.json"), PuckPaths.SettingsFile);
        Assert.Equal(Path.Combine(PuckPaths.Root, ".env"), PuckPaths.EnvFile);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter PuckPathsTests`
Expected: FAIL — `Puck.Diagnostics` 네임스페이스가 없다는 컴파일 에러

- [ ] **Step 3: `PuckPaths` 구현**

```csharp
namespace Puck.Diagnostics;

/// mac의 ~/Library/Application Support/Puck/ 에 해당하는 한 곳.
public static class PuckPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Puck");

    public static string Avatars => Path.Combine(Root, "Avatars");
    public static string Tank => Path.Combine(Root, "Tank");
    public static string Logs => Path.Combine(Root, "logs");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string EnvFile => Path.Combine(Root, ".env");

    /// 설정의 "커스터마이징 폴더 열기"가 폴더를 만들어 주는 것과 같은 동작.
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Avatars);
        Directory.CreateDirectory(Tank);
        Directory.CreateDirectory(Logs);
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter PuckPathsTests`
Expected: PASS (2 tests)

- [ ] **Step 5: 로거의 실패하는 테스트 작성**

`PuckTests/Diagnostics/JsonLinesFileAppenderTests.cs`:
```csharp
using System.Text.Json;
using Puck.Diagnostics;

namespace PuckTests.Diagnostics;

public class JsonLinesFileAppenderTests
{
    [Fact]
    public void WritesOneJsonObjectPerLine()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var appender = new JsonLinesFileAppender(dir);
            appender.Append(LogLevel.Warning, "avatar", "missing clip", new Dictionary<string, object?> { ["clip"] = "walk" });
            appender.Append(LogLevel.Error, "overlay", "no display", null);
            appender.Flush();

            var file = Directory.GetFiles(dir, "*.jsonl").Single();
            var lines = File.ReadAllLines(file);
            Assert.Equal(2, lines.Length);

            var first = JsonDocument.Parse(lines[0]).RootElement;
            Assert.Equal("warning", first.GetProperty("level").GetString());
            Assert.Equal("avatar", first.GetProperty("category").GetString());
            Assert.Equal("missing clip", first.GetProperty("message").GetString());
            Assert.Equal("walk", first.GetProperty("clip").GetString());
            Assert.True(first.TryGetProperty("ts", out _));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void FieldNameCollidingWithReservedKeyIsPrefixed()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var appender = new JsonLinesFileAppender(dir);
            appender.Append(LogLevel.Info, "x", "y", new Dictionary<string, object?> { ["message"] = "shadow" });
            appender.Flush();

            var line = File.ReadAllLines(Directory.GetFiles(dir, "*.jsonl").Single()).Single();
            var obj = JsonDocument.Parse(line).RootElement;
            Assert.Equal("y", obj.GetProperty("message").GetString());
            Assert.Equal("shadow", obj.GetProperty("field.message").GetString());
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
```

두 번째 테스트가 있는 이유: 필드 이름이 `level`/`ts`/`message`/`category`와 겹치면 로그 줄이 조용히 덮어씌워진다. 겹칠 때 무엇이 일어나는지를 코드가 아니라 테스트가 정해 둔다.

- [ ] **Step 6: 실패 확인**

Run: `dotnet test --filter JsonLinesFileAppenderTests`
Expected: FAIL — `JsonLinesFileAppender` 없음

- [ ] **Step 7: 로거 구현**

`Puck/Diagnostics/AppLogger.cs`:
```csharp
namespace Puck.Diagnostics;

public enum LogLevel { Debug, Info, Warning, Error }

public interface IJsonLinesSink
{
    void Append(LogLevel level, string category, string message, IReadOnlyDictionary<string, object?>? fields);
    void Flush();
}

/// 앱 전체의 로깅 진입점. 싱크를 갈아끼울 수 있게 해 둔 이유는 테스트가
/// 파일을 건드리지 않게 하기 위해서다.
public static class AppLogger
{
    private static IJsonLinesSink _sink = new NullSink();

    public static void Configure(IJsonLinesSink sink) => _sink = sink;

    public static void Log(LogLevel level, string category, string message,
                           IReadOnlyDictionary<string, object?>? fields = null)
        => _sink.Append(level, category, message, fields);

    public static void Warning(string category, string message,
                               IReadOnlyDictionary<string, object?>? fields = null)
        => Log(LogLevel.Warning, category, message, fields);

    public static void Error(string category, string message,
                             IReadOnlyDictionary<string, object?>? fields = null)
        => Log(LogLevel.Error, category, message, fields);

    private sealed class NullSink : IJsonLinesSink
    {
        public void Append(LogLevel level, string category, string message, IReadOnlyDictionary<string, object?>? fields) { }
        public void Flush() { }
    }
}
```

`Puck/Diagnostics/JsonLinesFileAppender.cs`:
```csharp
using System.Text;
using System.Text.Json;

namespace Puck.Diagnostics;

/// logs\puck-YYYY-MM-DD.jsonl — 한 줄이 한 이벤트.
public sealed class JsonLinesFileAppender : IJsonLinesSink
{
    private static readonly HashSet<string> Reserved = ["ts", "level", "category", "message"];

    private readonly string _directory;
    private readonly object _gate = new();

    public JsonLinesFileAppender(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Append(LogLevel level, string category, string message,
                       IReadOnlyDictionary<string, object?>? fields)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("ts", DateTimeOffset.UtcNow.ToString("O"));
            writer.WriteString("level", level.ToString().ToLowerInvariant());
            writer.WriteString("category", category);
            writer.WriteString("message", message);
            if (fields is not null)
            {
                foreach (var (key, value) in fields)
                {
                    var name = Reserved.Contains(key) ? $"field.{key}" : key;
                    writer.WritePropertyName(name);
                    JsonSerializer.Serialize(writer, value);
                }
            }
            writer.WriteEndObject();
        }

        var line = Encoding.UTF8.GetString(buffer.ToArray()) + Environment.NewLine;
        lock (_gate)
        {
            File.AppendAllText(CurrentFile(), line, Encoding.UTF8);
        }
    }

    public void Flush() { /* AppendAllText은 매 호출마다 닫는다 */ }

    private string CurrentFile()
        => Path.Combine(_directory, $"puck-{DateTime.Now:yyyy-MM-dd}.jsonl");
}
```

- [ ] **Step 8: 통과 확인**

Run: `dotnet test --filter Diagnostics`
Expected: PASS (4 tests)

- [ ] **Step 9: 커밋**

```bash
git add pet-app/Puck/Diagnostics pet-app/PuckTests/Diagnostics
git commit -m "feat: PuckPaths + JSONL logger"
```

---

## Task 3: 설정 저장소

**Files:**
- Create: `pet-app/Puck/Settings/SettingsStore.cs`
- Test: `pet-app/PuckTests/Settings/SettingsStoreTests.cs`

**원본:** `puck-mac/pet-app/Puck/Settings/SettingsStore.swift` (mac은 `UserDefaults`)

**Interfaces:**
- Consumes: `PuckPaths.SettingsFile` (Task 2)
- Produces:
  - `sealed class SettingsStore` — `SettingsStore.Load(string path)` / `store.Save()`
  - 속성: `string? AvatarName`, `double MovementSpeedMultiplier` (기본 1.0), `bool LaunchAtLogin` (기본 false), `string ThemeStyle` (기본 `"dark"`), `bool AvoidFocusedWindow` (기본 false)
  - `event EventHandler? Changed`

Phase 1이 실제로 읽는 건 `AvatarName`과 `MovementSpeedMultiplier` 둘뿐이다. 나머지는 이후 Phase가 쓸 자리이고, 지금 넣는 이유는 파일 포맷이 처음부터 확장 가능해야 하기 때문이다 — 모르는 키를 만나면 버리지 않고 보존한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Settings/SettingsStoreTests.cs`:
```csharp
using Puck.Settings;

namespace PuckTests.Settings;

public class SettingsStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

    [Fact]
    public void MissingFileYieldsDefaults()
    {
        var store = SettingsStore.Load(TempFile());
        Assert.Null(store.AvatarName);
        Assert.Equal(1.0, store.MovementSpeedMultiplier);
        Assert.Equal("dark", store.ThemeStyle);
        Assert.False(store.LaunchAtLogin);
    }

    [Fact]
    public void CorruptFileYieldsDefaultsInsteadOfThrowing()
    {
        var path = TempFile();
        File.WriteAllText(path, "{ this is not json");
        var store = SettingsStore.Load(path);
        Assert.Equal(1.0, store.MovementSpeedMultiplier);
    }

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        var path = TempFile();
        var store = SettingsStore.Load(path);
        store.AvatarName = "my-pet";
        store.MovementSpeedMultiplier = 1.5;
        store.Save();

        var reloaded = SettingsStore.Load(path);
        Assert.Equal("my-pet", reloaded.AvatarName);
        Assert.Equal(1.5, reloaded.MovementSpeedMultiplier);
    }

    [Fact]
    public void UnknownKeysSurviveARoundTrip()
    {
        var path = TempFile();
        File.WriteAllText(path, """{"avatar_name":"a","future_key":42}""");
        var store = SettingsStore.Load(path);
        store.AvatarName = "b";
        store.Save();

        Assert.Contains("future_key", File.ReadAllText(path));
    }

    [Fact]
    public void ChangedFiresOnPropertySet()
    {
        var store = SettingsStore.Load(TempFile());
        var fired = 0;
        store.Changed += (_, _) => fired++;
        store.MovementSpeedMultiplier = 2.0;
        Assert.Equal(1, fired);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter SettingsStoreTests`
Expected: FAIL — `SettingsStore` 없음

- [ ] **Step 3: 구현**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Puck.Diagnostics;

namespace Puck.Settings;

/// settings.json. 모르는 키는 보존한다 — 구버전이 신버전 설정을 날리지
/// 않게 하는 유일한 방법이고, mac의 UserDefaults가 공짜로 주던 성질이다.
public sealed class SettingsStore
{
    private readonly string _path;
    private JsonObject _raw;

    private SettingsStore(string path, JsonObject raw)
    {
        _path = path;
        _raw = raw;
    }

    public event EventHandler? Changed;

    public static SettingsStore Load(string path)
    {
        JsonObject raw;
        try
        {
            raw = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch (Exception ex)
        {
            AppLogger.Warning("settings", "settings.json을 읽지 못해 기본값으로 시작합니다",
                new Dictionary<string, object?> { ["error"] = ex.Message });
            raw = new JsonObject();
        }
        return new SettingsStore(path, raw);
    }

    public string? AvatarName
    {
        get => GetString("avatar_name", null);
        set => Set("avatar_name", value);
    }

    public double MovementSpeedMultiplier
    {
        get => GetDouble("movement_speed_multiplier", 1.0);
        set => Set("movement_speed_multiplier", value);
    }

    public bool LaunchAtLogin
    {
        get => GetBool("launch_at_login", false);
        set => Set("launch_at_login", value);
    }

    public string ThemeStyle
    {
        get => GetString("theme_style", "dark")!;
        set => Set("theme_style", value);
    }

    public bool AvoidFocusedWindow
    {
        get => GetBool("avoid_focused_window", false);
        set => Set("avoid_focused_window", value);
    }

    /// 임시 파일에 쓰고 갈아끼운다 — 저장 중에 죽어도 반쯤 쓰인
    /// settings.json이 남지 않는다.
    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = _path + ".tmp";
        File.WriteAllText(temp, _raw.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, overwrite: true);
    }

    private string? GetString(string key, string? fallback)
        => _raw[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;

    private double GetDouble(string key, double fallback)
        => _raw[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : fallback;

    private bool GetBool(string key, bool fallback)
        => _raw[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    private void Set<T>(string key, T value)
    {
        _raw[key] = JsonValue.Create(value);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter SettingsStoreTests`
Expected: PASS (5 tests)

- [ ] **Step 5: 커밋**

```bash
git add pet-app/Puck/Settings pet-app/PuckTests/Settings
git commit -m "feat: settings.json store preserving unknown keys"
```

---

## Task 4: 디자인 토큰

**Files:**
- Create: `pet-app/Puck/ClientWindow/ClientPalette.cs`
- Create: `pet-app/Puck/ClientWindow/ClientTheme.cs`
- Create: `pet-app/Puck/ClientWindow/Theme.xaml`
- Modify: `pet-app/Puck/App.xaml` (리소스 사전 병합)
- Test: `pet-app/PuckTests/ClientWindow/ClientPaletteTests.cs`

**원본:** `puck-mac/pet-app/Puck/ClientWindow/ClientPalette.swift`, `ClientTheme.swift`, `pet-app/design.md` §2–§3

Phase 1은 이 값을 쓰지 않는다 — 쓰는 건 Phase 4(클라이언트 창)다. 그럼에도 지금 넣는 이유는 이 포팅의 핵심 주장("디자인 시스템이 수치 그대로 이식된다")을 가장 싸게 검증하는 지점이기 때문이다. 순수 데이터 + 값 단언 테스트 하나로 끝난다.

**Interfaces:**
- Consumes: 없음
- Produces:
  - `ClientPalette.Light` / `.Dark` — `Background`, `Surface`, `SurfaceBorder`, `TextPrimary`, `TextSecondary`, `Accent`, `OnAccent`, `StatusSuccess`, `StatusError`, `StatusWarning` (`System.Windows.Media.Color`), 계산 속성 `StatusIdle` (= `TextSecondary`), `StatusActive` (= `Accent`)
  - `ClientTheme.Metrics.*`, `ClientTheme.Typography.*` — `double` 상수

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/ClientWindow/ClientPaletteTests.cs`:
```csharp
using System.Windows.Media;
using Puck.ClientWindow;

namespace PuckTests.ClientWindow;

public class ClientPaletteTests
{
    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    [Fact]
    public void DarkMatchesTheMacPalette()
    {
        var p = ClientPalette.Dark;
        Assert.Equal("#0a0a0a", Hex(p.Background));
        Assert.Equal("#131313", Hex(p.Surface));
        Assert.Equal("#242424", Hex(p.SurfaceBorder));
        Assert.Equal("#ededed", Hex(p.TextPrimary));
        Assert.Equal("#7a7a7a", Hex(p.TextSecondary));
        Assert.Equal("#ed8c33", Hex(p.Accent));
        Assert.Equal("#161616", Hex(p.OnAccent));
    }

    [Fact]
    public void LightMatchesTheMacPalette()
    {
        var p = ClientPalette.Light;
        Assert.Equal("#fafafa", Hex(p.Background));
        Assert.Equal("#ffffff", Hex(p.Surface));
        Assert.Equal("#e5e5e5", Hex(p.SurfaceBorder));
        Assert.Equal("#1a1a1a", Hex(p.TextPrimary));
        Assert.Equal("#6b6b6b", Hex(p.TextSecondary));
        Assert.Equal("#ed8c33", Hex(p.Accent));
        Assert.Equal("#ffffff", Hex(p.OnAccent));
    }

    [Fact]
    public void StatusColoursAreThemeIndependent()
    {
        foreach (var p in new[] { ClientPalette.Light, ClientPalette.Dark })
        {
            Assert.Equal("#3fb950", Hex(p.StatusSuccess));
            Assert.Equal("#f85149", Hex(p.StatusError));
            Assert.Equal("#e3b341", Hex(p.StatusWarning));
        }
    }

    [Fact]
    public void DerivedStatusColoursReuseTheirSource()
    {
        var p = ClientPalette.Dark;
        Assert.Equal(p.TextSecondary, p.StatusIdle);
        Assert.Equal(p.Accent, p.StatusActive);
    }
}
```

마지막 테스트가 design.md의 규칙("계산 프로퍼티는 다른 필드를 재사용해서 절대 따로 어긋나지 않는다")을 코드에 고정한다.

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter ClientPaletteTests`
Expected: FAIL — `ClientPalette` 없음

- [ ] **Step 3: `ClientPalette` 구현**

```csharp
using System.Windows.Media;

namespace Puck.ClientWindow;

/// 디자인 시스템 v2 (2026-08-14). 값의 정본은 puck-mac의
/// ClientPalette.swift이고 이 파일은 그 값을 그대로 옮긴 것이다.
public sealed record ClientPalette
{
    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceBorder { get; init; }
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color Accent { get; init; }
    public required Color OnAccent { get; init; }
    public required Color StatusSuccess { get; init; }
    public required Color StatusError { get; init; }
    public required Color StatusWarning { get; init; }

    public Color StatusIdle => TextSecondary;
    public Color StatusActive => Accent;

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;

    // 테마와 무관하게 고정.
    private const string Success = "#3fb950";
    private const string Error = "#f85149";
    private const string WarningColour = "#e3b341";
    private const string AccentColour = "#ed8c33";

    public static ClientPalette Light { get; } = new()
    {
        Background = Hex("#fafafa"),
        Surface = Hex("#ffffff"),
        SurfaceBorder = Hex("#e5e5e5"),
        TextPrimary = Hex("#1a1a1a"),
        TextSecondary = Hex("#6b6b6b"),
        Accent = Hex(AccentColour),
        OnAccent = Hex("#ffffff"),
        StatusSuccess = Hex(Success),
        StatusError = Hex(Error),
        StatusWarning = Hex(WarningColour),
    };

    public static ClientPalette Dark { get; } = new()
    {
        Background = Hex("#0a0a0a"),
        Surface = Hex("#131313"),
        SurfaceBorder = Hex("#242424"),
        TextPrimary = Hex("#ededed"),
        TextSecondary = Hex("#7a7a7a"),
        Accent = Hex(AccentColour),
        // 이 팔레트에서는 흰색보다 accent 위 대비/무드가 낫다.
        OnAccent = Hex("#161616"),
        StatusSuccess = Hex(Success),
        StatusError = Hex(Error),
        StatusWarning = Hex(WarningColour),
    };
}
```

- [ ] **Step 4: `ClientTheme` 구현**

```csharp
namespace Puck.ClientWindow;

/// 색은 ClientPalette, 여기는 타입/간격/모양. puck-mac ClientTheme.swift에서 옮김.
public static class ClientTheme
{
    public static class Typography
    {
        public const double SectionHeader = 13;      // semibold
        public const double WorkspaceName = 14;      // medium
        public const double SessionTitle = 13;
        public const double ToolLabel = 13;          // medium
        public const double Mono = 12.5;             // monospaced
        public const double Caption = 12;
        public const double TranscriptBody = 16;
        public const double TranscriptCode = 13.5;   // monospaced

        /// 본문(16)보다 반드시 커야 한다 — 그게 고정 크기를 쓰는 이유다.
        public static double TranscriptHeading(int level) => level switch
        {
            1 => 23,
            2 => 20,
            _ => 17,
        };
    }

    public static class Metrics
    {
        public const double SpacingSmall = 4;
        public const double SpacingMedium = 8;
        public const double SpacingLarge = 12;
        public const double SectionSpacing = 20;
        public const double WindowEdgePadding = 20;
        public const double TranscriptColumnWidth = 760;
        public const double TranscriptHorizontalPadding = 12;
        public const double PanelCornerRadius = 14;
        public const double PanelInset = 8;
        public const double CardCornerRadius = 6;
        public const double RowCornerRadius = 4;
        public const double WindowMinWidth = 560;
        public const double WindowMinWidthWithCode = 1040;
        public const double EditorWindowMinWidth = 540;
        public const double WindowTint = 0.78;
        public const double WindowMinHeight = 640;
    }
}
```

- [ ] **Step 5: `Theme.xaml` 작성 (다크 기준)**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">
  <SolidColorBrush x:Key="Background" Color="#0a0a0a" />
  <SolidColorBrush x:Key="Surface" Color="#131313" />
  <SolidColorBrush x:Key="SurfaceBorder" Color="#242424" />
  <SolidColorBrush x:Key="TextPrimary" Color="#ededed" />
  <SolidColorBrush x:Key="TextSecondary" Color="#7a7a7a" />
  <SolidColorBrush x:Key="Accent" Color="#ed8c33" />
  <SolidColorBrush x:Key="OnAccent" Color="#161616" />
  <SolidColorBrush x:Key="StatusSuccess" Color="#3fb950" />
  <SolidColorBrush x:Key="StatusError" Color="#f85149" />
  <SolidColorBrush x:Key="StatusWarning" Color="#e3b341" />

  <sys:Double x:Key="SpacingSmall">4</sys:Double>
  <sys:Double x:Key="SpacingMedium">8</sys:Double>
  <sys:Double x:Key="SpacingLarge">12</sys:Double>
  <sys:Double x:Key="CardCornerRadius">6</sys:Double>
  <sys:Double x:Key="RowCornerRadius">4</sys:Double>
  <sys:Double x:Key="PanelCornerRadius">14</sys:Double>
</ResourceDictionary>
```

라이트 팔레트로 통째로 갈아끼우는 스위치는 Phase 4에서 붙인다. 지금 `Theme.xaml`은 다크 한 벌이다.

- [ ] **Step 6: `App.xaml`에 병합**

```xml
<Application x:Class="Puck.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="ClientWindow/Theme.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 7: 통과 확인**

Run: `dotnet test --filter ClientPaletteTests`
Expected: PASS (4 tests)

- [ ] **Step 8: 커밋**

```bash
git add pet-app/Puck/ClientWindow pet-app/PuckTests/ClientWindow pet-app/Puck/App.xaml
git commit -m "feat: port design tokens (palette + theme) from puck-mac"
```

---

# Phase 1 — 펫이 산다

## Task 5: 아바타 매니페스트 모델과 파싱

**Files:**
- Create: `pet-app/Puck/Avatar/AvatarManifest.cs`
- Create: `pet-app/Puck/Avatar/ClipReferenceConverter.cs`
- Test: `pet-app/PuckTests/Avatar/AvatarManifestTests.cs`

**원본:** `puck-mac/pet-app/Puck/Avatar/AvatarManifest.swift`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `enum AvatarType { Usdz, Video, Sprites }`
  - `sealed record Hitbox(double Width, double Height)`
  - `abstract record ClipReference` — 하위 `ClipReference.Stem(string Value)`, `ClipReference.TimeRange(double In, double Out)`
  - `sealed record AvatarManifest` — `int SchemaVersion`, `string Name`, `AvatarType Type`, `double Scale`(기본 1), `double? BounceIntensity`, `Hitbox Hitbox`, `IReadOnlyDictionary<string, ClipReference> Clips`, `IReadOnlyDictionary<string, ClipReference>? Emotions`, `IReadOnlyDictionary<string, string> Sounds`(기본 빈 사전)
  - `AvatarManifest.JsonOptions` — snake_case + `ClipReferenceConverter`가 꽂힌 `JsonSerializerOptions`

`scale`과 `sounds`가 없어도 되는 이유는 원본 주석에 그대로 있다 — 폴더에 그림 하나 넣은 사람이 `"scale": 1.0`과 `"sounds": {}`를 써야 유효하다는 말을 들을 이유가 없다.

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Avatar/AvatarManifestTests.cs`:
```csharp
using System.Text.Json;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarManifestTests
{
    private static AvatarManifest Parse(string json) =>
        JsonSerializer.Deserialize<AvatarManifest>(json, AvatarManifest.JsonOptions)!;

    [Fact]
    public void SmallestWorkingManifestParses()
    {
        var m = Parse("""
        {
          "schema_version": 1,
          "name": "my-pet",
          "type": "sprites",
          "hitbox": { "width": 130, "height": 133 },
          "clips": { "idle": "idle" }
        }
        """);

        Assert.Equal(1, m.SchemaVersion);
        Assert.Equal("my-pet", m.Name);
        Assert.Equal(AvatarType.Sprites, m.Type);
        Assert.Equal(130, m.Hitbox.Width);
        Assert.Equal(133, m.Hitbox.Height);
        Assert.Equal(new ClipReference.Stem("idle"), m.Clips["idle"]);
    }

    [Fact]
    public void AbsentScaleSoundsAndEmotionsGetTheirDefaults()
    {
        var m = Parse("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"idle"}}
        """);

        Assert.Equal(1.0, m.Scale);
        Assert.Empty(m.Sounds);
        Assert.Null(m.Emotions);
        Assert.Null(m.BounceIntensity);
    }

    [Fact]
    public void FullManifestParses()
    {
        var m = Parse("""
        {
          "schema_version": 1, "name": "my-pet", "type": "sprites",
          "scale": 1.5, "bounce_intensity": 0.6,
          "hitbox": { "width": 130, "height": 133 },
          "clips": { "idle": "starry-eyed", "walk": "walk" },
          "emotions": { "happy": "beaming" },
          "sounds": { "land": "sounds/waah.wav" }
        }
        """);

        Assert.Equal(1.5, m.Scale);
        Assert.Equal(0.6, m.BounceIntensity);
        Assert.Equal(new ClipReference.Stem("starry-eyed"), m.Clips["idle"]);
        Assert.Equal(new ClipReference.Stem("beaming"), m.Emotions!["happy"]);
        Assert.Equal("sounds/waah.wav", m.Sounds["land"]);
    }

    [Fact]
    public void ClipMayBeATimeRangeForVideoAvatars()
    {
        var m = Parse("""
        {"schema_version":1,"name":"v","type":"video",
         "hitbox":{"width":1,"height":1},
         "clips":{"idle":{"in":0.5,"out":2.25}}}
        """);

        Assert.Equal(new ClipReference.TimeRange(0.5, 2.25), m.Clips["idle"]);
    }

    [Fact]
    public void RoundTripsThroughSerialisation()
    {
        var json = """
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":10,"height":20},
         "clips":{"idle":"idle","walk":{"in":0.0,"out":1.0}}}
        """;
        var once = Parse(json);
        var twice = Parse(JsonSerializer.Serialize(once, AvatarManifest.JsonOptions));
        Assert.Equal(once, twice);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter AvatarManifestTests`
Expected: FAIL — `Puck.Avatar` 없음

- [ ] **Step 3: `ClipReference`와 컨버터 구현**

`Puck/Avatar/ClipReferenceConverter.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Puck.Avatar;

/// 클립 테이블의 값. 문자열이면 매니페스트 옆 파일의 **스템**이다
/// ("idle" -> idle.png). video 아바타만 {"in":초,"out":초} 시간 구간을 쓴다.
public abstract record ClipReference
{
    public sealed record Stem(string Value) : ClipReference;
    public sealed record TimeRange(double In, double Out) : ClipReference;
}

public sealed class ClipReferenceConverter : JsonConverter<ClipReference>
{
    public override ClipReference Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new ClipReference.Stem(reader.GetString()!);

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("클립 값은 문자열이거나 {in,out} 객체여야 합니다");

        double? start = null, end = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            var name = reader.GetString();
            reader.Read();
            if (name == "in") start = reader.GetDouble();
            else if (name == "out") end = reader.GetDouble();
        }

        if (start is null || end is null)
            throw new JsonException("시간 구간 클립에는 in과 out이 모두 있어야 합니다");
        return new ClipReference.TimeRange(start.Value, end.Value);
    }

    public override void Write(Utf8JsonWriter writer, ClipReference value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ClipReference.Stem s:
                writer.WriteStringValue(s.Value);
                break;
            case ClipReference.TimeRange t:
                writer.WriteStartObject();
                writer.WriteNumber("in", t.In);
                writer.WriteNumber("out", t.Out);
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException($"알 수 없는 클립 값 종류: {value.GetType().Name}");
        }
    }
}
```

- [ ] **Step 4: `AvatarManifest` 구현**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Puck.Avatar;

public enum AvatarType { Usdz, Video, Sprites }

public sealed record Hitbox(double Width, double Height);

public sealed record AvatarManifest
{
    public required int SchemaVersion { get; init; }
    public required string Name { get; init; }
    public required AvatarType Type { get; init; }
    public double Scale { get; init; } = 1.0;
    public double? BounceIntensity { get; init; }
    public required Hitbox Hitbox { get; init; }
    public required IReadOnlyDictionary<string, ClipReference> Clips { get; init; }
    public IReadOnlyDictionary<string, ClipReference>? Emotions { get; init; }
    public IReadOnlyDictionary<string, string> Sounds { get; init; } =
        new Dictionary<string, string>();

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        Converters =
        {
            new ClipReferenceConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        },
    };
}
```

`record`의 값 동등성 때문에 `RoundTripsThroughSerialisation`이 성립한다. 단 `IReadOnlyDictionary`는 참조 동등이므로, 이 테스트가 실패하면 사전 비교를 `Assert.Equal(once.Clips, twice.Clips)`로 풀어서 쓴다.

- [ ] **Step 5: 통과 확인**

Run: `dotnet test --filter AvatarManifestTests`
Expected: PASS (5 tests)

- [ ] **Step 6: 커밋**

```bash
git add pet-app/Puck/Avatar pet-app/PuckTests/Avatar
git commit -m "feat: avatar manifest model and JSON parsing"
```

---

## Task 6: 매니페스트 검증과 클립 폴백 (AvatarLoader)

**Files:**
- Create: `pet-app/Puck/Avatar/AvatarLoader.cs`
- Test: `pet-app/PuckTests/Avatar/AvatarLoaderTests.cs`

**원본:** `puck-mac/pet-app/Puck/Avatar/AvatarLoader.swift`

**Interfaces:**
- Consumes: `AvatarManifest`, `ClipReference` (Task 5)
- Produces:
  - `const int AvatarLoader.SupportedSchemaVersion = 1`
  - `static IReadOnlyList<string> AvatarLoader.RequiredClips` = `["idle"]`
  - `static IReadOnlyList<string> AvatarLoader.RecommendedClips` = `["walk","climb","fall","land","point","type","listen","react_click","react_drag","kick"]`
  - `sealed record AvatarLoadResult(AvatarManifest Manifest, IReadOnlyList<string> MissingRecommendedClips)`
  - `AvatarLoader.Load(string avatarDirectory)` / `AvatarLoader.Load(ReadOnlySpan<byte> manifestData)` → `AvatarLoadResult`, 실패 시 `AvatarLoaderException` 던짐
  - `sealed class AvatarLoaderException : Exception` — `AvatarLoaderError Error` 속성 (`enum AvatarLoaderError { AvatarNotFound, ManifestNotDecodable, MissingRequiredClips, UnsupportedSchemaVersion }`)
  - `static string? AvatarLoader.ResolveClipStem(string clip, AvatarLoadResult result)` — 없으면 idle로 폴백, idle도 스템이 아니면 null

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Avatar/AvatarLoaderTests.cs`:
```csharp
using System.Text;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarLoaderTests
{
    private static AvatarLoadResult Load(string json) =>
        AvatarLoader.Load(Encoding.UTF8.GetBytes(json));

    private static AvatarLoaderException LoadFailure(string json) =>
        Assert.Throws<AvatarLoaderException>(() => AvatarLoader.Load(Encoding.UTF8.GetBytes(json)));

    private const string Minimal = """
    {"schema_version":1,"name":"a","type":"sprites",
     "hitbox":{"width":1,"height":1},"clips":{"idle":"idle"}}
    """;

    [Fact]
    public void IdleAloneIsAValidAvatar()
    {
        var result = Load(Minimal);
        Assert.Equal("a", result.Manifest.Name);
    }

    [Fact]
    public void MissingIdleIsRejected()
    {
        var e = LoadFailure("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"walk":"walk"}}
        """);
        Assert.Equal(AvatarLoaderError.MissingRequiredClips, e.Error);
    }

    [Fact]
    public void FutureSchemaVersionIsRejectedRatherThanTrusted()
    {
        var e = LoadFailure(Minimal.Replace("\"schema_version\":1", "\"schema_version\":2"));
        Assert.Equal(AvatarLoaderError.UnsupportedSchemaVersion, e.Error);
    }

    [Fact]
    public void UnparseableManifestIsRejected()
    {
        var e = LoadFailure("{ not json");
        Assert.Equal(AvatarLoaderError.ManifestNotDecodable, e.Error);
    }

    [Fact]
    public void MissingRecommendedClipsAreReportedNotFatal()
    {
        var result = Load(Minimal);
        Assert.Equal(AvatarLoader.RecommendedClips, result.MissingRecommendedClips);
    }

    [Fact]
    public void PresentRecommendedClipIsNotReportedMissing()
    {
        var result = Load("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"idle","walk":"w"}}
        """);
        Assert.DoesNotContain("walk", result.MissingRecommendedClips);
    }

    [Fact]
    public void MissingClipFallsBackToIdlesStem()
    {
        var result = Load("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"starry-eyed"}}
        """);
        Assert.Equal("starry-eyed", AvatarLoader.ResolveClipStem("walk", result));
        Assert.Equal("starry-eyed", AvatarLoader.ResolveClipStem("idle", result));
    }

    [Fact]
    public void PresentClipUsesItsOwnStem()
    {
        var result = Load("""
        {"schema_version":1,"name":"a","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"i","walk":"w"}}
        """);
        Assert.Equal("w", AvatarLoader.ResolveClipStem("walk", result));
    }

    [Fact]
    public void MissingDirectoryReportsAvatarNotFound()
    {
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var e = Assert.Throws<AvatarLoaderException>(() => AvatarLoader.Load(missing));
        Assert.Equal(AvatarLoaderError.AvatarNotFound, e.Error);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter AvatarLoaderTests`
Expected: FAIL — `AvatarLoader` 없음

- [ ] **Step 3: 구현**

```csharp
using System.Text.Json;

namespace Puck.Avatar;

public enum AvatarLoaderError
{
    AvatarNotFound,
    ManifestNotDecodable,
    /// idle에는 폴백 대상이 없다 — 권장 클립과 달리, 없으면 "성공적으로"
    /// 로드해서 나중에 눈치채게 두는 대신 그 자리에서 거절한다.
    MissingRequiredClips,
    /// 미래 스키마 버전은 디코딩에 성공하면서 의미가 다를 수 있다.
    /// 조용히 v1로 믿으면 오류 없이 잘못 로드된다.
    UnsupportedSchemaVersion,
}

public sealed class AvatarLoaderException(AvatarLoaderError error, string message)
    : Exception(message)
{
    public AvatarLoaderError Error { get; } = error;
}

public sealed record AvatarLoadResult(
    AvatarManifest Manifest,
    IReadOnlyList<string> MissingRecommendedClips);

public static class AvatarLoader
{
    public const int SupportedSchemaVersion = 1;

    public static IReadOnlyList<string> RequiredClips { get; } = ["idle"];

    public static IReadOnlyList<string> RecommendedClips { get; } =
    [
        "walk", "climb", "fall", "land", "point", "type", "listen",
        "react_click", "react_drag", "kick",
    ];

    public static AvatarLoadResult Load(string avatarDirectory)
    {
        var manifestPath = Path.Combine(avatarDirectory, "manifest.json");
        byte[] data;
        try
        {
            data = File.ReadAllBytes(manifestPath);
        }
        catch (Exception ex)
        {
            throw new AvatarLoaderException(AvatarLoaderError.AvatarNotFound,
                $"{Path.GetFileName(avatarDirectory)}의 manifest.json을 읽지 못했습니다: {ex.Message}");
        }
        return Load(data);
    }

    public static AvatarLoadResult Load(ReadOnlySpan<byte> manifestData)
    {
        AvatarManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<AvatarManifest>(manifestData, AvatarManifest.JsonOptions);
        }
        catch (Exception ex)
        {
            throw new AvatarLoaderException(AvatarLoaderError.ManifestNotDecodable, ex.Message);
        }

        if (manifest is null)
            throw new AvatarLoaderException(AvatarLoaderError.ManifestNotDecodable, "manifest.json이 비어 있습니다");

        if (manifest.SchemaVersion != SupportedSchemaVersion)
            throw new AvatarLoaderException(AvatarLoaderError.UnsupportedSchemaVersion,
                $"이 빌드가 모르는 schema_version입니다: {manifest.SchemaVersion}");

        var missingRequired = RequiredClips.Where(c => !manifest.Clips.ContainsKey(c)).ToList();
        if (missingRequired.Count > 0)
            throw new AvatarLoaderException(AvatarLoaderError.MissingRequiredClips,
                $"필수 클립이 없습니다: {string.Join(", ", missingRequired)}");

        var missingRecommended = RecommendedClips.Where(c => !manifest.Clips.ContainsKey(c)).ToList();
        return new AvatarLoadResult(manifest, missingRecommended);
    }

    /// 요청한 클립의 파일 스템. 없으면 idle의 것으로 떨어지고, idle 자체가
    /// 스템이 아니면(video 아바타) null.
    public static string? ResolveClipStem(string clip, AvatarLoadResult result)
    {
        if (result.Manifest.Clips.TryGetValue(clip, out var reference) &&
            reference is ClipReference.Stem named)
            return named.Value;

        if (result.Manifest.Clips.TryGetValue("idle", out var idle) &&
            idle is ClipReference.Stem idleNamed)
            return idleNamed.Value;

        return null;
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter AvatarLoaderTests`
Expected: PASS (9 tests)

- [ ] **Step 5: 커밋**

```bash
git add pet-app/Puck/Avatar/AvatarLoader.cs pet-app/PuckTests/Avatar/AvatarLoaderTests.cs
git commit -m "feat: avatar manifest validation and clip fallback"
```

---

## Task 7: 패키지 밖 경로 거부 (AvatarPackagePath)

**Files:**
- Create: `pet-app/Puck/Avatar/AvatarPackagePath.cs`
- Test: `pet-app/PuckTests/Avatar/AvatarPackagePathTests.cs`

**원본:** `puck-mac/pet-app/Puck/Avatar/AvatarPackagePath.swift`

manifest.json은 아바타 패키지가 들고 오는 데이터이고, 패키지는 설치한 사람이 어디서 구했든 상관없이 들어온다. 클립/사운드 테이블을 패키지 디렉터리에 그대로 이어붙이면 `../../../../etc/passwd`를 이름으로 쓴 매니페스트가 아바타와 무관한 파일을 읽는다. Windows에서는 macOS에 없는 표기 두 개가 더 있다 — 드라이브 접두사(`C:\`)와 대체 데이터 스트림(`file.png:hidden`).

**Interfaces:**
- Consumes: 없음
- Produces: `static string? AvatarPackagePath.ResolveFile(string directory, string relativePath)` — 패키지 안이면 절대 경로, 아니면 null

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Avatar/AvatarPackagePathTests.cs`:
```csharp
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarPackagePathTests
{
    private static readonly string Package =
        Path.Combine(Path.GetTempPath(), "puck-test-avatars", "my-pet");

    [Theory]
    [InlineData("idle.png")]
    [InlineData("sounds/waah.wav")]
    [InlineData("sounds\\waah.wav")]
    [InlineData("./idle.png")]
    public void NamesInsideThePackageResolve(string relative)
    {
        var resolved = AvatarPackagePath.ResolveFile(Package, relative);
        Assert.NotNull(resolved);
        Assert.StartsWith(Path.GetFullPath(Package), resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../other/idle.png")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("/Windows/System32")]
    [InlineData("\\\\server\\share\\idle.png")]
    [InlineData("idle.png:hidden")]
    [InlineData("sounds/../../escape.wav")]
    public void NamesOutsideThePackageAreRefused(string relative)
    {
        Assert.Null(AvatarPackagePath.ResolveFile(Package, relative));
    }

    [Fact]
    public void SiblingDirectoryWithTheSamePrefixIsNotInside()
    {
        // "my-pet-evil"은 "my-pet"으로 시작하지만 안이 아니다 —
        // 접두사 비교에 구분자를 빼먹으면 통과해 버리는 고전적인 구멍.
        Assert.Null(AvatarPackagePath.ResolveFile(Package, "../my-pet-evil/idle.png"));
    }

    [Fact]
    public void DirectoryNeedNotExistYet()
    {
        var nonexistent = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "pet");
        Assert.NotNull(AvatarPackagePath.ResolveFile(nonexistent, "idle.png"));
    }
}
```

마지막 테스트는 원본이 남긴 함정을 그대로 옮긴 것이다 — 아직 없는 폴더를 기준으로 상대 경로를 풀면 부모 기준으로 풀려서 멀쩡한 이름이 전부 거절되는 버그가 mac 쪽에 실제로 있었다.

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter AvatarPackagePathTests`
Expected: FAIL — `AvatarPackagePath` 없음

- [ ] **Step 3: 구현**

```csharp
namespace Puck.Avatar;

/// 매니페스트가 부르는 이름을 그 아바타 자신의 폴더 기준으로 풀고,
/// 폴더 밖에 떨어지는 것은 전부 거절한다.
///
/// 검사 대상은 파일이 아니라 경로다: 패키지 안에 놓인 심볼릭 링크는
/// 여전히 링크가 가리키는 곳으로 간다. 그건 사람이 설치한 패키지이고
/// 이미지 자체와 같은 신뢰 수준이다. 여기서 막는 건 매니페스트가
/// 스스로 밖으로 손을 뻗는 것뿐이다.
public static class AvatarPackagePath
{
    public static string? ResolveFile(string directory, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        // 드라이브 접두사(C:\)와 대체 데이터 스트림(file.png:hidden) 둘 다
        // 콜론으로 나타난다. 아바타 패키지 안의 정상적인 이름에는 콜론이
        // 쓰일 일이 없으므로 구분하지 않고 통째로 거절한다.
        if (relativePath.Contains(':')) return null;
        if (Path.IsPathRooted(relativePath)) return null;
        if (relativePath.StartsWith(@"\\") || relativePath.StartsWith("//")) return null;

        string root;
        string candidate;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))
                   + Path.DirectorySeparatorChar;
            candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        }
        catch (Exception)
        {
            // 경로에 못 쓰는 문자가 섞였거나 너무 길다 — 읽을 수 없는 이름이다.
            return null;
        }

        // 구분자를 붙여서 비교한다: 없으면 "my-pet-evil"이 "my-pet"의
        // 안쪽으로 통과한다. Windows 파일 경로는 대소문자를 구분하지 않는다.
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter AvatarPackagePathTests`
Expected: PASS (14 tests — Theory 케이스 포함)

- [ ] **Step 5: 커밋**

```bash
git add pet-app/Puck/Avatar/AvatarPackagePath.cs pet-app/PuckTests/Avatar/AvatarPackagePathTests.cs
git commit -m "feat: refuse manifest paths that escape the avatar package"
```

---

## Task 8: 스프라이트 로딩과 알파 경계

**Files:**
- Create: `pet-app/Puck/Avatar/IAvatarPlayable.cs`
- Create: `pet-app/Puck/Avatar/OpaquePixelBounds.cs`
- Create: `pet-app/Puck/Avatar/AlphaHitMask.cs`
- Create: `pet-app/Puck/Avatar/SpriteAvatar.cs`
- Create: `pet-app/Puck/Avatar/AvatarCatalogue.cs`
- Create: `pet-app/Puck/Resources/Avatars/dummy/manifest.json`, `idle.png`
- Test: `pet-app/PuckTests/Avatar/OpaquePixelBoundsTests.cs`, `AlphaHitMaskTests.cs`, `AvatarCatalogueTests.cs`

**원본:** `puck-mac/pet-app/Puck/Avatar/{SpriteAvatar,OpaquePixelBounds,AlphaHitMask,AvatarCatalogue,AvatarPlayable}.swift`

**Interfaces:**
- Consumes: `AvatarLoader`, `AvatarPackagePath` (Task 6, 7)
- Produces:
  - `enum AvatarFacing { Right, Left }`
  - `interface IAvatarPlayable` — `void SetScreenPosition(Point p)`, `void SetFacing(AvatarFacing f)`, `void SetUpsideDown(bool v)`, `Rect VisualBounds { get; }`, `bool HitTest(Point relative, double tolerance)`, `void Play(string clip, bool loop)`, `void Stop()`, `void UpdateBounce(string clip, TimeSpan elapsed, double intensity)`, `void TriggerJump()`
  - `static Int32Rect OpaquePixelBounds.Compute(BitmapSource source, byte alphaThreshold = 8)`
  - `sealed class AlphaHitMask` — `AlphaHitMask.From(BitmapSource)`, `bool Contains(int x, int y, int tolerance)`
  - `sealed class SpriteAvatar : IAvatarPlayable` — `SpriteAvatar.Load(string avatarDirectory)` 
  - `sealed record AvatarEntry(string Name, string Directory)`, `static IReadOnlyList<AvatarEntry> AvatarCatalogue.Scan(string avatarsRoot)`

좌표 규약: `VisualBounds`는 **펫의 접지점(발밑) 기준 상대 사각형**이다. 좌우 대칭 그림이면 `X = -width/2`, `Y = -height`. 대칭을 요구하지는 않는다 — 알파 경계에서 계산해 나온 값을 그대로 쓴다.

- [ ] **Step 1: 알파 경계의 실패하는 테스트 작성**

`PuckTests/Avatar/OpaquePixelBoundsTests.cs`:
```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class OpaquePixelBoundsTests
{
    /// (x,y)에 한 픽셀만 불투명한 w×h BGRA 비트맵.
    private static BitmapSource OnePixel(int w, int h, int x, int y)
    {
        var pixels = new byte[w * h * 4];
        var i = (y * w + x) * 4;
        pixels[i] = 255; pixels[i + 1] = 255; pixels[i + 2] = 255; pixels[i + 3] = 255;
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
    }

    [Fact]
    public void FindsTheSingleOpaquePixel()
    {
        var bounds = OpaquePixelBounds.Compute(OnePixel(10, 10, 3, 7));
        Assert.Equal(new Int32Rect(3, 7, 1, 1), bounds);
    }

    [Fact]
    public void FullyTransparentImageYieldsEmpty()
    {
        var pixels = new byte[4 * 4 * 4];
        var blank = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
        Assert.Equal(Int32Rect.Empty, OpaquePixelBounds.Compute(blank));
    }

    [Fact]
    public void AlphaBelowThresholdDoesNotCount()
    {
        var pixels = new byte[4 * 4 * 4];
        pixels[3] = 4; // (0,0) 알파 4 — 임계값 8 미만
        var faint = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
        Assert.Equal(Int32Rect.Empty, OpaquePixelBounds.Compute(faint));
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter OpaquePixelBoundsTests`
Expected: FAIL — `OpaquePixelBounds` 없음

- [ ] **Step 3: `OpaquePixelBounds` 구현**

```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Puck.Avatar;

/// 그림이 실제로 차지하는 사각형. 스프라이트 PNG는 보통 여백을 두고
/// 그려지므로, 이미지 크기를 그대로 쓰면 펫이 화면 가장자리에서
/// 눈에 보이는 것보다 일찍 멈춘다.
public static class OpaquePixelBounds
{
    public static Int32Rect Compute(BitmapSource source, byte alphaThreshold = 8)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * stride + x * 4 + 3] < alphaThreshold) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        return maxX < 0
            ? Int32Rect.Empty
            : new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
```

- [ ] **Step 4: 알파 히트마스크의 실패하는 테스트 작성**

`PuckTests/Avatar/AlphaHitMaskTests.cs`:
```csharp
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AlphaHitMaskTests
{
    private static BitmapSource OnePixel(int w, int h, int x, int y)
    {
        var pixels = new byte[w * h * 4];
        var i = (y * w + x) * 4;
        pixels[i + 3] = 255;
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
    }

    [Fact]
    public void OpaquePixelIsAHit()
    {
        var mask = AlphaHitMask.From(OnePixel(8, 8, 4, 4));
        Assert.True(mask.Contains(4, 4, tolerance: 0));
    }

    [Fact]
    public void TransparentPixelIsAMiss()
    {
        var mask = AlphaHitMask.From(OnePixel(8, 8, 4, 4));
        Assert.False(mask.Contains(0, 0, tolerance: 0));
    }

    [Fact]
    public void ToleranceGrowsTheHitAreaAroundOpaquePixels()
    {
        var mask = AlphaHitMask.From(OnePixel(8, 8, 4, 4));
        Assert.True(mask.Contains(6, 4, tolerance: 2));
        Assert.False(mask.Contains(7, 4, tolerance: 2));
    }

    [Fact]
    public void PointsOutsideTheImageAreAMiss()
    {
        var mask = AlphaHitMask.From(OnePixel(8, 8, 4, 4));
        Assert.False(mask.Contains(-1, 4, tolerance: 0));
        Assert.False(mask.Contains(8, 4, tolerance: 0));
    }
}
```

`tolerance`가 있는 이유: 펫을 잡으려는 사람은 그림의 정확한 실루엣을 겨냥하지 않는다. 몇 픽셀의 여유가 없으면 얇은 꼬리나 귀는 사실상 잡을 수 없다.

- [ ] **Step 5: 실패 확인**

Run: `dotnet test --filter AlphaHitMaskTests`
Expected: FAIL — `AlphaHitMask` 없음

- [ ] **Step 6: `AlphaHitMask` 구현**

```csharp
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Puck.Avatar;

/// 그려진 것 위를 눌렀는지 판정한다. 스프라이트의 사각형 전체를 쓰면
/// 펫의 머리 위 빈 공간을 눌러도 펫을 잡게 된다.
public sealed class AlphaHitMask
{
    private readonly bool[] _opaque;
    private readonly int _width;
    private readonly int _height;

    private AlphaHitMask(bool[] opaque, int width, int height)
    {
        _opaque = opaque;
        _width = width;
        _height = height;
    }

    public static AlphaHitMask From(BitmapSource source, byte alphaThreshold = 8)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        var opaque = new bool[width * height];
        for (var i = 0; i < opaque.Length; i++)
            opaque[i] = pixels[i * 4 + 3] >= alphaThreshold;

        return new AlphaHitMask(opaque, width, height);
    }

    /// `tolerance` 픽셀 이내에 불투명 픽셀이 있으면 명중.
    public bool Contains(int x, int y, int tolerance)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height) return false;
        if (tolerance <= 0) return _opaque[y * _width + x];

        var minX = Math.Max(0, x - tolerance);
        var maxX = Math.Min(_width - 1, x + tolerance);
        var minY = Math.Max(0, y - tolerance);
        var maxY = Math.Min(_height - 1, y + tolerance);

        for (var yy = minY; yy <= maxY; yy++)
            for (var xx = minX; xx <= maxX; xx++)
                if (_opaque[yy * _width + xx])
                    return true;

        return false;
    }
}
```

- [ ] **Step 7: `IAvatarPlayable` 작성**

```csharp
using System.Windows;

namespace Puck.Avatar;

public enum AvatarFacing { Right, Left }

/// FSM이 보는 아바타. 어떤 상태도 렌더러와 직접 말하지 않는다는 게
/// 원본의 규칙이고(F2: FSM은 아바타 타입을 몰라야 한다), 이 인터페이스가
/// 그 경계다.
public interface IAvatarPlayable
{
    void SetScreenPosition(Point position);
    void SetFacing(AvatarFacing facing);
    void SetUpsideDown(bool upsideDown);

    /// 접지점(발밑) 기준 상대 사각형. 좌우 대칭 그림이면 X = -너비/2.
    Rect VisualBounds { get; }

    bool HitTest(Point relativeToPosition, double tolerance);

    void Play(string clip, bool loop);
    void Stop();
    void UpdateBounce(string clip, TimeSpan elapsed, double intensity);
    void TriggerJump();
}
```

- [ ] **Step 8: `AvatarCatalogue`의 실패하는 테스트 작성**

`PuckTests/Avatar/AvatarCatalogueTests.cs`:
```csharp
using Puck.Avatar;

namespace PuckTests.Avatar;

public class AvatarCatalogueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AvatarCatalogueTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void MakeAvatar(string name, string manifest = """
        {"schema_version":1,"name":"x","type":"sprites",
         "hitbox":{"width":1,"height":1},"clips":{"idle":"idle"}}
        """)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifest);
    }

    [Fact]
    public void FolderNameIsTheDisplayedName()
    {
        MakeAvatar("my-pet");
        var entry = Assert.Single(AvatarCatalogue.Scan(_root));
        Assert.Equal("my-pet", entry.Name);
        Assert.Equal(Path.Combine(_root, "my-pet"), entry.Directory);
    }

    [Fact]
    public void FolderWithoutAManifestIsNotAnAvatar()
    {
        Directory.CreateDirectory(Path.Combine(_root, "not-an-avatar"));
        Assert.Empty(AvatarCatalogue.Scan(_root));
    }

    [Fact]
    public void BrokenManifestIsSkippedRatherThanFailingTheWholeScan()
    {
        MakeAvatar("good");
        MakeAvatar("broken", "{ not json");
        var names = AvatarCatalogue.Scan(_root).Select(e => e.Name).ToList();
        Assert.Equal(["good"], names);
    }

    [Fact]
    public void MissingRootIsEmptyNotAnError()
    {
        Assert.Empty(AvatarCatalogue.Scan(Path.Combine(_root, "nope")));
    }

    [Fact]
    public void ResultIsSortedByName()
    {
        MakeAvatar("zebra");
        MakeAvatar("apple");
        Assert.Equal(["apple", "zebra"], AvatarCatalogue.Scan(_root).Select(e => e.Name));
    }
}
```

- [ ] **Step 9: 실패 확인**

Run: `dotnet test --filter AvatarCatalogueTests`
Expected: FAIL — `AvatarCatalogue` 없음

- [ ] **Step 10: `AvatarCatalogue` 구현**

```csharp
using Puck.Diagnostics;

namespace Puck.Avatar;

public sealed record AvatarEntry(string Name, string Directory);

/// Avatars\ 아래 한 폴더 = 한 캐릭터. 폴더 이름이 그대로 선택 목록에 뜨는 이름이다.
public static class AvatarCatalogue
{
    public static IReadOnlyList<AvatarEntry> Scan(string avatarsRoot)
    {
        if (!Directory.Exists(avatarsRoot)) return [];

        var entries = new List<AvatarEntry>();
        foreach (var dir in Directory.EnumerateDirectories(avatarsRoot))
        {
            try
            {
                AvatarLoader.Load(dir);
            }
            catch (AvatarLoaderException ex)
            {
                // 하나가 깨졌다고 나머지가 안 보이면 안 된다. 이유는 로그에 남는다.
                AppLogger.Warning("avatar", "아바타 패키지를 건너뜁니다",
                    new Dictionary<string, object?>
                    {
                        ["directory"] = Path.GetFileName(dir),
                        ["reason"] = ex.Error.ToString(),
                        ["detail"] = ex.Message,
                    });
                continue;
            }
            entries.Add(new AvatarEntry(Path.GetFileName(dir), dir));
        }

        return entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
```

- [ ] **Step 11: `SpriteAvatar` 구현**

```csharp
using System.Windows;
using System.Windows.Media.Imaging;
using Puck.Diagnostics;

namespace Puck.Avatar;

/// 클립 하나 = PNG 하나. 실제 그리기는 Overlay/SpriteView가 하고,
/// 여기는 "지금 무엇을 어떤 크기로 어느 방향으로 그려야 하는가"만 갖는다.
public sealed class SpriteAvatar : IAvatarPlayable
{
    private readonly AvatarLoadResult _load;
    private readonly Dictionary<string, BitmapSource> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AlphaHitMask> _masks = new(StringComparer.Ordinal);

    private string _currentClip = "idle";

    public SpriteAvatar(AvatarLoadResult load, string packageDirectory)
    {
        _load = load;
        PackageDirectory = packageDirectory;
        Size = new Size(load.Manifest.Hitbox.Width * load.Manifest.Scale,
                        load.Manifest.Hitbox.Height * load.Manifest.Scale);
    }

    public string PackageDirectory { get; }

    /// manifest hitbox × scale — 그려지고 클릭되는 크기, 포인트 단위.
    public Size Size { get; }

    public Point Position { get; private set; }
    public AvatarFacing Facing { get; private set; } = AvatarFacing.Right;
    public bool UpsideDown { get; private set; }
    public double BounceScaleY { get; private set; } = 1.0;

    public BitmapSource? CurrentImage => Image(_currentClip);

    /// 접지점 기준. 대칭을 가정하지 않고 hitbox 폭의 절반만 왼쪽으로 민다.
    public Rect VisualBounds => new(-Size.Width / 2, -Size.Height, Size.Width, Size.Height);

    public static SpriteAvatar Load(string avatarDirectory)
        => new(AvatarLoader.Load(avatarDirectory), avatarDirectory);

    public void SetScreenPosition(Point position) => Position = position;
    public void SetFacing(AvatarFacing facing) => Facing = facing;
    public void SetUpsideDown(bool upsideDown) => UpsideDown = upsideDown;

    public void Play(string clip, bool loop) => _currentClip = clip;
    public void Stop() { }

    /// 정지 그림에 얹는 스쿼시&스트레치. 애니메이션 프레임이 없는 아바타에
    /// pet-app이 직접 주는 움직임이고, intensity 0이면 아무 일도 없다.
    public void UpdateBounce(string clip, TimeSpan elapsed, double intensity)
    {
        if (intensity <= 0) { BounceScaleY = 1.0; return; }
        const double frequency = 3.2; // Hz
        var phase = Math.Sin(elapsed.TotalSeconds * frequency * 2 * Math.PI);
        BounceScaleY = 1.0 + phase * 0.06 * intensity;
    }

    public void TriggerJump() { /* Overlay/SpriteView가 소비하는 일회성 신호 — Task 16 */ }

    public bool HitTest(Point relativeToPosition, double tolerance)
    {
        var image = Image(_currentClip);
        if (image is null) return false;

        var bounds = VisualBounds;
        if (!bounds.Contains(relativeToPosition) &&
            !Rect.Inflate(bounds, tolerance, tolerance).Contains(relativeToPosition))
            return false;

        // 그린 크기(Size)에서 이미지 픽셀 좌표로 되돌린다. 좌우 반전이면
        // X를 접어야 마스크가 실제로 보이는 실루엣과 맞는다.
        var localX = relativeToPosition.X - bounds.X;
        if (Facing == AvatarFacing.Left) localX = bounds.Width - localX;
        var localY = relativeToPosition.Y - bounds.Y;
        if (UpsideDown) localY = bounds.Height - localY;

        var px = (int)(localX / bounds.Width * image.PixelWidth);
        var py = (int)(localY / bounds.Height * image.PixelHeight);
        var pixelTolerance = (int)Math.Ceiling(tolerance / bounds.Width * image.PixelWidth);

        return Mask(_currentClip)?.Contains(px, py, pixelTolerance) ?? false;
    }

    private BitmapSource? Image(string clip)
    {
        if (_images.TryGetValue(clip, out var cached)) return cached;

        var stem = AvatarLoader.ResolveClipStem(clip, _load);
        if (stem is null) return null;

        var path = AvatarPackagePath.ResolveFile(PackageDirectory, stem + ".png");
        if (path is null || !File.Exists(path))
        {
            AppLogger.Warning("avatar", "클립 이미지를 찾지 못했습니다",
                new Dictionary<string, object?> { ["clip"] = clip, ["stem"] = stem });
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path);
        // 파일을 잠그지 않는다 — 그림을 다시 그린 사람이 앱을 끄지 않고도
        // "아바타 다시 불러오기"를 누를 수 있어야 한다.
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();

        _images[clip] = image;
        return image;
    }

    private AlphaHitMask? Mask(string clip)
    {
        if (_masks.TryGetValue(clip, out var cached)) return cached;
        var image = Image(clip);
        if (image is null) return null;
        var mask = AlphaHitMask.From(image);
        _masks[clip] = mask;
        return mask;
    }
}
```

- [ ] **Step 12: 동봉 아바타 작성**

`Puck/Resources/Avatars/dummy/manifest.json`:
```json
{
  "schema_version": 1,
  "name": "dummy",
  "type": "sprites",
  "hitbox": { "width": 130, "height": 133 },
  "clips": { "idle": "idle" }
}
```

`idle.png`는 `puck-mac/pet-app/Puck/Resources/Avatars/dummy/`에서 그대로 복사한다. `Puck.csproj`에 추가:

```xml
<ItemGroup>
  <Content Include="Resources\Avatars\**\*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 13: 전체 통과 확인**

Run: `dotnet test --filter Avatar`
Expected: PASS (Task 5~8의 테스트 전부)

- [ ] **Step 14: 커밋**

```bash
git add pet-app/Puck/Avatar pet-app/Puck/Resources pet-app/PuckTests/Avatar pet-app/Puck/Puck.csproj
git commit -m "feat: sprite avatar loading, alpha bounds and hit mask, catalogue"
```

---

## Task 9: 화면 공간 (ScreenSpace)

**Files:**
- Create: `pet-app/Puck/Movement/ScreenSpace.cs`
- Test: `pet-app/PuckTests/Movement/ScreenSpaceTests.cs`

**원본:** `puck-mac/pet-app/Puck/Movement/GlobalScreenSpace.swift`, `Puck/Overlay/ScreenManager.swift`, `DockInset.swift`

macOS 원본의 절반(AppKit 좌하단 원점 ↔ Quartz 좌상단 원점 Y 뒤집기)은 Windows에서 통째로 사라진다. 남는 건 "펫이 돌아다녀도 되는 영역이 어디인가" 하나다. mac의 `DockInset`(Dock을 피한다)에 대응하는 건 작업표시줄이고, `Screen.WorkingArea`가 그걸 이미 빼 준다.

**Interfaces:**
- Consumes: 없음
- Produces:
  - `sealed record ScreenSpace(IReadOnlyList<Rect> ScreenBoundsList, IReadOnlyList<Rect> WorkingAreas)`
  - `static ScreenSpace? ScreenSpace.Current()` — 디스플레이가 하나도 없으면 null
  - `Rect Bounds` — 모든 디스플레이의 합집합 경계 상자
  - `Rect RoamableArea` — 모든 작업 영역의 합집합 경계 상자 (펫이 돌아다니는 곳)
  - `Rect ScreenContaining(Point p)` — 그 점을 품은 디스플레이, 없으면 가장 가까운 것
  - `double FloorY(Point p)` — 그 점 아래의 바닥 Y (그 디스플레이 작업 영역의 아래 끝)

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Movement/ScreenSpaceTests.cs`:
```csharp
using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

public class ScreenSpaceTests
{
    // 1920×1080 주 모니터(작업표시줄 40px) + 오른쪽에 붙은 1280×1024 보조 모니터.
    private static ScreenSpace TwoMonitors() => new(
        ScreenBoundsList: [new Rect(0, 0, 1920, 1080), new Rect(1920, 0, 1280, 1024)],
        WorkingAreas: [new Rect(0, 0, 1920, 1040), new Rect(1920, 0, 1280, 1024)]);

    [Fact]
    public void BoundsIsTheUnionOfEveryDisplay()
    {
        Assert.Equal(new Rect(0, 0, 3200, 1080), TwoMonitors().Bounds);
    }

    [Fact]
    public void RoamableAreaExcludesTheTaskbar()
    {
        // 작업표시줄이 있는 주 모니터의 아래 40px은 작업 영역 밖이지만,
        // 합집합 경계 상자는 보조 모니터를 포함하므로 높이는 1040이 아니라 1040이다.
        var roamable = TwoMonitors().RoamableArea;
        Assert.Equal(0, roamable.X);
        Assert.Equal(3200, roamable.Width);
        Assert.Equal(1040, roamable.Bottom);
    }

    [Fact]
    public void ScreenContainingFindsTheRightDisplay()
    {
        var space = TwoMonitors();
        Assert.Equal(new Rect(0, 0, 1920, 1080), space.ScreenContaining(new Point(100, 100)));
        Assert.Equal(new Rect(1920, 0, 1280, 1024), space.ScreenContaining(new Point(2000, 100)));
    }

    [Fact]
    public void PointOffEveryDisplayFallsBackToTheNearestOne()
    {
        // 두 모니터의 높이가 달라서 생기는 계단 자리 — 실제로 존재하는 좌표다.
        var space = TwoMonitors();
        Assert.Equal(new Rect(1920, 0, 1280, 1024), space.ScreenContaining(new Point(2000, 1060)));
    }

    [Fact]
    public void FloorIsTheWorkingAreaBottomOfTheDisplayUnderfoot()
    {
        var space = TwoMonitors();
        Assert.Equal(1040, space.FloorY(new Point(100, 500)));   // 작업표시줄 위
        Assert.Equal(1024, space.FloorY(new Point(2000, 500)));  // 보조 모니터 바닥
    }

    [Fact]
    public void EmptyDisplayListIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new ScreenSpace([], []));
    }
}
```

디스플레이 목록이 빌 수 있다는 것은 가정이 아니라 실제로 일어나는 일이다 — 모든 디스플레이가 잠들거나 분리되는 순간이 있다. 원본은 그때 `nil`을 돌려주고 호출자가 마지막으로 알던 값을 유지하게 했다. 여기서도 같다: `Current()`가 null을 주면 호출자는 갖고 있던 `ScreenSpace`를 그대로 쓴다.

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter ScreenSpaceTests`
Expected: FAIL — `ScreenSpace` 없음

- [ ] **Step 3: 구현**

```csharp
using System.Windows;
using WinForms = System.Windows.Forms;

namespace Puck.Movement;

/// 펫이 사는 좌표계. 가상 화면 물리 픽셀, 좌상단 원점, Y는 아래로.
///
/// macOS 원본에 있던 좌표 변환(AppKit 좌하단 ↔ Quartz 좌상단)은 여기 없다.
/// Win32가 이미 좌상단 원점이라 뒤집을 것이 없다.
public sealed record ScreenSpace
{
    public ScreenSpace(IReadOnlyList<Rect> screenBoundsList, IReadOnlyList<Rect> workingAreas)
    {
        if (screenBoundsList.Count == 0)
            throw new ArgumentException("디스플레이가 하나도 없습니다", nameof(screenBoundsList));
        if (screenBoundsList.Count != workingAreas.Count)
            throw new ArgumentException("디스플레이 수와 작업 영역 수가 다릅니다", nameof(workingAreas));

        ScreenBoundsList = screenBoundsList;
        WorkingAreas = workingAreas;
    }

    public IReadOnlyList<Rect> ScreenBoundsList { get; }
    public IReadOnlyList<Rect> WorkingAreas { get; }

    public Rect Bounds => Union(ScreenBoundsList);
    public Rect RoamableArea => Union(WorkingAreas);

    /// 지금 연결된 디스플레이 구성. 전부 잠들어 목록이 비면 null —
    /// 호출자는 마지막으로 알던 ScreenSpace를 그대로 쓴다.
    public static ScreenSpace? Current()
    {
        var screens = WinForms.Screen.AllScreens;
        if (screens.Length == 0) return null;

        var bounds = screens.Select(s => ToRect(s.Bounds)).ToList();
        var working = screens.Select(s => ToRect(s.WorkingArea)).ToList();
        return new ScreenSpace(bounds, working);
    }

    public Rect ScreenContaining(Point point)
    {
        foreach (var screen in ScreenBoundsList)
            if (screen.Contains(point))
                return screen;

        // 모니터 크기가 다르면 어느 디스플레이에도 속하지 않는 좌표가 실제로 생긴다.
        return ScreenBoundsList.MinBy(s => SquaredDistance(s, point))!;
    }

    /// 그 지점에서 곧장 떨어지면 닿는 바닥. Phase 2에서 창 윗면이
    /// 착지면으로 끼어들기 전까지는 언제나 화면 바닥이다.
    public double FloorY(Point point)
    {
        var index = IndexOfScreenContaining(point);
        return WorkingAreas[index].Bottom;
    }

    private int IndexOfScreenContaining(Point point)
    {
        for (var i = 0; i < ScreenBoundsList.Count; i++)
            if (ScreenBoundsList[i].Contains(point))
                return i;

        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < ScreenBoundsList.Count; i++)
        {
            var distance = SquaredDistance(ScreenBoundsList[i], point);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }
        return best;
    }

    private static double SquaredDistance(Rect rect, Point point)
    {
        var dx = Math.Max(Math.Max(rect.Left - point.X, 0), point.X - rect.Right);
        var dy = Math.Max(Math.Max(rect.Top - point.Y, 0), point.Y - rect.Bottom);
        return dx * dx + dy * dy;
    }

    private static Rect Union(IReadOnlyList<Rect> rects)
    {
        var union = rects[0];
        for (var i = 1; i < rects.Count; i++) union.Union(rects[i]);
        return union;
    }

    private static Rect ToRect(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}
```

`RoamableAreaExcludesTheTaskbar` 테스트가 요구하는 `Bottom == 1040`이 나오려면 `Union`이 경계 상자를 합쳐야 한다 — 두 작업 영역의 아래 끝이 각각 1040과 1024이므로 합집합의 아래 끝은 1040이다.

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter ScreenSpaceTests`
Expected: PASS (6 tests)

- [ ] **Step 5: 커밋**

```bash
git add pet-app/Puck/Movement/ScreenSpace.cs pet-app/PuckTests/Movement/ScreenSpaceTests.cs
git commit -m "feat: screen space and roamable area from Win32 displays"
```

---

## Task 10: 운동 계산 (MovementSolver)

**Files:**
- Create: `pet-app/Puck/Movement/MovementSolver.cs`
- Test: `pet-app/PuckTests/Movement/MovementSolverTests.cs`

**원본:** `puck-mac/pet-app/Puck/Movement/MovementSolver.swift` — **상수는 하나도 바꾸지 않는다.**

물리 엔진은 없다. MoveTo는 등속(px/sec) + 도착 반경, Fall만 낙하 가속도. 어떤 엔티티나 상태에도 의존하지 않아서 계산만 따로 테스트된다.

**Interfaces:**
- Consumes: `AvatarFacing` (Task 8)
- Produces:
  - 상수: `WalkSpeed = 90`, `Gravity = 2400`, `TerminalVelocity = 1200`, `ArrivalRadius = 2`, `MaxThrowSpeed = 2500`, `GroundFrictionRate = 3.0` (전부 `double`)
  - `readonly record struct Step(Point Position, bool HasArrived)`
  - `readonly record struct FallStep(Point Position, double Velocity, bool HasLanded, bool TouchedFloor)`
  - `static Step MovementSolver.StepToward(Point from, Point target, double dt, double speed = WalkSpeed, double arrivalRadius = ArrivalRadius)`
  - `static Point MovementSolver.CappedThrow(Vector velocity, double maxSpeed = MaxThrowSpeed)`
  - `static AvatarFacing? MovementSolver.FacingToward(Point from, Point target)`
  - `static FallStep MovementSolver.FallStep(Point from, double velocity, double dt, double landingY)`
  - `static double MovementSolver.ApplyGroundFriction(double horizontalSpeed, double dt)`

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Movement/MovementSolverTests.cs`:
```csharp
using System.Windows;
using Puck.Avatar;
using Puck.Movement;

namespace PuckTests.Movement;

public class MovementSolverTests
{
    [Fact]
    public void ConstantsMatchTheMacOriginal()
    {
        Assert.Equal(90, MovementSolver.WalkSpeed);
        Assert.Equal(2400, MovementSolver.Gravity);
        Assert.Equal(1200, MovementSolver.TerminalVelocity);
        Assert.Equal(2, MovementSolver.ArrivalRadius);
        Assert.Equal(2500, MovementSolver.MaxThrowSpeed);
        Assert.Equal(3.0, MovementSolver.GroundFrictionRate);
    }

    [Fact]
    public void OneSecondAtWalkSpeedCoversWalkSpeedPixels()
    {
        var step = MovementSolver.StepToward(new Point(0, 0), new Point(1000, 0), dt: 1.0);
        Assert.Equal(90, step.Position.X, precision: 6);
        Assert.False(step.HasArrived);
    }

    [Fact]
    public void DiagonalTravelIsNotFasterThanAxisAligned()
    {
        var step = MovementSolver.StepToward(new Point(0, 0), new Point(1000, 1000), dt: 1.0);
        var travelled = Math.Sqrt(step.Position.X * step.Position.X + step.Position.Y * step.Position.Y);
        Assert.Equal(90, travelled, precision: 6);
    }

    [Fact]
    public void ATravelLongerThanTheRemainingDistanceLandsExactlyOnTarget()
    {
        var target = new Point(10, 0);
        var step = MovementSolver.StepToward(new Point(0, 0), target, dt: 1.0);
        Assert.Equal(target, step.Position);
        Assert.True(step.HasArrived);
    }

    [Fact]
    public void InsideArrivalRadiusIsAlreadyThere()
    {
        var from = new Point(0, 0);
        var step = MovementSolver.StepToward(from, new Point(1, 0), dt: 1.0);
        Assert.Equal(from, step.Position);
        Assert.True(step.HasArrived);
    }

    [Fact]
    public void ThrowIsCappedAlongItsOwnDirection()
    {
        var capped = MovementSolver.CappedThrow(new Vector(9000, 9000));
        var speed = Math.Sqrt(capped.X * capped.X + capped.Y * capped.Y);
        Assert.Equal(MovementSolver.MaxThrowSpeed, speed, precision: 6);
        // 방향은 그대로 — 대각선을 축별로 자르면 어디로 가는지가 휜다.
        Assert.Equal(capped.X, capped.Y, precision: 6);
    }

    [Fact]
    public void ThrowBelowTheCapIsUntouched()
    {
        var velocity = new Vector(100, -200);
        var capped = MovementSolver.CappedThrow(velocity);
        Assert.Equal(velocity.X, capped.X);
        Assert.Equal(velocity.Y, capped.Y);
    }

    [Fact]
    public void FacingFollowsHorizontalDirectionOnly()
    {
        Assert.Equal(AvatarFacing.Right, MovementSolver.FacingToward(new Point(0, 0), new Point(10, 0)));
        Assert.Equal(AvatarFacing.Left, MovementSolver.FacingToward(new Point(10, 0), new Point(0, 0)));
        // 순수 수직 이동은 방향을 바꾸지 않는다 — 벽을 타는 펫이 뒤집히면 안 된다.
        Assert.Null(MovementSolver.FacingToward(new Point(0, 0), new Point(0, 100)));
    }

    [Fact]
    public void FallAcceleratesDownward()
    {
        var step = MovementSolver.FallStep(new Point(0, 0), velocity: 0, dt: 0.1, landingY: 10_000);
        Assert.Equal(240, step.Velocity, precision: 6);   // 2400 * 0.1
        Assert.Equal(24, step.Position.Y, precision: 6);
        Assert.False(step.HasLanded);
        Assert.False(step.TouchedFloor);
    }

    [Fact]
    public void FallSettlesAtTerminalVelocity()
    {
        var step = MovementSolver.FallStep(new Point(0, 0), velocity: 1190, dt: 1.0, landingY: 10_000);
        Assert.Equal(MovementSolver.TerminalVelocity, step.Velocity, precision: 6);
    }

    [Fact]
    public void FallStopsOnTheLandingSurfaceInsteadOfSinkingThrough()
    {
        var step = MovementSolver.FallStep(new Point(0, 95), velocity: 1000, dt: 1.0, landingY: 100);
        Assert.Equal(100, step.Position.Y);
        Assert.True(step.HasLanded);
        Assert.True(step.TouchedFloor);
    }

    [Fact]
    public void GroundFrictionDecaysExponentiallyAndIsFrameRateIndependent()
    {
        // 한 번의 0.2초 = 두 번의 0.1초.
        var once = MovementSolver.ApplyGroundFriction(1000, 0.2);
        var twice = MovementSolver.ApplyGroundFriction(MovementSolver.ApplyGroundFriction(1000, 0.1), 0.1);
        Assert.Equal(once, twice, precision: 9);
        Assert.True(once < 1000);
    }
}
```

프레임률 독립성 테스트가 중요하다. 60fps에서 튜닝한 마찰이 144Hz 모니터에서 다르게 느껴지면 그건 버그이고, 지수 감쇠로 쓰면 공짜로 해결된다.

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter MovementSolverTests`
Expected: FAIL — `MovementSolver` 없음

- [ ] **Step 3: 구현**

```csharp
using System.Windows;
using Puck.Avatar;

namespace Puck.Movement;

/// MoveTo/Walk/Fall의 순수 산술. 엔티티도 상태도 모른다 — 이 위의 상태들은
/// 언제 이걸 부를지만 정한다.
///
/// 좌표는 가상 화면 물리 픽셀, Y는 아래로. 그래서 중력은 Y에 더해진다.
public static class MovementSolver
{
    /// 기본 걷기 속도, px/sec.
    public const double WalkSpeed = 90;

    /// 기본 중력, px/sec². 창 윗면에서 떨어지는 짧은 낙하도 창 한 채
    /// 높이 안에서 제대로 속도가 붙을 만큼 높다.
    public const double Gravity = 2400;

    /// 낙하가 안착하는 속도, px/sec. 상한이 없으면 긴 낙하의 마지막
    /// 4분의 1을 한 프레임에 35px씩 움직여, 착지가 아니라 바닥으로
    /// 낚아채이는 것처럼 보인다.
    public const double TerminalVelocity = 1200;

    /// "도착"으로 치는 거리.
    public const double ArrivalRadius = 2;

    /// 던져질 수 있는 최고 속도, px/sec. 손목 스냅 한 번이면 커서는
    /// 초당 수천 px을 가고, 그대로 두면 눈이 따라가기 전에 펫이 화면
    /// 반대편으로 사라진다. 이 값은 디스플레이 하나를 약 0.5초에 가로지른다.
    public const double MaxThrowSpeed = 2500;

    /// 착지 후 수평 속도가 줄어드는 비율. 즉시 멈추는 대신 미끄러져
    /// 멈춰야 착지가 통통 튀는 것으로 읽힌다.
    public const double GroundFrictionRate = 3.0;

    public readonly record struct Step(Point Position, bool HasArrived);

    public readonly record struct FallStep(
        Point Position, double Velocity, bool HasLanded, bool TouchedFloor);

    /// `target`을 향한 등속 한 프레임.
    ///
    /// 이동 거리는 남은 거리로 잘린다: 자르지 않으면 빠른 속도나 긴 프레임에서
    /// 목표를 지나쳐 그 주위를 영원히 진동한다.
    public static Step StepToward(Point from, Point target, double dt,
                                  double speed = WalkSpeed, double arrivalRadius = ArrivalRadius)
    {
        var dx = target.X - from.X;
        var dy = target.Y - from.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance <= arrivalRadius) return new Step(from, true);

        var travel = speed * dt;
        if (travel >= distance) return new Step(target, true);

        // 정규화 — 대각선이 축 방향보다 1.41배 빨라지지 않게.
        return new Step(new Point(from.X + dx / distance * travel,
                                  from.Y + dy / distance * travel), false);
    }

    /// 방향은 유지한 채 크기만 `maxSpeed`로 자른다. 축별이 아니라 던진
    /// 방향을 따라 자르므로, 강한 대각선은 느려지되 휘지는 않는다.
    public static Point CappedThrow(Vector velocity, double maxSpeed = MaxThrowSpeed)
    {
        var speed = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
        if (speed <= maxSpeed) return new Point(velocity.X, velocity.Y);

        var scale = maxSpeed / speed;
        return new Point(velocity.X * scale, velocity.Y * scale);
    }

    /// `target`으로 가려면 어느 쪽을 봐야 하는가. 순수 수직 이동이면 null —
    /// 벽을 타는 동안 뒤집히면 안 된다.
    public static AvatarFacing? FacingToward(Point from, Point target)
    {
        var dx = target.X - from.X;
        if (dx == 0) return null;
        return dx > 0 ? AvatarFacing.Right : AvatarFacing.Left;
    }

    /// 가속하는 자유낙하 한 프레임. `landingY`는 프레임이 지나쳤을 때
    /// 표면을 뚫고 내려가는 대신 그 위에 세운다.
    public static FallStep FallStep(Point from, double velocity, double dt, double landingY)
    {
        var next = Math.Min(velocity + Gravity * dt, TerminalVelocity);
        var y = from.Y + next * dt;

        if (y >= landingY)
            return new FallStep(new Point(from.X, landingY), next, HasLanded: true, TouchedFloor: true);

        return new FallStep(new Point(from.X, y), next, HasLanded: false, TouchedFloor: false);
    }

    /// 프레임률에 독립적인 지수 감쇠 — 60Hz에서 맞춘 감각이 144Hz에서도 같다.
    public static double ApplyGroundFriction(double horizontalSpeed, double dt)
        => horizontalSpeed * Math.Exp(-GroundFrictionRate * dt);
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter MovementSolverTests`
Expected: PASS (12 tests)

- [ ] **Step 5: 커밋**

```bash
git add pet-app/Puck/Movement/MovementSolver.cs pet-app/PuckTests/Movement/MovementSolverTests.cs
git commit -m "feat: port MovementSolver with the mac constants unchanged"
```

---

## Task 11: 화면 안에 가두기와 튕기기 (ScreenBounds)

**Files:**
- Create: `pet-app/Puck/Movement/PetBounds.cs`
- Test: `pet-app/PuckTests/Movement/PetBoundsTests.cs`

**원본:** `puck-mac/pet-app/Puck/Movement/ScreenBounds.swift`

이름을 `ScreenBounds`가 아니라 `PetBounds`로 바꾼다 — Task 9의 `ScreenSpace.ScreenBoundsList`와 이름이 겹쳐서 읽는 사람이 헷갈린다. 하는 일은 원본과 같다: "화면 안"이 무엇인지에 대한 정의가 걷기·떨어지기·던져지기에 걸쳐 한 곳에만 있게 하고, 그 정의를 펫의 **위치**가 아니라 **외곽선**으로 표현한다. 위치는 발밑 한 점이고 그림의 가로 중심이므로, 위치만 가두면 펫의 절반이 화면 밖에 걸린다.

**Interfaces:**
- Consumes: 없음
- Produces:
  - 상수: `Restitution = 0.55`, `MinimumBounceSpeed = 60`, `LandingRestitution = 0.35`, `MinimumLandingBounceSpeed = 100`
  - `readonly record struct Bounce(Point Position, double Velocity)`
  - `static bool PetBounds.IsOversizedHorizontally(Rect visualBounds, Rect area)`
  - `static Point PetBounds.Contain(Point position, Rect visualBounds, Rect area)`
  - `static Bounce PetBounds.BounceHorizontally(Point position, double velocity, Rect visualBounds, Rect area)`
  - `static Bounce PetBounds.BounceOffCeiling(Point position, double velocity, Rect visualBounds, Rect area)`
  - `static Bounce PetBounds.BounceOffFloor(Point position, double velocity, double floorY)`
  - `static (double Coordinate, double Velocity) PetBounds.Reflect(double coordinate, double limit, double velocity, double restitution = Restitution, double minimumBounceSpeed = MinimumBounceSpeed)`

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Movement/PetBoundsTests.cs`:
```csharp
using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

public class PetBoundsTests
{
    // 폭 100, 높이 200짜리 펫: 발밑 기준으로 좌우 50씩, 위로 200.
    private static readonly Rect Pet = new(-50, -200, 100, 200);
    private static readonly Rect Area = new(0, 0, 1000, 800);

    [Fact]
    public void ConstantsMatchTheMacOriginal()
    {
        Assert.Equal(0.55, PetBounds.Restitution);
        Assert.Equal(60, PetBounds.MinimumBounceSpeed);
        Assert.Equal(0.35, PetBounds.LandingRestitution);
        Assert.Equal(100, PetBounds.MinimumLandingBounceSpeed);
    }

    [Fact]
    public void ContainStopsWhenTheArtworkMeetsTheEdgeNotTheCentre()
    {
        // 발밑 x=10이면 그림의 왼쪽은 -40 — 화면 밖이다.
        var contained = PetBounds.Contain(new Point(10, 400), Pet, Area);
        Assert.Equal(50, contained.X);
        Assert.Equal(400, contained.Y);   // Y는 건드리지 않는다
    }

    [Fact]
    public void ContainLeavesAPositionThatAlreadyFits()
    {
        var position = new Point(500, 400);
        Assert.Equal(position, PetBounds.Contain(position, Pet, Area));
    }

    [Fact]
    public void APetWiderThanTheScreenIsPinnedToTheLeftEdge()
    {
        var huge = new Rect(-2000, -100, 4000, 100);
        Assert.True(PetBounds.IsOversizedHorizontally(huge, Area));
        Assert.Equal(2000, PetBounds.Contain(new Point(500, 400), huge, Area).X);
    }

    [Fact]
    public void HittingTheRightWallReversesAndDampsTheVelocity()
    {
        // 오른쪽 한계는 1000 - 50 = 950. 960까지 갔으니 10 지나쳤다.
        var bounce = PetBounds.BounceHorizontally(new Point(960, 400), 1000, Pet, Area);
        Assert.Equal(940, bounce.Position.X);            // 2*950 - 960
        Assert.Equal(-550, bounce.Velocity);             // -1000 * 0.55
    }

    [Fact]
    public void MovingAwayFromAWallIsNotABounce()
    {
        var position = new Point(960, 400);
        var bounce = PetBounds.BounceHorizontally(position, -1000, Pet, Area);
        Assert.Equal(position, bounce.Position);
        Assert.Equal(-1000, bounce.Velocity);
    }

    [Fact]
    public void ABounceWithTooLittleEnergyComesToRestAgainstTheEdge()
    {
        // 100 * 0.55 = 55 < 60 → 가장자리에 붙어 멈춘다.
        var bounce = PetBounds.BounceHorizontally(new Point(960, 400), 100, Pet, Area);
        Assert.Equal(950, bounce.Position.X);
        Assert.Equal(0, bounce.Velocity);
    }

    [Fact]
    public void OnlyUpwardMotionBouncesOffTheCeiling()
    {
        // 머리 한계는 0 - (-200) = 200. 190까지 올라갔으니 10 지나쳤다.
        var up = PetBounds.BounceOffCeiling(new Point(500, 190), -1000, Pet, Area);
        Assert.Equal(210, up.Position.Y);
        Assert.Equal(550, up.Velocity);

        // 내려오는 건 착지이고, 어느 면에 닿는지는 여기 소관이 아니다.
        var down = PetBounds.BounceOffCeiling(new Point(500, 190), 1000, Pet, Area);
        Assert.Equal(190, down.Position.Y);
        Assert.Equal(1000, down.Velocity);
    }

    [Fact]
    public void LandingLosesMoreEnergyThanAWallHit()
    {
        var bounce = PetBounds.BounceOffFloor(new Point(500, 810), 1000, floorY: 800);
        Assert.Equal(790, bounce.Position.Y);            // 2*800 - 810
        Assert.Equal(-350, bounce.Velocity);             // -1000 * 0.35
    }

    [Fact]
    public void ASoftLandingJustRests()
    {
        // 200 * 0.35 = 70 < 100 → 바닥에 눕는다.
        var bounce = PetBounds.BounceOffFloor(new Point(500, 810), 200, floorY: 800);
        Assert.Equal(800, bounce.Position.Y);
        Assert.Equal(0, bounce.Velocity);
    }

    [Fact]
    public void ADeepOvershootComesBackOutAsFarAsItWentIn()
    {
        // 반사는 가장자리에 대해 대칭 — 한계에 딱 붙이면 빠른 튕김이
        // 눈에 보이게 거리를 잃는다.
        var bounce = PetBounds.BounceHorizontally(new Point(1000, 400), 2000, Pet, Area);
        Assert.Equal(900, bounce.Position.X);            // 2*950 - 1000
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter PetBoundsTests`
Expected: FAIL — `PetBounds` 없음

- [ ] **Step 3: 구현**

```csharp
using System.Windows;

namespace Puck.Movement;

/// 펫을 화면 안에 두고, 가장자리에서 튕긴다.
///
/// 규칙이 여기 사는 이유는 "화면 안"의 정의가 걷기·떨어지기·던져지기에
/// 대해 하나여야 하기 때문이다. 그리고 그 정의는 펫의 위치(발밑 한 점,
/// 그림의 가로 중심)가 아니라 펫 자신의 외곽선으로 쓰여 있다.
public static class PetBounds
{
    /// 튕긴 뒤 남는 속도의 비율. 완전 탄성이면 펫이 영원히 벽 사이를
    /// 오가는 것으로 읽힌다.
    public const double Restitution = 0.55;

    /// 이 아래면 튕길 값어치가 없다 — 점점 작아지는 도약으로 가장자리에서
    /// 떨리기만 한다.
    public const double MinimumBounceSpeed = 60;

    /// 착지는 벽 충돌보다 에너지를 더 잃는다. 고무공이 벽에서 튀는 게
    /// 아니라 바닥에 털썩 떨어지는 것 — 짧게 두어 번 쿵, 긴 랠리가 아니라.
    public const double LandingRestitution = 0.35;

    /// 이 아래면 그냥 바닥에 눕는다.
    public const double MinimumLandingBounceSpeed = 100;

    public readonly record struct Bounce(Point Position, double Velocity);

    /// 펫이 영역보다 넓으면 맞는 위치가 존재하지 않아 Contain/BounceHorizontally가
    /// 입력과 무관하게 언제나 왼쪽 한계로 고정한다. 자기 위치와 그 결과를
    /// 비교해 튕김을 감지하는 호출자는 매 프레임 불일치를 보고 영원히
    /// 진동하게 되므로, 그런 호출자는 이걸 먼저 물어야 한다.
    public static bool IsOversizedHorizontally(Rect visualBounds, Rect area)
        => (area.Left - visualBounds.Left) > (area.Right - visualBounds.Right);

    /// 속도를 개입시키지 않고 `visualBounds`가 `area` 안에 있도록 가둔다.
    /// 스스로 움직이는 상태(걷기, 오르기)가 화면을 벗어나지만 않게 할 때.
    public static Point Contain(Point position, Rect visualBounds, Rect area)
    {
        // 외곽선이 접지점에서 얼마나 벗어나 있는가. 좌우 대칭 그림이면
        // -너비/2와 +너비/2지만, 대칭을 요구하지는 않는다.
        var leftLimit = area.Left - visualBounds.Left;
        var rightLimit = area.Right - visualBounds.Right;
        if (leftLimit > rightLimit) return new Point(leftLimit, position.Y);

        return new Point(Math.Clamp(position.X, leftLimit, rightLimit), position.Y);
    }

    public static Bounce BounceHorizontally(Point position, double velocity, Rect visualBounds, Rect area)
    {
        var leftLimit = area.Left - visualBounds.Left;
        var rightLimit = area.Right - visualBounds.Right;
        if (leftLimit > rightLimit) return new Bounce(new Point(leftLimit, position.Y), 0);

        double limit;
        if (position.X < leftLimit && velocity < 0) limit = leftLimit;
        else if (position.X > rightLimit && velocity > 0) limit = rightLimit;
        else return new Bounce(position, velocity);

        var (coordinate, reflected) = Reflect(position.X, limit, velocity);
        return new Bounce(new Point(coordinate, position.Y), reflected);
    }

    /// 위로 가는 움직임만 다룬다: 내려오는 건 착지이고, 어느 면에
    /// 내려앉는지는 Phase 2(창 윗면, 화면 바닥)의 일이다.
    public static Bounce BounceOffCeiling(Point position, double velocity, Rect visualBounds, Rect area)
    {
        // Y는 아래로 증가하므로 위쪽 속도는 음수이고, 펫의 머리는
        // position.Y + visualBounds.Top(음수 오프셋)에 있다.
        var topLimit = area.Top - visualBounds.Top;
        if (velocity >= 0 || position.Y >= topLimit) return new Bounce(position, velocity);

        var (coordinate, reflected) = Reflect(position.Y, topLimit, velocity);
        return new Bounce(new Point(position.X, coordinate), reflected);
    }

    /// 착지면에 멈춰 서는 대신 튕긴다. `floorY`는 고정된 영역 가장자리가
    /// 아니라 그때그때의 착지면이다.
    public static Bounce BounceOffFloor(Point position, double velocity, double floorY)
    {
        if (velocity <= 0 || position.Y < floorY) return new Bounce(position, velocity);

        var (coordinate, reflected) = Reflect(
            position.Y, floorY, velocity, LandingRestitution, MinimumLandingBounceSpeed);
        return new Bounce(new Point(position.X, coordinate), reflected);
    }

    /// 지금 가장자리에 닿고 있는 한 축의 튕김. 산술이 1차원이라 좌/우/상/하
    /// 특수 케이스가 여기 없고, 호출자가 Point를 소유한다.
    ///
    /// 속도 0을 돌려주면 튕길 에너지가 다해 가장자리에 정지했다는 뜻이다.
    public static (double Coordinate, double Velocity) Reflect(
        double coordinate, double limit, double velocity,
        double restitution = Restitution, double minimumBounceSpeed = MinimumBounceSpeed)
    {
        var speed = Math.Abs(velocity) * restitution;
        if (speed < minimumBounceSpeed)
            // 에너지 소진: 가장자리에서 떨지 말고 붙어서 쉰다.
            return (limit, 0);

        // 가장자리에 대해 반사 — 깊이 지나친 프레임은 들어간 만큼 되나온다.
        // 한계에 딱 세우면 움직임의 일부를 삼켜 빠른 튕김이 눈에 띄게
        // 거리를 잃는다.
        return (2 * limit - coordinate, velocity < 0 ? speed : -speed);
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter PetBoundsTests`
Expected: PASS (11 tests)

- [ ] **Step 5: 커밋**

```bash
git add pet-app/Puck/Movement/PetBounds.cs pet-app/PuckTests/Movement/PetBoundsTests.cs
git commit -m "feat: keep the pet on screen and bounce it off edges"
```

---

## Task 12: 상태기계 계약과 CharacterBody

**Files:**
- Create: `pet-app/Puck/Movement/StateKind.cs`
- Create: `pet-app/Puck/Movement/IStateHandler.cs`
- Create: `pet-app/Puck/Movement/StateContext.cs`
- Create: `pet-app/Puck/Movement/CharacterBody.cs`
- Test: `pet-app/PuckTests/Movement/CharacterBodyTests.cs`

**원본:** `puck-mac/pet-app/Puck/Movement/{StateHandler,StateContext,CharacterBody}.swift`

**Interfaces:**
- Consumes: `IAvatarPlayable`, `AvatarFacing` (Task 8)
- Produces:
  - `enum StateKind { Idle, Walk, Fall, Land, ReactClick, ReactDrag }` — Phase 2 이후가 여기에 값을 더한다
  - `interface IStateHandler` — `string Name`, `string ClipKey`, `bool LoopsClip`, `string SoundKey`, `bool LoopsSound`, `bool RestartsOnReentry`, `void Enter()`, `void Update(double dt, StateContext context)`, `void Exit()`. 마지막 넷은 기본 구현을 갖는다.
  - `sealed class StateContext` — `CharacterBody Body`, `Rect RoamableArea`, `double AvatarHeight`, `Rect VisualBounds`, `double WalkSpeed`, `Func<Point, double> LandingY`, `Action<StateKind> RequestTransition`
  - `sealed class CharacterBody` — `const double DefaultBounceIntensity = 0.6`, `Point Position`, `AvatarFacing Facing`, `Vector LaunchVelocity`, `Rect VisualBounds`, `bool HitTest(Point, double)`, `void Play(string, bool)`, `void Stop()`, `void UpdateBounce(string, TimeSpan)`, `void TriggerJump()`

`LaunchVelocity`는 다음에 그것을 소화할 수 있는 상태(지금은 `FallState`뿐)가 첫 프레임에 읽고 지우는 일회성 발사 충격량이다. 던져진 속도는 그것을 측정한 드래그 상태보다 오래 살아야 하고, 두 상태가 이미 공유하는 것은 이것뿐이다. 매 프레임 적분되는 상시 속도가 **아니다** — 상태들은 설계상 운동학적이고, 가속도를 다루는 건 Fall뿐이다.

- [ ] **Step 1: `StateKind`와 `IStateHandler` 작성**

`Puck/Movement/StateKind.cs`:
```csharp
namespace Puck.Movement;

/// FSM 상태 식별자. Phase 2 이후(Climb, Ceiling, Point, Type, Listen,
/// Spin, Petting, Pinned, MoveTo, Travel, WalkOnTop, 공놀이 셋)가 여기에 더한다.
public enum StateKind
{
    Idle,
    Walk,
    Fall,
    Land,
    ReactClick,
    ReactDrag,
}
```

`Puck/Movement/IStateHandler.cs`:
```csharp
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
```

인터페이스 기본 구현(C# 8+)이 Swift의 프로토콜 확장 기본값 자리를 그대로 대신한다.

- [ ] **Step 2: `StateContext` 작성**

```csharp
using System.Windows;

namespace Puck.Movement;

/// 한 프레임 동안 상태가 볼 수 있고 할 수 있는 것.
/// 모든 좌표는 가상 화면 물리 픽셀, 좌상단 원점, Y는 아래로.
public sealed class StateContext
{
    public required CharacterBody Body { get; init; }

    /// 펫이 돌아다녀도 되는 영역. 보통 모든 디스플레이 작업 영역의 합집합.
    public required Rect RoamableArea { get; init; }

    /// 그려진 아바타의 현재 높이 (매니페스트 hitbox × scale).
    public required double AvatarHeight { get; init; }

    /// 접지점 기준 펫의 외곽선. 상태는 맨 위치가 아니라 이것을 기준으로
    /// 가두고 튕긴다 — 그래야 그림이 화면 가장자리에 닿을 때 멈춘다.
    public required Rect VisualBounds { get; init; }

    /// Walk/MoveTo용 px/sec. MovementSolver.WalkSpeed에 설정의 이동 속도
    /// 슬라이더를 곱한 값.
    public required double WalkSpeed { get; init; }

    /// 그 지점에서 곧장 떨어지면 닿는 표면의 Y. 상태가 창 목록에
    /// 의존하지 않도록 클로저로 주입한다 — Phase 2에서 창 윗면이
    /// 여기 끼어들 때 상태 코드는 한 줄도 바뀌지 않는다.
    public required Func<Point, double> LandingY { get; init; }

    /// 이 프레임이 끝난 뒤 다른 상태로 가 달라는 요청. 즉시가 아니라
    /// 지연되는 이유는, 어떤 상태도 자기 update 도중에 컨트롤러를
    /// 변형하면 안 되기 때문이다.
    public required Action<StateKind> RequestTransition { get; init; }
}
```

- [ ] **Step 3: `CharacterBody`의 실패하는 테스트 작성**

`PuckTests/Movement/CharacterBodyTests.cs`:
```csharp
using System.Windows;
using Puck.Avatar;
using Puck.Movement;

namespace PuckTests.Movement;

/// 무엇이 전달됐는지만 기록하는 아바타.
internal sealed class FakeAvatar : IAvatarPlayable
{
    public List<Point> Positions { get; } = [];
    public List<AvatarFacing> Facings { get; } = [];
    public List<bool> UpsideDowns { get; } = [];
    public List<(string Clip, bool Loop)> Played { get; } = [];
    public List<(string Clip, TimeSpan Elapsed, double Intensity)> Bounces { get; } = [];
    public int Jumps { get; private set; }

    public Rect VisualBounds { get; set; } = new(-50, -100, 100, 100);

    public void SetScreenPosition(Point position) => Positions.Add(position);
    public void SetFacing(AvatarFacing facing) => Facings.Add(facing);
    public void SetUpsideDown(bool upsideDown) => UpsideDowns.Add(upsideDown);
    public bool HitTest(Point relative, double tolerance) => VisualBounds.Contains(relative);
    public void Play(string clip, bool loop) => Played.Add((clip, loop));
    public void Stop() { }
    public void UpdateBounce(string clip, TimeSpan elapsed, double intensity)
        => Bounces.Add((clip, elapsed, intensity));
    public void TriggerJump() => Jumps++;
}

public class CharacterBodyTests
{
    [Fact]
    public void ConstructionPushesTheInitialPositionOntoTheAvatar()
    {
        var avatar = new FakeAvatar();
        _ = new CharacterBody(avatar, new Point(10, 20));
        Assert.Equal(new Point(10, 20), Assert.Single(avatar.Positions));
    }

    [Fact]
    public void MovingPushesTheNewPosition()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0));
        body.Position = new Point(5, 5);
        Assert.Equal(new Point(5, 5), avatar.Positions[^1]);
    }

    [Fact]
    public void WritingTheSameFacingIsANoOp()
    {
        // FSM은 걷는 동안 매 프레임 이걸 쓴다. 초당 60번 같은 변환을
        // 다시 적용하는 건 의미 없는 일이다.
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0)) { Facing = AvatarFacing.Right };
        avatar.Facings.Clear();

        body.Facing = AvatarFacing.Right;
        Assert.Empty(avatar.Facings);

        body.Facing = AvatarFacing.Left;
        Assert.Equal(AvatarFacing.Left, Assert.Single(avatar.Facings));
    }

    [Fact]
    public void UpsideDownHasTheSameNoOpGuard()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0));
        avatar.UpsideDowns.Clear();

        body.IsUpsideDown = false;
        Assert.Empty(avatar.UpsideDowns);

        body.IsUpsideDown = true;
        Assert.True(Assert.Single(avatar.UpsideDowns));
    }

    [Fact]
    public void LaunchVelocityStartsAtZero()
    {
        var body = new CharacterBody(new FakeAvatar(), new Point(0, 0));
        Assert.Equal(new Vector(0, 0), body.LaunchVelocity);
    }

    [Fact]
    public void BounceIntensityDefaultsToTheAppsOwnValue()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0));
        body.UpdateBounce("idle", TimeSpan.FromSeconds(1));
        Assert.Equal(CharacterBody.DefaultBounceIntensity, avatar.Bounces[^1].Intensity);
    }

    [Fact]
    public void ManifestBounceIntensityWins()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0), bounceIntensity: 0.2);
        body.UpdateBounce("idle", TimeSpan.FromSeconds(1));
        Assert.Equal(0.2, avatar.Bounces[^1].Intensity);
    }

    [Fact]
    public void VisualBoundsComesStraightFromTheAvatar()
    {
        var avatar = new FakeAvatar { VisualBounds = new Rect(-1, -2, 3, 4) };
        var body = new CharacterBody(avatar, new Point(0, 0));
        Assert.Equal(new Rect(-1, -2, 3, 4), body.VisualBounds);
    }
}
```

- [ ] **Step 4: 실패 확인**

Run: `dotnet test --filter CharacterBodyTests`
Expected: FAIL — `CharacterBody` 없음

- [ ] **Step 5: `CharacterBody` 구현**

```csharp
using System.Windows;
using Puck.Avatar;

namespace Puck.Movement;

/// 펫의 위치와 방향, 그리고 그 둘을 아바타로 밀어 넣는 유일한 곳.
///
/// 어떤 상태도 렌더러와 직접 말하지 않는다는 규칙(FSM은 아바타 타입을
/// 몰라야 한다)이 여기서 지켜진다.
public sealed class CharacterBody
{
    /// 매니페스트에 bounce_intensity가 없을 때 pet-app 자신의 기본값.
    public const double DefaultBounceIntensity = 0.6;

    private readonly IAvatarPlayable _avatar;
    private readonly double _bounceIntensity;

    private Point _position;
    private AvatarFacing _facing;
    private bool _isUpsideDown;

    public CharacterBody(IAvatarPlayable avatar, Point position,
                         AvatarFacing facing = AvatarFacing.Right,
                         double bounceIntensity = DefaultBounceIntensity)
    {
        _avatar = avatar;
        _position = position;
        _facing = facing;
        _bounceIntensity = bounceIntensity;
        avatar.SetScreenPosition(position);
    }

    public Point Position
    {
        get => _position;
        set { _position = value; _avatar.SetScreenPosition(value); }
    }

    /// 같은 방향을 다시 쓰는 건 아무 일도 하지 않는다 — FSM이 걷는 동안
    /// 매 프레임 이걸 쓰기 때문이다.
    public AvatarFacing Facing
    {
        get => _facing;
        set
        {
            if (_facing == value) return;
            _facing = value;
            _avatar.SetFacing(value);
        }
    }

    /// 다음에 이걸 소화할 수 있는 상태(오늘은 FallState뿐)가 첫 프레임에
    /// 읽고 지우는 일회성 발사 충격량, px/sec. 던져진 속도는 그걸 측정한
    /// 드래그 상태보다 오래 살아야 한다. 0이면 그냥 떨어뜨린 것이라,
    /// Fall로 들어오는 다른 모든 경로는 영향을 받지 않는다.
    public Vector LaunchVelocity { get; set; }

    public bool IsUpsideDown
    {
        get => _isUpsideDown;
        set
        {
            if (_isUpsideDown == value) return;
            _isUpsideDown = value;
            _avatar.SetUpsideDown(value);
        }
    }

    /// 아바타에서 그대로 전달 — FSM이 렌더러와 말하지 않게 하는 규칙 그대로.
    public Rect VisualBounds => _avatar.VisualBounds;

    public bool HitTest(Point relativeToPosition, double tolerance)
        => _avatar.HitTest(relativeToPosition, tolerance);

    public void Play(string clip, bool loop) => _avatar.Play(clip, loop);
    public void Stop() => _avatar.Stop();

    public void UpdateBounce(string clip, TimeSpan elapsed)
        => _avatar.UpdateBounce(clip, elapsed, _bounceIntensity);

    public void TriggerJump() => _avatar.TriggerJump();
}
```

- [ ] **Step 6: 통과 확인**

Run: `dotnet test --filter CharacterBodyTests`
Expected: PASS (8 tests)

- [ ] **Step 7: 커밋**

```bash
git add pet-app/Puck/Movement pet-app/PuckTests/Movement/CharacterBodyTests.cs
git commit -m "feat: FSM contracts and CharacterBody"
```

---

## Task 13: 상태들 (Idle / Walk / Fall / Land)

**Files:**
- Create: `pet-app/Puck/Movement/WanderScheduler.cs`
- Create: `pet-app/Puck/Movement/States/IdleState.cs`
- Create: `pet-app/Puck/Movement/States/WalkState.cs`
- Create: `pet-app/Puck/Movement/States/FallState.cs`
- Create: `pet-app/Puck/Movement/States/LandState.cs`
- Test: `pet-app/PuckTests/Movement/StatesTests.cs`, `pet-app/PuckTests/Movement/WanderSchedulerTests.cs`

**원본:** `puck-mac/pet-app/Puck/Movement/States/{Idle,Walk,Fall,Land}State.swift`, `WanderScheduler.swift`

Phase 1은 네 상태로 완결된다: 서 있다가(Idle) → 걷고(Walk) → 발밑이 사라지면 떨어지고(Fall) → 착지한다(Land) → 다시 Idle. 클릭/드래그 반응 두 상태는 Task 16에서 붙는다. 나머지 15개 상태(Climb, Ceiling, Point, Type, Listen, Spin, 공놀이 등)는 Phase 2 이후다.

**Interfaces:**
- Consumes: `IStateHandler`, `StateContext`, `CharacterBody`, `MovementSolver`, `PetBounds`
- Produces:
  - `sealed class WanderScheduler` — `TimeSpan MinimumInterval`/`MaximumInterval` (기본 3초/9초), `bool Tick(double dt)` (간격이 차면 true를 주고 스스로 재무장), `void Reset()`, 생성자에 `Random`을 받아 테스트가 결정적으로 돌게 한다
  - `sealed class IdleState : IStateHandler` — `Name = "Idle"`, `ClipKey = "idle"`, `LoopsClip = true`
  - `sealed class WalkState : IStateHandler` — `Name = "Walk"`, `ClipKey = "walk"`, `LoopsClip = true`, `double? TargetX` (null이면 Enter에서 뽑는다)
  - `sealed class FallState : IStateHandler` — `Name = "Fall"`, `ClipKey = "fall"`
  - `sealed class LandState : IStateHandler` — `Name = "Land"`, `ClipKey = "land"`, `RestartsOnReentry = true`

- [ ] **Step 1: `WanderScheduler`의 실패하는 테스트 작성**

`PuckTests/Movement/WanderSchedulerTests.cs`:
```csharp
using Puck.Movement;

namespace PuckTests.Movement;

public class WanderSchedulerTests
{
    [Fact]
    public void DoesNotFireBeforeTheMinimumInterval()
    {
        var scheduler = new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(3),
            MaximumInterval = TimeSpan.FromSeconds(3),
        };
        scheduler.Reset();
        Assert.False(scheduler.Tick(2.9));
    }

    [Fact]
    public void FiresOnceTheIntervalElapses()
    {
        var scheduler = new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(3),
            MaximumInterval = TimeSpan.FromSeconds(3),
        };
        scheduler.Reset();
        Assert.False(scheduler.Tick(2.0));
        Assert.True(scheduler.Tick(1.5));
    }

    [Fact]
    public void RearmsItselfAfterFiring()
    {
        var scheduler = new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(1),
        };
        scheduler.Reset();
        Assert.True(scheduler.Tick(1.0));
        Assert.False(scheduler.Tick(0.5));
        Assert.True(scheduler.Tick(0.5));
    }

    [Fact]
    public void IntervalStaysWithinItsRange()
    {
        var scheduler = new WanderScheduler(new Random(42))
        {
            MinimumInterval = TimeSpan.FromSeconds(3),
            MaximumInterval = TimeSpan.FromSeconds(9),
        };
        for (var i = 0; i < 100; i++)
        {
            scheduler.Reset();
            Assert.InRange(scheduler.NextInterval.TotalSeconds, 3, 9);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter WanderSchedulerTests`
Expected: FAIL — `WanderScheduler` 없음

- [ ] **Step 3: `WanderScheduler` 구현**

```csharp
namespace Puck.Movement;

/// 가만히 있는 펫이 다음에 뭔가 할 때까지의 시간. 무작위인 이유는
/// 정확히 N초마다 움직이는 것이 살아 있는 것으로 읽히지 않기 때문이다.
///
/// Random을 주입받는 이유는 테스트가 결정적으로 돌아야 하기 때문이다.
public sealed class WanderScheduler(Random random)
{
    private double _elapsed;

    public WanderScheduler() : this(Random.Shared) { }

    public TimeSpan MinimumInterval { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan MaximumInterval { get; init; } = TimeSpan.FromSeconds(9);

    /// 지금 재무장된 간격. 테스트와 로깅용.
    public TimeSpan NextInterval { get; private set; }

    public void Reset()
    {
        _elapsed = 0;
        var span = MaximumInterval.TotalSeconds - MinimumInterval.TotalSeconds;
        NextInterval = TimeSpan.FromSeconds(MinimumInterval.TotalSeconds + random.NextDouble() * span);
    }

    /// 간격이 찼으면 true를 돌려주고 스스로 다시 무장한다.
    public bool Tick(double dt)
    {
        if (NextInterval == TimeSpan.Zero) Reset();

        _elapsed += dt;
        if (_elapsed < NextInterval.TotalSeconds) return false;

        Reset();
        return true;
    }
}
```

- [ ] **Step 4: 상태들의 실패하는 테스트 작성**

`PuckTests/Movement/StatesTests.cs`:
```csharp
using System.Windows;
using Puck.Avatar;
using Puck.Movement;
using Puck.Movement.States;

namespace PuckTests.Movement;

public class StatesTests
{
    private static readonly Rect Area = new(0, 0, 1000, 800);
    private static readonly Rect Pet = new(-50, -100, 100, 100);

    private static (StateContext Context, CharacterBody Body, List<StateKind> Requested)
        MakeContext(Point start, Func<Point, double>? landingY = null)
    {
        var body = new CharacterBody(new FakeAvatar { VisualBounds = Pet }, start);
        var requested = new List<StateKind>();
        var context = new StateContext
        {
            Body = body,
            RoamableArea = Area,
            AvatarHeight = 100,
            VisualBounds = Pet,
            WalkSpeed = MovementSolver.WalkSpeed,
            LandingY = landingY ?? (_ => 800),
            RequestTransition = requested.Add,
        };
        return (context, body, requested);
    }

    // --- Idle ---

    [Fact]
    public void IdleAsksToWanderWhenItsTimerFires()
    {
        var (context, _, requested) = MakeContext(new Point(500, 800));
        var idle = new IdleState(new WanderScheduler(new Random(1))
        {
            MinimumInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(1),
        });
        idle.Enter();

        idle.Update(0.5, context);
        Assert.Empty(requested);

        idle.Update(0.6, context);
        Assert.Equal(StateKind.Walk, Assert.Single(requested));
    }

    [Fact]
    public void IdleFallsWhenTheSurfaceUnderfootIsGone()
    {
        // 발밑(y=400)보다 훨씬 아래(800)에 바닥이 있다 = 서 있던 것이 사라졌다.
        var (context, _, requested) = MakeContext(new Point(500, 400));
        var idle = new IdleState(new WanderScheduler(new Random(1)));
        idle.Enter();
        idle.Update(0.016, context);
        Assert.Equal(StateKind.Fall, Assert.Single(requested));
    }

    [Fact]
    public void IdleDoesNotRestartOnReentry()
    {
        // 같은 종류의 이벤트가 반복될 때마다 타이머가 초기화되면 안 된다.
        Assert.False(new IdleState(new WanderScheduler()).RestartsOnReentry);
    }

    // --- Walk ---

    [Fact]
    public void WalkMovesTowardItsTargetAndFacesThatWay()
    {
        var (context, body, _) = MakeContext(new Point(100, 800));
        var walk = new WalkState { TargetX = 900 };
        walk.Enter();
        walk.Update(1.0, context);

        Assert.Equal(190, body.Position.X, precision: 6);
        Assert.Equal(800, body.Position.Y);
        Assert.Equal(AvatarFacing.Right, body.Facing);
    }

    [Fact]
    public void WalkGoesIdleOnArrival()
    {
        var (context, _, requested) = MakeContext(new Point(100, 800));
        var walk = new WalkState { TargetX = 101 };
        walk.Enter();
        walk.Update(1.0, context);
        Assert.Equal(StateKind.Idle, Assert.Single(requested));
    }

    [Fact]
    public void WalkStopsWhenTheArtworkMeetsTheScreenEdge()
    {
        var (context, body, _) = MakeContext(new Point(940, 800));
        var walk = new WalkState { TargetX = 5000 };
        walk.Enter();
        walk.Update(1.0, context);
        // 오른쪽 한계는 1000 - 50 = 950.
        Assert.Equal(950, body.Position.X);
    }

    [Fact]
    public void WalkFallsWhenItWalksOffAnEdge()
    {
        // x가 600을 넘으면 바닥이 400에서 800으로 떨어진다.
        var (context, _, requested) = MakeContext(new Point(595, 400),
            landingY: p => p.X > 600 ? 800 : 400);
        var walk = new WalkState { TargetX = 900 };
        walk.Enter();
        walk.Update(1.0, context);
        Assert.Contains(StateKind.Fall, requested);
    }

    // --- Fall ---

    [Fact]
    public void FallConsumesTheLaunchVelocityOnItsFirstFrameOnly()
    {
        var (context, body, _) = MakeContext(new Point(500, 100));
        body.LaunchVelocity = new Vector(200, -300);

        var fall = new FallState();
        fall.Enter();
        fall.Update(0.1, context);

        Assert.Equal(new Vector(0, 0), body.LaunchVelocity);
        // 수평 성분이 살아 있어야 던지기가 던지기로 보인다.
        Assert.Equal(520, body.Position.X, precision: 6);
    }

    [Fact]
    public void FallAcceleratesAndThenLands()
    {
        var (context, body, requested) = MakeContext(new Point(500, 100));
        var fall = new FallState();
        fall.Enter();

        for (var i = 0; i < 120 && requested.Count == 0; i++)
            fall.Update(1.0 / 60, context);

        Assert.Equal(StateKind.Land, Assert.Single(requested));
        Assert.Equal(800, body.Position.Y);
    }

    [Fact]
    public void FallBouncesOffTheSideWalls()
    {
        var (context, body, _) = MakeContext(new Point(940, 100));
        body.LaunchVelocity = new Vector(2000, 0);

        var fall = new FallState();
        fall.Enter();
        fall.Update(0.1, context);

        // 오른쪽 한계 950을 지나쳤으니 되튕겨 나와야 한다.
        Assert.True(body.Position.X <= 950);
    }

    // --- Land ---

    [Fact]
    public void LandGoesIdleAfterItsClipLength()
    {
        var (context, _, requested) = MakeContext(new Point(500, 800));
        var land = new LandState();
        land.Enter();

        land.Update(0.1, context);
        Assert.Empty(requested);

        land.Update(LandState.Duration, context);
        Assert.Equal(StateKind.Idle, Assert.Single(requested));
    }

    [Fact]
    public void LandRestartsOnReentrySoARepeatedBounceReplaysIt()
    {
        Assert.True(new LandState().RestartsOnReentry);
    }
}
```

- [ ] **Step 5: 실패 확인**

Run: `dotnet test --filter StatesTests`
Expected: FAIL — `Puck.Movement.States` 없음

- [ ] **Step 6: `IdleState` 구현**

```csharp
using System.Windows;

namespace Puck.Movement.States;

/// 서 있기. 타이머가 차면 걷겠다고 요청하고, 발밑이 사라지면 떨어진다.
public sealed class IdleState(WanderScheduler scheduler) : IStateHandler
{
    /// 발밑이 이 정도 어긋나는 건 서 있는 것으로 친다 — 반올림과
    /// 픽셀 경계 때문에 정확히 같은 값이 나오지 않는다.
    public const double FootTolerance = 2;

    public string Name => "Idle";
    public string ClipKey => "idle";
    public bool LoopsClip => true;

    public void Enter() => scheduler.Reset();

    public void Update(double dt, StateContext context)
    {
        // WalkState는 매 프레임 발밑을 다시 확인하는데 Idle은 그러지 않아서,
        // 창 윗면에 착지해 쉬던 펫이 그 창이 닫히면 영원히 공중에 떠 있었다.
        // landingY가 지금 위치보다 아래면 밑에 틈이 생긴 것이다.
        var surfaceY = context.LandingY(context.Body.Position);
        if (surfaceY > context.Body.Position.Y + FootTolerance)
        {
            context.RequestTransition(StateKind.Fall);
            return;
        }

        if (scheduler.Tick(dt))
            context.RequestTransition(StateKind.Walk);
    }
}
```

Phase 2에서 `IdleWanderDelegate`(창 목록을 아는 쪽이 "어디로 갈지"를 정하는 구멍)와 `idleStateDidLoseFootingBehind`(발밑이 사라진 게 아니라 창 **뒤로** 갔을 때의 분기)가 여기에 들어온다. Phase 1에는 창이 없으므로 둘 다 아직 필요 없다.

- [ ] **Step 7: `WalkState` 구현**

```csharp
using System.Windows;

namespace Puck.Movement.States;

/// 목표 X까지 등속으로 걷는다. 화면 가장자리에서 멈추고, 발밑이 사라지면 떨어진다.
public sealed class WalkState : IStateHandler
{
    private readonly Random _random;
    private double _target;

    public WalkState(Random? random = null) => _random = random ?? Random.Shared;

    public string Name => "Walk";
    public string ClipKey => "walk";
    public bool LoopsClip => true;

    /// 목적지. null이면 Enter에서 뽑는다.
    public double? TargetX { get; init; }

    public void Enter() => _target = TargetX ?? double.NaN;

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;

        if (double.IsNaN(_target))
            _target = context.RoamableArea.Left +
                      _random.NextDouble() * context.RoamableArea.Width;

        var step = MovementSolver.StepToward(
            body.Position, new Point(_target, body.Position.Y), dt, context.WalkSpeed);

        var facing = MovementSolver.FacingToward(body.Position, new Point(_target, body.Position.Y));
        if (facing is not null) body.Facing = facing.Value;

        body.Position = PetBounds.Contain(step.Position, context.VisualBounds, context.RoamableArea);

        // 걸어 나간 자리에 바닥이 없으면 떨어진다. Idle과 같은 판정.
        var surfaceY = context.LandingY(body.Position);
        if (surfaceY > body.Position.Y + IdleState.FootTolerance)
        {
            context.RequestTransition(StateKind.Fall);
            return;
        }

        // 가장자리에 눌려 더 못 가는 경우도 도착으로 친다 — 아니면
        // 벽에 붙어 걷는 클립을 영원히 재생한다.
        var blocked = Math.Abs(body.Position.X - step.Position.X) > 0.001;
        if (step.HasArrived || blocked)
            context.RequestTransition(StateKind.Idle);
    }
}
```

- [ ] **Step 8: `FallState` 구현**

```csharp
using System.Windows;

namespace Puck.Movement.States;

/// 자유낙하. 첫 프레임에 발사 충격량을 소화하고, 옆벽에서 튕기며,
/// 착지면에 닿으면 Land로 넘긴다.
public sealed class FallState : IStateHandler
{
    private double _verticalVelocity;
    private double _horizontalVelocity;
    private bool _consumedLaunch;

    public string Name => "Fall";
    public string ClipKey => "fall";

    public void Enter()
    {
        _verticalVelocity = 0;
        _horizontalVelocity = 0;
        _consumedLaunch = false;
    }

    public void Update(double dt, StateContext context)
    {
        var body = context.Body;

        // 던져진 속도는 그것을 측정한 드래그 상태보다 오래 살아야 한다.
        // 첫 프레임에 읽고 지운다 — 0이면 그냥 떨어뜨린 것이고,
        // Fall로 들어오는 다른 경로는 영향을 받지 않는다.
        if (!_consumedLaunch)
        {
            var launch = MovementSolver.CappedThrow(body.LaunchVelocity);
            _horizontalVelocity = launch.X;
            _verticalVelocity = launch.Y;
            body.LaunchVelocity = new Vector(0, 0);
            _consumedLaunch = true;
        }

        var floorY = context.LandingY(body.Position);
        var next = MovementSolver.FallStep(body.Position, _verticalVelocity, dt, floorY);
        _verticalVelocity = next.Velocity;

        var position = new Point(next.Position.X + _horizontalVelocity * dt, next.Position.Y);

        var horizontal = PetBounds.BounceHorizontally(
            position, _horizontalVelocity, context.VisualBounds, context.RoamableArea);
        position = horizontal.Position;
        _horizontalVelocity = horizontal.Velocity;

        var ceiling = PetBounds.BounceOffCeiling(
            position, _verticalVelocity, context.VisualBounds, context.RoamableArea);
        position = ceiling.Position;
        _verticalVelocity = ceiling.Velocity;

        if (next.TouchedFloor)
        {
            var floor = PetBounds.BounceOffFloor(position, _verticalVelocity, floorY);
            position = floor.Position;
            _verticalVelocity = floor.Velocity;
            _horizontalVelocity = MovementSolver.ApplyGroundFriction(_horizontalVelocity, dt);
        }

        body.Position = position;

        if (MovementSolver.FacingToward(body.Position, position) is { } facing)
            body.Facing = facing;

        // 튕길 에너지가 남아 있으면 아직 착지가 아니다.
        if (next.HasLanded && _verticalVelocity == 0)
            context.RequestTransition(StateKind.Land);
    }
}
```

- [ ] **Step 9: `LandState` 구현**

```csharp
namespace Puck.Movement.States;

/// 착지 자세를 한 번 재생하고 Idle로 돌아간다.
public sealed class LandState : IStateHandler
{
    /// land 클립의 길이. 정지 그림 아바타에도 착지가 한 박자 보이도록
    /// 하는 최소 시간이다.
    public const double Duration = 0.35;

    private double _elapsed;

    public string Name => "Land";
    public string ClipKey => "land";

    /// 튕겨서 두 번 착지하면 두 번 재생돼야 한다.
    public bool RestartsOnReentry => true;

    public void Enter() => _elapsed = 0;

    public void Update(double dt, StateContext context)
    {
        _elapsed += dt;
        if (_elapsed >= Duration)
            context.RequestTransition(StateKind.Idle);
    }
}
```

- [ ] **Step 10: 통과 확인**

Run: `dotnet test --filter "StatesTests|WanderSchedulerTests"`
Expected: PASS (16 tests)

- [ ] **Step 11: 커밋**

```bash
git add pet-app/Puck/Movement pet-app/PuckTests/Movement
git commit -m "feat: idle/walk/fall/land states and the wander scheduler"
```

---

## Task 14: 프레임 루프와 상태 전이 (CharacterController)

**Files:**
- Create: `pet-app/Puck/Movement/FrameClock.cs`
- Create: `pet-app/Puck/Movement/CharacterController.cs`
- Test: `pet-app/PuckTests/Movement/CharacterControllerTests.cs`

**원본:** `puck-mac/pet-app/Puck/Movement/{CharacterController,FrameClock,IdleFrameRatePolicy}.swift`

**Interfaces:**
- Consumes: `IStateHandler`, `StateContext`, `CharacterBody`, `StateKind` (Task 12), 상태들 (Task 13)
- Produces:
  - `interface IFrameClock` — `event Action<double>? Tick`, `void Start()`, `void Stop()`
  - `sealed class CompositionFrameClock : IFrameClock` — `CompositionTarget.Rendering` 기반, `dt`는 초 단위이고 0.1초로 상한을 둔다
  - `sealed class CharacterController` — 생성자 `(CharacterBody body, IReadOnlyDictionary<StateKind, IStateHandler> states, StateKind initial, Func<StateContext> contextFactory)`, `StateKind Current`, `void Advance(double dt)`, `void Request(StateKind kind)`, `event Action<StateKind, StateKind>? Transitioned`

`dt` 상한이 필요한 이유: 노트북 뚜껑을 닫았다 열면 마지막 프레임 이후 몇 시간이 지나 있다. 상한이 없으면 그 한 프레임에 펫이 몇 킬로픽셀을 이동한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Movement/CharacterControllerTests.cs`:
```csharp
using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

/// 무엇이 호출됐는지만 기록하는 상태.
internal sealed class RecordingState(string name, string clipKey = "idle") : IStateHandler
{
    public string Name => name;
    public string ClipKey => clipKey;
    public bool LoopsClip { get; init; }
    public bool RestartsOnReentry { get; init; }

    public int Enters { get; private set; }
    public int Exits { get; private set; }
    public List<double> Updates { get; } = [];

    /// 이 상태가 update에서 요청할 전이. null이면 아무것도 요청하지 않는다.
    public StateKind? RequestOnUpdate { get; set; }

    public void Enter() => Enters++;
    public void Exit() => Exits++;

    public void Update(double dt, StateContext context)
    {
        Updates.Add(dt);
        if (RequestOnUpdate is { } kind) context.RequestTransition(kind);
    }
}

public class CharacterControllerTests
{
    private static CharacterController Make(
        IReadOnlyDictionary<StateKind, IStateHandler> states,
        StateKind initial,
        CharacterBody? body = null)
    {
        body ??= new CharacterBody(new FakeAvatar(), new Point(0, 0));
        return new CharacterController(body, states, initial, () => new StateContext
        {
            Body = body,
            RoamableArea = new Rect(0, 0, 1000, 800),
            AvatarHeight = 100,
            VisualBounds = new Rect(-50, -100, 100, 100),
            WalkSpeed = MovementSolver.WalkSpeed,
            LandingY = _ => 800,
            RequestTransition = _ => { },
        });
    }

    [Fact]
    public void TheInitialStateIsEnteredOnce()
    {
        var idle = new RecordingState("Idle");
        _ = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle }, StateKind.Idle);
        Assert.Equal(1, idle.Enters);
    }

    [Fact]
    public void AdvanceForwardsDtToTheCurrentState()
    {
        var idle = new RecordingState("Idle");
        var controller = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle }, StateKind.Idle);
        controller.Advance(0.016);
        Assert.Equal([0.016], idle.Updates);
    }

    [Fact]
    public void ATransitionExitsTheOldStateAndEntersTheNew()
    {
        var idle = new RecordingState("Idle");
        var walk = new RecordingState("Walk", "walk");
        var controller = Make(new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = idle,
            [StateKind.Walk] = walk,
        }, StateKind.Idle);

        controller.Request(StateKind.Walk);
        controller.Advance(0.016);

        Assert.Equal(1, idle.Exits);
        Assert.Equal(1, walk.Enters);
        Assert.Equal(StateKind.Walk, controller.Current);
    }

    [Fact]
    public void ATransitionRequestedDuringUpdateTakesEffectAfterThatFrame()
    {
        // 어떤 상태도 자기 update 도중에 교체되면 안 된다.
        var idle = new RecordingState("Idle") { RequestOnUpdate = StateKind.Walk };
        var walk = new RecordingState("Walk", "walk");
        var controller = Make(new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = idle,
            [StateKind.Walk] = walk,
        }, StateKind.Idle);

        controller.Advance(0.016);

        Assert.Single(idle.Updates);       // 이 프레임은 끝까지 Idle의 것
        Assert.Equal(StateKind.Walk, controller.Current);
        Assert.Empty(walk.Updates);        // Walk의 첫 update는 다음 프레임
    }

    [Fact]
    public void ReenteringTheSameStateIsANoOpByDefault()
    {
        var idle = new RecordingState("Idle");
        var controller = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle }, StateKind.Idle);

        controller.Request(StateKind.Idle);
        controller.Advance(0.016);

        Assert.Equal(1, idle.Enters);
        Assert.Equal(0, idle.Exits);
    }

    [Fact]
    public void AStateThatRestartsOnReentryIsRestarted()
    {
        var land = new RecordingState("Land", "land") { RestartsOnReentry = true };
        var controller = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Land] = land }, StateKind.Land);

        controller.Request(StateKind.Land);
        controller.Advance(0.016);

        Assert.Equal(2, land.Enters);
        Assert.Equal(1, land.Exits);
    }

    [Fact]
    public void EnteringAStatePlaysItsClipWithItsLoopFlag()
    {
        var avatar = new FakeAvatar();
        var body = new CharacterBody(avatar, new Point(0, 0));
        var idle = new RecordingState("Idle") { LoopsClip = true };
        var walk = new RecordingState("Walk", "walk") { LoopsClip = true };
        var controller = Make(new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = idle,
            [StateKind.Walk] = walk,
        }, StateKind.Idle, body);

        controller.Request(StateKind.Walk);
        controller.Advance(0.016);

        Assert.Equal(("walk", true), avatar.Played[^1]);
    }

    [Fact]
    public void AnUnknownStateIsRefusedRatherThanCrashingTheFrameLoop()
    {
        var idle = new RecordingState("Idle");
        var controller = Make(new Dictionary<StateKind, IStateHandler> { [StateKind.Idle] = idle }, StateKind.Idle);

        controller.Request(StateKind.Fall);   // 등록되지 않았다
        controller.Advance(0.016);

        Assert.Equal(StateKind.Idle, controller.Current);
    }

    [Fact]
    public void TransitionedFiresWithBothStates()
    {
        var idle = new RecordingState("Idle");
        var walk = new RecordingState("Walk", "walk");
        var controller = Make(new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = idle,
            [StateKind.Walk] = walk,
        }, StateKind.Idle);

        (StateKind From, StateKind To)? seen = null;
        controller.Transitioned += (from, to) => seen = (from, to);

        controller.Request(StateKind.Walk);
        controller.Advance(0.016);

        Assert.Equal((StateKind.Idle, StateKind.Walk), seen);
    }
}
```

`AnUnknownStateIsRefused...`가 중요하다. Phase 2 이후 상태가 늘어나면서 등록을 빠뜨리는 일이 반드시 생기고, 그때 프레임 루프가 죽으면 앱 전체가 얼어붙는다.

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter CharacterControllerTests`
Expected: FAIL — `CharacterController` 없음

- [ ] **Step 3: `FrameClock` 구현**

```csharp
using System.Diagnostics;
using System.Windows.Media;

namespace Puck.Movement;

public interface IFrameClock
{
    /// 인자는 지난 프레임 이후 경과한 초.
    event Action<double>? Tick;
    void Start();
    void Stop();
}

/// WPF의 합성 프레임에 얹은 시계. 화면 주사율을 따라가므로 60Hz든
/// 144Hz든 그리기와 어긋나지 않는다.
public sealed class CompositionFrameClock : IFrameClock
{
    /// 한 프레임에 허용하는 최대 dt. 노트북 뚜껑을 닫았다 열면 마지막
    /// 프레임 이후 몇 시간이 지나 있고, 상한이 없으면 펫이 그 한 프레임에
    /// 몇 킬로픽셀을 이동한다.
    public const double MaximumDelta = 0.1;

    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _last;
    private bool _running;

    public event Action<double>? Tick;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _stopwatch.Restart();
        _last = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
        _stopwatch.Stop();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _stopwatch.Elapsed;
        var dt = (now - _last).TotalSeconds;
        _last = now;

        if (dt <= 0) return;
        Tick?.Invoke(Math.Min(dt, MaximumDelta));
    }
}
```

- [ ] **Step 4: `CharacterController` 구현**

```csharp
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
            RequestTransition = Request,
        };

        Handler.Update(dt, frameContext);

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
```

- [ ] **Step 5: 통과 확인**

Run: `dotnet test --filter CharacterControllerTests`
Expected: PASS (9 tests)

- [ ] **Step 6: 커밋**

```bash
git add pet-app/Puck/Movement/CharacterController.cs pet-app/Puck/Movement/FrameClock.cs pet-app/PuckTests/Movement/CharacterControllerTests.cs
git commit -m "feat: frame clock and deferred state transitions"
```

---

## Task 15: 투명·항상위·클릭스루 오버레이 창

**Files:**
- Create: `pet-app/Puck/Interop/Win32.cs`
- Create: `pet-app/Puck/Interop/WindowStyles.cs`
- Create: `pet-app/Puck/Overlay/OverlayPositioner.cs`
- Create: `pet-app/Puck/Overlay/PetOverlayWindow.xaml`, `PetOverlayWindow.xaml.cs`
- Test: `pet-app/PuckTests/Overlay/OverlayPositionerTests.cs`

**원본:** `puck-mac/pet-app/Puck/Overlay/{OverlayWindow,OverlayWindowController,ClickThroughController}.swift`

Phase 1에서 가장 위험한 부분이다. 여기가 안 되면 나머지가 서 있을 바닥이 없으므로 먼저 뚫는다.

**Interfaces:**
- Consumes: `SpriteAvatar` (Task 8)
- Produces:
  - `static class Win32` — `SetWindowPos`, `GetWindowLong`, `SetWindowLong`, `GetDpiForWindow` P/Invoke와 관련 상수
  - `static class WindowStyles` — `void MakeOverlay(IntPtr hwnd)`, `void SetClickThrough(IntPtr hwnd, bool clickThrough)`
  - `static class OverlayPositioner` — `static Int32Rect FrameFor(Point petPosition, Rect visualBounds, double padding)`
  - `sealed partial class PetOverlayWindow : Window` — `void MoveTo(Point petPosition, Rect visualBounds)`, `bool ClickThrough { get; set; }`, `double DpiScale { get; }`

**창 크기 결정:** 오버레이는 펫의 외곽선에 `padding`(기본 48px)을 두른 사각형이다. 여유가 필요한 이유는 스쿼시&스트레치와 점프가 외곽선 밖으로 삐져나오고, 클릭 판정의 `tolerance`도 밖으로 자라기 때문이다.

- [ ] **Step 1: 배치 계산의 실패하는 테스트 작성**

`PuckTests/Overlay/OverlayPositionerTests.cs`:
```csharp
using System.Windows;
using Puck.Overlay;

namespace PuckTests.Overlay;

public class OverlayPositionerTests
{
    private static readonly Rect Pet = new(-50, -200, 100, 200);

    [Fact]
    public void FrameSurroundsTheArtworkWithPadding()
    {
        var frame = OverlayPositioner.FrameFor(new Point(500, 800), Pet, padding: 48);
        // 그림은 (450, 600)-(550, 800). 사방으로 48씩.
        Assert.Equal(402, frame.X);
        Assert.Equal(552, frame.Y);
        Assert.Equal(196, frame.Width);
        Assert.Equal(296, frame.Height);
    }

    [Fact]
    public void ZeroPaddingIsExactlyTheArtwork()
    {
        var frame = OverlayPositioner.FrameFor(new Point(0, 0), Pet, padding: 0);
        Assert.Equal(-50, frame.X);
        Assert.Equal(-200, frame.Y);
        Assert.Equal(100, frame.Width);
        Assert.Equal(200, frame.Height);
    }

    [Fact]
    public void FractionalPositionsRoundOutwardSoNothingIsClipped()
    {
        var frame = OverlayPositioner.FrameFor(new Point(500.7, 800.2), Pet, padding: 0);
        Assert.Equal(450, frame.X);              // floor(450.7)
        Assert.Equal(600, frame.Y);              // floor(600.2)
        Assert.True(frame.Width >= 100);
        Assert.True(frame.Height >= 200);
    }

    [Fact]
    public void ADegenerateOutlineStillYieldsAUsableFrame()
    {
        var frame = OverlayPositioner.FrameFor(new Point(0, 0), Rect.Empty, padding: 4);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }
}
```

바깥쪽으로 반올림하는 게 중요하다. 안쪽으로 자르면 서브픽셀 위치에서 그림의 오른쪽 한 줄이 창 밖으로 잘려 나간다.

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter OverlayPositionerTests`
Expected: FAIL — `OverlayPositioner` 없음

- [ ] **Step 3: `OverlayPositioner` 구현**

```csharp
using System.Windows;

namespace Puck.Overlay;

/// 펫 좌표 → 오버레이 창이 있어야 할 물리 픽셀 사각형.
///
/// 오버레이는 화면 전체가 아니라 펫만 한 크기다 (플랜의 "macOS 원본과
/// 다르게 가는 것" 표 참고). 여유(padding)를 두는 이유는 스쿼시&스트레치와
/// 점프가 외곽선 밖으로 나가고, 클릭 판정의 tolerance도 밖으로 자라기 때문이다.
public static class OverlayPositioner
{
    public const double DefaultPadding = 48;

    public static Int32Rect FrameFor(Point petPosition, Rect visualBounds, double padding = DefaultPadding)
    {
        var outline = visualBounds.IsEmpty ? new Rect(0, 0, 1, 1) : visualBounds;

        var left = petPosition.X + outline.Left - padding;
        var top = petPosition.Y + outline.Top - padding;
        var right = petPosition.X + outline.Right + padding;
        var bottom = petPosition.Y + outline.Bottom + padding;

        // 바깥쪽으로 반올림 — 안쪽으로 자르면 서브픽셀 위치에서 그림의
        // 가장자리 한 줄이 창 밖으로 잘려 나간다.
        var x = (int)Math.Floor(left);
        var y = (int)Math.Floor(top);
        var width = Math.Max(1, (int)Math.Ceiling(right) - x);
        var height = Math.Max(1, (int)Math.Ceiling(bottom) - y);

        return new Int32Rect(x, y, width, height);
    }
}
```

- [ ] **Step 4: `Win32` P/Invoke 작성**

```csharp
using System.Runtime.InteropServices;

namespace Puck.Interop;

/// P/Invoke 선언은 전부 여기 산다. 다른 코드는 Win32를 직접 부르지 않는다.
internal static partial class Win32
{
    public const int GWL_EXSTYLE = -20;

    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const int WM_DPICHANGED = 0x02E0;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    public static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                            int X, int Y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(IntPtr hWnd);
}
```

64비트에서 `GetWindowLong`/`SetWindowLong`은 확장 스타일(32비트 값)에는 그대로 쓸 수 있다. 포인터 크기 필드를 다루게 되면 `GetWindowLongPtrW`로 바꿔야 한다.

- [ ] **Step 5: `WindowStyles` 작성**

```csharp
namespace Puck.Interop;

/// 오버레이 창의 확장 스타일. mac의 NSWindow 설정(레벨, ignoresMouseEvents,
/// collectionBehavior)에 해당하는 것들이 여기 모여 있다.
public static class WindowStyles
{
    /// 레이어드(픽셀 단위 투명) + 도구 창(Alt+Tab과 작업표시줄에 안 뜸)
    /// + 활성화되지 않음(클릭해도 지금 작업 중인 창의 포커스를 빼앗지 않음).
    public static void MakeOverlay(IntPtr hwnd)
    {
        var style = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        style |= Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, style);
    }

    /// 켜면 마우스 이벤트가 창을 그대로 통과해 아래 창으로 간다.
    /// 펫은 대부분의 시간을 이 상태로 보낸다 — 그림 밖의 여백에서
    /// 클릭이 막히면 아래 앱을 쓸 수 없다.
    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        var style = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        style = clickThrough
            ? style | Win32.WS_EX_TRANSPARENT
            : style & ~Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, style);
    }
}
```

- [ ] **Step 6: `PetOverlayWindow` 작성**

`Puck/Overlay/PetOverlayWindow.xaml`:
```xml
<Window x:Class="Puck.Overlay.PetOverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ShowInTaskbar="False"
        Topmost="True"
        ResizeMode="NoResize"
        SizeToContent="Manual">
  <Canvas x:Name="Root" Background="Transparent" />
</Window>
```

`Puck/Overlay/PetOverlayWindow.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Interop;
using Puck.Avatar;
using Puck.Interop;

namespace Puck.Overlay;

/// 펫이 그려지는 창. 화면 전체가 아니라 펫만 하고, SetWindowPos로
/// 펫을 따라다닌다.
public partial class PetOverlayWindow : Window
{
    private IntPtr _handle;
    private bool _clickThrough = true;

    public PetOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    /// 지금 이 창이 올라가 있는 모니터의 배율 (1.0 = 96 DPI).
    public double DpiScale { get; private set; } = 1.0;

    public bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            if (_clickThrough == value) return;
            _clickThrough = value;
            if (_handle != IntPtr.Zero) WindowStyles.SetClickThrough(_handle, value);
        }
    }

    /// 창을 펫에 맞춰 옮긴다. 좌표는 가상 화면 물리 픽셀이므로
    /// WPF의 Left/Top(DIP)이 아니라 SetWindowPos를 쓴다.
    public void MoveTo(Point petPosition, Rect visualBounds)
    {
        if (_handle == IntPtr.Zero) return;

        var frame = OverlayPositioner.FrameFor(petPosition, visualBounds);
        Win32.SetWindowPos(_handle, Win32.HWND_TOPMOST,
            frame.X, frame.Y, frame.Width, frame.Height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);

        // 창 안의 그리기는 이 프레임의 좌상단을 원점으로 한다.
        OriginInVirtualScreen = new Point(frame.X, frame.Y);
    }

    /// 이 창의 좌상단이 가상 화면 어디에 있는가. SpriteView가 펫의
    /// 절대 좌표를 창 안의 상대 좌표로 바꿀 때 쓴다.
    public Point OriginInVirtualScreen { get; private set; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        _handle = source.Handle;

        WindowStyles.MakeOverlay(_handle);
        WindowStyles.SetClickThrough(_handle, _clickThrough);

        DpiScale = Win32.GetDpiForWindow(_handle) / 96.0;
        source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != Win32.WM_DPICHANGED) return IntPtr.Zero;

        // wParam의 하위 워드가 새 DPI. 창이 다른 배율의 모니터로 넘어갔다.
        DpiScale = (wParam.ToInt32() & 0xFFFF) / 96.0;
        DpiScaleChanged?.Invoke(DpiScale);

        // 크기/위치는 우리가 SetWindowPos로 직접 정하므로, Windows가
        // 제안하는 사각형(lParam)은 쓰지 않고 처리했다고만 알린다.
        handled = true;
        return IntPtr.Zero;
    }

    public event Action<double>? DpiScaleChanged;
}
```

- [ ] **Step 7: 통과 확인**

Run: `dotnet test --filter OverlayPositionerTests`
Expected: PASS (4 tests)

창 자체는 단위 테스트하지 않는다 — HWND가 필요하고, 그건 Task 18에서 실제로 띄워 눈으로 확인한다. `docs/verification.md`에 항목으로 들어간다.

- [ ] **Step 8: 커밋**

```bash
git add pet-app/Puck/Interop pet-app/Puck/Overlay pet-app/PuckTests/Overlay
git commit -m "feat: transparent topmost click-through overlay window"
```

---

## Task 16: 스프라이트 그리기

**Files:**
- Create: `pet-app/Puck/Overlay/SpriteView.cs`
- Modify: `pet-app/Puck/Overlay/PetOverlayWindow.xaml.cs` (SpriteView 호스팅)
- Test: `pet-app/PuckTests/Overlay/SpriteTransformTests.cs`

**원본:** `puck-mac/pet-app/Puck/Overlay/SpriteLayerView.swift`, `CALayer+ImplicitAnimations.swift`, `Puck/Avatar/{FlipAnimation,JumpFlourish,BouncePreset}.swift`

**Interfaces:**
- Consumes: `SpriteAvatar` (Task 8), `PetOverlayWindow` (Task 15)
- Produces:
  - `static class SpriteTransform` — `static (double ScaleX, double ScaleY) For(AvatarFacing facing, bool upsideDown, double bounceScaleY)`
  - `sealed class SpriteView : FrameworkElement` — `SpriteAvatar? Avatar { get; set; }`, `Point OriginInVirtualScreen { get; set; }`, `double DpiScale { get; set; }`, `void Invalidate()`

그리기는 `OnRender` 하나로 끝난다. 좌우 반전은 `ScaleX = -1`, 상하 반전은 `ScaleY = -1`, 스쿼시&스트레치는 `ScaleY`에 곱해진다.

- [ ] **Step 1: 실패하는 테스트 작성**

`PuckTests/Overlay/SpriteTransformTests.cs`:
```csharp
using Puck.Avatar;
using Puck.Overlay;

namespace PuckTests.Overlay;

public class SpriteTransformTests
{
    [Fact]
    public void FacingRightIsTheDrawnOrientation()
    {
        var (sx, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: false, bounceScaleY: 1.0);
        Assert.Equal(1, sx);
        Assert.Equal(1, sy);
    }

    [Fact]
    public void FacingLeftMirrorsHorizontally()
    {
        // 그림은 오른쪽을 보게 그려지고, 반대로 걸을 때 뒤집힌다.
        var (sx, _) = SpriteTransform.For(AvatarFacing.Left, upsideDown: false, bounceScaleY: 1.0);
        Assert.Equal(-1, sx);
    }

    [Fact]
    public void UpsideDownMirrorsVertically()
    {
        var (_, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: true, bounceScaleY: 1.0);
        Assert.Equal(-1, sy);
    }

    [Fact]
    public void BounceMultipliesTheVerticalScale()
    {
        var (_, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: false, bounceScaleY: 1.06);
        Assert.Equal(1.06, sy, precision: 9);
    }

    [Fact]
    public void BounceAndUpsideDownCompose()
    {
        var (_, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: true, bounceScaleY: 1.06);
        Assert.Equal(-1.06, sy, precision: 9);
    }

    [Fact]
    public void ANonPositiveBounceIsClampedSoTheSpriteNeverInverts()
    {
        // 매니페스트의 bounce_intensity가 이상해도 그림이 뒤집히면 안 된다.
        var (_, sy) = SpriteTransform.For(AvatarFacing.Right, upsideDown: false, bounceScaleY: -0.5);
        Assert.True(sy > 0);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter SpriteTransformTests`
Expected: FAIL — `SpriteTransform` 없음

- [ ] **Step 3: `SpriteTransform`과 `SpriteView` 구현**

```csharp
using System.Windows;
using System.Windows.Media;
using Puck.Avatar;

namespace Puck.Overlay;

/// 지금 그려야 할 배율. 좌우 반전, 상하 반전, 스쿼시&스트레치가 여기서 합쳐진다.
public static class SpriteTransform
{
    /// 스쿼시&스트레치가 그림을 뒤집는 일이 없게 하는 하한.
    public const double MinimumBounceScale = 0.05;

    public static (double ScaleX, double ScaleY) For(AvatarFacing facing, bool upsideDown, double bounceScaleY)
    {
        var bounce = Math.Max(MinimumBounceScale, bounceScaleY);
        var scaleX = facing == AvatarFacing.Left ? -1.0 : 1.0;
        var scaleY = upsideDown ? -bounce : bounce;
        return (scaleX, scaleY);
    }
}

/// 스프라이트 하나를 그리는 것 말고는 아무것도 하지 않는다.
public sealed class SpriteView : FrameworkElement
{
    public SpriteAvatar? Avatar { get; set; }

    /// 이 뷰가 올라가 있는 오버레이 창의 좌상단이 가상 화면 어디인가.
    public Point OriginInVirtualScreen { get; set; }

    /// 물리 픽셀 → DIP 환산 배율. 창이 다른 배율의 모니터로 넘어가면 바뀐다.
    public double DpiScale { get; set; } = 1.0;

    public void Invalidate() => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        var avatar = Avatar;
        var image = avatar?.CurrentImage;
        if (avatar is null || image is null) return;

        // 펫의 절대 좌표를 창 안의 좌표로 옮기고, 물리 픽셀을 DIP로 바꾼다.
        var localX = (avatar.Position.X - OriginInVirtualScreen.X) / DpiScale;
        var localY = (avatar.Position.Y - OriginInVirtualScreen.Y) / DpiScale;

        var width = avatar.Size.Width / DpiScale;
        var height = avatar.Size.Height / DpiScale;

        var (scaleX, scaleY) = SpriteTransform.For(avatar.Facing, avatar.UpsideDown, avatar.BounceScaleY);

        // 반전과 스쿼시의 기준점은 접지점(발밑) — 여기를 중심으로 잡지 않으면
        // 뒤집을 때 펫이 옆으로 튀고, 스쿼시할 때 바닥에서 뜬다.
        drawingContext.PushTransform(new TranslateTransform(localX, localY));
        drawingContext.PushTransform(new ScaleTransform(scaleX, scaleY));
        drawingContext.DrawImage(image, new Rect(-width / 2, -height, width, height));
        drawingContext.Pop();
        drawingContext.Pop();
    }
}
```

- [ ] **Step 4: `PetOverlayWindow`에 호스팅**

`PetOverlayWindow.xaml.cs`의 생성자에 추가:

```csharp
    public SpriteView Sprite { get; } = new();

    // InitializeComponent() 직후:
    //   Root.Children.Add(Sprite);
    //   Sprite.Width = double.NaN; Sprite.Height = double.NaN;
```

`MoveTo`가 `OriginInVirtualScreen`을 갱신할 때 스프라이트에도 넘긴다:

```csharp
        OriginInVirtualScreen = new Point(frame.X, frame.Y);
        Sprite.OriginInVirtualScreen = OriginInVirtualScreen;
        Sprite.DpiScale = DpiScale;
        Sprite.Width = frame.Width / DpiScale;
        Sprite.Height = frame.Height / DpiScale;
        Sprite.Invalidate();
```

`DpiScaleChanged`에도 같은 갱신을 붙인다:

```csharp
        DpiScaleChanged += scale =>
        {
            Sprite.DpiScale = scale;
            Sprite.Invalidate();
        };
```

- [ ] **Step 5: 통과 확인**

Run: `dotnet test --filter SpriteTransformTests`
Expected: PASS (6 tests)

- [ ] **Step 6: 커밋**

```bash
git add pet-app/Puck/Overlay pet-app/PuckTests/Overlay/SpriteTransformTests.cs
git commit -m "feat: sprite rendering with flip, upside-down and bounce"
```

---

## Task 17: 클릭 / 드래그 / 던지기

**Files:**
- Create: `pet-app/Puck/Movement/CursorVelocityTracker.cs`
- Create: `pet-app/Puck/Movement/States/ReactClickState.cs`
- Create: `pet-app/Puck/Movement/States/ReactDragState.cs`
- Create: `pet-app/Puck/Overlay/PetGestureRecognizer.cs`
- Modify: `pet-app/Puck/Overlay/PetOverlayWindow.xaml.cs` (히트 테스트로 클릭스루 토글)
- Test: `pet-app/PuckTests/Movement/CursorVelocityTrackerTests.cs`, `pet-app/PuckTests/Overlay/PetGestureRecognizerTests.cs`

**원본:** `puck-mac/pet-app/Puck/Movement/{CursorVelocityTracker}.swift`, `States/{ReactClick,ReactDrag}State.swift`, `Puck/Overlay/PetGestureRecognizer.swift`

**클릭스루의 핵심:** 오버레이 창은 기본적으로 클릭이 통과한다. 마우스가 실제로 **그려진 픽셀 위**에 올 때만 통과를 끈다. 이게 없으면 펫 주위 여백에서 아래 앱을 클릭할 수 없다. `WS_EX_TRANSPARENT`가 켜져 있으면 창은 마우스 이벤트를 아예 못 받으므로, 커서 위치는 오버레이가 아니라 **전역 커서 위치를 프레임마다 폴링해서** 판정한다.

**Interfaces:**
- Consumes: `CharacterBody`, `SpriteAvatar`, `PetOverlayWindow`
- Produces:
  - `sealed class CursorVelocityTracker` — `void Record(Point position, double timestamp)`, `Vector Velocity { get; }`, `void Reset()`
  - `sealed class ReactClickState : IStateHandler` — `Name = "ReactClick"`, `ClipKey = "react_click"`, `RestartsOnReentry = true`, `Duration = 0.4`
  - `sealed class ReactDragState : IStateHandler` — `Name = "ReactDrag"`, `ClipKey = "react_drag"`, `Point? DragPosition { get; set; }`, `Vector ReleaseVelocity { get; }`
  - `sealed class PetGestureRecognizer` — `event Action? Clicked`, `event Action<Point>? Dragged`, `event Action<Vector>? Released`, `void OnMouseDown(Point p, double t)`, `void OnMouseMove(Point p, double t)`, `void OnMouseUp(Point p, double t)`, `const double DragThreshold = 4`

- [ ] **Step 1: 커서 속도 추적의 실패하는 테스트 작성**

`PuckTests/Movement/CursorVelocityTrackerTests.cs`:
```csharp
using System.Windows;
using Puck.Movement;

namespace PuckTests.Movement;

public class CursorVelocityTrackerTests
{
    [Fact]
    public void NoSamplesMeansNoVelocity()
    {
        Assert.Equal(new Vector(0, 0), new CursorVelocityTracker().Velocity);
    }

    [Fact]
    public void ASingleSampleStillMeansNoVelocity()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0);
        Assert.Equal(new Vector(0, 0), tracker.Velocity);
    }

    [Fact]
    public void TwoSamplesGiveThePixelsPerSecondBetweenThem()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0);
        tracker.Record(new Point(100, -50), 0.5);
        Assert.Equal(200, tracker.Velocity.X, precision: 6);
        Assert.Equal(-100, tracker.Velocity.Y, precision: 6);
    }

    [Fact]
    public void VelocityIsSmoothedAcrossTheRecentSamplesNotJustTheLastPair()
    {
        // 마지막 한 쌍만 보면 손을 멈춘 채로 놓는 순간 속도가 0이 되어
        // 세게 휘두른 던지기가 제자리 낙하가 된다.
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0.00);
        tracker.Record(new Point(100, 0), 0.02);
        tracker.Record(new Point(200, 0), 0.04);
        tracker.Record(new Point(200, 0), 0.06);   // 마지막 순간 정지
        Assert.True(tracker.Velocity.X > 1000);
    }

    [Fact]
    public void SamplesOlderThanTheWindowAreForgotten()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0);
        tracker.Record(new Point(10_000, 0), 0.001);   // 아주 빠른 옛날 움직임
        tracker.Record(new Point(10_000, 0), 5.0);     // 5초 뒤
        tracker.Record(new Point(10_010, 0), 5.1);
        Assert.True(tracker.Velocity.X < 200);
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 0);
        tracker.Record(new Point(500, 0), 0.1);
        tracker.Reset();
        Assert.Equal(new Vector(0, 0), tracker.Velocity);
    }

    [Fact]
    public void SamplesAtTheSameTimestampDoNotDivideByZero()
    {
        var tracker = new CursorVelocityTracker();
        tracker.Record(new Point(0, 0), 1.0);
        tracker.Record(new Point(50, 0), 1.0);
        Assert.Equal(new Vector(0, 0), tracker.Velocity);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter CursorVelocityTrackerTests`
Expected: FAIL — `CursorVelocityTracker` 없음

- [ ] **Step 3: `CursorVelocityTracker` 구현**

```csharp
using System.Windows;

namespace Puck.Movement;

/// 던져진 속도를 재는 것. 마지막 한 쌍의 표본만 보면 손을 멈춘 채로
/// 놓는 흔한 동작이 속도 0으로 읽혀, 세게 휘두른 던지기가 제자리
/// 낙하가 된다. 최근 창(window) 전체에 걸쳐 재는 이유가 그것이다.
public sealed class CursorVelocityTracker
{
    /// 이보다 오래된 표본은 버린다. 짧게 잡으면 던지기가 마지막 순간의
    /// 잡음을 따라가고, 길게 잡으면 방향을 바꾼 손짓이 평균으로 상쇄된다.
    public const double WindowSeconds = 0.12;

    private readonly List<(Point Position, double Timestamp)> _samples = [];

    public void Record(Point position, double timestamp)
    {
        _samples.Add((position, timestamp));
        _samples.RemoveAll(s => timestamp - s.Timestamp > WindowSeconds);
    }

    public Vector Velocity
    {
        get
        {
            if (_samples.Count < 2) return new Vector(0, 0);

            var first = _samples[0];
            var last = _samples[^1];
            var elapsed = last.Timestamp - first.Timestamp;
            if (elapsed <= 0) return new Vector(0, 0);

            return new Vector((last.Position.X - first.Position.X) / elapsed,
                              (last.Position.Y - first.Position.Y) / elapsed);
        }
    }

    public void Reset() => _samples.Clear();
}
```

- [ ] **Step 4: 제스처 인식의 실패하는 테스트 작성**

`PuckTests/Overlay/PetGestureRecognizerTests.cs`:
```csharp
using System.Windows;
using Puck.Overlay;

namespace PuckTests.Overlay;

public class PetGestureRecognizerTests
{
    [Fact]
    public void PressAndReleaseWithoutMovingIsAClick()
    {
        var recognizer = new PetGestureRecognizer();
        var clicks = 0;
        var drags = 0;
        recognizer.Clicked += () => clicks++;
        recognizer.Dragged += _ => drags++;

        recognizer.OnMouseDown(new Point(100, 100), 0);
        recognizer.OnMouseUp(new Point(101, 100), 0.1);

        Assert.Equal(1, clicks);
        Assert.Equal(0, drags);
    }

    [Fact]
    public void MovingPastTheThresholdBecomesADrag()
    {
        var recognizer = new PetGestureRecognizer();
        var clicks = 0;
        var positions = new List<Point>();
        recognizer.Clicked += () => clicks++;
        recognizer.Dragged += positions.Add;

        recognizer.OnMouseDown(new Point(100, 100), 0);
        recognizer.OnMouseMove(new Point(120, 100), 0.05);
        recognizer.OnMouseUp(new Point(120, 100), 0.1);

        Assert.Equal(0, clicks);           // 드래그였다면 클릭이 아니다
        Assert.Equal([new Point(120, 100)], positions);
    }

    [Fact]
    public void MovementBelowTheThresholdIsStillAClick()
    {
        // 손 떨림으로 클릭이 사라지면 안 된다.
        var recognizer = new PetGestureRecognizer();
        var clicks = 0;
        recognizer.Clicked += () => clicks++;

        recognizer.OnMouseDown(new Point(100, 100), 0);
        recognizer.OnMouseMove(new Point(102, 101), 0.02);
        recognizer.OnMouseUp(new Point(102, 101), 0.05);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ReleasingADragReportsTheThrowVelocity()
    {
        var recognizer = new PetGestureRecognizer();
        Vector? released = null;
        recognizer.Released += v => released = v;

        recognizer.OnMouseDown(new Point(0, 0), 0.00);
        recognizer.OnMouseMove(new Point(50, 0), 0.02);
        recognizer.OnMouseMove(new Point(100, 0), 0.04);
        recognizer.OnMouseUp(new Point(100, 0), 0.04);

        Assert.NotNull(released);
        Assert.True(released!.Value.X > 1000);
    }

    [Fact]
    public void AClickReleaseReportsNoThrow()
    {
        var recognizer = new PetGestureRecognizer();
        var releases = 0;
        recognizer.Released += _ => releases++;

        recognizer.OnMouseDown(new Point(0, 0), 0);
        recognizer.OnMouseUp(new Point(0, 0), 0.1);

        Assert.Equal(0, releases);
    }

    [Fact]
    public void MoveWithoutAPressIsIgnored()
    {
        var recognizer = new PetGestureRecognizer();
        var drags = 0;
        recognizer.Dragged += _ => drags++;

        recognizer.OnMouseMove(new Point(500, 500), 0);

        Assert.Equal(0, drags);
    }

    [Fact]
    public void ASecondPressStartsAFreshGesture()
    {
        var recognizer = new PetGestureRecognizer();
        var clicks = 0;
        recognizer.Clicked += () => clicks++;

        recognizer.OnMouseDown(new Point(0, 0), 0);
        recognizer.OnMouseMove(new Point(200, 0), 0.05);
        recognizer.OnMouseUp(new Point(200, 0), 0.1);      // 드래그

        recognizer.OnMouseDown(new Point(200, 0), 1.0);
        recognizer.OnMouseUp(new Point(200, 0), 1.1);      // 클릭

        Assert.Equal(1, clicks);
    }
}
```

- [ ] **Step 5: 실패 확인**

Run: `dotnet test --filter PetGestureRecognizerTests`
Expected: FAIL — `PetGestureRecognizer` 없음

- [ ] **Step 6: `PetGestureRecognizer` 구현**

```csharp
using System.Windows;
using Puck.Movement;

namespace Puck.Overlay;

/// 누르고, 끌고, 놓는 것을 클릭 / 드래그 / 던지기로 읽는다.
/// 좌표는 가상 화면 물리 픽셀, timestamp는 초.
public sealed class PetGestureRecognizer
{
    /// 이만큼 움직이기 전까지는 아직 클릭이다. 손 떨림 때문에 클릭이
    /// 사라지면 안 된다.
    public const double DragThreshold = 4;

    private readonly CursorVelocityTracker _velocity = new();

    private Point _origin;
    private bool _pressed;
    private bool _dragging;

    public event Action? Clicked;
    public event Action<Point>? Dragged;
    public event Action<Vector>? Released;

    public void OnMouseDown(Point position, double timestamp)
    {
        _pressed = true;
        _dragging = false;
        _origin = position;
        _velocity.Reset();
        _velocity.Record(position, timestamp);
    }

    public void OnMouseMove(Point position, double timestamp)
    {
        if (!_pressed) return;

        _velocity.Record(position, timestamp);

        if (!_dragging)
        {
            var moved = (position - _origin).Length;
            if (moved < DragThreshold) return;
            _dragging = true;
        }

        Dragged?.Invoke(position);
    }

    public void OnMouseUp(Point position, double timestamp)
    {
        if (!_pressed) return;
        _pressed = false;

        if (!_dragging)
        {
            Clicked?.Invoke();
            return;
        }

        _velocity.Record(position, timestamp);
        Released?.Invoke(_velocity.Velocity);
        _dragging = false;
    }
}
```

- [ ] **Step 7: 두 반응 상태 구현**

`Puck/Movement/States/ReactClickState.cs`:
```csharp
namespace Puck.Movement.States;

/// 눌린 것에 대한 반응. 한 번 재생하고 Idle로 돌아간다.
public sealed class ReactClickState : IStateHandler
{
    public const double Duration = 0.4;

    private double _elapsed;

    public string Name => "ReactClick";
    public string ClipKey => "react_click";

    /// 연달아 누르면 연달아 재생돼야 한다.
    public bool RestartsOnReentry => true;

    public void Enter() => _elapsed = 0;

    public void Update(double dt, StateContext context)
    {
        _elapsed += dt;
        if (_elapsed >= Duration)
            context.RequestTransition(StateKind.Idle);
    }
}
```

`Puck/Movement/States/ReactDragState.cs`:
```csharp
using System.Windows;

namespace Puck.Movement.States;

/// 끌려다니는 동안. 물리는 없다 — 커서를 그대로 따라간다.
/// 놓이는 순간의 속도는 CharacterBody.LaunchVelocity에 실려 FallState로 넘어간다.
public sealed class ReactDragState : IStateHandler
{
    public string Name => "ReactDrag";
    public string ClipKey => "react_drag";
    public bool LoopsClip => true;

    /// 제스처 인식기가 매 이동마다 갱신한다.
    public Point? DragPosition { get; set; }

    public void Enter() => DragPosition = null;

    public void Update(double dt, StateContext context)
    {
        if (DragPosition is not { } position) return;

        // 끌려가는 중에도 화면 밖으로는 못 나간다.
        context.Body.Position = PetBounds.Contain(position, context.VisualBounds, context.RoamableArea);
    }
}
```

- [ ] **Step 8: 오버레이의 클릭스루 토글 연결**

`PetOverlayWindow.xaml.cs`에 추가 — 매 프레임 전역 커서 위치를 물어 그림 위인지 보고 통과 여부를 정한다. `WS_EX_TRANSPARENT`가 켜진 창은 마우스 이벤트를 못 받으므로 창의 이벤트로는 이 판정을 할 수 없다.

`Interop/Win32.cs`에 추가:
```csharp
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    public const int VK_LBUTTON = 0x01;
```

`PetOverlayWindow.xaml.cs`에 추가:
```csharp
    /// 그림 위 몇 px까지를 "펫을 눌렀다"로 칠 것인가. 얇은 꼬리나 귀는
    /// 여유가 없으면 사실상 잡을 수 없다.
    public const double HitTolerance = 6;

    /// 가상 화면 물리 픽셀 기준 현재 커서 위치.
    public static Point CursorPosition
    {
        get
        {
            Win32.GetCursorPos(out var p);
            return new Point(p.X, p.Y);
        }
    }

    public static bool LeftButtonDown => (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;

    /// 커서가 그려진 픽셀 위에 있으면 클릭을 받고, 아니면 통과시킨다.
    public void UpdateClickThrough(SpriteAvatar avatar)
    {
        var cursor = CursorPosition;
        var relative = new Point(cursor.X - avatar.Position.X, cursor.Y - avatar.Position.Y);
        ClickThrough = !avatar.HitTest(relative, HitTolerance);
    }
```

- [ ] **Step 9: 통과 확인**

Run: `dotnet test --filter "CursorVelocityTrackerTests|PetGestureRecognizerTests"`
Expected: PASS (14 tests)

- [ ] **Step 10: 커밋**

```bash
git add pet-app/Puck/Movement pet-app/Puck/Overlay pet-app/Puck/Interop pet-app/PuckTests
git commit -m "feat: click, drag and throw gestures with pixel-accurate click-through"
```

---

## Task 18: 트레이 아이콘과 조립

**Files:**
- Create: `pet-app/Puck/Localization/Strings.cs`
- Create: `pet-app/Puck/App/TrayIcon.cs`
- Create: `pet-app/Puck/App/PetBootstrap.cs`
- Modify: `pet-app/Puck/App.xaml.cs`
- Create: `pet-app/Puck/Resources/puck.ico`
- Create: `docs/verification.md`
- Test: `pet-app/PuckTests/Localization/StringsTests.cs`

**원본:** `puck-mac/pet-app/Puck/App/{MenuBarController,PuckApp,AppDelegate}.swift`, `Puck/Localization/`

마지막 태스크. 여기까지 오면 `dotnet run`으로 펫이 화면에 뜬다.

**Interfaces:**
- Consumes: 앞의 모든 태스크
- Produces:
  - `static class Strings` — `static string Get(string key)`, 인덱서 `Strings.TrayShowPet` 등의 명명 속성
  - `sealed class TrayIcon : IDisposable` — 생성자 `(Action onToggleVisible, Action onOpenCustomisationFolder, Action onReloadAvatar, Action onQuit)`
  - `sealed class PetBootstrap : IDisposable` — `void Start()`, `void ReloadAvatar()`

- [ ] **Step 1: `Strings`의 실패하는 테스트 작성**

`PuckTests/Localization/StringsTests.cs`:
```csharp
using Puck.Localization;

namespace PuckTests.Localization;

public class StringsTests
{
    [Fact]
    public void KnownKeysResolveToKorean()
    {
        Assert.Equal("펫 보이기/숨기기", Strings.TrayToggleVisible);
        Assert.Equal("커스터마이징 폴더 열기", Strings.TrayOpenCustomisationFolder);
        Assert.Equal("아바타 다시 불러오기", Strings.TrayReloadAvatar);
        Assert.Equal("종료", Strings.TrayQuit);
    }

    [Fact]
    public void AnUnknownKeyReturnsTheKeyItselfRatherThanThrowing()
    {
        // 문자열 하나가 빠졌다고 UI가 죽으면 안 된다 — 키가 그대로 보이면
        // 무엇이 빠졌는지도 알 수 있다.
        Assert.Equal("no.such.key", Strings.Get("no.such.key"));
    }

    [Fact]
    public void EveryNamedPropertyHasAnEntry()
    {
        // 명명 속성이 늘어나는데 테이블에 넣는 걸 잊는 게 이 클래스의
        // 유일한 실패 방식이다.
        foreach (var property in typeof(Strings).GetProperties())
        {
            if (property.PropertyType != typeof(string)) continue;
            var value = (string)property.GetValue(null)!;
            Assert.False(value.Contains('.') && value == value.ToLowerInvariant(),
                $"{property.Name}이 테이블에 없습니다");
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter StringsTests`
Expected: FAIL — `Strings` 없음

- [ ] **Step 3: `Strings` 구현**

```csharp
namespace Puck.Localization;

/// UI 문자열. 하드코딩된 사용자 노출 문자열은 없다 — 전부 여기를 거친다.
/// Phase 1이 쓰는 것만 있고, 이후 Phase가 자기 몫을 더한다.
public static class Strings
{
    private static readonly Dictionary<string, string> Table = new(StringComparer.Ordinal)
    {
        ["tray.toggleVisible"] = "펫 보이기/숨기기",
        ["tray.openCustomisationFolder"] = "커스터마이징 폴더 열기",
        ["tray.reloadAvatar"] = "아바타 다시 불러오기",
        ["tray.quit"] = "종료",
        ["avatar.loadFailed"] = "아바타를 불러오지 못했습니다",
        ["avatar.noneInstalled"] = "설치된 아바타가 없습니다",
    };

    /// 없는 키는 키 자체를 돌려준다 — 문자열 하나가 빠졌다고 UI가
    /// 죽지 않고, 무엇이 빠졌는지도 화면에 보인다.
    public static string Get(string key) => Table.TryGetValue(key, out var value) ? value : key;

    public static string TrayToggleVisible => Get("tray.toggleVisible");
    public static string TrayOpenCustomisationFolder => Get("tray.openCustomisationFolder");
    public static string TrayReloadAvatar => Get("tray.reloadAvatar");
    public static string TrayQuit => Get("tray.quit");
    public static string AvatarLoadFailed => Get("avatar.loadFailed");
    public static string AvatarNoneInstalled => Get("avatar.noneInstalled");
}
```

- [ ] **Step 4: `TrayIcon` 구현**

```csharp
using System.Drawing;
using System.Windows.Forms;
using Puck.Localization;

namespace Puck.App;

/// mac의 메뉴막대 항목에 해당하는 것. 창이 하나도 없어도 앱이
/// 살아 있다는 걸 보여 주는 유일한 표시이기도 하다.
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(Action onToggleVisible, Action onOpenCustomisationFolder,
                    Action onReloadAvatar, Action onQuit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.TrayToggleVisible, null, (_, _) => onToggleVisible());
        menu.Items.Add(Strings.TrayOpenCustomisationFolder, null, (_, _) => onOpenCustomisationFolder());
        menu.Items.Add(Strings.TrayReloadAvatar, null, (_, _) => onReloadAvatar());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TrayQuit, null, (_, _) => onQuit());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Puck",
            Visible = true,
            ContextMenuStrip = menu,
        };
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "puck.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
```

- [ ] **Step 5: `PetBootstrap` 구현**

```csharp
using System.Diagnostics;
using System.Windows;
using Puck.Avatar;
using Puck.Diagnostics;
using Puck.Localization;
using Puck.Movement;
using Puck.Movement.States;
using Puck.Overlay;
using Puck.Settings;

namespace Puck.App;

/// 전부를 엮는 한 곳. 여기서 아바타를 고르고, 창을 띄우고, 프레임
/// 루프를 돌리고, 제스처를 상태 전이로 옮긴다.
public sealed class PetBootstrap : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly CompositionFrameClock _clock = new();
    private readonly PetGestureRecognizer _gestures = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private PetOverlayWindow? _window;
    private SpriteAvatar? _avatar;
    private CharacterBody? _body;
    private CharacterController? _controller;
    private ScreenSpace? _screens;
    private ReactDragState? _drag;
    private TrayIcon? _tray;
    private bool _wasPressed;

    public PetBootstrap(SettingsStore settings) => _settings = settings;

    public void Start()
    {
        PuckPaths.EnsureCreated();
        AppLogger.Configure(new JsonLinesFileAppender(PuckPaths.Logs));

        _tray = new TrayIcon(
            onToggleVisible: ToggleVisible,
            onOpenCustomisationFolder: OpenCustomisationFolder,
            onReloadAvatar: ReloadAvatar,
            onQuit: () => Application.Current.Shutdown());

        _window = new PetOverlayWindow();
        _window.Show();

        ReloadAvatar();

        _gestures.Clicked += () => _controller?.Request(StateKind.ReactClick);
        _gestures.Dragged += position =>
        {
            if (_drag is null) return;
            _drag.DragPosition = position;
            _controller?.Request(StateKind.ReactDrag);
        };
        _gestures.Released += velocity =>
        {
            if (_body is null) return;
            _body.LaunchVelocity = velocity;
            _controller?.Request(StateKind.Fall);
        };

        _clock.Tick += OnFrame;
        _clock.Start();
    }

    /// 설정에 저장된 아바타를, 없으면 첫 번째로 찾은 것을 불러온다.
    /// 다시 부르면 디스크에 있는 것으로 살아 있는 펫을 다시 만든다 —
    /// 그림을 고쳐 그리거나 매니페스트를 수정한 걸 앱을 끄지 않고
    /// 보는 방법이 이것이다.
    public void ReloadAvatar()
    {
        var catalogue = AvatarCatalogue.Scan(PuckPaths.Avatars);
        var entry = catalogue.FirstOrDefault(e => e.Name == _settings.AvatarName)
                    ?? catalogue.FirstOrDefault()
                    ?? BundledAvatar();

        if (entry is null)
        {
            AppLogger.Error("avatar", Strings.AvatarNoneInstalled);
            return;
        }

        SpriteAvatar avatar;
        try
        {
            avatar = SpriteAvatar.Load(entry.Directory);
        }
        catch (AvatarLoaderException ex)
        {
            AppLogger.Error("avatar", Strings.AvatarLoadFailed,
                new Dictionary<string, object?> { ["name"] = entry.Name, ["reason"] = ex.Message });
            return;
        }

        _screens ??= ScreenSpace.Current();
        if (_screens is null) return;

        var start = _body?.Position
                    ?? new Point(_screens.RoamableArea.Left + _screens.RoamableArea.Width / 2,
                                 _screens.RoamableArea.Bottom);

        _avatar = avatar;
        _body = new CharacterBody(avatar, start,
            bounceIntensity: avatar.BounceIntensityOrDefault);
        _drag = new ReactDragState();

        var states = new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = new IdleState(new WanderScheduler()),
            [StateKind.Walk] = new WalkState(),
            [StateKind.Fall] = new FallState(),
            [StateKind.Land] = new LandState(),
            [StateKind.ReactClick] = new ReactClickState(),
            [StateKind.ReactDrag] = _drag,
        };

        _controller = new CharacterController(_body, states, StateKind.Idle, MakeContext);
        _window!.Sprite.Avatar = avatar;
    }

    private StateContext MakeContext() => new()
    {
        Body = _body!,
        RoamableArea = _screens!.RoamableArea,
        AvatarHeight = _avatar!.Size.Height,
        VisualBounds = _body!.VisualBounds,
        WalkSpeed = MovementSolver.WalkSpeed * _settings.MovementSpeedMultiplier,
        LandingY = _screens.FloorY,
        RequestTransition = _ => { },   // CharacterController가 자기 것으로 갈아 끼운다
    };

    private void OnFrame(double dt)
    {
        if (_window is null || _avatar is null || _body is null || _controller is null) return;

        // 디스플레이 구성이 바뀌었을 수 있다. 목록이 비면(전부 잠듦)
        // 마지막으로 알던 것을 그대로 쓴다.
        _screens = ScreenSpace.Current() ?? _screens;

        PollMouse();
        _controller.Advance(dt);
        _body.UpdateBounce(_avatar.CurrentClipKey, _stopwatch.Elapsed);

        _window.MoveTo(_body.Position, _body.VisualBounds);
        _window.UpdateClickThrough(_avatar);
    }

    /// 오버레이는 대부분의 시간 WS_EX_TRANSPARENT 상태라 마우스 이벤트를
    /// 받지 못한다. 그래서 커서와 버튼 상태를 프레임마다 직접 묻는다.
    private void PollMouse()
    {
        var cursor = PetOverlayWindow.CursorPosition;
        var pressed = PetOverlayWindow.LeftButtonDown;
        var now = _stopwatch.Elapsed.TotalSeconds;

        if (pressed && !_wasPressed)
        {
            // 그림 위를 눌렀을 때만 제스처가 시작된다.
            var relative = new Point(cursor.X - _avatar!.Position.X, cursor.Y - _avatar.Position.Y);
            if (_avatar.HitTest(relative, PetOverlayWindow.HitTolerance))
                _gestures.OnMouseDown(cursor, now);
        }
        else if (pressed)
        {
            _gestures.OnMouseMove(cursor, now);
        }
        else if (_wasPressed)
        {
            _gestures.OnMouseUp(cursor, now);
        }

        _wasPressed = pressed;
    }

    private void ToggleVisible()
    {
        if (_window is null) return;
        if (_window.IsVisible) _window.Hide(); else _window.Show();
    }

    private static void OpenCustomisationFolder()
    {
        PuckPaths.EnsureCreated();
        Process.Start(new ProcessStartInfo(PuckPaths.Root) { UseShellExecute = true });
    }

    private static AvatarEntry? BundledAvatar()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Resources", "Avatars", "dummy");
        return Directory.Exists(directory) ? new AvatarEntry("dummy", directory) : null;
    }

    public void Dispose()
    {
        _clock.Stop();
        _tray?.Dispose();
        _window?.Close();
    }
}
```

- [ ] **Step 6: `SpriteAvatar`에 보조 속성 두 개 추가**

`PetBootstrap`이 쓰는 것들이다. `Puck/Avatar/SpriteAvatar.cs`에 추가:

```csharp
    /// 매니페스트에 bounce_intensity가 있으면 그 값, 없으면 앱의 기본값.
    public double BounceIntensityOrDefault =>
        _load.Manifest.BounceIntensity ?? Puck.Movement.CharacterBody.DefaultBounceIntensity;

    /// 지금 재생 중인 클립 키. UpdateBounce에 넘길 값이다.
    public string CurrentClipKey => _currentClip;
```

- [ ] **Step 7: `App.xaml.cs` 연결**

```csharp
using System.Windows;
using Puck.App;
using Puck.Diagnostics;
using Puck.Settings;

namespace Puck;

public partial class App : Application
{
    private PetBootstrap? _bootstrap;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = SettingsStore.Load(PuckPaths.SettingsFile);
        _bootstrap = new PetBootstrap(settings);
        _bootstrap.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrap?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 8: 아이콘 준비**

`Puck/Resources/puck.ico`를 만든다. puck-mac의 `pet-app/design/app-icon-source.png`를 원본으로 16/32/48/256px를 담은 ICO로 변환한다:

```powershell
# ImageMagick이 있는 경우
magick ..\..\puck-mac\pet-app\design\app-icon-source.png -define icon:auto-resize=256,48,32,16 Resources\puck.ico
```

없으면 `SystemIcons.Application` 폴백이 동작하므로 Phase 1을 막지 않는다. `Puck.csproj`의 `Content Include="Resources\**\*"` 규칙이 이미 출력 폴더로 복사한다.

- [ ] **Step 9: 전체 테스트 통과 확인**

Run: `pwsh pet-app/scripts/test.ps1`
Expected: 실패 0. Task 1~18의 테스트 전부 통과.

- [ ] **Step 10: 실제로 띄워서 확인**

Run: `dotnet run --project pet-app/Puck/Puck.csproj`

눈으로 확인할 것:
1. 트레이에 아이콘이 뜬다.
2. 펫이 화면 아래쪽에 서 있고, 배경이 투명하다 (사각형 상자가 보이지 않는다).
3. 몇 초 기다리면 스스로 걷기 시작하고, 방향에 맞게 좌우로 뒤집힌다.
4. 화면 가장자리에서 그림이 잘리지 않고 멈춘다.
5. 펫의 **그림 위**를 클릭하면 반응하고, 펫 **주위 여백**을 클릭하면 아래 창이 클릭된다.
6. 펫을 끌어서 화면 중앙에 놓으면 떨어져 바닥에서 튕긴다.
7. 세게 던지면 옆벽에서 튕긴다.
8. 모니터가 둘이면 다른 모니터로 걸어갈 수 있고, 배율이 다른 모니터에서도 크기가 맞다.
9. 트레이 → 종료로 프로세스가 완전히 끝난다 (작업 관리자에 `Puck.exe`가 남지 않는다).

- [ ] **Step 11: `docs/verification.md` 작성**

```markdown
# Puck for Windows — 수동 검증

자동화하기 어렵거나 비싼 것들. 릴리스 전에 한 번씩 손으로 확인한다.

## Phase 1 — 펫

- [ ] 트레이 아이콘이 뜨고, 메뉴 네 항목이 전부 동작한다.
- [ ] 오버레이 배경이 투명하다 (사각형 상자가 보이지 않는다).
- [ ] 펫이 스스로 걷고, 진행 방향에 맞게 좌우로 뒤집힌다.
- [ ] 그림이 화면 가장자리에서 잘리지 않고 멈춘다.
- [ ] 그림 위 클릭은 펫이 받고, 주위 여백 클릭은 아래 창으로 통과한다.
- [ ] 드래그로 들어 올렸다 놓으면 떨어지고 바닥에서 튕긴다.
- [ ] 세게 던지면 옆벽에서 튕기고, 화면 밖으로 나가지 않는다.
- [ ] 전체화면 앱(게임, 프레젠테이션) 위에서의 동작을 확인한다.
- [ ] 배율이 다른 두 모니터를 오갈 때 펫 크기가 각 모니터에서 맞다.
- [ ] 모니터를 뽑았다 꽂아도 펫이 화면 밖에 남지 않는다.
- [ ] 절전에서 복귀했을 때 펫이 한 프레임에 순간이동하지 않는다.
- [ ] 화면 잠금 후 복귀했을 때 오버레이가 여전히 최상단이다.
- [ ] 작업표시줄 자동 숨김을 켜고 껐을 때 착지 높이가 따라온다.
- [ ] `%LOCALAPPDATA%\Puck\Avatars\`에 mac에서 만든 아바타 폴더를 넣고
      "아바타 다시 불러오기"를 누르면 앱을 끄지 않고 바뀐다.
- [ ] 매니페스트가 깨진 아바타가 섞여 있어도 나머지가 보이고, 이유가
      `logs\*.jsonl`에 남는다.
- [ ] 트레이 → 종료 후 작업 관리자에 `Puck.exe`가 남지 않는다.
```

- [ ] **Step 12: 커밋**

```bash
git add pet-app docs/verification.md
git commit -m "feat: tray icon, bootstrap wiring — the pet walks"
```

---

# Phase 1 완료 조건

- `pwsh pet-app/scripts/test.ps1`이 실패 0으로 끝난다.
- CI가 초록이다.
- `docs/verification.md`의 Phase 1 항목을 전부 손으로 확인했다.
- puck-mac에서 만든 아바타 폴더가 그대로 로드된다.

# 다음 플랜

Phase 2(감각 기관 — 창 열거, UI Automation, 전역 핫키, 화면 캡처, 합성 클릭, 효과음)가 다음 플랜을 받는다. 그때 이 플랜이 남겨 둔 구멍들이 채워진다:

- `ScreenSpace.FloorY`에 창 윗면이 착지면으로 끼어든다 (`LandingSurfaceResolver`). `StateContext.LandingY`가 클로저인 덕분에 상태 코드는 바뀌지 않는다.
- `IdleState`에 `IdleWanderDelegate`가 붙어, 어디로 걸어갈지를 창 목록을 아는 쪽이 정한다.
- `IdleState`가 "발밑이 사라졌다"와 "발밑이 창 뒤로 갔다"를 구분한다.
- `StateKind`에 Climb, Ceiling, WalkOnTop, MoveTo, Travel, Point, Type, Listen, Spin, Petting, Pinned, 공놀이 셋이 더해진다.
- 마우스 폴링(`PetBootstrap.PollMouse`)이 저수준 마우스 훅으로 바뀌어, 프레임 사이에 일어난 클릭도 놓치지 않는다.
