---
name: unity-version-matrix
description: Compiles and runs the Search Extensions tests in projects/SearchExtensionsExamples against every supported Unity version (highest installed build of each stream), fixes what breaks, and reports a version × compile × tests table. Use when the user asks to check, validate or test the package against all supported Unity versions, run the version matrix, or check compatibility with a new Unity release.
---

# Unity version matrix

Scripts live in `scripts/` (Git Bash). All paths are derived from the repo; logs go to
`projects/SearchExtensionsExamples/Logs/version-matrix/editor-<version>.log`.

| Script | Does |
|---|---|
| `resolve.sh` | Supported streams (`versions.txt`) → highest installed build, or `MISSING` |
| `matrix.sh [versions…]` | Open → wait → test each version, then close. No args = all resolved versions |
| `open.sh <v>` / `wait.sh <v>` / `close.sh` | One step at a time, for fixing a single version |
| `runtests.sh` | All EditMode tests on the open Editor; prints summary and failures |

Supported streams are the lines of `scripts/versions.txt`. When support changes, edit that file only.

## Workflow

1. `scripts/resolve.sh`. If a stream is `MISSING`, tell the user and ask whether to install it or skip it.
2. Check `projects/SearchExtensionsExamples/Packages/manifest.json` has `com.unity.pipeline` and
   `"testables": ["com.unity.search.extensions.examples"]`. Without testables no tests are listed.
3. `scripts/matrix.sh` (run in background, ~3 min per version). Per version it prints
   `READY` + test summary, or `COMPILE_FAILED`.
4. For each failing version, oldest first:
   - `close.sh`, `open.sh <v>`, `wait.sh <v>` prints the compile errors.
   - Fix (see conventions), then `close.sh` + `open.sh` + `wait.sh` again. Do not try to recompile
     in place: after a startup compile error the Editor sits on the Safe Mode prompt with no Pipeline.
   - Once `READY`, `runtests.sh`. A failing test gets diagnosed, not skipped.
5. After the last fix, run `scripts/matrix.sh` again over **every** version: a fix for one version
   must not break another.
6. Clean up: `close.sh`, then `git checkout -- package-examples/Textures/` (opening the project
   rewrites those `.meta` files). Revert any other `.meta`/asset churn you did not intend.
7. Report a table (version, build, compile, passed/skipped/failed), the files changed, and anything
   left for the user. Do not commit.

## Fixing conventions

- Guard, don't replace: `#if UNITY_6000_6_OR_NEWER … #else <old code> #endif`, so older versions
  keep their path. Match the guards already in the file.
- Prefer an API that exists on every version over a guard (e.g. `PingObject(obj)` instead of
  `PingObject(obj.GetInstanceID())`, `GlobalObjectId.GetGlobalObjectIdSlow(obj)`).
- To find which versions ship an API, grep the editor DLLs:
  `grep -c -a <Name> "/c/Program Files/Unity/Hub/Editor/<v>/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"`
  (also `UnityEditor.CoreModule.dll`). For signatures: `DOTNET_ROLL_FORWARD=Major ilspycmd -t <Type> <dll>`.
- Obsolete-as-error (CS0619) cannot be suppressed with `#pragma`; it must be guarded.
- The dependency viewer (`package/Dependencies`) and its tests are compiled out on
  `UNITY_7000_0_OR_NEWER`. A new file there needs the same guard.
- **Changing a public type or member, even under a guard, is an API change: stop and ask first.**

## Expected results

- 6.x: 39 tests, 4 skipped (`CustomIndexationTests`, `[Ignore]` in source), 0 failed.
- 7.0: 5 tests, 4 skipped, 0 failed (dependency tests compiled out).
- `DependencyProviderTests` never runs: it requires `USE_SEARCH_LMDB_STORAGE`, which nothing defines.

A different count means tests appeared or disappeared; find out why before reporting success.

## Pitfalls

- `unity` CLI commands are grouped: `unity cmd test run`, `unity cmd script recompile`. There is no
  Pipeline command that quits the Editor; `close.sh` force-kills it.
- Never start a second Editor on the project: always `close.sh` before `open.sh`.
- A stall with the Pipeline up is usually a modal dialog waiting for a click. Ask the user to look
  at the Editor rather than killing it.
