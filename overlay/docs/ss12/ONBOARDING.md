# Host a 3D version of your own SS14 server

*[Русская версия](ONBOARDING.ru.md)*

SS12 is SS13 in 3D, using SS14: a 3D view for Space Station 14 (third-person and first-person). Gameplay, rules, roles and balance are
whatever your codebase already has: the 3D view is an extra client renderer plus a small server-side camera
component. You do **not** have to change the engine (`RobustToolbox/` is never touched), so players join with the
normal launcher and download the 3D client from your server like any other content build.

The installer in `Tools/ss12/` does the conversion for you. Point it at the folder of your server codebase
(upstream or any fork) and it adds the 3D files, makes the handful of small edits it needs, and tells you exactly
what it did.

## Requirements

* The .NET SDK your codebase already requires (see its `global.json`) and git.
* **A codebase on RobustToolbox 286 or newer** (check `RobustToolbox/MSBuild/Robust.Engine.Version.props`). Mouse-look
  needs the engine's relative mouse mode and the FOV hiding needs the viewport's FOV render target, which older engines
  do not have. The installer refuses older ones with that explanation. In practice this means codebases that track
  current upstream; see [COMPAT.md](COMPAT.md) for what was tried, including the popular forks that are not there yet.

## Five-minute path

```
# 1. get this repository next to your server codebase
git clone <url of the SS12 repository> ss12

# 2. convert your codebase (creates a git branch named 3d and one commit; nothing is pushed)
ss12/Tools/ss12/ss12.sh install /path/to/your/server/codebase            # Linux, macOS, Git Bash
ss12\Tools\ss12\ss12.ps1 install C:\path\to\your\server\codebase         # Windows PowerShell

# 3. look at what it did, and check it
cd /path/to/your/server/codebase
git show --stat HEAD
ss12/Tools/ss12/ss12.sh doctor /path/to/your/server/codebase --build

# 4. package a server build like you normally do, or use the shortcut
ss12/Tools/ss12/ss12.sh package /path/to/your/server/codebase

# 5. host it, with the preset applied (see "Server configuration")
```

Add `--enforce` to step 2 to make every living player use 3D; without it players choose with `F12`.
Add `--dry-run` to see what would change without changing anything.

## What the installer changes

**New files** (about 45): the 3D renderer and camera (`Content.Client/Render3D`, `Content.Shared/Render3D`,
`Content.Server/Render3D`), its shaders and prototypes, its settings (`CCVars.Render3D.cs`), tests and docs. Nothing
here replaces a file that exists in your codebase.

**Edits to existing files** (9 files, a few lines each). Each edit is a one-line call into the new code, written with
fully qualified names so no `using` lines change:

| Edit | File(s) | Severity | Without it |
|------|---------|----------|------------|
| `pointer-aim` | any `Content.Client` file that aims with the mouse (gun, melee, drag-and-drop, target outline) | required | Guns, melee and drag-and-drop do nothing while the 3D mouse is captured. |
| `pick-merge` | `Gameplay/GameplayStateBase.cs` | required | Nothing can be clicked, examined or used in 3D. |
| `drag-drop` | `Interaction/DragDropSystem.cs` | optional | Drag-and-drop does not start with a captured mouse (hold Alt to drag). |
| `popups` | `Popups/PopupOverlay.cs` | optional | Popup text appears in the wrong place. |
| `map-text` | `MapText/MapTextOverlay.cs` | optional | Mapper area labels pile up in 3D. |
| `sprite-fade` | `Sprite/SpriteFadeSystem.cs` | optional | Things between the camera and the cursor fade out. |
| `health-bars` | `Overlays/EntityHealthBarOverlay.cs` | optional | No health bars in 3D. |

The edits are found by what the code looks like, not by line numbers, and re-running is safe. If a **required** edit
cannot be found (a fork rewrote that code) the installer stops and changes nothing; it prints the exact change to make
by hand. An **optional** edit that cannot be found is skipped with a warning that says what you lose. Their
definitions are in `Tools/ss12/BuiltIn.cs`.

