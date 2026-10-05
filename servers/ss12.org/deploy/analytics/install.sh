#!/usr/bin/env bash
# Installs the ss12.org stats on the server. Run as root from this folder. Safe to run again.
# It does not touch the web server's configuration: merge Caddyfile.example into /etc/caddy/Caddyfile yourself.
set -euo pipefail
cd "$(dirname "$0")"

install -m 755 -o root -g root ss12-stats /usr/local/bin/ss12-stats
python3 /usr/local/bin/ss12-stats selftest

install -d -m 755 /var/lib/ss12-stats /var/lib/ss12-stats/public
if [ ! -f /etc/ss12-stats.conf ]; then
  install -m 644 -o root -g root ss12-stats.conf.example /etc/ss12-stats.conf
  echo "Wrote /etc/ss12-stats.conf: put the account names to leave out of the player numbers in exclude_names."
fi

for u in ss12-stats-sample.service ss12-stats-sample.timer ss12-stats-report.service ss12-stats-report.timer; do
  install -m 644 -o root -g root "$u" "/etc/systemd/system/$u"
done
systemctl daemon-reload
systemctl enable --now ss12-stats-sample.timer ss12-stats-report.timer

systemctl start ss12-stats-sample.service
systemctl start ss12-stats-report.service
echo "Installed. Report on demand: ss12-stats report"
