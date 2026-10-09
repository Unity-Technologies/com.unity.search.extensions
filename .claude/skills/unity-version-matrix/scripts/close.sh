#!/bin/bash
# Force-kills every Editor that has this project open. There is no Pipeline command that quits the Editor.
source "$(dirname "$0")/env.sh"
for p in $(powershell -NoProfile -c "Get-CimInstance Win32_Process -Filter \"name='Unity.exe'\" | ? CommandLine -like '*SearchExtensionsExamples*' | % ProcessId"); do
  taskkill //PID "$(echo "$p" | tr -d '\r')" //F
done
sleep 3
rm -f "$PROJECT/Temp/UnityLockfile"
