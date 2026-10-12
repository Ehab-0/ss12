# Compatibility: which codebases the installer works on

*[Русская версия](COMPAT.ru.md)*

The installer (`ss12`, see [ONBOARDING.md](ONBOARDING.md)) was tried on real codebases. This page says what
was found, honestly, including what does **not** work yet.

Terms: "dry run" = the edits were planned against the code to see whether their anchors are found (nothing written);
"built" = client and server were compiled after installing; "launched" = a server and a 3D client were started from that
build. Everything was done with read-only clones in a temp folder; nothing was pushed to any remote.

Tested on 2026-10-02 with installer 1.0.0.

## The one hard requirement: engine 264 or newer

The installer refuses engines older than 264 with a message (check yours in
`RobustToolbox/MSBuild/Robust.Engine.Version.props`). Engines 264 to 285 lack two features the 3D view uses on newer ones:
relative mouse mode for windows (engine 286) and the viewport's FOV render target (engine 283), and a few engine calls
moved. For them the installer copies versions of the `Content.Client/Render3D/Engine/Render3DEngine.*.cs` files from
`Tools/ss12-engine/before-<version>/` of the SS12 repository, picked from the engine version (the install summary lists
them). There, the hidden cursor steers the view (mouse steering, see `render3d.mouse_steer` in README.md) and what the
eye cannot see comes from a mask captured from the 2D view; the result is the same as on newer engines. After an engine
update run `ss12 update`; `ss12 doctor` warns when the files are for another engine.

| Codebase | Commit | Engine | Edits found (required / optional) | Result |
|----------|--------|--------|-----------------------------------|--------|
| Space Wizards upstream (the base this project was developed on) | `989c2caf16` | 291.0.0 | 2/2, 5/5 | built; 266 unit + 7 integration tests of the 3D code pass |
| Space Wizards upstream `master` (what "Wizard's Den Lizard" ran on 2026-10-02) | `fb9401cf` | 291.0.0 | 2/2, 5/5 | built, launched (3D at 300+ FPS), uninstall returned the exact original commit; also packaged with `ss12 package` (HybridACZ), the packaged server serves the 3D client files, and a 3D client joined it. the official launcher (Direct connect) joined a local copy of the packaged server and downloaded the 3D game from it; not tried over the internet |
| Starlight (the 124-player server's exact deployed commit) | `ae6aeadf` | 290.0.0 | 2/2, 5/5 | built, **launched**: server + 3D client connected, in game, walls/entities/UI drawn (`screenshots/compat_starlight_3d.png`) |
| Russian Marine Corps (73-player server's exact deployed commit; a large Marine Corps fork with z-levels and a Russian UI) | `ae059179` | 290.0.0 | 2/2, 6/6 | built, **launched** and played in 3D (`screenshots/compat_rumc_3d.png`); `update` and `doctor --build` pass. See the note below about one engine call it makes every frame |
| Frontier Station | `cc55eb68` (current head; the server runs `b069ad38`) | 290.0.0 | 2/2, 5/5 | dry run only: every required edit applies. Not built |
| Sector Frontier, Funky Station | `110c0dee`, `c4c4e322` | not checked | 2/2, 5/5 | dry run only (their `/info` page gave no engine version): every required edit applies. Not built |
| Stalker | n/a | 286.0.0 | not tried | not tried |
| Space Wizards upstream at engine 264 (2025-07-23) | `eb21b5826a^` | 264.0.0 | 2/2, 5/5 | built on .NET 10 with newer packages standing in for the exact ones (and the engine changes later engines made for them), 659 unit tests pass, 26 of 32 Render3D integration tests pass (the same 6 name checks), **launched**: mouse steering, FOV mask, doors, windows and lamps checked in game. Found and fixed: StringComparer outside the 264 client sandbox, EntityStorageComponent and PoweredLightComponent not being shared yet, test harness differences |
| Space Wizards upstream at engine 277 (2026-06-25) | `51925662e2^` | 277.2.1 | 2/2, 5/5 | installed with the versions for older engines, built, 659 unit tests pass, 26 of the 32 Render3D integration tests pass (the 6 others check that the rules name only prototypes of this codebase, and it predates some names), **launched**: mouse steering and the FOV mask checked in game |
| RMC14 (Rouny's Marine Corps) | `1c173c31` | 264.0.2 | 2/2, 5/5 (October 2) | accepted since engine 264 is; not built yet |
| Delta-V | `0bcc69b3` | 275.2.1 | 2/2, 5/5 (October 2) | accepted since engine 264 is; not built yet |
| Einstein Engines | `10d41858` | 267.3.0 | 2/2, 3/5 (October 2) | accepted since engine 264 is; not built yet |
| Goob Station | `cbd25950` | 270.1.0 | 2/2, 5/5 (October 2) | accepted since engine 264 is; not built yet |
| Floofstation | `eff4e2d9` | 239.0.1 | 2/2, 3/5 | **refused: engine too old** (edits themselves apply) |

What this means today: the 3D view installs on codebases on engine 264 or newer. Of the 14 of the 18 most populated
servers on the public list whose version could be read, 5 run engine 286 or newer (Starlight, Russian Marine Corps,
Frontier, Stalker, upstream Wizard's Den), and most of the others (Delta-V, Goob and its relatives, SS220, Corvax,
Arcane, RMC14, ...) run engines 264 to 277, which are now accepted too. Upstream code at engines 264 and 277 has been
built and launched; the forks themselves have not.

## Engine calls that forks make which the 3D view had to answer

* **`EyeManager.GetWorldViewbounds()` every frame.** Upstream only calls it when examining, but the Russian Marine Corps
  fork calls it every frame (to cull z-level lighting). In 3D the real screen corners do not form a valid box for every
  camera heading, which made that fork's *debug* build stop on an engine assertion (release builds do not check it and
  would have culled lights wrongly). The 3D view now answers the two corner queries with a square around the camera
  (rotated to cancel the eye rotation), found by launching that fork; fixed in the same commit as this note.
* **Debug versus release builds.** The launch tests above used `DebugOpt` builds, which have extra assertions. A release
  build of the Russian fork was not launched.

## Differences between codebases the installer already handles

* `GetClickableEntities` with or without the trailing `excludeFaded` parameter: the inserted call forwards whatever
  parameter list the method has.
* The drag deadzone constant named `Deadzone` or `_deadzone`; the mouse-down position stored from `args.ScreenCoordinates`
  or from `_inputManager.MouseScreenPosition`: matched by pattern.
* Older map text overlay (draws at `pos - dimensions / 2f`) and the newer one (rounded `drawPosition`, outline).
* Mouse-aiming code anywhere in `Content.Client` that calls `PixelToMap(<input manager>.MouseScreenPosition)` (Goob has one
  more, in its actions UI): one pattern rewrites all of them.
* Walls identified by a `Wall` component (current) or only by a `Wall` tag (older): the 3D code finds walls by scanning the
  anchored entities around the camera and asks `Render3DCompat.IsWall`, which looks the component up by registered name;
  the ghost component, which moved namespaces, is resolved the same way.
* Health-bar overlay: the optional edit is only applied to the overlay design it was written for.

Optional edits that were skipped in the dry runs: `map-text` (Einstein Engines, Floofstation have no map text overlay) and
`sprite-fade` (Einstein Engines, Floofstation have an older design or none); both are cosmetic.

## Not covered

* Codebases whose `GameplayStateBase.GetClickableEntities` was restructured beyond the two shapes above: the installer stops
  before changing anything and prints the manual edit (about 12 lines).
* GLES2 mode, macOS, and unusual DPI scaling have not been tested.
