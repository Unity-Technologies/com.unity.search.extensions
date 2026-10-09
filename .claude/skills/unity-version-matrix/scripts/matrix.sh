#!/bin/bash
# Opens, checks and tests every supported version in turn, then closes the Editor.
# usage: matrix.sh [full version ...]   (defaults to every stream resolve.sh finds installed)
D="$(dirname "$0")"
source "$D/env.sh"
versions="$*"
[ -z "$versions" ] && versions=$("$D/resolve.sh" | awk '$2 != "MISSING" {print $2}')
for v in $versions; do
  "$D/close.sh" > /dev/null 2>&1
  "$D/open.sh" "$v" > /dev/null
  r=$("$D/wait.sh" "$v" | head -1)
  echo "== $v $r compile_errors=$(grep -c 'error CS' "$LOG_DIR/editor-$v.log")"
  [ "$r" = READY ] && "$D/runtests.sh"
done
"$D/close.sh" > /dev/null 2>&1
