#!/bin/bash
# usage: aimrel.sh <dx> <dy>   aim at floor point head+(dx,dy) of tester1 (waits for fresh stats)
sleep 3.5
read hx hy <<< $(grep "render3d: stats" ${R3D_LOGS:-$HOME/render3d-logs}/client_${CLIENT:-tester1}.log | tail -1 | grep -o "head=<[^>]*>" | sed 's/head=<//; s/>//; s/,//g' | awk '{print $1, $2}')
tx=$(python -c "print($hx+$1)"); ty=$(python -c "print($hy+$2)")
echo "$hx $hy -> $tx $ty"
CLIENT=${CLIENT:-tester1} $(dirname "$0")/cmd.sh "r3d_aim $tx $ty"
