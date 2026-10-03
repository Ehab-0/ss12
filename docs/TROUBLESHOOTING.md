# Troubleshooting

*[Русская версия](ru/TROUBLESHOOTING.md)*

Find your problem below. Every answer is in plain words. If yours is not here, see
[Getting help](#getting-help) at the bottom.

---

## Playing

### The game is slow or choppy
1. Press **`F11`**.
2. Set **Quality preset** to **Low**.
3. Still slow? Move **Render scale** down a bit (for example to 0.75).

The game also does this by itself after a few seconds of slow running, and shows a message when it does. You can turn
that off in the same window if you do not want it.

### The mouse will not turn the camera
- Click once inside the game window so it knows you are using it.
- The mouse is freed while a menu, a window or the chat box is open. Close them (or press `Esc`) and it comes back.
- Holding `Alt` also frees the mouse on purpose.

### I cannot click on things in a menu
Hold **`Alt`** while you click. That frees the mouse. When you let go, the mouse turns the camera again.

### Everything looks too dark
The game uses the station's real lights, so rooms without power are dark. Press **`F12`** (if the server allows it) to
compare with the old flat view. If a room is only dark in 3D, tell us which one.

### W A S D do not go where I am looking (sometimes inverted)
This was a bug in versions before the next release, in a server set up with the installer's configuration preset
(`render3d.enforced = false` together with `shuttle.camera_rotation_locked = true`): the server ignored the camera
direction your game sent, so your game moved you one way and the server another, and the direction flipped depending on
where you faced. **Fix:** update the 3D files on the server (`ss12 update`) and publish a new build. A quick workaround on
an old install is to set `shuttle.camera_rotation_locked = false` in the server's config and restart; after updating you
can set it back to `true` (it only switches off the 90 degree camera-rotate keys). `W` walks the way the crosshair points,
`S` backwards, `A` and `D` sideways.

### I want the old flat view back
Press **`F12`**. Some servers make 3D mandatory while you are alive, and then `F12` only works if you are a ghost.

### The picture is black, or the game closes when it starts
- Your graphics card may be too old (it must support OpenGL 3.3). Update your graphics driver first.
- Try the demo or server with the flat view: set the setting `render3d.enabled` to `false` (or ask the server owner).

### A key does nothing
Another program may be using it. Open **`F11`** (3D settings), scroll to **Keys**, click the key and press a new one.

---

## Trying the demo

### The demo window does not open
- Make sure you **unzipped** the whole download. Do not run it from inside the zip file.
- If Windows shows "Windows protected your PC", click **More info** then **Run anyway**. (The program is not signed
  because this is a small fan project. You can check the source code in this repository.)
- If an antivirus program blocks it, allow it, or tell us which one so we can look.

### The server window says something about a port being in use
Another copy of the demo (or of a game server) is already running. Close all black windows from the demo and try again.

---

## Installing on a server

### "Engine ... is older than 286"
Your server's game engine is too old for 3D (mouse-look needs a feature added in engine 286). Update your server's code
to a newer version first, the way you would for any update, then run the helper again. Nothing was changed.

### "A required edit cannot be applied automatically"
Your server changed a spot in its code that the helper needs. Nothing was changed. Attach the report file
(`ss12-report.txt`, saved in your server folder) to a new [issue](https://github.com/Ehab-0/ss12/issues/new/choose) and we will add support. It
usually takes a few lines. If you want to fix it yourself right now, let an AI assistant do it:
[Use it with an AI assistant](USE-WITH-AI.md).

### "does not look like a Space Station 14 codebase"
You picked the wrong folder. Pick the one that **contains** the folders `Content.Client`, `Content.Server` and
`Content.Shared`.

### "The git working tree has uncommitted changes"
The helper wants a clean starting point so you can undo it. Commit or stash your changes, or make a copy of the folder
and install into the copy.

### "Parts of the game engine were not downloaded"
Your code was cloned without the engine's own parts (git calls them submodules), so it cannot be built. In your
server's folder run `git submodule update --init --recursive` once, then try again.

### "Packaging failed" or "cannot copy ... used by another process" (Windows)
Fixed in this version: update the helper (download the newest release). Older versions ran the packaging tool in a
way Windows locks.

### The build failed after installing
Choose **"Check"** (option 2) in the helper and read what it says. If it lists compile errors, attach the report file
to an issue. The 3D files are in place but not committed yet. You can also choose **"Remove 3D"** (option 3): your
server goes back exactly as it was.

### I installed it but my players do not see 3D
- Did you publish a new build of your server after installing? 3D is part of the server's code, so a new build is needed.
- Players using an old cached version may need to restart their launcher once.

---

## Getting help

1. Open an issue: **[New issue](https://github.com/Ehab-0/ss12/issues/new/choose)**.
2. Say, in your own words, what you did and what you saw. You do not need technical words.
3. Attach `ss12-report.txt` if you have one (the helper writes it into your server folder), or the screenshot.

Never share passwords or private server addresses. The report file contains none; it lists versions, file names and
messages.
