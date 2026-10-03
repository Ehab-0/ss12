#!/bin/bash
# Run every 10 minutes by ss14-restart.timer. Only ever acts when nobody is connected.
#  - A new build staged in /opt/ss14/pending/server.zip is installed (the old one is kept in /opt/ss14/server_prev).
#  - A new configuration staged in /opt/ss14/pending/server_config.toml is installed (the old one is kept as
#    server_config.toml.prev) after it has been checked to be valid TOML.
#  - Either of those restarts the server. With nothing staged, the server is restarted for a fresh round once it has been
#    up for at least an hour.
PENDING_DIR=/opt/ss14/pending
PENDING_BUILD=$PENDING_DIR/server.zip
PENDING_CFG=$PENDING_DIR/server_config.toml
CFG=/opt/ss14/server_config.toml

players_now() {
    curl -s -m 5 http://127.0.0.1:1212/status | grep -o '"players":[0-9]*' | grep -o '[0-9]*$'
}

players=$(players_now)
[ -z "$players" ] && exit 0
[ "$players" -ne 0 ] && exit 0

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

if [ -f "$PENDING_BUILD" ]; then
    logger -t ss14-restart "no players: installing the staged build"
    rm -rf /opt/ss14/server_new && mkdir -p /opt/ss14/server_new || exit 1
    unzip -q "$PENDING_BUILD" -d /opt/ss14/server_new || { logger -t ss14-restart "unzip failed"; rm -rf /opt/ss14/server_new; exit 1; }
    chmod +x /opt/ss14/server_new/Robust.Server
    chown -R ss14:ss14 /opt/ss14/server_new
    # somebody may have joined while unpacking
    again=$(players_now)
    if [ "${again:-1}" -ne 0 ]; then
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

pid=$(systemctl show -p MainPID --value ss14.service)
[ -z "$pid" ] || [ "$pid" = "0" ] && exit 0
up=$(ps -o etimes= -p "$pid" | tr -d ' ')
if [ "${up:-0}" -ge 3600 ]; then
    logger -t ss14-restart "no players and uptime ${up}s: restarting ss14"
    systemctl restart ss14.service
fi