The installer also writes `Resources/ConfigPresets/Build/render3d.toml` and `.ss12/manifest.json` (what it
installed, with hashes, so updating and removing are exact).

## Server configuration

Apply the preset `Resources/ConfigPresets/Build/render3d.toml` (for example copy its sections into your
`server_config.toml`), or set the cvars by hand:

| CVar | Value | Why |
|------|-------|-----|
| `render3d.enforced` | `true` / `false` | Force 3D for living players. Ghosts, active admins and the lobby can always use 2D. |
| `physics.relative_movement` | `true` | WASD is relative to where the camera looks. |
| `shuttle.camera_rotation_locked` | `true` | The camera-rotate keys make no sense in 3D. |

## What players need

Nothing. They join with the stock launcher; the 3D client is part of your server's client build. Their controls and
settings are in `docs/ss12/README.md` (look with the mouse, `F12` toggles 3D/2D when allowed, `N` first/third person,
`Alt` frees the cursor, `F11` opens the 3D settings window).

## Updating and removing

```
ss12.sh update    /path/to/your/server/codebase     # after pulling a newer SS12
ss12.sh uninstall /path/to/your/server/codebase     # restores your original files exactly
ss12.sh doctor    /path/to/your/server/codebase     # checks files, edits, config; --build also compiles
```

## When something does not fit

* **Engine version.** Older than 286 is refused (see Requirements). Newer than the tested version (291) only gets a
  warning; if the build then fails, `ss12 doctor <path> --build --report` tells you what differs.
* **A required edit is missing.** Your fork changed that code. The installer tells you the file and the change; make
  it by hand (it is one call) and run the installer again, or open an issue with the report below.
* **The build fails after installing.** Run `ss12 doctor <path> --build --report`. It writes `ss12-report.txt`
  with the versions, which edits applied and the compiler errors, with hints for known engine API differences.
  Attach that file when asking for help.
* **Merge conflicts later.** Only 9 files of yours contain a 3D edit. When you merge upstream changes and one of those
  conflicts, keep both sides (the edit is a single added call), then run `ss12 doctor`.

## Letting an AI assistant adapt it (MCP)

For a heavily customised codebase, `ss12 mcp` starts a [Model Context Protocol](https://modelcontextprotocol.io) server
over stdin/stdout. An MCP-capable assistant (Claude Desktop, Claude Code, Cursor, VS Code...) can then inspect the
codebase, read exactly what each edit must achieve (`explain_edit`), find the right place in unfamiliar code
(`find_edit_location`), apply the edit by hand, and run `install_3d`, `check_install` and `build_codebase`.
Installing, updating and removing are dry runs unless the assistant passes `dry_run=false`, which the guide
(`ss12://guide`) tells it to do only after you agreed. Config for most clients:

```json
{ "mcpServers": { "ss12": { "command": "dotnet", "args": ["Tools/ss12/bin/Release/net10.0/ss12.dll", "mcp"] } } }
```

Claude Code: `claude mcp add ss12 -- dotnet Tools/ss12/bin/Release/net10.0/ss12.dll mcp`.

## FAQ

**Does it change gameplay?** No. Mobs, items, jobs, combat, atmos, power are the same; only how the world is drawn
and which direction WASD moves (camera-relative) change.

**Can some players stay on 2D?** Yes, unless you enforce 3D. Ghosts and admins can always switch.

**Does it need a powerful GPU?** No. A Radeon RX 580 runs it at 200+ FPS at 1080p. Every effect can be turned off in the
3D settings window for older machines.

**Does it work with my map and content?** Maps and prototypes are used as they are. Entities the renderer does not know
are drawn automatically from their components and draw depth; special cases are listed in
`Resources/Prototypes/Render3D/rules.yml`, which you can extend for your own prototypes (it also sets how thick things
are drawn and whether they lean, see "Shape of things" in the README).

**Is anything sent anywhere?** No. The installer never contacts a remote and never pushes.
