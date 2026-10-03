#!/bin/bash
# Applies the ss12.org server changes to a Wizard's Den checkout that already has SS12 installed.
#
# usage: apply.sh <path to the SS14 checkout>
#
# The patch is made against Wizard's Den commit fb9401cf (engine 291). It does not apply to a different upstream
# version unchanged; see README.md.
set -e

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
target="${1:?usage: apply.sh <path to the SS14 checkout>}"

cd "$target"

if [ ! -d Content.Client/Render3D ]; then
    echo "SS12 is not installed in $target. Run 'ss12 install' on it first."
    exit 1
fi

if ! git apply --check "$here/ss12.org.patch"; then
    echo "The patch does not apply. It is made for Wizard's Den commit fb9401cf; see README.md."
    exit 1
fi

git apply "$here/ss12.org.patch"
cp -r "$here/files/." .

echo "ss12.org changes applied. Build and package as usual (ss12 package)."
