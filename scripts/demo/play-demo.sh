#!/usr/bin/env bash
# Starts a private SS12 server on this computer and joins it with the 3D client.
# Linux version of "Play 3D Demo.bat" / play-demo.ps1. Nothing here talks to the internet.

set -u
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$here" || exit 1

server_exe="$here/bin/Content.Server/Content.Server"
client_exe="$here/bin/Content.Client/Content.Client"
if [ ! -f "$server_exe" ] || [ ! -f "$client_exe" ]; then
    echo
    echo " I cannot find the game files. Please unzip the whole download first (unzip SS12-demo-linux.zip),"
    echo " then run ./play-demo.sh from the unzipped folder."
    exit 1
fi
# some unzip tools drop the executable bit
chmod +x "$server_exe" "$client_exe" 2>/dev/null || true

data="$here/data"
mkdir -p "$data/server" "$data/client"
server_log="$data/server.log"
rm -f "$server_log"

echo
echo " Starting a small private game on your computer. This takes about a minute the first time."
echo " (The game server runs in the background and stops when you close the game.)"
echo

# only this computer can reach the demo server
(
    cd "$(dirname "$server_exe")" || exit 1
    exec "$server_exe" \
        --data-dir "$data/server" \
        --cvar net.bindto=127.0.0.1 \
        --cvar status.bind=127.0.0.1:1212 \
        --cvar game.lobbyenabled=false \
        --cvar game.map=Saltern \
        --cvar game.defaultpreset=Sandbox
) > "$server_log" 2>&1 &
server_pid=$!

cleanup() {
    if kill -0 "$server_pid" 2>/dev/null; then
        echo " Closing the private game server..."
        kill "$server_pid" 2>/dev/null
        for _ in 1 2 3 4 5 6 7 8 9 10; do
            kill -0 "$server_pid" 2>/dev/null || break
            sleep 0.5
        done
        kill -9 "$server_pid" 2>/dev/null || true
    fi
}
trap cleanup EXIT
trap 'exit 130' INT TERM

ready=0
for _ in $(seq 1 180); do
    kill -0 "$server_pid" 2>/dev/null || break
    if grep -qF -- '-> Ready' "$server_log" 2>/dev/null; then ready=1; break; fi
    sleep 1
done

if [ "$ready" = 1 ]; then
    # the server answers on port 1212 once it accepts players
    ready=0
    for _ in $(seq 1 60); do
        kill -0 "$server_pid" 2>/dev/null || break
        if (exec 3<>/dev/tcp/127.0.0.1/1212) 2>/dev/null; then ready=1; break; fi
        sleep 1
    done
fi

if [ "$ready" != 1 ]; then
    echo " The game server did not start. Details are in:"
    echo "   $server_log"
    echo " Please attach that file if you ask for help."
    exit 1
fi

echo " Ready. Opening the game..."
echo
echo " Quick controls:  W A S D  walk      mouse  look around      N  first / third person"
echo "                  F11  graphics settings (turn things off if it is slow)      F12  3D on / off"
echo

(
    cd "$(dirname "$client_exe")" || exit 1
    "$client_exe" --connect --connect-address 127.0.0.1 --username Visitor
)
status=$?
if [ "$status" != 0 ]; then
    echo
    echo " The game window closed with an error (code $status). The 3D client needs a desktop session"
    echo " and a graphics driver with OpenGL 3.3. Server log: $server_log"
fi
echo " Thanks for trying it!"
