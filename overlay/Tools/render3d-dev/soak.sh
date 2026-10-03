#!/bin/bash
# usage: soak.sh <seconds>  random walk/look/click/zoom through the dev channel, sampling client memory (MB)
end=$(( $(date +%s) + $1 ))
mem() { powershell -NoProfile -Command "[int]((Get-Process Content.Client).WorkingSet64/1MB)" | tr -d '\r'; }
./cmd.sh "r3d_capture on"
echo "$(date +%T) start mem=$(mem)MB"
keys=(MoveUp MoveDown MoveLeft MoveRight)
n=0
while [ $(date +%s) -lt $end ]; do
  k=${keys[$((RANDOM%4))]}
  yaw=$(python -c "import random;print(round(random.uniform(-180,180),2))"); pitch=$(python -c "import random;print(round(random.uniform(-35,30),2))")
  ./cmd.sh "r3d_look $yaw $pitch" "r3d_press $k down"
  sleep 1.$((RANDOM%9))
  ./cmd.sh "r3d_press $k up"
  [ $((n%3)) -eq 0 ] && ./cmd.sh "r3d_press Use tap"
  [ $((n%7)) -eq 0 ] && ./cmd.sh "r3d_cam fp"
  [ $((n%7)) -eq 3 ] && ./cmd.sh "r3d_cam tp"
  n=$((n+1))
  [ $((n%40)) -eq 0 ] && echo "$(date +%T) iter $n mem=$(mem)MB"
done
echo "$(date +%T) done $n iterations mem=$(mem)MB"
