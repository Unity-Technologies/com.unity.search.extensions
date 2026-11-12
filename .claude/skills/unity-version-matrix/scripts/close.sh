#!/bin/bash
# Force-kills every Editor that has this project open and waits for it to exit, so the next Editor
# does not find the project still locked and quit. There is no Pipeline command that quits the Editor.
source "$(dirname "$0")/env.sh"
pids() { powershell -NoProfile -c "Get-CimInstance Win32_Process -Filter \"name='Unity.exe'\" | ? CommandLine -like '*SearchExtensionsExamples*' | % ProcessId" | tr -d '\r'; }
for p in $(pids); do taskkill //PID "$p" //F; done
for i in $(seq 1 30); do [ -z "$(pids)" ] && break; sleep 1; done
rm -f "$PROJECT/Temp/UnityLockfile"
