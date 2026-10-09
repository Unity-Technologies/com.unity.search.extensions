#!/bin/bash
# usage: open.sh <full version, e.g. 6000.3.25f1>
# Points ProjectVersion.txt at the target so Unity skips the version-mismatch modal, then launches.
source "$(dirname "$0")/env.sh"
V=$1
printf "m_EditorVersion: %s\nm_EditorVersionWithRevision: %s\n" "$V" "$V" > "$PROJECT/ProjectSettings/ProjectVersion.txt"
LOG="$LOG_DIR/editor-$V.log"
rm -f "$LOG"
"$EDITORS/$V/Editor/Unity.exe" -projectPath "$PROJECT_WIN" -logFile "$(cygpath -m "$LOG")" > /dev/null 2>&1 &
echo "$LOG"
