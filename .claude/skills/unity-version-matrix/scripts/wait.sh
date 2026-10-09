#!/bin/bash
# usage: wait.sh <full version>
# Prints READY, COMPILE_FAILED or TIMEOUT, then any compile errors from the Editor log.
source "$(dirname "$0")/env.sh"
LOG="$LOG_DIR/editor-$1.log"
result=TIMEOUT
for i in $(seq 1 150); do
  if unity status 2>/dev/null | grep -qi SearchExtensionsExamples; then result=READY; break; fi
  if grep -q "Tundra build failed" "$LOG" 2>/dev/null; then result=COMPILE_FAILED; break; fi
  sleep 4
done
echo $result
grep -E "error CS" "$LOG" 2>/dev/null | sort -u | head -60
