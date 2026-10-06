# Which servers can get 3D?

*[Русская версия](ru/COMPATIBILITY.md)*

**Short answer:** any Space Station 14 server whose game engine is **version 286 or newer**. Older ones cannot, yet.

## How do I know my engine version?

Open the file `RobustToolbox/MSBuild/Robust.Engine.Version.props` in your server's code folder. The number inside
`<Version>` is the engine version. Or ask your server's lead developer: "which RobustToolbox version do we run?".

The installer also checks this for you and says so plainly. If it is too old, it **changes nothing**.

## What was actually tried

Everything below was tried on real code in October 2026 with this project's installer (version 1.0.0).
"Launched" means a server and a 3D game window were started from the converted code, connected, and showed the game.

| Server code | Engine | Result |
|-------------|--------|--------|
| Space Station 14 (Wizard's Den, the version its public servers ran on 2026-10-02) | 291 | Works. Converted, built, packaged into a server build, launched in 3D; the server hands out the 3D game files. Removing 3D gave back the exact original code. Full walk-through: [the worked example](EXAMPLE-WIZARDS-DEN.md). The official launcher joined a local copy of the packaged server and downloaded the 3D game from it (not tried over the internet). |
| Starlight | 290 | Works. Converted at the exact version the public server runs, built, launched. |
| Russian Marine Corps | 290 | Works. Converted at the exact version the public server runs, built, launched and played. Update and check also pass. |
| Frontier Station | 290 | The installer's changes all fit. Not built or launched. |
| Sector Frontier, Funky Station | unknown | The installer's changes all fit. Not built or launched. |
| Stalker | 286 | Exactly the minimum. Not tried. |
| Delta-V, Einstein Engines, Goob Station, Floofstation, RMC14 and others | 239 to 277 | **Not possible yet**: the engine is too old. The installer refuses and changes nothing. |

Of the 18 most-played public servers we looked at, 14 had a readable version, and 5 of those run engine 286 or newer. The
rest are on older engines, because their teams have not updated yet. When they do (they all do eventually), 3D can be
added.

## Things to know

- **"The installer's changes all fit" is not the same as "it works".** It means the installer found the places it needs
  in that code. Building and launching is the real test; only the first three rows have had it.
- **Heavily customised servers** can have code that the installer cannot find an exact spot in. It then stops and says
  what is missing, changing nothing. You can fix that with an AI assistant: see [USE-WITH-AI.md](USE-WITH-AI.md).
- **Release versus test builds.** The launch tests used a test ("debug") build. Release builds are what players get;
  they are normally *less* strict, but a release build of each server has not been launched.
- **Computers.** The author tested on Windows. On Linux, the release build starts a packaged server and waits until it
  accepts connections (see `SS12-server-linux-x64.zip`), and the installer runs there. No person has yet played the Linux
  game window, and Mac has not been tried at all.

If you try it on a server not listed here, please tell us how it went (works, or the report file if it did not): open
an [issue](https://github.com/Ehab-0/ss12/issues/new/choose) and choose "I tested 3D on a server".
