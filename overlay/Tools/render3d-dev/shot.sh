#!/bin/bash
# usage: [CLIENT=name] shot.sh <name>
name="${1:-shot}"
d="${R3D_DATA:-$APPDATA/Space Station 14/data}/Screenshots"
mkdir -p ${R3D_LOGS:-$HOME/render3d-logs}/shots
rm -f "$d/$name.png"
$(dirname "$0")/cmd.sh "r3d_shot $name"
for i in $(seq 1 40); do [ -f "$d/$name.png" ] && { sleep 0.3; cp "$d/$name.png" ${R3D_LOGS:-$HOME/render3d-logs}/shots/$name.png; echo "${R3D_LOGS:-$HOME/render3d-logs}/shots/$name.png"; exit 0; }; sleep 0.25; done
echo "no screenshot"
