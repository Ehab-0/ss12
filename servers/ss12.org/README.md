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

## What the changes do

- **Welcome pages.** The first-join popup and the Rules and Info window show an SS12 page instead of the Wizard's Den
  rules: the controls first (with the keys each player has bound), then what the server is, links and credits. The
  Guidebook's first page (the one that opens by itself for new players) and the Tutorial introduction are rewritten the
  same way, and the chat greetings say SS12.
- **External links.** Links that start with `https://` in the Rules window and the Guidebook open in the browser (they
  only followed guide entries before).
- **Lobby background.** One background, drawn by `tools/make_lobby_bg.py`. The scene is kept inside the left 72% of the
  image, because the lobby draws its chat panel over the right side and crops a little from the top and bottom.

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
round once the server has been up for an hour. Staging a file never disturbs the running server.

## Notes

- The patch is pinned to one upstream commit. For another upstream version, regenerate it: apply the changes by hand to
  a checkout of that version and run `git diff` on the files listed in the patch.
- `tools/make_lobby_bg.py <ss14 checkout> <output.png>` needs Pillow (`pip install pillow`) and uses the Boxfont Round
  font from the checkout.
