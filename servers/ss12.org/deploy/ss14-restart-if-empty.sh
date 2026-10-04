#!/bin/bash
# Run every 10 minutes by ss14-restart.timer. Only ever acts when nobody is connected.
#  - A new build staged in /opt/ss14/pending/server.zip is installed (the old one is kept in /opt/ss14/server_prev).
#  - A new configuration staged in /opt/ss14/pending/server_config.toml is installed (the old one is kept as
#    server_config.toml.prev) after it has been checked to be valid TOML.
#  - Either of those restarts the server. With nothing staged, the server is restarted for a fresh round once it has been
#    up for at least three hours (every restart makes the first player who joins wait while the game compiles its code).
#  - If the server's status endpoint does not answer at all (it can wedge when many client downloads are abandoned half way),
#    the players are counted from the server log instead, and a wedged server is restarted as soon as that count is zero.
#    A server that started less than three minutes ago is left alone, because it has not opened the endpoint yet.
PENDING_DIR=/opt/ss14/pending
PENDING_BUILD=$PENDING_DIR/server.zip
PENDING_CFG=$PENDING_DIR/server_config.toml
CFG=/opt/ss14/server_config.toml

# Players according to the server's status endpoint; empty when it does not answer.
players_status() {
    curl -s -m 8 http://127.0.0.1:1212/status | grep -o '"players":[0-9]*' | grep -o '[0-9]*$'
}

# Sessions that were approved since the service started and have not been disconnected, from the log.
# Every connection has its own "address:port", so an approval is matched with the disconnect of the same endpoint.
players_log() {
    local start
    start="$(systemctl show -p ActiveEnterTimestamp --value ss14.service)"
    journalctl -u ss14.service --since "$start" --no-pager 2>/dev/null | awk '
        /net: Approved "/ { if (match($0, /Approved "[^"]+"/)) a[substr($0, RSTART + 10, RLENGTH - 11)] = 1 }
        /": Disconnected/ { if (match($0, /net: "[^"]+"/)) delete a[substr($0, RSTART + 6, RLENGTH - 7)] }
        END { n = 0; for (k in a) n++; print n }'
}

wedged=0
players=$(players_status)
if [ -z "$players" ]; then
    # no answer: is the process even running?
    pid=$(systemctl show -p MainPID --value ss14.service)
    [ -z "$pid" ] || [ "$pid" = "0" ] && exit 0
    # a server that has only just started has not opened its status endpoint yet
    up=$(ps -o etimes= -p "$pid" | tr -d ' ')
    [ "${up:-0}" -lt 180 ] && exit 0
    wedged=1
    players=$(players_log)
    [ -z "$players" ] && exit 0
fi
[ "$players" -ne 0 ] && exit 0

# empty (and, if the status endpoint was silent, confirmed empty by the log)
if [ "$wedged" -eq 1 ]; then
    logger -t ss14-restart "the status endpoint does not answer and the log shows no players"
fi

have_cfg=0
if [ -f "$PENDING_CFG" ]; then
    if python3 -c 'import sys, tomllib; tomllib.load(open(sys.argv[1], "rb"))' "$PENDING_CFG" 2>/dev/null; then
        have_cfg=1
    else
        logger -t ss14-restart "the staged configuration is not valid TOML, ignoring it"
        mv "$PENDING_CFG" "$PENDING_CFG.invalid"
    fi
fi

install_cfg() {
    cp "$CFG" "$CFG.prev"
    install -o ss14 -g ss14 -m 644 "$PENDING_CFG" "$CFG"
    rm -f "$PENDING_CFG"
    logger -t ss14-restart "staged configuration installed"
}

# still nobody? (uses the log when the endpoint is silent)
nobody_now() {
    local n
    n=$(players_status)
    [ -z "$n" ] && n=$(players_log)
    [ "${n:-1}" -eq 0 ]
}

if [ -f "$PENDING_BUILD" ]; then
    logger -t ss14-restart "no players: installing the staged build"
    rm -rf /opt/ss14/server_new && mkdir -p /opt/ss14/server_new || exit 1
    unzip -q "$PENDING_BUILD" -d /opt/ss14/server_new || { logger -t ss14-restart "unzip failed"; rm -rf /opt/ss14/server_new; exit 1; }
    chmod +x /opt/ss14/server_new/Robust.Server
    chown -R ss14:ss14 /opt/ss14/server_new
    # somebody may have joined while unpacking
    if ! nobody_now; then
        logger -t ss14-restart "a player joined while unpacking; trying again next time"
        rm -rf /opt/ss14/server_new
        exit 0
    fi
    systemctl stop ss14.service
    rm -rf /opt/ss14/server_prev
    mv /opt/ss14/server /opt/ss14/server_prev
    mv /opt/ss14/server_new /opt/ss14/server
    mv "$PENDING_BUILD" /opt/ss14/server.zip
    [ "$have_cfg" -eq 1 ] && install_cfg
    systemctl start ss14.service
    logger -t ss14-restart "staged build installed"
    exit 0
fi

if [ "$have_cfg" -eq 1 ]; then
    logger -t ss14-restart "no players: installing the staged configuration and restarting"
    install_cfg
    systemctl restart ss14.service
    exit 0
fi

if [ "$wedged" -eq 1 ]; then
    logger -t ss14-restart "restarting the wedged server"
    systemctl restart ss14.service
    exit 0
fi

pid=$(systemctl show -p MainPID --value ss14.service)
[ -z "$pid" ] || [ "$pid" = "0" ] && exit 0
up=$(ps -o etimes= -p "$pid" | tr -d ' ')
if [ "${up:-0}" -ge 10800 ]; then
    logger -t ss14-restart "no players and uptime ${up}s: restarting ss14"
    systemctl restart ss14.service
fi
