#!/bin/bash
# Opens, checks and tests every supported version in turn, then closes the Editor.
# usage: matrix.sh [full version ...]   (defaults to every stream resolve.sh finds installed)
# Each Editor rewrites manifest.json with packages of its own version and leaves import data in
# Library, so before every open (and on exit) the manifest is restored to its state at start, and
# packages-lock.json and Library are deleted.
D="$(dirname "$0")"
source "$D/env.sh"
MANIFEST="$PROJECT/Packages/manifest.json"
BASELINE="$LOG_DIR/manifest.baseline.json"
cp "$MANIFEST" "$BASELINE"
restore() { cp "$BASELINE" "$MANIFEST"; rm -rf "${PROJECT:?}/Packages/packages-lock.json" "${PROJECT:?}/Library"; }
trap '"$D/close.sh" > /dev/null 2>&1; restore' EXIT
versions="$*"
[ -z "$versions" ] && versions=$("$D/resolve.sh" | awk '$2 != "MISSING" {print $2}')
for v in $versions; do
  "$D/close.sh" > /dev/null 2>&1
  restore
  "$D/open.sh" "$v" > /dev/null
  r=$("$D/wait.sh" "$v" | head -1)
  echo "== $v $r compile_errors=$(grep -c 'error CS' "$LOG_DIR/editor-$v.log")"
  [ "$r" = READY ] && "$D/runtests.sh"
done
