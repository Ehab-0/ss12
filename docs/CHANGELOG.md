# Changelog

*[Русская версия](ru/CHANGELOG.md)*

## Unreleased
- **Documentation cleanup.** The default git branch of the install is `ss12` (several pages said `3d`), the installer's own paths in
  the pages that ship into a server (`Tools/ss12/...`) now describe the real installer (`ss12 install ...`), links to files that were
  never shipped are gone, the numbers in the Wizard's Den example are the current ones (97 files added, 10 edits in 9 files, 108 in
  the commit), the README controls table no longer lists the minimap twice, and the thickness, test and mouse-capture notes are up
  to date.
- **Potted plants stand up.** A potted plant can be picked up, so the 3D view treated it as an item and drew it lying flat on the floor.
  It is now drawn standing, like the rest of the furniture (it keeps turning to face you, since a plant has no front).
- **What lies in an open locker lies on the floor.** An open locker, closet or crate counted as a surface, so its contents were
  drawn standing on top of it. An open one is no longer a surface: what is in it is on the floor, drawn in front of it.
- **Machines face the right way.** Vending machines and similar machines never rotate in the game, so the fixed card faced south
  whatever wall they stood against. They now face away from the wall they stand against (when exactly one side has a wall);
  things that can rotate, such as chairs, face the way they are rotated. Seen from behind, a fixed thing shows its back (the north
  picture of a chair is its back) instead of its front mirrored.
- **Menus open next to the cursor.** A window that opens because you used something (a vending machine, a console) is moved next to
  the cursor, which is where the mouse comes back to, instead of a fixed place on the screen.
- **Window doors are easier to read.** A brighter, thicker frame and a push bar across the middle, and an open window door is drawn
  as its frame only, so the doorway no longer vanishes when it opens (`bar: true` in the glass rules of `rules.yml`).
- **Chairs are thinner.** Chairs, stools and benches get a thin slab (0.05 tile) instead of the plank-like default.
- **The server's name** starts with "[3D]" and spells SURPRISE in letter emoji.
- **`ss12 doctor` checks your content.** A new "Your content" section lists the prototypes the 3D rules name that your codebase does not
  define (a fork that renamed them), and the places that still aim with the real cursor (`_eyeManager.PixelToMap(... MouseScreenPosition)`)
  after the install. Warnings only: they never make the doctor fail, and a codebase that matches upstream gets none. The install's
  last lines now point to your own rules file.
- **A guide for server owners: [Porting your server](PORTING-YOUR-SERVER.md).** What we learned turning Wizard's Den into the test
  server: putting your own rules in your own file (updates leave it alone), the symptoms to look for on a fork and the rule that fixes
  each, packaging and server settings, restarts that never kick anyone, and what to show new players.
- **Clicking a row of the list works.** With `Alt` held the engine does not send a plain click: Alt + left button is its own
  binding and wins over the plain one, so the row never saw a click. It now takes that one as a click too.
- **After alt+tab the mouse is taken back.** The cursor could stay free, and the camera not follow the mouse, until another
  alt+tab. The game asked the window system once for the captured mouse and never again, so a request it ignored in the moment the
  window got focus was lost. The request is now repeated for a while after the window gets focus and every second after that, the
  state of the free-mouse key is forgotten on every focus change, and a chat box that kept the keyboard focus is released. The
  console command `render3d_capture_state` says why the mouse is free, if it ever is.
- **Thick things look solid.** Chairs, machines, characters and items get their depth from copies of the sprite behind its face, and
  at an angle the copies came apart into slices with bright dots along the edges. The number of copies now follows the angle and the
  distance (up to 6 per step of the thickness setting), and they are drawn without smoothing.
- **Furniture and machines stay where they are.** They no longer turn to face the camera as you walk round them: a vending machine
  shows its face from the front and its edge from the side. Floor items keep a fixed tilt instead of tilting towards the camera.
  Characters and animals still turn; trees, statues and potted plants too (`fixed: false` in the shape rules of `rules.yml`). New
  effects `object_fixed` and `item_fixed`, on at every quality level.
