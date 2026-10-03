#!/bin/bash
# usage: aimproto.sh <ProtoId>  -> dumps entities and aims the crosshair at the most recent entity of that prototype
c="${CLIENT:-tester1}"
CLIENT=$c $(dirname "$0")/cmd.sh "r3d_dump"; sleep 1
line=$(grep "render3d:   [0-9]*: $1 " ${R3D_LOGS:-$HOME/render3d-logs}/client_$c.log | tail -1)
pos=$(echo "$line" | grep -o "pos=<[^>]*>" | sed 's/pos=<//; s/>//; s/,//')
CLIENT=$c $(dirname "$0")/cmd.sh "r3d_aim $pos"; sleep 1.5
CLIENT=$c $(dirname "$0")/cmd.sh "r3d_pick"; sleep 1
grep "pick coords" ${R3D_LOGS:-$HOME/render3d-logs}/client_$c.log | tail -1 | grep -o "dist=[0-9.]* first=[^(]*([^)]*)"
