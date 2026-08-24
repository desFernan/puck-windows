# Puck for Windows

> A Windows port of [**desFernan/puck-mac**](https://github.com/desFernan/puck-mac)
> (Swift/AppKit, macOS). C# / .NET 8 + WPF.
>
> Platforms: [macOS](https://github.com/desFernan/puck-mac) · **Windows** (here) · [Linux](https://github.com/desFernan/puck-linux)

### 💬 [Join the Discord](https://discord.gg/ePBZVnwSYE)

Bugs, feature requests, build help, or just want to hang out — the
[support server](https://discord.gg/ePBZVnwSYE) is the fastest way to reach
us. Come say hi!

**Current status: Phase 0 + Phase 1 done — the pet walks the screen.**
A transparent, always-on-top, click-through overlay shows the avatar; physics
and the state machine are running; click, drag and throw work; it lands on
the taskbar. It reads avatar folders made on macOS as-is. The agent, chat
window, editor and terminal are Phase 2–6.

## Build & run

```powershell
pet-app\scripts\build.ps1                     # Release build
pet-app\scripts\test.ps1                      # xUnit, unattended
dotnet run --project pet-app\Puck\Puck.csproj # launch the pet
```

Needs the .NET 8 SDK (or newer, targeting `net8.0-windows`).

## Avatars

User data lives under `%LOCALAPPDATA%\Puck\`.

```
%LOCALAPPDATA%\Puck\
  Avatars\<name>\manifest.json   one folder = one character
  logs\puck-YYYY-MM-DD.jsonl     one line = one event
  settings.json
```

If `Avatars\` is empty, the bundled `dummy` avatar is used. Drop in or edit a
folder, then press "Reload avatars" in the tray menu — no restart needed.
The package format (`schema_version: 1`) is defined by puck-mac; Windows only
reads it.

## Docs

- [`docs/porting-design.md`](docs/porting-design.md) — stack choice,
  module-by-module port map, phase plan, the four places Windows
  deliberately differs
- [`docs/plans/`](docs/plans) — per-phase implementation plans
- [`docs/decisions.md`](docs/decisions.md) — where the build diverged from
  the plan
- [`docs/verification.md`](docs/verification.md) — manual checks that aren't
  automated

## Community

Want to help plan the Windows port, or just curious about progress — join us
on **[Discord](https://discord.gg/ePBZVnwSYE)**.
