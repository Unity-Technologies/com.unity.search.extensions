#!/bin/bash
# Runs every EditMode test in the project and prints the summary plus each failing test.
source "$(dirname "$0")/env.sh"
cd "$PROJECT"
unity cmd test run --mode EditMode --async_tests true --json > /dev/null 2>&1
sleep 3
for i in $(seq 1 90); do
  s=$(unity cmd test status --json 2>/dev/null)
  echo "$s" | grep -q '"status": *"completed"' && break
  sleep 5
done
echo "$s" | python -c "
import json, sys
d = json.load(sys.stdin); r = d.get('data', d).get('result', d)
print(r.get('status'), r.get('summary'))
for t in r.get('results', r.get('tests', [])) or []:
    if str(t.get('Status', '')).lower() not in ('passed', 'skipped'): print(t)
"