- **Items rest on what they lie on.** A big sprite such as a bedsheet or a pile of clothes tilted towards the camera by up to
  20 degrees, which lifted its far edge by a third of a tile and made it hover over the table. The far edge of a lying item now
  rises by at most 0.1 tile (`render3d.item_max_rise`, 0 = no limit), so a small item still tilts fully and a big one hardly at
  all. Items also rest on the real top of what they lie on: a table as before, a bed lower (0.28), a sink at 0.5, a locker or a
  crate as high as its picture, and a rack is drawn as a table top so what lies on it is seen. Where a rule is missing the
  `surfaces` list of `rules.yml` names the height. The new `item_surface` effect (on at every quality level) also draws an item
  after the surface it lies on, so a rack or a locker no longer hides it, and puts its shadow on the surface.
- **Items piled on one spot are spread apart.** A pile of clothes, a tray of tools or the contents of an emptied box all land on
  one spot, so each hid the one below it. Items closer than about a quarter tile are now moved apart inside their tile, only in
  the picture, always the same way for the same item (`item_spread`, on at every quality level).
- **A list of what you point at (`L`), and you can choose from it.** A small panel to the right of the view lists what the
  crosshair points at: the thing under it first, then the others that lie on the same spot, such as a pile on a table, with those
  out of reach greyed and equal names counted. Up and down move a highlight, `Space` selects the highlighted row and `Space` again acts on it as a left click would; with the
  free-mouse key (Alt) held a row can be clicked. The chosen row is the target for your next action: use, attack, pull, the name under the crosshair
  and the outline all go to it, until you have used it, you look away from it, a few seconds have passed, it is further than 9 tiles
  or gone, or you choose it again. What you can touch is listed first and loose things (items) come before fixtures (windows,
  tables). The panel only shows while there is something to list, is on by default and says how to choose and "Press L to toggle";
  `L`, a checkbox in the `F11` view section or `render3d_pointlist` switch it.
- **Holding Alt to free the mouse is easier to find.** The lobby picture shows it highlighted, the key is called "Free mouse
  (Hold)" in amber in the `F11` keys section, and the README and the demo guide print the row in bold.

## 1.3.0
See-through windows, a minimap, graphics that start high and step down by themselves, and many fixes to how things are drawn.
- **Objects next to the camera no longer leave a smeared band across the view.** The shader fades out things very close to the
  camera, by the depth of each pixel. The thickness layers of an object lie behind its front face, so they are further from the
  camera and faded less: when you stood next to a vending machine or a disposal unit its face went see-through and its layers
  showed as a long green or yellow band over the screen. Objects closer than about one tile to the camera now get no layers.
- **A minimap to find the way.** A small map of the station sits in the top left corner of the 3D view: floors, walls and doors
  from the navigation data the game already sends every client for the station map on the wall, centred on you, with an arrow for
  where you are and which way you face, and an "N" for north. It turns with the camera, so up on the map is where you look. It
  shows nothing else, in particular no other players. The `M` key switches it on and off, and the `-` key makes it small or
  large (about two and a half times as much of the station, with the names of the areas); the 3D settings window has a switch, a size slider and a range, and an option
  to keep north up (`render3d.minimap_mode`, `minimap_size`, `minimap_range`, `minimap_rotate`, `render3d_minimap`). The small
  map follows the size of the window (the setting is its side in a 720 pixel tall window) and never takes more than about a third
  of it. It is hidden
  in space, where there is no grid to map.
- **Every round starts on the highest graphics, and says so.** The automatic step-down already measured the frame rate and
  lowered the preset when it was too low, but what it lowered was saved, so a player whose computer struggled once started every
  later round on low. Now, when the game itself lowered the graphics last time, the next round puts them back on High before it
  measures again, and a message at the start tells the player the frame rate is being measured and the graphics may lower by
  themselves. A preset or effect the player chose in the settings is never overridden, and the message when the graphics are
  lowered stays on screen longer. The new `render3d.auto_lowered` setting remembers whether the last lowering was the game's.
- **Docking airlocks fill their doorway.** The docking airlock has a clamp layer one tile below the door, so its sprite is two
  tiles tall. The whole picture was put on the one-tile doorway, which squeezed the door into the top half and left the rest of
  the opening empty. Doors, windows and panels now show only their own tile of the picture.
- **Security cameras hang on their wall.** A camera is rotated to point at its wall, and the 3D view only looked for a wall behind
  a wall-mounted thing, so it never found it and drew every camera at the open edge of its tile, floating a tile from the wall.
  It now looks in front as well, hangs the camera on that wall facing the room, and puts cameras near the ceiling like lamps.
