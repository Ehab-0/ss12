# Changelog

*[Русская версия](ru/CHANGELOG.md)*

## Unreleased
- **Turning the view sends fewer updates to the server.** While you turn with the mouse, the client told the server the
  new direction up to 30 times a second, and each of those is a message that must arrive in order. On a connection that
  drops packets, every lost one holds up the messages behind it, including your movement keys. The client now sends at
  most 12 a second by default (`render3d.yaw_send_rate`, 4 to 30; WASD follows the new direction at that rate). This cuts
  the traffic to less than half; it is meant to help with a character that stops for a moment and snaps forward on a
  bad connection, but it has not been shown to cure that by itself.
- **Far things no longer blink in and out while you walk.** Only the nearest few hundred entities are drawn (200 on Low, 400
  on the other presets), and the distance of the last one that makes it changes as you move or as lag nudges your
  position, so everything near that distance, such as lamps and litter at the far end of a corridor, dropped out and came
  back from frame to frame. An entity that was drawn last frame now counts as closer when the cut is made
  (`render3d.cap_hysteresis`). In a test walk the number of entities that dropped out and returned fell from about 105 to
  about 0 at the strongest setting; the default is gentler, so things at the very edge may appear a few tiles later.
- **A server can open the graphics settings for players when they join.** The new server setting
  `render3d.settings_on_join` opens the 3D settings window (the one on `F11`) by itself when a player enters a round:
  `0` never (the default, so nothing changes for existing servers), `1` the first time only on each computer, `2` every
  round. It lets new players choose a quality level before the game picks one for them.
- **Wall lamps hang near the ceiling at every quality level, and wall-mounted things draw in a stable order.** Lamps were
  only hung up there when the "fixtures" effect was on (it is off in the Low preset, which slow computers fall back to)
  and when a wall was found behind them (lamps on windows and grilles have none). Otherwise they were drawn like
  posters at eye level, so their glowing tube sat just above the floor, where the thin strip popped in and out as you
  moved and showed up over the feet of your character. Lamps now always hang near the ceiling; the "fixtures" effect
  only adds the glow. Separately, upright quads (wall lamps, windows, doors, posters, characters) are now ordered by their
  distance on the ground plane instead of the depth of their 3D centre, so two quads on the same wall at different
  heights no longer swap places when the camera pitches.
- **W A S D follow the camera on every server setup.** With the installer's preset (`render3d.enforced = false` and
  `shuttle.camera_rotation_locked = true`) the server threw away the camera direction, so the camera turned while the
  keys kept walking along the map axes (inverted in some directions, fine in others). The lock only switches off the 90
  degree camera-rotate keys, so the camera direction is now always accepted. `W` walks where the crosshair points.
  Servers on an older version can set `shuttle.camera_rotation_locked = false` as a workaround.

## 1.2.0
Linux support.
- **Linux demo.** `SS12-demo-linux.zip` is a complete copy of the game with the 3D view for 64-bit Linux. Unzip it and
  run `./play-demo.sh`: it starts a private server on your computer and opens the game, like the Windows demo.
- **Linux server.** `SS12-server-linux-x64.zip` is a ready-to-run server (upstream game plus 3D, packaged so players need
  only the normal launcher), with `run-server.sh`, an example config and a systemd unit.
- **What was checked.** The release build starts the finished Linux server from the unzipped download and waits until it
  accepts connections. The Linux game window itself has not been opened by anyone yet, so treat the Linux client as
  untested.
- **Windows and grilles no longer flicker when you move.** A window tile is drawn as a glass box with four faces, and the
  far faces were seen through the near ones, so every window pattern appeared twice and the two copies shimmered against
  each other. Only the faces turned towards the camera are drawn now. A window and the grille in the same tile, and other
  things on the same plane, are also drawn in a fixed order (by the sprite's draw depth) instead of an arbitrary one.

## 1.1.0
Things on the floor no longer look like stickers.
- **Items stand out from spills.** Items lying on the ground are lifted a little and get a soft contact shadow and a thin
  outline, so a crowbar reads as a crowbar and a puddle still reads as a puddle.
- **Thickness.** Items, characters and objects (machines, furniture) are drawn with a body: copies of the sprite are
  stacked behind the front, a little darker with depth, so the silhouette gets sides like a small slab.
- **Lean.** Items on the ground tilt up towards the camera (up to 20 degrees, none when looking straight down), and
  characters and objects lean back slightly, so top-down art is easier to read from a low camera.
- **Each category has its own switches** in the settings window (`F11`, section "Shape of things") and in
  `render3d_fx`: `item_lift`, `item_lean`, `item_thick`, `char_lean`, `char_thick`, `object_lean`, `object_thick`,
  plus the setting `render3d.thickness_layers`. Low turns them all off, Medium keeps the item effects and character lean,
  High turns everything on.
- **Per-item rules.** The new `shapes` list in `Resources/Prototypes/Render3D/rules.yml` sets thickness and lean for a
  prototype or component (paper and ID cards stay paper thin, toolboxes are thicker).
- Only the front face of a thick thing can be clicked, and the tilted face is what you aim at.

## 1.0.2
Picture quality and camera fixes, found by watching real footage.
- **No more crawling dot pattern.** Things very close to the camera (windows, walls, tables) used to fade out by throwing
  away every other pixel, which showed as a screen-door pattern that crawled when you moved. They now fade smoothly.
- **Smoother, shimmer-free picture.** New setting **Smoothing (supersampling)**: the picture is drawn larger and averaged
  down, which removes shimmer and jagged edges on every surface at once. On by default on High (2.0), 1.5 on Medium,
  off on Low; limited automatically on very large screens. Costs frame rate, so the automatic quality step-down lowers it first.
- **The camera no longer goes through windows** (it used to end up outside the station, in the black), keeps a gap to
  walls, and returns smoothly instead of popping when you turn.
- **Ceiling panel seams** follow the walls of a rotated station instead of running diagonally.
- **Lamp glow** fades out when the camera is close instead of becoming a huge disc.
- **No black holes:** new effect "Dim light in places you cannot see" gives surfaces the 2D line of sight leaves unlit a
  faint light (things there stay hidden). On by default on Medium and High.
- `ss12 package` builds with HybridACZ; the installer checks that the engine's submodules were downloaded, and a failed
  build during install no longer blocks `uninstall`.

## 1.0.1
- Worked example for Wizard's Den (`docs/EXAMPLE-WIZARDS-DEN.md`).
- `ss12 package` now builds with HybridACZ (the server hands the 3D client to joining players) and works on Windows.
- The installer warns when the engine's submodules were not downloaded, and a failed install build no longer blocks `uninstall`.
- Russian translation of the guides (`docs/ru/`, `README.ru.md`).

## 1.0.0
First public release.
- 3D view (third and first person) for Space Station 14, with mouse look, crosshair aiming and all normal interactions.
- Installer that adds 3D to a Space Station 14 server's code (engine 286 or newer), with check, update, remove.
- AI assistant mode (`ss12 mcp`): connect an MCP-capable assistant to adapt 3D to heavily customised servers, with previews before every change.
- Visual extras (glow, soft corner shadows, contact shadows, outlines, sharper sprites, haze, starry sky, lamp glow),
  each switchable, with Low / Medium / High presets and automatic lowering on slow computers.
- Settings window (`F11`), tested on current upstream, Starlight and a Marine Corps codebase.
