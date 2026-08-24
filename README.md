# Puck for Windows

> Language: **English** (here) · [한국어](README.ko.md)

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

## Build

```powershell
pet-app\scripts\build.ps1                     # Release build
dotnet run --project pet-app\Puck\Puck.csproj # launch the pet
```

Needs the .NET 8 SDK (or newer, targeting `net8.0-windows`).

## Test

```powershell
pet-app\scripts\test.ps1   # xUnit, unattended
```

Unattended, exits nonzero on any failure.

## Agent providers

Not built yet on Windows — chat, tool execution and the ACP-backed code
editor are Phase 2+. On macOS these talk to the Anthropic or OpenAI API
directly, with `code_editor` running a vendored ACP agent under `node`;
Windows is expected to work the same way once built.

## Making it your own

Everything you can swap lives in one folder — same package format as
macOS, just a different root:

```
%LOCALAPPDATA%\Puck\
    Avatars\<name>\     one folder per character
    Tank\seabed.png     the picture the island is filled with
    logs\puck-YYYY-MM-DD.jsonl
    settings.json
```

These folders are created automatically on first launch.

### The tank

Drop a `seabed.png` into `Tank\` and it replaces the one the app ships. It is
read once at launch, so restart the pet after changing it — same behavior as
macOS, since the rendering rules for the island are unchanged in the port.

### A character

An avatar is a folder with a `manifest.json` and one PNG per clip beside it:

```
Avatars\my-pet\
    manifest.json
    idle.png  walk.png  fall.png  …
    sounds\*.wav
```

The package format (`schema_version: 1`) is defined by puck-mac and read
as-is on Windows — an avatar folder you built on macOS drops in unchanged.
The full field reference (`clips`, `emotions`, `sounds`, `hitbox`,
`bounce_intensity`, the minimal working manifest) lives in
[puck-mac's README](https://github.com/desFernan/puck-mac#a-character); it
applies here without changes.

**Loading a new or edited avatar on Windows:** drop the folder into
`Avatars\`, then use "Reload avatars" in the tray menu — no restart needed,
same as macOS's reload button.

## Community

Want to help plan the Windows port, or just curious about progress — join us
on **[Discord](https://discord.gg/ePBZVnwSYE)**.
