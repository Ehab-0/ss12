# The ss12.org test server

*[Русская версия](README.ru.md)*

This folder is what runs the public SS12 test server (`lol.ss12.org`): a copy of Wizard's Den with SS12 installed, plus a
few changes that only make sense for that one server. **None of this is part of the SS12 installer.** Servers that
install SS12 never get these files; it is here so the test server can be rebuilt from this repository.

## What is here

| Path | What |
|------|------|
| `ss12.org.patch` | The changes to existing files, made against Wizard's Den commit `fb9401cf` (engine 291). |
| `files/` | New binary files the patch cannot carry (the lobby background). |
| `apply.sh` | Applies the patch and the files to a checkout that already has SS12 installed. |
| `deploy/` | The server configuration and the systemd units and script used on the host. |
| `tools/make_lobby_bg.py` | Draws the lobby background in code (no third-party art). |
| `deploy/analytics/` | Simple private stats for the website and the server (see "Simple stats" below). |

## What the changes do

- **Welcome pages.** The first-join popup and the Rules and Info window show an SS12 page instead of the Wizard's Den
  rules: the controls first (with the keys each player has bound), then what the server is, links and credits. The
  Guidebook's first page (the one that opens by itself for new players) and the Tutorial introduction are rewritten the
  same way, and the chat greetings say SS12.
- **External links.** Links that start with `https://` in the Rules window and the Guidebook open in the browser (they
  only followed guide entries before).
- **Lobby background.** One background, drawn by `tools/make_lobby_bg.py`: the title, a table of the controls (walk, look,
  first / third person, hold Alt to free the mouse, minimap, 3D settings, 3D / flat view), three short tips and the three
  characters along the bottom. The scene is kept inside the left 72% of the image, because the lobby draws its chat panel over
  the right side, crops a little from the top and bottom and puts its own buttons in the top left and its credits in the
  bottom left corner. When a key changes, change the table in the script and draw it again.

## Using it

```bash
ss12 install path/to/space-station-14        # SS12 first
servers/ss12.org/apply.sh path/to/space-station-14
ss12 package path/to/space-station-14
```

Then use `deploy/server_config.toml` as the server configuration. It is the Wizard's Den base configuration without the
parts that only apply to the official servers (IPIntel contact, panic bunker, their privacy policy), plus the SS12
settings. Check it before you reuse it: the host name, the address and `hub.advertise` are this server's.

## Restarts that never kick anyone

`deploy/ss14-restart-if-empty.sh` is run every 10 minutes by `ss14-restart.timer`. It does nothing while any player is
connected (the server's reported player count includes admins, so `admin.admins_count_in_playercount = true` is set).
When the server is empty it installs a new build if one is staged in `/opt/ss14/pending/server.zip` (the old build is
kept in `/opt/ss14/server_prev`) and a new configuration if one is staged in `/opt/ss14/pending/server_config.toml` (checked
to be valid TOML first; the old one is kept as `server_config.toml.prev`). With nothing staged it restarts for a fresh
round once the server has been up for three hours (each restart makes the first player who joins wait while the game compiles its code). Staging a file never disturbs the running server. If the server's status endpoint stops answering (it can wedge when many
client downloads are abandoned half way), the script counts the players from the server log instead and restarts the
wedged server as soon as that count is zero. A server that started less than three minutes ago is left alone, since it has not opened the endpoint yet.

## The lighter configuration

The test server runs on a small machine, so it is set up to cost less per tick: `Resources/Prototypes/Maps/Pools/default.yml`
(part of `ss12.org.patch`) keeps only the four maps with the fewest entities (Elkridge, Packed, Exo, Snowball),
`Resources/Prototypes/Entities/Stations/base.yml` spawns 2 space wrecks and 1 ruin around the station instead of 12 to 16 and 2
and no mining asteroid (the asteroid alone was about 50 000 of the round's 81 000 entities and most of the 16 to 25 seconds of
server time it took to generate the round's extra grids), `nanotrasen.yml` leaves the salvage magnet, expeditions and job
board out of the station, and the four pool maps have no Salvage Specialist slots, so there is no mining at all,
`deploy/server_config.toml` turns creature AI off (`npc.enabled = false`), and the restart script waits until the server has
been up for three hours before it restarts an empty server for a fresh round.

## Simple stats

`deploy/analytics/` counts visits to the website and players on the server without any third-party service, cookies or
extra daemons.

- **Website.** The web server (Caddy) writes an access log with cookies and credentials removed and every visitor address
  cut to its network part (`/24` for IPv4, `/48` for IPv6). The page's `script.js` sends a tiny request to `/a/<name>` when a
  visitor copies the server address, opens a screenshot, or follows the GitHub or Space Station 14 links; the log line is
  the count. Browsers that send Do Not Track or Global Privacy Control are left out of every count.
- **Server.** `ss12-stats sample` (run every minute by `ss12-stats-sample.timer`) records the player count from the
  status endpoint. `ss12-stats report` adds, from the game's own database (opened read only): players per day, new players,
  joins, rounds and the average play time, leaving out the account names listed in `exclude_names`.
- **What is kept.** The website log holds the time, the page, the browser's name string, the referring site and the visitor's
  address cut to its network part (for example `203.0.113.0`); there is no country lookup and no cookie. The server numbers
  use only the account id, account name and time from the game's database, and the report prints counts, never names or
  addresses. (The game itself keeps full addresses in its own database and log, as every SS14 server does; the stats do not
  read or show them.)
- **Reading it.** `ss12-stats report [--days N]` prints a summary. `ss12-stats-report.timer` also writes an hourly page
  that Caddy serves at `/stats/` behind a password (`Caddyfile.example` shows how; make the hash with
  `caddy hash-password`).
- **Setting up.** Run `deploy/analytics/install.sh` as root on the server, merge `Caddyfile.example` into
  `/etc/caddy/Caddyfile` (replace `STATS_PASSWORD_HASH`) and copy the page's `script.js`. `ss12-stats selftest` checks the
  script on made-up data.

The page says how the counting works (a short privacy note in its footer); keep that note in step with these settings.

## Notes

- The patch is pinned to one upstream commit. For another upstream version, regenerate it: apply the changes by hand to
  a checkout of that version and run `git diff` on the files listed in the patch.
- `tools/make_lobby_bg.py <ss14 checkout> <output.png>` needs Pillow (`pip install pillow`) and uses the Boxfont Round
  font from the checkout.
