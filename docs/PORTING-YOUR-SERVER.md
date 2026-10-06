# Porting your server: what we learned running ss12.org

*[Русская версия](ru/PORTING-YOUR-SERVER.md)*

[Install it on your server](INSTALL-ON-YOUR-SERVER.md) gets 3D into your code and builds it. This page is about what comes
after: making it look right with **your** content, running it well, and welcoming players who have never seen it. It is
everything we ran into while turning a copy of Wizard's Den into the public test server `lol.ss12.org`, written down so you
do not have to find it out again.

Five stages, in order. You can stop after any of them: after stage 1 you already have a working 3D server.

| Stage | What | Time |
|-------|------|------|
| 1. Install | The installer puts 3D in your code and builds it | an hour, mostly building |
| 2. Tune | Teach the 3D view about the things *your* fork added or renamed | an afternoon |
| 3. Host | Packaging, configuration, a server that restarts without kicking anyone | an evening |
| 4. Welcome | Tell players about the mouse, the keys and the list, before they ask | an hour |
| 5. Stay current | Update SS12 and your own code without losing your tuning | minutes, every update |

> **What this is based on.** One server (`lol.ss12.org`: Wizard's Den commit `fb9401cf`, engine 291) that we ran and played
> on for weeks, plus launches of Starlight and Russian Marine Corps ([COMPATIBILITY](COMPATIBILITY.md)). We have **not**
> run a full tuning pass on any other fork. Where this page says "your fork may differ", it means it.

---

## Stage 1: Install

Follow [Install it on your server](INSTALL-ON-YOUR-SERVER.md) (or the [worked example](EXAMPLE-WIZARDS-DEN.md)). Three things
from our own mistakes:

- **Clone with the engine parts** (`git submodule update --init --recursive`). Without them nothing builds and the errors are
  hundreds of lines of noise.
- **Launch the client, do not stop at "it builds".** The client runs in a sandbox that rejects some code only when the game
  starts, never at build time. `ss12 doctor --build` compiles; only starting a client and joining a server proves it.
- **Close the game and the server before you build.** Their files are locked and the build fails with copy errors. On Windows,
  packaging also leaves the `bin` folders in a state the next normal build chokes on: run `dotnet restore` and rebuild the
  client and the server before you run the game again.

---

## Stage 2: Tune the 3D view for your content

The 3D view draws every entity from its components and its sprite, so a fork's new content mostly just works. What it cannot
know is *what a thing is*: a table, a window, a chair, a tree. For the base prototypes of upstream it has rules. For the ones your
fork renamed or invented, it guesses, and the guesses are the things players notice first.

### Put your rules in your own file

Everything is steered by `render3dRules` prototypes in `Resources/Prototypes/Render3D/`. **All** of them are read, so:

- **Do not edit the shipped `rules.yml`.** `ss12 update` replaces it, and refuses to touch it at all if you changed it ("already
  exists and was not installed by this tool").
- Make `Resources/Prototypes/Render3D/yourserver.yml` with its own id and put your rules there. Unknown prototype names are ignored,
  so a rule for something that does not exist in your fork is harmless.

```yaml
- type: render3dRules
  id: YourServerRules
  rules: []     # what a thing is (see below)
  shapes: []    # thickness, leaning, whether it faces the camera
  surfaces: []  # how high the top of a table-like thing is
  glass: []     # how windows are drawn
```

Names are matched against the prototype **and every prototype it inherits from**, and the nearest ancestor wins. So you rarely list
a hundred things: list the base prototype of your fork's chairs, windows or tables.

### Step 1: find out which names broke

**Quick, no build:** `ss12 doctor path/to/your/server` has a "Your content" section. It reads the rule files and lists every
prototype they name that your codebase does not define. (On upstream it lists nothing.)

**Thorough:** the 3D code ships integration tests that check every prototype name in `rules.yml` exists in the codebase. On a fork they are your
first to-do list: each failure is a base prototype that was renamed or removed.

```bash
dotnet test Content.IntegrationTests --filter "FullyQualifiedName~Render3D" -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed NUnit.NumberOfTestWorkers=3
```

A failing name is not an error in your fork. Find the new name, add a rule for it in your own file. (The test that checks that doors,
windows and tables *inherit* from the upstream base names will also fail if your fork reorganised them; read that one as
information.) We have not run this on a fork ourselves.

### Step 2: walk the station

Start a client with the developer channel (`--cvar render3d.dev_channel=true`; the commands are listed in
[docs/ss12/README.md](../overlay/docs/ss12/README.md#developer-aids)) and walk every department looking for the symptoms below.
Two commands do most of the work: **`r3d_pick`** names what is under the crosshair, with its prototype id (that is what goes into a rule), and **`render3d_tune`**
opens a window with a control for every setting, applied at once.

Look in **lit** rooms. A room without power shows nothing useful and tempts you to "fix" things that are only dark. And look at
the default third-person distance: indoors the camera cannot rise above the ceiling, so a far-away camera hides problems.

| You see | Why | What to write in your file |
|---------|-----|----------------------------|
| Items float above a table-like thing, or sink into it | A surface your fork added is lower or higher than a table | `surfaces:` with `parents:` and `height:` (tiles above the floor). Anything with the `PlaceableSurface` component already counts as a surface; a rule only sets the height. Beds are 0.28, sinks 0.5 |
| Furniture or a machine turns as you walk round it | That is intended for things with a front: they stay put in the world. It is wrong for round things | `shapes:` with `fixed: false` for trees, statues, plants, barrels, anything without a front |
| A machine shows its back, or faces a wall | Machines that never rotate in the game (vending machines) cannot tell the 3D view where their front is, so it looks at the one wall beside them and faces away from it | Nothing to write when it works. With walls on two sides, or none, there is no clear front and it keeps turning towards you; `fixed: false` makes that the rule |
| Something you can pick up but that stands upright (a potted plant) lies flat on the floor | Everything you can pick up is drawn as a loose item | `rules:` with `mode: Billboard` and `parents:` |
| A door or shutter type of your fork is drawn like any other object | Its base prototype is not in the shipped rules | `rules:` with `mode: Panel` (airlocks, shutters) |
| A wall-mounted thing (poster, sign, light) stands out from the wall | Your fork's wallmount base is not in the shipped rules | `rules:` with `mode: WallDecal` |
| Windows look like a solid wall, or a window door vanishes | Windows are drawn by the 3D view itself (frame, pane, grille), from rules; unlisted ones keep their sprite | `glass:` with `tint`, `alpha`, `frame`, `frameWidth`; `bar: true` for a window door; `disabled: true` for corner and diagonal windows, whose sprite is not a plain pane |
| Chairs look like planks, paper like cards | Default thickness for objects is 0.14 tile, for items 0.06 | `shapes:` with `thickness:` in tiles (0 = paper thin). We use 0.05 for seats and 0.01 for paper |
| Items on top of an open locker | An open locker or crate (`EntityStorage`) is not a surface: what was in it lies on the floor in it. This is automatic | A fork with its own storage component is not covered; tell us |

A rule is one short block. For example, a fork whose custom chairs inherit `ChairBase` and whose potted plants are `FloraPotted`:

```yaml
- type: render3dRules
  id: YourServerRules
  rules:
  - mode: Billboard
    parents: [FloraPotted]
  shapes:
  - parents: [ChairBase]
    thickness: 0.05
  - parents: [FloraPotted]
    fixed: false
```

Prefer the nearest base prototype. Listing leaf prototypes one by one is how a rule file becomes unmaintainable.

### Step 3: check the code your fork added around it

The installer edits a handful of existing places. A fork often has **more** places that do the same thing, and the installer
cannot know them:

- Anything that turns the mouse position into a world position (`_eyeManager.PixelToMap(... MouseScreenPosition)`) aims with the
  real cursor, which in 3D is not where you are looking. The installer rewrites the usual forms, and `ss12 doctor` lists the ones
  it left ("Your content", file and line). A custom targeting UI that still uses the old form will aim at the wrong place; change
  it to `Content.Client.Render3D.Render3DPointer.PixelToMap(eyeManager, inputManager.MouseScreenPosition)`. Other ways of finding the
  mouse in the world (a different manager, your own helper) it cannot see: search for them.
- Client overlays that cull by the camera's view box. One fork asked for it every frame and the 3D view's corners are not a valid
  box for every heading, which stopped a debug build on an engine assertion
  ([COMPAT.md](../overlay/docs/ss12/COMPAT.md#engine-calls-that-forks-make-which-the-3d-view-had-to-answer)). If a debug build stops on an assertion
  the first time you load a map, look there first.
- Things that open a window when you use them (consoles, vending machines): the 3D view moves such a window next to the cursor
  so it appears where the mouse comes back to. A fork with its own window system may need a look.

### Step 4: run the linter and the tests

Run the YAML linter (`Content.YAMLLinter`) from a **Release** build; in the optimised debug configuration an assertion stops it
for reasons that have nothing to do with you. Then the Render3D tests from step 1 again.

---

## Stage 3: Host it

### Build a package players can join

```bash
ss12 package path/to/your/server -- server --platform linux-x64 --hybrid-acz --no-wipe-release
```

(Without the part after `--` it builds for the computer you are on.) **HybridACZ** means your *server* hands the game files to joining
players, so you need no separate download site. The price: every new player downloads the whole client from your server's own
connection, and many players joining at once can use up a small host's bandwidth. The server zip of the Wizard's Den example was about 250 MB.

### Server configuration

The installer writes `Resources/ConfigPresets/Build/render3d.toml`; apply it (its sections go into your `server_config.toml`).
The three settings that matter:

| CVar | Why |
|------|-----|
| `render3d.enforced` | `true` forces 3D on living players; otherwise each player chooses with `F12`. We left it off on the test server so people can compare |
| `physics.relative_movement` | `true`: WASD goes where the camera looks |
| `shuttle.camera_rotation_locked` | the camera-rotate keys make no sense in 3D |

Our public server's whole configuration is in [`servers/ss12.org/deploy/server_config.toml`](../servers/ss12.org/deploy/server_config.toml),
as an example of "Wizard's Den's base configuration minus what only applies to their official servers" (IPIntel contact,
panic bunker, their privacy policy). Check every line before reusing it: the host name, address and `hub.advertise` are ours.

**A name that says 3D.** Players pick a server from a list, so say it in the name: we use `[3D] ...` at the front. Emoji work in the
host name. If you spell a word with the letter emoji (the "regional indicators"), **put a space between the letters**: two next to
each other fuse into a country flag.

**`admin.admins_count_in_playercount = true`** makes the player count the server reports include admins. The restart script below
trusts that number, so an admin alone in the round must count as "somebody is here".

### A small, cheap host

The test server runs on a small machine, so it is set up to cost less per tick. These are the things that made the difference,
and they are not 3D-specific:

- **Fewer entities.** One mining asteroid was about 50 000 of a round's 81 000 entities and most of the 16 to 25 seconds the server
  needed to generate the round's extra grids. We spawn two wrecks and one ruin instead of 12 to 16 and two, no asteroid, no salvage
  magnet or expeditions, and keep only the four lightest maps in the pool.
- **`npc.enabled = false`** (creature AI off), `net.tickrate = 30`.
- **Do not restart often.** After every restart the first player who joins waits while the game compiles its code.

The files are in [`servers/ss12.org/`](../servers/ss12.org/README.md); the map and station changes are a patch against Wizard's Den
`fb9401cf`, so take it as a list of what to change, not as something to apply to a different version.

### Restarts that never kick anyone

The rule we run by: **never restart or deploy a server while anyone is connected.** Staging a build is free; installing it is
what kicks people.

[`servers/ss12.org/deploy/`](../servers/ss12.org/deploy/) has the pieces, small and plain:

- `ss14.service`: the systemd unit. `Restart=on-failure` is worth having: once, in weeks, a fresh build crashed at its very first
  start (an engine race while it froze the prototypes) and the second start was clean: systemd restarted it by itself.
- `ss14-restart.timer` runs `ss14-restart-if-empty.sh` every 10 minutes. The script does nothing while any player is connected.
  When the server is empty it installs a build staged as `pending/server.zip` (keeping the old one as `server_prev`, your
  rollback) and a configuration staged as `pending/server_config.toml` (checked to be valid TOML first, the old one kept as
  `.prev`). With nothing staged it restarts for a fresh round after three hours of uptime.
- If the server's status endpoint stops answering (it can wedge when many client downloads are abandoned half way), the script
  counts the players from the log instead and restarts a wedged server as soon as the count is zero.

To use it: change the paths, the user and the port in the three files, install them, and deploy by **staging**:

```bash
scp server.zip host:/tmp/server.zip.part
ssh host 'sudo mv /tmp/server.zip.part /opt/ss14/pending/server.zip'     # a rename, so the timer never sees half a file
```

Never put passwords, keys or tokens into files in your repository. The files above contain none and your own should not either.

---

## Stage 4: Welcome your players

What we saw players trip on, in order of how often:

1. **"The mouse is stuck / I can't click the menu."** In 3D the mouse looks around. Hold **`Alt`** to get a cursor. It frees itself
   whenever a window or a chat box opens, and the game takes it back afterwards (it asks the window system repeatedly for a while after you
   alt+tab, because one request can be ignored).
2. **"What am I pointing at, there are three things stacked here."** `L` opens a list of what the crosshair is over; `Up`/`Down` and
   `Space` select, `Space` again acts on it.
3. **"Where is the old view / the settings?"** `F12` and `F11`. The game also lowers its own graphics if the frame rate is low, and
   says so.

Put those on the first screen a player sees:

- **The lobby background.** We draw one that lists the keys, with the free-mouse key highlighted. `servers/ss12.org/tools/make_lobby_bg.py`
  draws it in code (Pillow, and the Boxfont Round font from your checkout), so when a key changes you change one table and run it
  again. Keep the picture inside the **left 72%**: the lobby draws its chat panel over the right side, crops a little from the top
  and bottom, and puts its own buttons top left and its credits bottom left.
- **The rules window and the first guidebook page.** We replaced both with a page that starts with the controls (reading the keys each
  player actually has bound), then what the server is. New players read these.
- **The chat greeting.**

Our versions are in the `ss12.org.patch`. They name our server and quote our rules, so write your own and borrow the structure.

---

## Stage 5: Stay current

- **Updating SS12:** download the new release, run `ss12 update path/to/your/server`, then `ss12 doctor path/to/your/server --build`.
  It replaces its own files only. Your `yourserver.yml` is not one of them.
- **Merging your upstream:** only about ten of your files carry a 3D edit, each a single added call. If one conflicts, keep both sides
  and run `ss12 doctor`.
- **After an engine update,** launch the client again. The sandbox rules change with the engine and only a launch shows it.
- **Tell us what you found.** If a rule you needed is one the shipped file should have, or a fork pattern the installer could not
  cope with, open an [issue](https://github.com/Ehab-0/ss12/issues/new/choose) ("I tested 3D on a server") with the `ss12-report.txt`.

---

## Before you announce it

- [ ] Client launched against your packaged server, not just a local debug build
- [ ] Render3D tests and the YAML linter pass, or each failure is understood
- [ ] You walked every department in lit rooms and wrote rules for what looked wrong
- [ ] `render3d.enforced`, `physics.relative_movement` and the hub settings are what you want
- [ ] The server name says 3D; the lobby and the first page tell players about `Alt`, `L`, `F11` and `F12`
- [ ] Builds are staged and installed only when the server is empty, and you know where the rollback is
- [ ] A player on a weak computer has tried it (the game steps down its own graphics, but you should have seen it)

**Not verified by us, so please try it before you promise anything:** joining over the internet from the public server list, Linux and
Mac game windows, and any fork other than the ones in [COMPATIBILITY](COMPATIBILITY.md).
