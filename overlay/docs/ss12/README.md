# SS12

*[Русская версия](README.ru.md)*

**SS12 is SS13 in 3D, using SS14.** (Under the hood it is the SS14 game seen in 3D; the name is the pitch.)

A 3D-only view of Space Station 14 with third-person (default) and first-person cameras. Gameplay, rules, roles,
balance and multiplayer behaviour are the upstream ones. Everything is implemented in content code
(`Content.Client`, a little in `Content.Shared`/`Content.Server`); **`RobustToolbox/` is untouched**, so players join
with the official launcher.

Contents: [Quick start](#quick-start) - [Controls](#controls) - [Status](#status-what-has-been-verified) -
[Hosting](#hosting-a-3d-server) - [Settings reference](#settings-reference) - [Graphics and older PCs](#graphics-and-turning-things-off-for-older-pcs) - [Slower machines](#slower-machines) -
[Troubleshooting](#troubleshooting) - [Known limitations](#known-limitations) - [How it works](#how-it-works) -
[Where the code is](#where-the-code-is) - [Developer aids](#developer-aids) - [Impostor pipeline](#impostor-pipeline-optional-phase-8)

More documents: [PLAN.md](PLAN.md) (the design), [PROGRESS.md](PROGRESS.md) (every verification step with its
evidence), [REPORT.md](REPORT.md) (the end-of-project report), `screenshots/` (2D vs 3D pairs and feature shots).

## Quick start

Requires the .NET SDK pinned in `global.json` (10.0.100) and a GPU with OpenGL 3.3.

```
dotnet build --configuration DebugOpt -m
# server: 3D enforced, straight into a round
cd bin/Content.Server && ./Content.Server.exe --cvar render3d.enforced=true --cvar physics.relative_movement=true --cvar shuttle.camera_rotation_locked=true --cvar game.lobbyenabled=false
# client (run twice with different --username values for a two player test)
cd bin/Content.Client && ./Content.Client.exe --connect --connect-address 127.0.0.1 --username player1
```

After a `Release` build run `dotnet restore` before building `DebugOpt` again (the two configurations share `obj/`).

## Controls

| Action | Default key (rebindable under Options > Controls > 3D view) |
|--------|-----|
| Look | mouse (captured while the game window has focus and no window/menu is open) |
| Move | WASD, relative to where the camera looks |
| Use / attack / shoot / throw | left click at the crosshair (the same bindings as 2D) |
| Toggle 3D / 2D (when allowed) | `F12` |
| Toggle first / third person | `N` |
| Free cursor (hold) | `Alt` (Alt-click keeps its normal meaning) |
| 3D settings window (general, keys, view, graphics, shape of things) | `F11`, or the console command `render3d_settings` |
| Third-person distance | the existing Zoom in / out keys (0.8 - 3.0) |

Everything that opens a window, menu or popup, and a focused chat box, frees the cursor automatically and gives it back
when you are done. With the cursor free, clicking the world casts the ray through the cursor instead of the crosshair.
While captured, the crosshair is "the mouse" for every system: guns, melee, interaction, throwing, drag-and-drop,
context menu, examine and target outlines all aim at it (`Render3DPointer`).

The crosshair turns green and its name label stays white when the thing under it is within hand reach (1.5 tiles, or held/worn); out of reach the crosshair stays white and the label is gray. Handy for telling "too far to pick up / open / cuff" from "wrong target".

Options > 3D: field of view, mouse sensitivity, render scale, invert Y, crosshair names, start in first person, and the
on/off switch for the 3D view.

## Status: what has been verified

Verification is by scripted play through the real viewport input path on the Dev map (see [Developer aids](#developer-aids)),
by tests, and by screenshots. **Not verified: feel with a physical mouse, and anything past short scripted sessions with
real human play.** Details and evidence per item are in `PROGRESS.md`.

| Area | State |
|------|-------|
| Rendering: walls, floors, ceilings, space, fog, lights, FOV hiding, doors, windows, tables, wall decals, glowing layers | working |
| Camera: third-person spring arm, first person, yaw sync to the server, camera-relative movement | working, covered by unit + integration tests |
| Interaction: pick up, drop, throw, open lockers, doors, pull, cuff, melee, flash, gun firing at the crosshair, context menu, examine | each exercised live |
| HUD in 3D: health bars, status/sec HUD icons, speech bubbles, popups, map text | working |
| Enforcement (`render3d.enforced`), options tab, key rebinding | working |
| Performance | about 200-300 FPS uncapped at 1080p/900p on a Radeon RX 580; a 20-minute scripted soak showed no errors and flat memory (about 1.25 GB) |
| Tests | 686 unit tests (+266 new), 5 camera integration tests, the existing integration suite unchanged and passing in slices; YAML linter and RSI validator clean |
| Mouse feel | **needs a human** |

## Hosting a 3D server

Apply the preset `Resources/ConfigPresets/Build/render3d.toml` (or set the cvars by hand):

| CVar | Value | Why |
|------|-------|-----|
| `render3d.enforced` | `true` | Living players cannot switch back to 2D (ghosts, active admins and the lobby can). |
| `physics.relative_movement` | `true` | WASD is relative to the camera yaw. |
| `shuttle.camera_rotation_locked` | `true` | The 90 degree camera-rotate keys are meaningless in 3D. |

Players need nothing special: the stock launcher works. A server that does not set `render3d.enforced` lets every
player choose 2D or 3D with the `Toggle3DView` key.

## Settings reference

All client-side unless noted. Defaults in parentheses; most are also in Options > 3D.

| CVar | Meaning |
|------|---------|
| `render3d.enabled` (true) | Use the 3D view at all. |
| `render3d.enforced` (false, server, replicated) | Living players may not use 2D. |
| `render3d.yaw_send_rate` (12, client) | How many camera yaw updates per second the client sends while the mouse turns the view (4 to 30). Lower sends less over a lossy connection; WASD follows the new direction at this rate. |
| `render3d.settings_on_join` (0, server, replicated), `render3d.settings_shown` (false) | Opens the 3D settings window by itself when a player enters a round: 0 never, 1 the first time only (`settings_shown` remembers it on that computer), 2 every round. |
| `render3d.wall_height` (1.6), `eye_height` (0.9) | Tiles are 1 unit wide; the plan's 1.25 / 0.75 felt cramped. |
| `render3d.table_height` (0.45), `effect_height` (0.6), `ui_anchor_height` (0.9) | Where table tops, flat effects and floating labels sit. |
| `render3d.fov` (80, degrees, integer) | Vertical field of view. |
| `render3d.render_scale` (1.0) | Resolution of the 3D scene relative to the window (below 1 draws fewer pixels). |
| `render3d.supersample` (2.0) | Smoothing by supersampling: the scene is drawn at this many times the window size in each direction and averaged down, which removes shimmer and jagged edges on every surface. 1 = off. Limited automatically so the scene never exceeds about 4K worth of pixels. The Low / Medium / High presets set 1.0 / 1.5 / 2.0. |
| `render3d.ground_radius` (24) | Tiles around the camera captured from the 2D renderer. |
| `render3d.atlas_size` (2048), `billboard_cap` (400) | Sprite atlas size and the maximum entities drawn (nearest first). |
| `render3d.tp_distance` (1.6) | Third-person camera distance (also changed by the zoom keys). |
| `render3d.mouse_sensitivity` (1.0), `invert_y` (false) | Mouse look. |
| `render3d.crosshair_names` (true) | Name of what is under the crosshair. |
| `render3d.first_person` (false) | Start in first person. |
| `render3d.quality` (2), `render3d.auto_quality` (true), `render3d.auto_quality_fps` (45) | Preset (0 low, 1 medium, 2 high, 3 custom), and the automatic step-down. |
| `render3d.fx.bloom`, `fxaa`, `ao`, `surface`, `ambient`, `shadows`, `outline`, `sharp`, `grade`, `haze`, `sky`, `fixtures`, `head_bob`, `item_lift`, `item_lean`, `item_thick`, `char_lean`, `char_thick`, `object_lean`, `object_thick` | The individual effects (see above). |
| `render3d.thickness_layers` (4) | How many stacked layers thick things are drawn with (0 = flat). The Low / Medium / High presets set 0 / 3 / 4. |
| `render3d.dev_channel` (false), `render3d.debug_view` (0; 1 ground capture, 2 light, 3 field of view, 41 billboard atlas, 42 its glow layer), `render3d.cap_hysteresis` (0.6), `render3d.dev_atlas_offset` (true) | Developer aids. `cap_hysteresis` is how much closer an entity drawn last frame counts when the nearest `billboard_cap` are picked (1 = no head start). |

## Graphics, and turning things off for older PCs

Every visual effect is its own on/off setting, so older machines can drop exactly what they cannot afford. Open the
settings window (`F11`) and use the **Graphics** section: a quality preset (Low / Medium / High) plus one checkbox per
effect. Toggling a single effect by hand makes the preset read "Custom". The same switches exist as console commands:
`render3d_quality low|medium|high` and `render3d_fx <effect> on|off` (`render3d_fx` alone lists them).

| Effect (`render3d_fx` name) | What it does | Low | Medium | High |
|-----------------------------|--------------|:---:|:------:|:----:|
| `bloom` | glow around lights and screens | - | - | on |
| `fxaa` | smooths wall and floor silhouettes (depth-guided, sprite pixels stay crisp) | - | on | on |
| `ao` | soft shadows in corners and under things (screen-space ambient occlusion) | - | - | on |
| `surface` | wall/floor/ceiling shading: edge darkening, floor sheen, ceiling panels | - | on | on |
| `ambient` | a faint light on walls, floors and ceilings that the 2D line of sight leaves unlit, so a third-person camera does not show pure black holes (entities there stay hidden) | - | on | on |
| `shadows` | contact shadows under characters, items and thrown things | - | on | on |
| `outline` | thin dark outline on characters and items | - | - | on |
| `sharp` | sharp-bilinear sprites: no shimmer or stair-steps, pixels stay crisp | - | on | on |
| `grade` | tone mapping, colour grading, vignette, dithering | - | on | on |
| `haze` | distance haze tinted by the local light | - | - | on |
| `sky` | layered stars and a faint nebula | - | on | on |
| `fixtures` | wall lamps get a soft glow halo (they hang near the ceiling at every level) | - | on | on |
| `head_bob` | gentle bob while walking in first person | - | - | - |
| `item_lift` | items on the ground are lifted a little and get a contact shadow and a thin outline, so they read as objects and not as stains | - | on | on |
| `item_lean` | items on the ground tilt towards the camera (up to 20 degrees, none when looking straight down) | - | on | on |
| `item_thick` | items on the ground get thickness | - | on | on |
| `char_lean` | characters (and bodies on the floor) lean back / tilt slightly | - | on | on |
| `char_thick` | characters get thickness | - | - | on |
| `object_lean` | machines, furniture and other standing objects lean back slightly | - | - | on |
| `object_thick` | machines, furniture and other standing objects get thickness | - | - | on |

Low is the plain look (and also draws fewer things: entity cap 200, 16 tiles of view). **Automatic quality** is on by
default: if the average frame rate stays below 45 FPS for about five seconds after a 10 second warm-up, the game steps
the preset down one level and tells the player (`render3d.auto_quality`, `render3d.auto_quality_fps`); with everything
already off it lowers the render scale as a last resort. It never steps up and never touches a Custom setup. On a
Radeon RX 580 at 1600x900 the whole High preset costs about 0.65 ms per frame (342 FPS Low, 280 FPS High with 20 extra
characters in view).

## Shape of things: lean and thickness

Sprites are flat pictures, so by default a thing on the floor looks like a sticker and is easy to mistake for a stain
(spills and decals are part of the floor image). The "shape" effects give things a body, each category on its own
switch (settings window, section **Shape of things**, or `render3d_fx <effect> on|off`):

| Category | Switches | What happens |
|----------|----------|--------------|
| Items on the ground (and thrown ones) | `item_lift`, `item_lean`, `item_thick` | lifted a little with a contact shadow and a thin outline; tilted up towards the camera by up to 20 degrees (less the more the camera looks down); drawn as a small solid slab |
| Characters (also lying bodies) | `char_lean`, `char_thick` | leaning back up to about 17 degrees so they are less squashed seen from above; a thin slab |
| Objects (machines, furniture, other standing things) | `object_lean`, `object_thick` | the same as characters |

Thickness is drawn by *sprite stacking*: copies of the sprite are stacked behind the front face along the face's normal,
darker with depth, so the sides of the silhouette show like the sides of a slab (`render3d.thickness_layers` layers,
fewer beyond 9 tiles, none beyond 17 tiles, and at most 3000 extra quads per frame). Only the front face is clickable
and outlined. Lean is a plain tilt of the sprite's quad about its near edge (flat things) or its foot (standing things);
picking uses the tilted quad, so it is easier to click.

Per prototype, the `shapes` list of a `render3dRules` prototype (`Resources/Prototypes/Render3D/rules.yml`) overrides
the thickness (in tiles, 0 = paper thin) and can keep a thing from leaning (`lean: false`). It matches prototype ids and
their descendants (the nearest ancestor wins) and component names, like the `rules` list; unknown names are ignored.
Entities can also carry a `Render3D` component with `thickness` / `lean`. The defaults are 0.06 tiles for items, 0.16
for characters and 0.14 for other objects; the shipped rules make paper, ID cards and cartridges nearly flat and
toolboxes thicker.

## Slower machines

`render3d.render_scale 0.5`, `render3d.billboard_cap 200`, `render3d.ground_radius 16`. The defaults already run at
about 200 FPS uncapped on a Radeon RX 580 at 1080p, so this is only for much weaker GPUs.

## Troubleshooting

- **The view is black or the game crashes at start:** needs OpenGL 3.3. Try `--cvar render3d.enabled=false` to confirm
  the 2D game still works, then update the GPU driver.
- **FPS stuck near 60 or lower when the window is covered:** that is the OS vsync throttling an occluded window (also
  true in 2D). Turn vsync off in Options > Graphics to measure.
- **The mouse stays free / look does not work:** the window must have focus and no window, popup or focused text box may
  be open. Hold or tap `Alt` to see whether the free cursor is the cause.
- **A GUI element still appears in 2D style (outlines etc.):** see [Known limitations](#known-limitations).
- **Everything is too dark or too bright:** it uses the game's own lighting; check the 2D view with `F12` (if allowed).
- **Options window crashes when opened from a debug console:** a pre-existing DebugOpt engine assertion that also
  happens without this change; opening it from the menu is fine.

## Known limitations

- Diagonal walls (and diagonal windows/grilles) render as full blocks.
- No vertical aiming: projectiles stay in the horizontal plane like in 2D.
- Gas, fire and explosion overlays are drawn on the floor only (they come from the 2D ground capture).
- The sky is procedural, not the map's parallax.
- Scoped-gun eye offset and sprite-fade towards the cursor are disabled in 3D.
- Sprite post-shaders (the 2D interaction outline) cannot be drawn through `DrawEntity`; the hovered entity is
  brightened instead.
- Sprites that have a layer with its own shader still get their glowing layers lit (the glow pass cannot cover them).
- The normal screenshot key captures the 3D image (it takes the final frame); the "no UI" screenshot variant reads the hidden 2D viewport instead.
- Speech bubbles are not hidden when the speaker is behind a wall; status icons, health bars and map-text labels are.
- Drag-and-drop with a captured mouse starts after about a dozen pixels of mouse travel.
- GLES2 mode and unusual DPI scaling have not been tested.

## How it works

The normal 2D renderer is run into an offscreen viewport around the camera. A world overlay that sits just above
`DrawDepth.HighFloorObjects` copies the lit floor layer (tiles, decals, puddles, pipes...) into a texture. A fragment
shader then raymarches the tile map (walls) on the GPU and textures floors, ceilings and space. Every visible entity
is composited with `DrawEntity` into a per-frame atlas (plus a second atlas with only its unshaded layers) and drawn as
a depth-tested 3D quad (billboards, flat items, door panels, windows, tables, wall decals). Input goes through
`IViewportControl`, so every existing 2D system that turns a screen position into a world position keeps working
against the crosshair.

## Where the code is

| Path | What |
|------|------|
| `Content.Client/Render3D/` | control, controller (view toggle, mouse capture, enforcement, auto quality), camera, ground capture, tile world (walls), atlases, classifier, entity pass, quad builder, `PostFx` (bloom, AO, grade), `EntityShape` (lean and thickness math), picking, status icons, `Render3DPointer`/`Render3DCompat`, settings window, quality presets, dev channel |
| `Content.Shared/Render3D/`, `Content.Server/Render3D/` | `Render3D` component, camera yaw maths and sync, facing |
| `Content.Shared/CCVar/CCVars.Render3D.cs` | all cvars |
| `Resources/Textures/Shaders/Render3D/` | `raymarch`, `entity`, `glowmask`, `present`, `post`, `bloom_extract`, `blur` shaders (registered in `Resources/Prototypes/Shaders/render3d.yml`) |
| `Content.Tests/**/Render3D`, `Content.IntegrationTests/Tests/Render3D` | tests |
| `Tools/render3d-dev/`, `Tools/render3d-pipeline/` | scripted-play helpers; impostor pipeline |

Upstream files with small hooks (3D picks, WorldToScreen for overlays, crosshair aiming, key bindings, options, ten base
prototypes carrying one `Render3D` component) are listed in `REPORT.md`.

## Developer aids

`render3d.dev_channel=true` makes the client poll `render3d_dev_<player name>.txt` in its user data folder and run each
line as a console command (start the client with `--cvar player.name=<name>` so the file name is predictable).

| Command | Purpose |
|---------|---------|
| `r3d_shot <name>` | PNG of the 3D view into `Screenshots/` |
| `r3d_look <yaw> <pitch>` / `r3d_look off`, `r3d_aim <x> <y>` | point the camera (degrees) or at a floor point |
| `r3d_cam fp\|tp` | camera mode |
| `r3d_press <KeyFunction> [down\|up\|tap]` | synthetic input through the real viewport path (use `down`, wait, `up` for melee-class items) |
| `r3d_capture on\|off` | force the captured-mouse path without window focus |
| `render3d_tune` | open the tuning window: a slider, box or switch for every client-side `render3d.*` setting, applied at once; "Copy changes" copies the changed values |
| `r3d_atlas <name>` | save the billboard atlas and its glow layer as `atlas_<name>.png` / `atlasglow_<name>.png` |
| `r3d_audit <name>` | draw every entity of the next frame alone in a roomy cell, save the sheets as `audit_<name>_<n>.png` and log the atlas slot of each cell (art outside its slot is painted onto a neighbour in the real atlas) |
| `r3d_pick`, `r3d_dump` | what is under the crosshair; every drawn entity with its net id |
| `r3d_noself on\|off`, `r3d_glow on\|off` | exclude the own body from picks; compare with/without the glow atlas |
| `render3d_fx <effect> on\|off`, `render3d_quality ...` | switch effects (also real player commands), used for the A/B screenshots in `docs/ss12/screenshots/visual_*` |

`render3d.debug_view` selects visualisations (1 ground capture, 2 light, 3 FOV, 4 rays, 5 raw ground, 6 depth, 7 tile
flags, 8 light at hit, 9 tile map, 10 grid 0). With the dev channel on the client logs a `render3d: stats` line every
3 seconds (FPS and CPU milliseconds per stage). `Tools/render3d-dev/` holds the shell helpers; `soak.sh <seconds>` runs a
random walk/look/click soak and samples memory.

## Impostor pipeline (optional, Phase 8)

`Tools/render3d-pipeline/` turns static prop sprites into pre-rendered 8-direction impostor RSIs through
TRELLIS.2 + headless Blender. It needs a >=24 GB CUDA GPU for the real run; this repository only ships the
no-GPU dry-run path (stubs). See its README.
