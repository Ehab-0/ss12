# render3d dev scripts

Bash helpers (Git Bash on Windows) for driving a client started with `--cvar render3d.dev_channel=true --cvar player.name=<name>`:

- `cmd.sh "<console command>" ...` runs commands in the client (`CLIENT=<name>` selects it).
- `shot.sh <name>` takes a screenshot with the game itself and copies it to `$R3D_LOGS/shots/`.
- `aimrel.sh dx dy` aims the crosshair at a floor point relative to the player; `aimproto.sh <ProtoId>` aims at an entity.

Commands available through the channel are listed in docs/ss12/README.md ("Developer aids"). Set `R3D_DATA` to the client's
user data directory if it is not `$APPDATA/Space Station 14/data`.
