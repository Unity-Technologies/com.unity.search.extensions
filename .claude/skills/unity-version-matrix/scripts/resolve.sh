#!/bin/bash
# Prints "<stream> <highest installed version>" for each supported stream, or "<stream> MISSING".
source "$(dirname "$0")/env.sh"
while read -r stream; do
  [ -z "$stream" ] && continue
  v=$(ls "$EDITORS" | grep "^$stream\." | sort -V | tail -1)
  echo "$stream ${v:-MISSING}"
done < "$(dirname "$0")/versions.txt"
