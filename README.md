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

A Windows desktop pet that is also an AI agent. One .NET 8 app:

- **The pet** — a transparent, always-on-top, click-through character that
  walks your screen, climbs a window or the screen's own edge all the way to
  the ceiling and crawls it upside down, lands on the taskbar, and can be
  clicked, dragged and thrown.
- **Its chat window** — tray → **대화 열기**. It drives Windows through nine
  tools: window listing, frontmost window, UI element search, pointing,
  clicking, screen capture, app launch, `run_shell` and `run_powershell`.
  Anything that changes something asks before it runs.

The tray also holds **설정…** (avatar, movement speed, theme,
launch-at-login), **커스터마이징 폴더 열기** and **아바타 다시 불러오기**. The
agent core (chat, tools, approvals) lives in `pet-app/Puck/Agent`.

Not ported yet: the code editor, terminal pane and workspaces that live in
puck-mac's separate `PuckClient` app.

## Build

```powershell
pet-app\scripts\build.ps1                     # Release build
dotnet run --project pet-app\Puck\Puck.csproj # launch the pet
```

Needs the .NET 8 SDK (or newer, targeting `net8.0-windows`).

## Test

```powershell
pet-app\scripts\test.ps1   # xUnit
```

Unattended, exits nonzero on any failure.

## Agent providers

Anthropic, via the official C# SDK (macOS calls the HTTP API by hand because
there is no official Swift SDK). Put your key in `%LOCALAPPDATA%\Puck\.env`:

```
ANTHROPIC_API_KEY=sk-ant-...
PUCK_MODEL=claude-opus-5      # optional
AGENT_PERMISSIONS=tools       # tools | edits | all | auto
```

`auto` is the one mode that stops asking: Puck's own approval-gated tools —
a shell command, a click on somebody else's window — run straight away instead
of putting a prompt in the chat. The other three decide only what a coding CLI
may do on its own and leave that gate exactly where it is. Anything absent or
unrecognised falls back to `tools`, the narrowest.

Environment variables win over the file, and the file is re-read per request —
adding a key does not need a restart. macOS's `run_applescript` is
`run_powershell` here; the ACP-backed code editor is not ported yet.

## Making it your own

Everything you can swap lives in one folder — the same package format as
macOS, just a different root:

```
%LOCALAPPDATA%\Puck\
    Avatars\<name>\                  one folder per character
    settings.json
    .env
    logs\puck-YYYY-MM-DD.jsonl
```

The tray's **커스터마이징 폴더 열기** opens it, and creates the folders if they
are not there yet.

### A character

An avatar is a folder with a `manifest.json` and one PNG per clip beside it:

```
Avatars\my-pet\
    manifest.json
    idle.png  walk.png  fall.png  …
    sounds\*.wav
```

#### Adding one, start to finish

1. **Open the folder.** Tray → **커스터마이징 폴더 열기**. It creates `Avatars\`
   if it is not there yet, so this also tells you the folder exists.
2. **Make a folder for your character** inside `Avatars\`. Its name is the name
   the picker shows: `Avatars\my-pet\` appears as `my-pet`.
3. **Drop in one PNG and a `manifest.json`.** One drawing is a working
   character — `idle` is the only clip that has to exist and every other state
   falls back to it, so you can start with a single picture and add walking,
   climbing and the rest whenever you feel like it. Transparent background,
   drawn facing right (the pet is mirrored when it walks the other way).
   The smallest manifest that works:

   ```json
   {
     "schema_version": 1,
     "name": "my-pet",
     "type": "sprites",
     "hitbox": { "width": 130, "height": 133 },
     "clips": { "idle": "idle" }
   }
   ```

   `hitbox` is the size it will be drawn and clicked at — match your drawing's
   proportions or it will look squashed.
4. **Load it.** Tray → **아바타 다시 불러오기**, then pick it under **설정…**.
   No restart: the reload rebuilds the running pet from what is on disk, which
   is also how you see a redrawn sprite or an edited manifest without quitting.

If something is wrong with the package the pet does not change and the reason
is in the log (`%LOCALAPPDATA%\Puck\logs\`) — a missing `idle` file, a manifest
that will not parse, or a `schema_version` this build does not know.

The package format (`schema_version: 1`) is defined by puck-mac and read here
as-is, so an avatar folder built on macOS drops in unchanged. The full field
reference — `clips`, `emotions`, `sounds`, `hitbox`, `bounce_intensity` and
what each one defaults to — lives in
[puck-mac's README](https://github.com/desFernan/puck-mac#a-character) and
applies here without changes.

## Community

Questions, bug reports, feature ideas, or just want to show off your custom
avatar — join us on **[Discord](https://discord.gg/ePBZVnwSYE)**.
