# Add 3D to your own server

*[Русская версия](ru/INSTALL-ON-YOUR-SERVER.md)*

This page is for people who run a Space Station 14 server and want to offer it in 3D. You do **not** need to be a
programmer. If you can already build and host your server, you can do this too.

**What happens:** a small helper program adds the 3D files to your server's folder and changes about nine lines in
existing files. It tells you everything it does, it can undo all of it, and it never sends anything anywhere.

---

## Before you start (2 minutes)

You need three things:

1. **Your server's code folder.** It is the folder you get when you download or clone your server's source code. If you
   open it you should see folders named `Content.Client`, `Content.Server` and `Content.Shared`.
2. **A fairly recent version of that code.** The game engine inside it must be version **286 or newer**. To check, open
   the file `RobustToolbox/MSBuild/Robust.Engine.Version.props` in a text editor: the number after `<Version>` is the
   engine version. If it is lower than 286, the helper will say so and **change nothing**. (See
   [COMPATIBILITY](COMPATIBILITY.md) for which popular servers qualify.)
3. **A backup or a git copy.** If your code lives in git (most do), the helper makes its own branch and leaves your
   main work alone. If it does not, make a copy of the folder first. Removing 3D later restores your files exactly,
   but a copy is a good habit.

You do **not** need to install anything else to run the helper (to build and test the result you need git and the
.NET 10 SDK, which you already have if you build your server).

**Want to see it done first?** [Worked example: turning Wizard's Den into SS12](EXAMPLE-WIZARDS-DEN.md) shows every
step on a real codebase, with what the screen said.

**Cloned your code with git?** Make sure the engine parts came with it: run `git submodule update --init --recursive`
once in your server's folder. The helper checks this and tells you if they are missing.

---

## Step by step (Windows)

1. Go to the [Releases page](https://github.com/Ehab-0/ss12/releases) of this project and download **`SS12-installer-windows.zip`**.
2. Unzip it anywhere (for example your Desktop). You will see a file called **`Install 3D.bat`**.
3. **Drag your server's code folder onto `Install 3D.bat`.** (Or double-click it and pick the folder in the window
   that opens.)
4. A black window opens and asks what you want to do. Type **1** and press Enter.
5. It first shows a list of what it **would** change, and nothing is changed yet. Read it if you like, then type
   **y** to go ahead.
6. When it finishes it says **"3D is installed"**. The window also tells you the next steps. A file called
   `ss12-report.txt` is saved in your server folder: keep it, it is useful if you need help.

**Mac or Linux:** download `SS12-installer-linux.zip` (or `-mac`), unzip it, open a terminal in that folder and run
`./install-ss12.sh /path/to/your/server/folder`.

### If the helper says it cannot do it

It stops *before* changing anything and explains why in plain words. The usual reasons:

| What it says | What it means | What to do |
|--------------|---------------|-----------|
| "Engine ... is older than 286" | your server's game engine is too old for 3D | update your server to a newer engine version first, as you would for any update |
| "A required edit cannot be applied automatically" | your server changed a part of the code the helper needs to touch | send us the report file (see [Troubleshooting](TROUBLESHOOTING.md)); it is a ten-minute fix on our side |
| "does not look like a Space Station 14 codebase" | you picked the wrong folder | pick the folder that contains `Content.Client` |

---

## After installing: put it online

3D is now part of your server's code. Do what you normally do to publish a new version of your server.

Running it for real players? [Porting your server](PORTING-YOUR-SERVER.md) covers what comes next: teaching the 3D view about
the things your fork renamed or added, hosting and restarting without kicking anyone, and welcoming players.

- **Players need nothing.** They join with the normal launcher and the 3D game downloads with the rest of your server.
- **To make 3D mandatory** for everyone, run the helper with the "mandatory" choice, or set `render3d.enforced = true`
  in your server settings. Otherwise each player can switch with `F12`.
- **Your server settings file** gets a ready-made snippet in `Resources/ConfigPresets/Build/render3d.toml`. Copy its
  three lines into your server configuration.

If you use the official build script (`Content.Packaging`), the helper has a shortcut: choose "Build a package" in the
menu, or run `ss12 package <your folder>`.

### Just want a Linux server to try?

Each release has **`SS12-server-linux-x64.zip`**: upstream Space Station 14 with 3D already in it, packaged for players
who use the normal launcher. It needs no .NET and no building:

```bash
unzip SS12-server-linux-x64.zip -d ss12-server
cd ss12-server
./run-server.sh
```

It reads `server_config.toml` (created from `server_config.example.toml` on the first start), listens on port 1212
(TCP and UDP), and includes `ss12.service` for systemd. The release build starts this server on Linux and waits until it
accepts connections; nobody has joined a Linux-hosted SS12 server with the real game yet. Details are in the `README.md`
inside the zip.

---

## Trying it before you commit to it

- Pick **"Check"** (choice 2) any time to see if everything is still in place.
- Run the helper once with **"Show what would change"** (it is the default first step): it changes nothing.
- Install it on a **copy** of your code to try it.
- Not happy? Pick **"Remove 3D"** (choice 3). Your files go back exactly as they were.

---

## Updating later

When a new version of this project comes out, download it and run the same helper again, this time choosing
**"Update"**. Your own changes are kept; the helper only touches its own files and the nine small edits.

---

## Questions we get

**Will my players need to do anything?** No. They just join your server.

**Does it change my game rules or balance?** No.

**Does it send data anywhere?** No. The helper only reads and writes files in the folder you give it. It never
connects to the internet and never uploads or pushes anything.

**Can I see exactly what it changed?** Yes. If your code is in git, run `git show --stat` on the new commit. The nine
edited files are listed in [FOR-DEVELOPERS](FOR-DEVELOPERS.md).
