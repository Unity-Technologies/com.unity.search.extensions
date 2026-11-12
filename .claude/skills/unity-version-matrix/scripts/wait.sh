#!/bin/bash
# usage: wait.sh <full version>
# Prints READY, COMPILE_FAILED, PACKAGE_ERROR, EXITED or TIMEOUT, then any compile errors from the Editor log.
source "$(dirname "$0")/env.sh"
LOG="$LOG_DIR/editor-$1.log"
result=TIMEOUT
ok=0
for i in $(seq 1 300); do
  # READY needs this project reported ready on this exact version (not a leftover Editor), then two
  # idle recompile checks in a row: one alone can land between an import and its domain reload.
  if unity status 2>/dev/null | awk -F'	' -v v="$1" '$2 == "ready" && $3 ~ /SearchExtensionsExamples/ && $4 == v {f=1} END {exit !f}' &&
     (cd "$PROJECT" && unity cmd script recompile_status --json 2>/dev/null) | grep -q '"status": *"idle"'; then
    ok=$((ok + 1)); [ $ok -ge 2 ] && { result=READY; break; }
  else ok=0; fi
  if grep -q "Tundra build failed" "$LOG" 2>/dev/null; then result=COMPILE_FAILED; break; fi
  # A manifest entry this version cannot resolve opens a modal dialog the Editor never gets past.
  if grep -q "An error occurred while resolving packages" "$LOG" 2>/dev/null; then result=PACKAGE_ERROR; break; fi
  if grep -q "Application will terminate" "$LOG" 2>/dev/null; then result=EXITED; break; fi
  sleep 4
done
echo $result
grep -E "error CS" "$LOG" 2>/dev/null | sort -u | head -60