- **Windows are see-through glass in the 3D view.** The picture of a window in the game is a top-down drawing of a bevelled
  frame around a small pane. Stuck on a standing face (as before) about 62% of it was solid frame, and a grille under every window
  tile added a dark mesh, so reinforced windows read as walls and hid what 2D players could see through them. Edge windows and
  window doors were worse: their picture is a thin strip, which showed as a sliver. With the new `glass` effect (on at every quality
  level) the 3D view draws windows, window doors and grilles itself: a thin frame and a mostly transparent pane tinted for the
  type (plain, reinforced, plasma, uranium, shuttle, tinted, frosted and so on), a faint highlight, and a sparse mesh for grilles.
  The cracks of a damaged window are still drawn over it, an open window door keeps its sprite, and corner and diagonal windows
  keep theirs. The colours and thickness live in the new `glass` list of `rules.yml`. Switch it off in the 3D settings
  ("See-through glass in windows and grilles") or with `render3d.fx.glass`.
- **The default wall height is 2.3 tiles (was 1.6).** Players who never changed `render3d.wall_height` get the taller rooms;
  anyone who set their own value keeps it. The wall picture repeats a whole number of times as the walls get taller, choosing
  the count whose repeats are closest to the original proportions.
- **The 3D settings window (`F11`) is reorganised, and has a wall height slider.** It now opens with a **General** section:
  use the 3D view, the quality preset, the wall height (new, 1 to 4 tiles, the same as `render3d.wall_height`) and the field of
  view. The other sections follow in this order: **Keys**, **View**, **Graphics**, **Shape of things**.
- **A tuning window for developers (`render3d_tune`).** It lists every client-side `render3d.*` setting with a slider, a box
  to type an exact value, or a switch, and applies each change at once, so a value such as the ceiling height can be found by
  looking at the game. It picks up new settings by itself, has a filter box, "Reset all" (back to the values from when it
  opened) and "Copy changes" (the changed values as `name = value` lines, on the clipboard and in the log).
- **A taller roof no longer stretches the wall picture.** The wall picture covers 1.6 tiles of height; when `render3d.wall_height`
  is raised, the picture is now repeated a whole number of times (2 at 3.2) instead of being stretched, so the pixels stay
  square. Nothing changes at the default height. Doors, windows and other panels that are drawn as one picture still stretch to
  the new height.
- **Wall equipment, characters lying down and rotated sprites no longer paint onto their neighbours in the atlas, and only real
  lamps hang at the ceiling.** The slot of a sprite in the atlas now follows how the engine really draws it: the sprite's own
  rotation (which a lying character has) and the "never rotates" and "snaps to quarter turns" flags are counted as well as its
  offset. The same offset gap also hit security cameras, the APC, air alarms, station maps and potted plants (their art spilled
  a few pixels onto the next slot), so the first fix cured those too. Separately, anything with a point light was treated as a
  lamp, so an APC was hung up at the ceiling with a glow halo; now only light fixtures (a bulb in a socket, an emergency light) are.
  A new developer command, `r3d_audit <name>`, draws every entity of a frame alone in a roomy cell, saves the sheet to
  `Screenshots/` and logs the slot reserved for each cell, so spills can be measured. Run over the 2,650 prototypes the pool
  maps use and the station around the spawn, no sprite lay outside its slot. Lying characters could not be exercised in that
  run, only checked by unit tests.
- **The 3D view is never added twice.** A gameplay screen that is loaded again (seen once after a respawn through the lobby) now
  drops any 3D view its host still holds before it adds a new one, so two views cannot end up side by side. The original
  report could not be reproduced here, so this guards the most likely cause; forcing the screen to reload several times
  left a single view.
- **Glowing lamp tubes no longer appear on characters, items and walls that have no lamp.** The 3D view draws every sprite into
  a shared picture (the atlas) and gives each one a slot sized from the sprite's bounds. Those bounds leave out the offset of the
  sprite itself, and wall lamps have one (`offset: 0, 1`: their art is drawn one tile from the lamp so that it lands on the
  wall). The lamp's art was therefore painted one tile outside its own slot, on top of whatever sat next to it in the atlas: a
  glowing tube with a dark plate showed up on a character's shoulder, lay on the floor on top of a loose item, or hung on another
  wall fixture, and it jumped to a different thing whenever the nearest-first order of the atlas changed (that is, whenever you
  walked), while the lamp's own place showed only its glow. The slot now covers the sprite's offset (`AtlasBounds`), and art that
  is larger than one slot is left out instead of spilling. The developer view `render3d.debug_view` 41 and 42 show the atlas and
  its glow layer.
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
