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
[Where the code is](#where-the-code-is) - [Developer aids](#developer-aids) - [Impostors](#impostors-optional)

More documents: [ONBOARDING.md](ONBOARDING.md) (host a 3D version of your own server), [COMPAT.md](COMPAT.md) (which codebases
it was tried on).

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
| Toggle first / third person (from the overhead view: back to walking) | `N` |
| Toggle the overhead view | `O` |
| Next camera (third person, first person, overhead, dynamic view, and round again) | `Insert` |
| Dynamic view: closer to the character / further and higher, all the way to the 2D view | the mouse wheel (the zoom keys do the same) |
| Overhead camera: turn and tilt it (the high dynamic view: turn it) | hold the middle mouse button over the view and drag |
| Turn the overhead view an eighth turn left or right (the dynamic view: a quarter turn), or back to north up | the existing Camera rotate left / right / reset keys (while a high view is on) |
| **Free the mouse to click menus, buttons and the inventory (hold)** | **`Alt`** (Alt-click keeps its normal meaning) |
| 3D settings window (tabs: General, Interface, Controls, Graphics) | `F11`, or the console command `render3d_settings [tab]` (`general`, `interface`, `controls`, `graphics`) |
| Minimap on and off | `M`, or the console command `render3d_minimap` |
| Minimap small or large | `-` |
| List of what the crosshair points at, on and off | `L`, or the console command `render3d_pointlist` |
| Choose from that list: select, then act | `Up`, `Down`, `Space` (select), `Space` again (act); or hold `Alt` and click a row |
| Third-person distance | the existing Zoom in / out keys (0.8 - 3.0); in the overhead view they move the camera in and out (4 - 16) |

Everything that opens a window, menu or popup, and a focused chat box, frees the cursor automatically and gives it back
when you are done. With the cursor free, clicking the world casts the ray through the cursor instead of the crosshair.
While captured, the crosshair is "the mouse" for every system: guns, melee, interaction, throwing, drag-and-drop,
context menu, examine and target outlines all aim at it (`Render3DPointer`).

The crosshair turns green and its name label stays white when the thing under it is within hand reach (1.5 tiles, or held/worn); out of reach the crosshair stays white and the label is gray. Handy for telling "too far to pick up / open / cuff" from "wrong target".

Options > 3D: field of view, mouse sensitivity, render scale, invert Y, crosshair names, start in first person, and the
on/off switch for the 3D view.

### The overhead view

A third camera, like the classic top-down game seen in 3D: high above the character (about 9 tiles along the line of sight),
pitched down 55 degrees, with a narrower field of view (45), **no ceiling** and **lower walls** (1.1 tiles), so a wall shows as a
dark cap with a lighter line where it borders open floor. Doors and window frames get caps too, so a wall with a door in it has
no hole.

- **The mouse is always free.** There is no mouse look and no crosshair: the cursor points at the world, and every click, aim,
  throw, drag, context menu and examine goes through the pixel under it, as in 2D. The name of what the cursor is over shows
  beside it (`render3d.crosshair_names`), and it gets the same highlight as under the crosshair. The pointing list (`L`) is off in
  this view; a pile of items is picked from the context menu.
- **WASD walks along the screen**, like 2D. The view is north-up (the north of the grid you stand on, so a rotating shuttle turns it) and
  the Camera rotate left / right keys turn it by quarter turns, smoothly; the reset key goes back to north up. The movement keys
  follow the new heading at once. The yaw goes to the server through the same message as in the other views.
- The zoom keys move the camera in and out. Characters and machines lean back by most of the pitch, so they keep their proportions.
- **Turn and tilt it freely.** Hold the middle mouse button over the view and drag: dragging sideways turns the picture around the character, dragging up and down tilts it (the look-down angle is the `render3d.overhead_pitch` setting, so the slider follows). WASD walks along the screen at whatever heading it has. The Camera rotate keys snap the heading to the nearest eighth turn (a diagonal heading is the classic isometric look), and the reset key goes back to north up. This replaces the separate isometric and free orbit cameras of earlier builds.
- Switch it with `O`, with the camera dropdown in the settings window (`render3d.overhead`, kept between rounds) or `r3d_cam oh`. `N` goes back to walking.
  Pitch, distance, field of view and wall height have sliders (`render3d.overhead_*`).

The overhead view shows what the classic view shows: the field of view and the lights are the 2D ones, and entities and walls are hidden the
same way, so it gives no extra sight.

### The other cameras

`Insert` goes through all four cameras (a short notice names the one you land on); `N` and `O` still switch first / third person and
the overhead view. The General tab of the settings window (`F11`) starts with the quality preset and the performance settings, then has a dropdown for the camera and, under it, the sliders of every camera in groups (first and third person, overhead, dynamic view); none of them is hidden behind the camera you picked.

| Camera | What it is |
|--------|------------|
| Third person (default) | Behind the character, with a crosshair; the mouse looks around. |
| First person | Through the character's eyes. |
| **Overhead** | See above. It is also the isometric and the free orbit camera: drag to turn it to any heading (a diagonal gives the isometric look; the Camera rotate keys snap to eighth turns) and to tilt it; the pitch, distance, field of view and wall height sliders set the rest. |
| **Dynamic view** | One camera that the mouse wheel moves all the way: first person, third person, a high third person, isometric, overhead, straight down, and last the classic 2D view. See below. |

**The dynamic view** (`render3d.camera_extra` 4; the cameras in `Insert`'s order, last):

- The wheel moves one number from 0 (first person) to 1 (the classic 2D view); the camera glides there. Rolling the wheel away from you
  brings the camera closer to the character, towards first person; `render3d.dynamic_view_invert` turns that round, `render3d.dynamic_view_speed`
  (0.06, the part of the whole way per notch) says how far a notch goes, and the zoom keys do the same as the wheel. Where it was left is
  kept (`render3d.dynamic_view_u`).
- The stops, from close to far: first person, third person (the third person settings), a high third person, isometric (the isometric
  settings), overhead (the overhead settings), straight down. The sliders of those cameras are the stops, so there is nothing else to tune. Between the stops the distance is worked out so
  that the picture shows steadily more of the floor with every notch (by ratio, so a notch feels the same near third person and near
  isometric): the field of view narrows towards isometric, so a distance that merely grew would have zoomed the picture in again on the way.
- Close to the character the mouse looks around with the crosshair, as in first and third person. A little above the high third person the
  camera counts as a high one: the mouse is released and becomes a free cursor, the camera turns to the grid's heading (and to the isometric
  diagonal on the way), there is no ceiling and the walls come down to their low height. On the way back the mouse look returns.
- From there up (isometric and overhead, until the view starts to go straight down) holding the middle mouse button over the view and dragging
  turns the picture, as in the overhead camera; only the heading turns, the look-down angle stays what the wheel set. WASD follows the screen
  at the new heading. The turn is given up on the way down to the walking cameras and fades out towards the straight-down view, so the hand-over
  to the classic 2D view is not turned; the camera rotate keys reset it.
- **A smooth wheel.** The numbers between the stops follow a smooth curve through them (not straight lines, whose bends made the speed jump at each stop), the wheel position glides with a critically damped spring (it starts and ends softly, and several notches in a row run together), and the camera's collision with the walls lets go as the camera rises over them, then moves smoothly, instead of snapping in on the way up. The picture also never shows less floor than at the stop before it: a wide walking field of view (110, say) shows more floor than the narrow isometric stop at a similar distance, which used to make the picture zoom in and out again half way between the 2D view and first person; the stops are now lifted to keep growing (the straight-down one stays fixed, to match the 2D view).
- **The last step is the real 2D view**, if the server allows 2D (`render3d.dynamic_view_2d`, on by default; a server that makes 3D mandatory stops
  the scroll at the straight-down view). The straight-down camera is set up to cover as many tiles as the 2D view does, and the 2D picture is
  faded in over it; at the very end the 3D view is switched off and the game shows its own 2D view, where the wheel still works and the
  game's camera keys turn the view. Scrolling back fades the 3D picture in again.
- Limitation: the wheel goes to whatever is under the mouse pointer. With the mouse captured (first and third person) that is wherever the
  pointer was left, so the wheel can land on a HUD element; the zoom keys always work.

## Status: what has been verified

Verification is by scripted play through the real viewport input path on the Dev map (see [Developer aids](#developer-aids)),
by tests, and by screenshots. **Not verified: feel with a physical mouse, and anything past short scripted sessions with
real human play.**

| Area | State |
|------|-------|
| Rendering: walls, floors, ceilings, space, fog, lights, FOV hiding, doors, windows, tables, wall decals, glowing layers | working |
| Camera: third-person spring arm, first person, yaw sync to the server, camera-relative movement | working, covered by unit + integration tests |
| Overhead camera (high, no ceiling, free cursor) | unit tests (camera geometry, wall-top ray casts, quad order, mouse capture); drawn and turned by the quarter-turn key in a local round. **Needs a human:** clicking, aiming and dragging with a real cursor, a rotating shuttle, doors and windows seen from above |
| The overhead camera (with free turn and tilt), the dynamic view, the tabbed settings window | unit tests (camera modes and their settings, the mapping of the old isometric / orbit / shoulder settings, the isometric heading, the dynamic view's stops, its steadily growing picture, its wheel steps and switches, the smooth curve and the damped glide, the turn fade-out); in a local round the cameras were drawn, the camera key went through them, the dynamic view was put at positions from third person to the classic 2D view (the cross-fade and the real 2D view were seen, and scrolling back), and the settings tabs were opened (a Debug build, about 4 FPS). **Needs a human:** the mouse wheel itself, the middle-mouse drag (in the overhead camera and the dynamic view) |
| Interaction: pick up, drop, throw, open lockers, doors, pull, cuff, melee, flash, gun firing at the crosshair, context menu, examine | each exercised live |
| HUD in 3D: health bars, status/sec HUD icons, speech bubbles, popups, map text | working |
| Enforcement (`render3d.enforced`), options tab, key rebinding | working |
| Performance | about 200-300 FPS uncapped at 1080p/900p on a Radeon RX 580; a 20-minute scripted soak showed no errors and flat memory (about 1.25 GB) |
| Tests | 898 unit tests and 31 `Render3D` integration tests pass (the 3D code's own are in the `Render3D` folders); the YAML linter is clean |
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

Fitting the 3D view to your own content (rules for the prototypes your fork added or renamed), hosting, and what to show new
players are in [ONBOARDING.md](ONBOARDING.md) and in `PORTING-YOUR-SERVER.md` in the docs folder of the SS12 repository.

## Settings reference

All client-side unless noted. Defaults in parentheses; most are also in Options > 3D.

| CVar | Meaning |
|------|---------|
| `render3d.enabled` (true) | Use the 3D view at all. |
| `render3d.enforced` (false, server, replicated) | Living players may not use 2D. |
| `render3d.yaw_send_rate` (12, client) | How many camera yaw updates per second the client sends while the mouse turns the view (4 to 30). Lower sends less over a lossy connection; WASD follows the new direction at this rate. |
| `render3d.settings_on_join` (0, server, replicated), `render3d.settings_shown` (false) | Opens the 3D settings window by itself when a player enters a round: 0 never, 1 the first time only (`settings_shown` remembers it on that computer), 2 every round. |
| `render3d.wall_height` (2.7), `eye_height` (0.9) | Tiles are 1 unit wide; the plan's 1.25 / 0.75 felt cramped, so rooms are 2.7 tiles tall (also a slider in the F11 window). |
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
| `render3d.overhead` (false) | Use the overhead camera (see [The overhead view](#the-overhead-view)); it takes the place of first and third person while on. |
| `render3d.overhead_pitch` (55, degrees), `overhead_distance` (9, tiles; 4 to 16), `overhead_fov` (45, degrees), `overhead_wall_height` (1.1, tiles) | The overhead camera: how far it looks down (dragging up and down changes it), how far it hangs from the character along the line of sight, its field of view, and the height walls have in this view. |
| `render3d.camera_extra` (0) | Which of the other cameras is on: 0 none, 4 the dynamic view (the older values 1 isometric and 2 free orbit now mean the overhead camera, 3 shoulder means third person). It wins over `overhead` and `first_person`; the camera key and the settings window set all three together, and a high camera leaves `first_person` as it was so leaving it returns to the walking camera you had. |
| `render3d.dynamic_view_u` (0.2), `dynamic_view_speed` (0.06), `dynamic_view_invert` (false), `dynamic_view_2d` (true) | The dynamic view: where the wheel left it (0 first person to 1 the classic 2D view), how far a notch goes, the wheel turned round, and whether the last step is the real 2D view. |
| `render3d.iso_pitch` (38, degrees), `iso_fov` (30, degrees) | The isometric step of the dynamic view: how far it looks down, and its field of view. |
| `render3d.pointlist` (true) | A small list, to the right, of what the crosshair points at: the thing under it first, then the others on the same spot (a pile on a table), with those out of reach greyed. Up / Down move a highlight, Space (or a click with `Alt` held) selects the row as the target and Space again acts on it like a left click (the target is let go once used, when you look away, after 8 seconds or beyond 9 tiles). Reachable loose things are listed first. The panel only shows while there is something to list. Key `L`; `render3d_pointlist [on\|off]`. |
| `render3d.minimap_mode` (1), `minimap_size` (200), `minimap_range` (20), `minimap_rotate` (true) | The minimap: 0 hidden, 1 small, 2 large (names of areas); side in pixels in a 720 pixel tall window (it scales with the window); tiles to the edge; whether it turns with the camera. Key `M` switches it on and off, `-` makes it small or large; `render3d_minimap [off\|small\|large]`. |
| `render3d.quality` (2), `render3d.auto_quality` (true), `render3d.auto_quality_fps` (45), `render3d.auto_lowered` (false) | Preset (0 low, 1 medium, 2 high, 3 custom), the automatic step-down, and whether the last lowering was the game's own (then the next round starts on High again). |
| `render3d.fx.bloom`, `fxaa`, `ao`, `surface`, `ambient`, `shadows`, `outline`, `sharp`, `grade`, `haze`, `sky`, `fixtures`, `glass`, `head_bob`, `item_lift`, `item_lean`, `item_thick`, `item_surface`, `item_spread`, `item_fixed`, `object_fixed`, `char_lean`, `char_thick`, `object_lean`, `object_thick` | The individual effects (see above). |
| `render3d.thickness_layers` (4) | How many stacked layers thick things are drawn with (0 = flat). The Low / Medium / High presets set 0 / 3 / 4. |
| `render3d.dev_channel` (false), `render3d.debug_view` (0; 1 ground capture, 2 light, 3 field of view, 41 billboard atlas, 42 its glow layer), `render3d.cap_hysteresis` (0.6), `render3d.dev_atlas_offset` (true) | Developer aids. `cap_hysteresis` is how much closer an entity drawn last frame counts when the nearest `billboard_cap` are picked (1 = no head start). |

## Graphics, and turning things off for older PCs

Every visual effect is its own on/off setting, so older machines can drop exactly what they cannot afford. Open the
settings window (`F11`) and use the **Graphics** tab: a quality preset (Low / Medium / High) plus one checkbox per
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
| `sky` | layered stars and a faint nebula; the game's own space background (the 2D parallax stars) shows through where there is no tile, instead of black | - | on | on |
| `fixtures` | wall lamps get a soft glow halo (they hang near the ceiling at every level) | - | on | on |
| `glass` | windows, window doors and grilles are drawn as see-through glass (a thin frame and a mostly transparent pane) instead of the sprite's top-down picture, which looked like a solid wall | on | on | on |
| `head_bob` | gentle bob while walking in first person | - | - | - |
| `item_lift` | items on the ground are lifted a little and get a contact shadow and a thin outline, so they read as objects and not as stains | - | on | on |
| `item_lean` | items on the ground tilt towards the camera (up to 20 degrees, none when looking straight down) | - | on | on |
| `item_thick` | items on the ground get thickness | - | on | on |
| `item_surface` | items rest on the real top of what they lie on (a table, a rack, a bed, a locker; see `surfaces` in `rules.yml`), are drawn after it so a rack or locker never hides them, and cast their shadow on it | on | on | on |
| `item_spread` | items that lie on the same spot are spread apart a little, inside their tile, so each can be seen (only in the picture) | on | on | on |
| `item_fixed` | items on the ground keep a fixed tilt (about 11 degrees towards the south) instead of tilting towards the camera | on | on | on |
| `object_fixed` | furniture and machines stay fixed in the world, facing the way they point, instead of turning to face the camera (a shape rule with `fixed: false` keeps a prototype turning, as trees, statues and potted plants do); characters and animals always turn | on | on | on |
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

Every round starts on the highest preset: a round is joined with a short message that the frame rate is being measured, and if the
game itself had lowered the graphics last time (`render3d.auto_lowered`) they are put back on High first. A preset or effect the
player chose themselves is never overridden. When it does lower the graphics, it says so on screen for a few seconds.

## Shape of things: lean and thickness

Sprites are flat pictures, so by default a thing on the floor looks like a sticker and is easy to mistake for a stain
(spills and decals are part of the floor image). The "shape" effects give things a body, each category on its own
switch (settings window, section **Shape of things**, or `render3d_fx <effect> on|off`):

| Category | Switches | What happens |
|----------|----------|--------------|
| Items on the ground (and thrown ones) | `item_lift`, `item_lean`, `item_thick`, `item_surface`, `item_spread` | lifted a little with a contact shadow and a thin outline; tilted up towards the camera by up to 20 degrees (less the more the camera looks down, and never so much that the far edge of a big sprite rises more than `render3d.item_max_rise`, 0.1 tile; 0 = no limit); drawn as a small solid slab; resting on the top of tables, racks, beds and lockers; piles spread apart |
| Characters (also lying bodies) | `char_lean`, `char_thick` | leaning back up to about 17 degrees so they are less squashed seen from above; a thin slab |
| Objects (machines, furniture, other standing things) | `object_lean`, `object_thick` | the same as characters |

Thickness is drawn by *sprite stacking*: copies of the sprite are stacked behind the front face along the face's normal,
darker with depth, so the sides of the silhouette show like the sides of a slab. `render3d.thickness_layers` sets the density
(up to 6 copies per step of it, more the thicker the thing is and the more sideways it is seen, so the copies never come apart
into slices); there are fewer beyond 9 tiles, none beyond 17 tiles, and at most 7000 extra quads per frame. The copies are drawn
without smoothing, because the smoothing filter leaves bright dots along each copy's edge. Only the front face is clickable
and outlined. Lean is a plain tilt of the sprite's quad about its near edge (flat things) or its foot (standing things);
picking uses the tilted quad, so it is easier to click.

The `glass` list of a `render3dRules` prototype says how windows, window doors and grilles are drawn when the `glass` effect is
on: a `tint` and an `alpha` for the pane (0 clear, 1 solid), a `frame` colour and `frameWidth` (pixels of the 32 pixel picture of
a tile), an optional fine `mesh` (spacing in pixels, for grilles) with its `meshColor` (may carry an alpha, `#rrggbbaa`), and
`shine` for the faint diagonal highlight. The picture is built from these and written into the sprite atlas in place of the
sprite; the layers of the sprite above the first one (the cracks of a damaged window) are still drawn over it. The nearest
ancestor wins, and `disabled: true` keeps the sprite (corner and diagonal windows). A window door that is open keeps its
sprite too. Prototypes the list does not name are drawn from their sprite as before.

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
  be open. Hold or tap `Alt` to see whether the free cursor is the cause. The game asks the window system for the captured mouse
  again and again after the window gets focus (a single request can be ignored), so alt+tab should not be needed; the console
  command `render3d_capture_state` says why the mouse is free.
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
| `Tools/render3d-dev/` | scripted-play helpers |

Upstream files with small hooks (3D picks, WorldToScreen for overlays, crosshair aiming, key bindings, options, ten base
prototypes carrying one `Render3D` component) are listed in [ONBOARDING.md](ONBOARDING.md).

## Developer aids

`render3d.dev_channel=true` makes the client poll `render3d_dev_<player name>.txt` in its user data folder and run each
line as a console command (start the client with `--cvar player.name=<name>` so the file name is predictable).

| Command | Purpose |
|---------|---------|
| `r3d_shot <name>` | PNG of the 3D view into `Screenshots/` |
| `r3d_look <yaw> <pitch>` / `r3d_look off`, `r3d_aim <x> <y>` | point the camera (degrees) or at a floor point |
| `r3d_cam tp\|fp\|oh\|dynamic` | camera mode (third person, first person, overhead, dynamic view) |
| `render3d_dynamic_view <0 to 1>` | put the dynamic view at a position (0 first person, 1 the classic 2D view) |
| `r3d_press <KeyFunction> [down\|up\|tap]` | synthetic input through the real viewport path (use `down`, wait, `up` for melee-class items) |
| `r3d_capture on\|off` | force the captured-mouse path without window focus |
| `render3d_tune` | open the tuning window: a slider, box or switch for every client-side `render3d.*` setting, applied at once; "Copy changes" copies the changed values |
| `r3d_atlas <name>` | save the billboard atlas and its glow layer as `atlas_<name>.png` / `atlasglow_<name>.png` |
| `r3d_audit <name>` | draw every entity of the next frame alone in a roomy cell, save the sheets as `audit_<name>_<n>.png` and log the atlas slot of each cell (art outside its slot is painted onto a neighbour in the real atlas) |
| `r3d_pick`, `r3d_dump` | what is under the crosshair; every drawn entity with its net id |
| `r3d_noself on\|off`, `r3d_glow on\|off` | exclude the own body from picks; compare with/without the glow atlas |
| `render3d_capture_state`, `render3d_pointlist_rows` | why the mouse is captured or free; the rows of the pointing list and where they are on screen |
| `render3d_fx <effect> on\|off`, `render3d_quality ...` | switch effects (also real player commands), handy for A/B screenshots |

`render3d.debug_view` selects visualisations (1 ground capture, 2 light, 3 FOV, 4 rays, 5 raw ground, 6 depth, 7 tile
flags, 8 light at hit, 9 tile map, 10 grid 0). With the dev channel on the client logs a `render3d: stats` line every
3 seconds (FPS and CPU milliseconds per stage). `Tools/render3d-dev/` holds the shell helpers; `soak.sh <seconds>` runs a
random walk/look/click soak and samples memory.

## Impostors (optional)

An entity can carry a pre-rendered 8-direction sprite (`impostor` on the `Render3D` component), which is shown in place of the
flat sprite when the entity is in its default state. The tool that was used to generate such sprites is an experiment that this
repository does not ship, and no prototype uses one by default.
