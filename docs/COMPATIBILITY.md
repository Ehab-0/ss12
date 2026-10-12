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

## Supported engine versions

| Engine | | Servers on it (public server list, 12 October 2026) |
|--------|---|------------------------------------------------------|
| 292 | tested, played | Goob Station (Sigma, Basileus), Corvax Вега |
| 291 | tested, played | Wizard's Den Lizard |
| 290 | tested, played | Starlight, Russian Marine Corps, Colonial Marines Universe |
| 289 | tested | Время Приключений, Wizard's Den Leviathan |
| 286 to 288 | supported | Misfits, S.T.A.L.K.E.R. Zona 14 |
| 278 to 285 | supported | |
| 277 | tested, played | LUST STATION, Рыбья станция, SS220 |
| 276 | tested | Мёртвый космос |
| 275 | tested | Euphoria Station, Delta-V |
| 274 | tested | Hardlight Sector |
| 271 to 273 | supported | |
| 270 | tested | ARCANE, Omu Station, CorvaxGoob, RatBite |
| 268 | tested | Funky Station |
| 267 | tested | Frontier, Lone Star |
| 265, 266 | supported | |
| 264 | tested, played | Rouny's Marine Corps and Space Stories (their engine `264.0.2-fix-physics`; RMC-14's own code was built with 3D) |
| 263 and older | **not supported** | Floofstation (239) |

*Tested*: SS12 was installed into Space Station 14 code on that engine, built, and its tests were run. *Played*: also
launched and checked in game. *Supported*: the installer accepts it, and it lies between tested versions. A fork can
still have changed a spot the installer needs; it then says where and changes nothing.

## What was actually tried

Everything below was tried on real code in October 2026. "Launched" means a server and a 3D game window were started
from the converted code, connected, and showed the game. "The tests" are the 3D unit tests (659) and the 3D
integration tests (32). Six of the integration tests check that the 3D rules name only prototypes that exist in that
codebase; on code older than some of the names (before about August 2026) those six fail, as expected, and the rules
for the older names (`aliases:`) take over.

| Server code | Engine | Result |
|-------------|--------|--------|
| Space Station 14 (Wizard's Den, the version its public servers ran on 2026-10-02) | 291 | Works. Converted, built, packaged into a server build, launched in 3D; the server hands out the 3D game files. Removing 3D gave back the exact original code. Full walk-through: [the worked example](EXAMPLE-WIZARDS-DEN.md). The official launcher joined a local copy of the packaged server and downloaded the 3D game from it (not tried over the internet). |
| Space Station 14 code of 2026-09-30 on engine 292 | 292 | Works. Built, all the tests pass, launched and walked. Engine 292 keeps the smoothed position of moving things apart from the simulated one, so on 292 the 3D view draws them where the 2D view does (a version of one file for 292 and newer). That code is older than engine 292, so three spots in it that 292 changed were adjusted by hand for the test; a real 292 codebase has them already. |
| Space Station 14 code of 2026-09-13 | 289 | Works. Built, the tests pass (30 of 32: two of the name checks). |
| Starlight | 290 | Works. Converted at the exact version the public server runs, built, launched. |
| Russian Marine Corps | 290 | Works. Converted at the exact version the public server runs, built, launched and played. Update and check also pass. |
| Space Station 14 code of 2026-06-25 | 277.2.1 | Works. Built, the tests pass (26 of 32), launched: mouse steering and hiding what you cannot see checked in game. |
| Space Station 14 code of 2026-05-09 | 277.0.0 | Works. Built, the tests pass (26 of 32). |
| Space Station 14 code of 2026-05-01 | 276 | Works. Built, the tests pass (26 of 32). |
| Space Station 14 code of 2026-03-29 | 275.2.1 | Works. Built, the tests pass (26 of 32). |
| Space Station 14 code of 2026-03-17 | 274 | Works. Built, the tests pass (26 of 32). |
| Space Station 14 code of 2026-01-01 | 270.1.0 | Works. Built, the tests pass (26 of 32). Built with the next version of one engine package (SDL bindings) and the two-line engine change that came with it. |
| Space Station 14 code of 2025-11-13 | 268 | Works\*. Built, the tests pass (26 of 32). |
| Space Station 14 code of 2025-10-14 | 267.3.0 | Works\*. Built, the tests pass (26 of 32). |
| Space Station 14 code of 2025-07-23 | 264.0.0 | Works\*. Built, the tests pass (26 of 32), launched: mouse steering, hiding what you cannot see, doors, windows and lamps checked in game. Building and launching it found four things that would have broken older forks, fixed since. |
| RMC-14 (Rouny's Marine Corps, Space Stories; their code of 2026-10-01) | 264.0.2-fix-physics | Client and server build with 3D; every edit applies (16 files). Found one thing in the 3D code that their code needed, fixed since. Not launched: that needs their game resources, which were not downloaded. Their engine is engine 264 with three small bug fixes. |
| Frontier Station | 267 | The installer's changes all fit. Not built or launched. |
| Sector Frontier | unknown | The installer's changes all fit. Not built or launched. |
| Floofstation and others | 239 | **Not possible yet**: the engine is too old. The installer refuses and changes nothing. |

\* Engines up to 268 build with .NET 9. These were built with .NET 10 and newer packages standing in for the exact ones
(and the small engine changes later engines made for them), so they check the 3D code against that engine's API and
client sandbox, not that engine build itself.

Every one of the most-played servers on the public list runs engine 264 or newer: on 12 October 2026 that was 1,579 of
the 1,584 players online (the server of the other 5 did not say its engine). The forks themselves, apart from RMC-14,
have not been built with 3D; their engines are covered by the tests above.

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
