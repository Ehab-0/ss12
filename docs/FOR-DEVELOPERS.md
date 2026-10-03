# For developers

*[Русская версия](ru/FOR-DEVELOPERS.md)*

This page is for people who want to read or change the code. Everyone else: see the [README](../README.md).

## The idea in one paragraph

SS12 is *content only*: nothing in the game engine (RobustToolbox) is changed. The normal 2D renderer runs into an
offscreen viewport around the camera. A world overlay copies the lit floor layer into a texture. A fragment shader then
raymarches the tile map (walls) on the GPU and textures floors, ceilings and space. Every visible entity is drawn with
`DrawEntity` into a per-frame atlas and then as a depth-tested 3D quad (billboards, flat items, door panels, windows,
tables, wall decals). Input goes through `IViewportControl`, so every existing 2D system that turns a screen position
into a world position keeps working against the crosshair. A post chain (bloom, ambient occlusion, grade, vignette) is
optional and every effect has its own switch.

## What is in this repository

| Folder | What |
|--------|------|
| `overlay/` | Every file the installer **adds** to a server's code: `Content.Client/Render3D`, `Content.Shared/Render3D`, `Content.Server/Render3D`, shaders, prototypes, locale, tests, notes. It is a generated copy of the working code (see below), laid out like the server's own folders. |
| `installer/` | The installer, a single small .NET program with no packages (`ss12`). Commands: `install`, `update`, `uninstall`, `doctor`, `package`, `selftest`, `mcp`. |
| `scripts/` | The double-click wrappers for non-technical users, `demo/` (the Windows and Linux demo launchers), `server/` (the Linux server package: `run-server.sh`, an example config, a systemd unit) and `ci/` (helpers for the workflows). |
| `docs/` | Guides. |
| `.github/` | Issue forms, CI (installer `selftest`), the release build and the demo / Linux server build. |

The installer **edits** a handful of existing files in a server's code (aiming, picking, popups, map text, sprite
fade, health bars). Each edit is described by what the code looks like (a regular expression), never by line numbers, is
idempotent, and is listed with its reason and impact in `installer/BuiltIn.cs`. `ss12 mcp` and
[USE-WITH-AI.md](USE-WITH-AI.md) expose the same descriptions to an AI assistant.

## Building and testing the installer

You need the .NET 10 SDK.

```bash
dotnet build installer/ss12.csproj -c Release
dotnet installer/bin/Release/net10.0/ss12.dll selftest
```

`selftest` builds throw-away fixture codebases (LF and CRLF line endings), then checks: install, a second install being
refused, update, doctor, uninstall giving back byte-identical files, a missing required edit stopping with nothing
written, a missing optional edit only degrading, a newer health-bar overlay shape, a folder that is not SS14, and the
MCP server over its protocol. It needs no engine and no network. The release build runs it too.

## Where the 3D code is developed

The overlay is generated from a development tree: a Space Station 14 codebase with the 3D view installed on a branch.
That tree is not part of this repository. If you want to change the 3D code itself, the simplest way:

1. Take any SS14 codebase on engine 286 or newer, install 3D into it with the installer.
2. Edit the files under `Content.Client/Render3D`, `Content.Shared/Render3D`, `Content.Server/Render3D` and `Resources/...`.
3. Build and run it (`dotnet build`, then run `Content.Server` and `Content.Client`).
4. Copy the changed files back into `overlay/` here (same relative paths). `installer/overlay.manifest` lists which
   paths belong to the overlay.

For the 3D code the developer helpers are in `overlay/Tools/render3d-dev/`, and the settings reference and the
list of console commands are in `overlay/docs/ss12/README.md`. Run the client with `--cvar render3d.dev_channel=true
--cvar player.name=<name>` and `render3d_dev_<name>.txt` in the client's user data folder becomes a command channel
(`r3d_shot`, `r3d_look`, `r3d_press`, `render3d_fx`, ...). Handy for screenshots and scripted tests.

## Rules the 3D code follows (please keep them)

- **Content only.** Nothing under `RobustToolbox/`. Players use the official launcher and get the 3D client from the
  server like any other content.
- **No new NuGet packages in `Content.*`.**
- **Stay inside the client sandbox.** For example no `[ThreadStatic]`, no `StackTrace`. Violations only show when the
  client starts, so always launch it after a change.
- **Every visual effect has its own switch** and a place in the Low / Medium / High presets, so older computers can
  turn it off.
- **Shaders (swsl) have quirks:** no `return` in `fragment()`, no exponent literals (`1e-5`), `fwidth` must be inline in
  `fragment()`, helper functions are compiled into the vertex stage too, and shader instance parameters are read when
  the draw runs, so give each blur direction its own instance.
- **Edits to a server's existing files stay tiny** (one call into the new code) and are written with fully qualified
  names so no `using` line changes.

## Tests of the 3D code itself

They live in the overlay (`Content.Tests/**/Render3D`, `Content.IntegrationTests/Tests/Render3D`) and are copied into a
server's code with it, so the server's own test run covers them: `dotnet test Content.Tests` and the integration tests
filtered to `Render3D`.

## Cutting a release

Push a version tag (for example `v1.0.1`). `.github/workflows/release.yml` builds the installer for Windows, Linux and
macOS, bundles it with `overlay/`, the guides and the double-click scripts, runs `selftest` on the result, and attaches
the zips to the GitHub release. Update `installer/version.txt` and `docs/CHANGELOG.md` first.

The same tag also runs `.github/workflows/demo.yml`, which builds `SS12-demo-windows.zip`, `SS12-demo-linux.zip` and
`SS12-server-linux-x64.zip` from an upstream commit and attaches them. The Linux jobs start the unzipped server and wait
until it accepts connections; no runner has a graphics card, so the Linux client window is not exercised there.
