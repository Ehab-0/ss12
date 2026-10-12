# Which servers can get 3D?

*[Русская версия](ru/COMPATIBILITY.md)*

**Short answer:** any Space Station 14 server whose game engine is **version 264 or newer**. On engines 264 to 285 the
mouse works a little differently (see [below](#engines-264-to-285)). Older engines cannot, yet.

## How do I know my engine version?

Open the file `RobustToolbox/MSBuild/Robust.Engine.Version.props` in your server's code folder. The number inside
`<Version>` is the engine version. Or ask your server's lead developer: "which RobustToolbox version do we run?".

The installer also checks this for you and says so plainly. If it is too old, it **changes nothing**.

## Engines 264 to 285

These engines lack two things the 3D view uses on newer ones. The installer puts in versions of a few files made for
them; it picks them by itself from your engine version, and the summary lists them (`[engine]`).

- **The mouse is not captured.** Engines before 286 cannot hold the mouse inside the window. The cursor is hidden instead
  and works like a joystick: move it away from the cross in the middle and the view turns, faster the further out. In
  the small circle around the cross nothing turns. Hold the middle mouse button and drag to turn directly. After a menu,
  the free-mouse key (`Alt`) or another window, move the mouse back into the circle once and the view follows it again
  (a hint says so). In a window, the cursor can leave it and turning stops at the window's edge, so **fullscreen works
  best**. Speed, the size of the circle and looking up and down are in the 3D settings (`F11`).
- **What you cannot see is worked out another way.** Before 283 the engine does not hand out its field of view, so the
  3D view reads it from the 2D picture instead. You see exactly what a 2D player would see, as on newer engines.
- **Older names.** Older code has older names for a few base things (windows, airlocks, window doors, beds, signs). The
  3D rules know those names too.
- **If you update your engine later**, run `ss12 update`: it puts in the files for the new engine. `ss12 doctor` warns
  when they do not match.

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
| Stalker | 286 | Not tried. |
| Space Station 14 (Wizard's Den code as it was on 2026-06-25) | 277 | Works. Installed with the versions for older engines, built, the unit tests and 26 of 32 3D integration tests pass, launched: mouse steering and hiding what you cannot see checked in game. The six integration tests that fail check that the 3D rules name only prototypes that exist, and that code is older than some of the names. |
| Space Station 14 (Wizard's Den code as it was on 2025-07-23) | 264 | Works, with a caveat: it was built on .NET 10 with the newest packages on this computer instead of the exact ones engine 264 asks for (and the small engine changes later engines made for them), so it checks the 3D code against engine 264, not engine 264 itself. Installed with the versions for older engines, unit tests and 26 of 32 3D integration tests pass (the same six name checks as below fail), launched: mouse steering, hiding what you cannot see, doors, windows and lamps checked in game. Building it found four things that would have broken older forks, fixed since. |
| Delta-V, Goob Station, Einstein Engines, RMC14 | 264 to 275 | Should work now: their engines lie between the two tested above. **The forks themselves have not been built or launched yet.** |
| Floofstation and others | 239 | **Not possible yet**: the engine is too old. The installer refuses and changes nothing. |

Of the 18 most-played public servers we looked at, 14 had a readable version, and 5 of those run engine 286 or newer.
Most of the others run engines between 264 and 277, which the installer now accepts.

## Things to know

- **"The installer's changes all fit" is not the same as "it works".** It means the installer found the places it needs
  in that code. Building and launching is the real test; only the rows that say "Works" have had it.
- **Heavily customised servers** can have code that the installer cannot find an exact spot in. It then stops and says
  what is missing, changing nothing. You can fix that with an AI assistant: see [USE-WITH-AI.md](USE-WITH-AI.md).
- **Release versus test builds.** The launch tests used a test ("debug") build. Release builds are what players get;
  they are normally *less* strict, but a release build of each server has not been launched.
- **Computers.** The author tested on Windows. On Linux, the release build starts a packaged server and waits until it
  accepts connections (see `SS12-server-linux-x64.zip`), and the installer runs there. No person has yet played the Linux
  game window, and Mac has not been tried at all.

If you try it on a server not listed here, please tell us how it went (works, or the report file if it did not): open
an [issue](https://github.com/Ehab-0/ss12/issues/new/choose) and choose "I tested 3D on a server".
