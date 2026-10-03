#!/usr/bin/env bash
# Starts the SS12 game server (Space Station 14 with the 3D view) from this folder.
# Settings: server_config.toml (copied from server_config.example.toml the first time).
# Extra arguments are passed to the server, for example:  ./run-server.sh --cvar game.map=Saltern

set -eu
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$here"

if [ ! -f server/Robust.Server ]; then
    echo "I cannot find server/Robust.Server. Unzip the whole download and run this script from the unzipped folder." >&2
    exit 1
fi
chmod +x server/Robust.Server 2>/dev/null || true

if [ ! -f server_config.toml ]; then
    cp server_config.example.toml server_config.toml
    echo "Created server_config.toml from the example. Edit it to change the name, port and rules."
fi

mkdir -p data
cd server
exec ./Robust.Server --config-file "$here/server_config.toml" --data-dir "$here/data" "$@"
