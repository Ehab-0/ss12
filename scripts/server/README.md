# SS12 server for Linux

*[Русская версия](README.ru.md)*

A ready-to-run Space Station 14 game server with the SS12 3D view, for Linux (64-bit Intel/AMD). It is the upstream game
at the commit named in the release notes, packaged with HybridACZ: players join with the normal launcher and the 3D
client downloads from your server by itself.

You need a recent 64-bit Linux with `glibc`, `unzip`, and a free TCP/UDP port (1212 by default). You do **not** need to
install .NET: it is inside the download.

## Start it

```bash
unzip SS12-server-linux-x64.zip -d ss12-server
cd ss12-server
./run-server.sh
```

The first start creates `server_config.toml` from `server_config.example.toml` and a `data/` folder next to it. Edit the
config (name, port, rules) and restart. Stop the server with `Ctrl+C`.

## Run it as a service

`ss12.service` is a systemd unit. The commands are at the top of the file. It expects the folder at `/opt/ss12` and runs
as a user called `ss12`.

## Join it

Open the Space Station 14 launcher, choose *Direct connect*, and enter `your-address:1212`. Use `127.0.0.1:1212` from
the same computer. For a public server also set `status.connectaddress` in the config so the launcher can find it.

## What is verified

The release workflow starts this exact server on a Linux runner and waits until it reports that it is ready and accepts
connections. Nobody has joined a Linux-hosted SS12 server with a real client yet; if you do, please tell us in an
[issue](https://github.com/Ehab-0/ss12/issues/new/choose).
