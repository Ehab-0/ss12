#!/bin/bash
# usage: [CLIENT=name] cmd.sh "cmd1" "cmd2" ...   (runs in that client via the dev channel; default tester1)
c="${CLIENT:-tester1}"
f="${R3D_DATA:-$APPDATA/Space Station 14/data}/render3d_dev_$c.txt"
for x in "$@"; do echo "$x" >> "$f"; done
for i in $(seq 1 40); do [ -f "$f" ] || break; sleep 0.25; done
