# Compatibility: which codebases the installer works on

*[Русская версия](COMPAT.ru.md)*

The installer (`ss12`, see [ONBOARDING.md](ONBOARDING.md)) was tried on real codebases. This page says what
was found, honestly, including what does **not** work yet.

Terms: "dry run" = the edits were planned against the code to see whether their anchors are found (nothing written);
"built" = client and server were compiled after installing; "launched" = a server and a 3D client were started from that
build. Everything was done with read-only clones in a temp folder; nothing was pushed to any remote.

Tested on 2026-10-02 with installer 1.0.0.

## The one hard requirement: engine 286 or newer

The 3D view needs two engine features that only exist in newer RobustToolbox versions: relative mouse mode for windows
(for mouse-look, engine 286) and the viewport's FOV render target (engine 283). A codebase on an older engine cannot
compile it, so the installer refuses with a message (check yours in `RobustToolbox/MSBuild/Robust.Engine.Version.props`).
`--force` tries anyway and will end in compile errors.

| Codebase | Commit | Engine | Edits found (required / optional) | Result |
|----------|--------|--------|-----------------------------------|--------|
| Space Wizards upstream (the base this project was developed on) | `989c2caf16` | 291.0.0 | 2/2, 5/5 | built; 266 unit + 7 integration tests of the 3D code pass |
| Space Wizards upstream `master` (what "Wizard's Den Lizard" ran on 2026-10-02) | `fb9401cf` | 291.0.0 | 2/2, 5/5 | built, launched (3D at 300+ FPS), uninstall returned the exact original commit; also packaged with `ss12 package` (HybridACZ), the packaged server serves the 3D client files, and a 3D client joined it. the official launcher (Direct connect) joined a local copy of the packaged server and downloaded the 3D game from it; not tried over the internet |
| Starlight (the 124-player server's exact deployed commit) | `ae6aeadf` | 290.0.0 | 2/2, 5/5 | built, **launched**: server + 3D client connected, in game, walls/entities/UI drawn (`screenshots/compat_starlight_3d.png`) |
| Russian Marine Corps (73-player server's exact deployed commit; a large Marine Corps fork with z-levels and a Russian UI) | `ae059179` | 290.0.0 | 2/2, 6/6 | built, **launched** and played in 3D (`screenshots/compat_rumc_3d.png`); `update` and `doctor --build` pass. See the note below about one engine call it makes every frame |
| Frontier Station | `cc55eb68` (current head; the server runs `b069ad38`) | 290.0.0 | 2/2, 5/5 | dry run only: every required edit applies. Not built |
| Sector Frontier, Funky Station | `110c0dee`, `c4c4e322` | not checked | 2/2, 5/5 | dry run only (their `/info` page gave no engine version): every required edit applies. Not built |
| Stalker | n/a | 286.0.0 | not tried | engine is at the minimum; not tried |
| RMC14 (Rouny's Marine Corps) | `1c173c31` | 264.0.2 | 2/2, 5/5 | **refused: engine too old** (edits themselves apply) |
| Delta-V | `0bcc69b3` | 275.2.1 | 2/2, 5/5 | **refused: engine too old.** A forced build fails on the missing engine APIs |
| Einstein Engines | `10d41858` | 267.3.0 | 2/2, 3/5 | **refused: engine too old** (edits themselves apply) |
| Goob Station | `cbd25950` | 270.1.0 | 2/2, 5/5 | **refused: engine too old** (edits themselves apply) |
| Floofstation | `eff4e2d9` | 239.0.1 | 2/2, 3/5 | **refused: engine too old** (edits themselves apply) |

What this means today: the 3D view installs and runs on codebases that track current upstream (engine 286+). Of the
14 of the 18 most populated servers on the public list whose version could be read, 5 run engine 286 or newer
(Starlight, Russian Marine Corps, Frontier, Stalker, upstream Wizard's Den). The others (Delta-V, Goob and its
relatives, SS220, Corvax, Arcane, RMC14, ...) are on engines 264 to 277, so their hosts would first have to merge a
newer RobustToolbox and the content changes that come with it, which is what they do for their own updates anyway.
Once they are on 286+ the edits are already known to apply to their code (see the "Edits found" column).

A "legacy engine" mode (hold-a-key-to-look instead of a captured mouse, no FOV hiding) would lift the requirement to
about engine 267; it is not implemented.

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
