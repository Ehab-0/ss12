#!/usr/bin/env bash
# CI helper: waits until a game server (already started in the background) says "-> Ready" in its log and accepts
# connections on 127.0.0.1:1212.   usage: wait-ready.sh <log file> <server pid> [seconds, default 240]
# Exit code: 0 ready, 1 not ready in time or the server exited (the end of the log is printed).
set -u
log="$1"; pid="$2"; limit="${3:-240}"

for _ in $(seq 1 "$limit"); do
    if ! kill -0 "$pid" 2>/dev/null; then echo "The server exited early."; break; fi
    if grep -qF -- '-> Ready' "$log" 2>/dev/null && (exec 3<>/dev/tcp/127.0.0.1/1212) 2>/dev/null; then
        echo "The server is ready and accepts connections."
        exit 0
    fi
    sleep 1
done

echo "The server did not become ready. End of its log:"
tail -n 80 "$log" 2>/dev/null || true
exit 1
