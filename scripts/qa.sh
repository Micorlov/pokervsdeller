#!/usr/bin/env bash
# Visual QA in an isolated project copy, so several people (or agents) can render screens at once
# without fighting over Unity's one-process-per-project lock.
#
#   scripts/qa.sh SLOT SCENARIO [OUT.png]
#
#   SLOT       any short name (e.g. table, shell); each gets its own copy + Library under artifacts/qa/SLOT
#   SCENARIO   home | stats | settings | howto | table-kicker | table-kicker-picked | table-climb |
#              table-pressure | table-forge | table-bailed | table-result
#   OUT.png    default artifacts/captures/SLOT/SCENARIO.png (relative paths resolve from the repo root)
#
# Edit code in the repo as usual; this syncs Assets/Packages/ProjectSettings into the copy, builds the Mac
# QA player there and captures the scenario at 780x1688 (a 390x844 dp phone at 2x). A first run for a new
# slot imports the project from scratch (a few minutes); later runs are fast.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SLOT="${1:?usage: scripts/qa.sh SLOT SCENARIO [OUT.png]}"
SCENARIO="${2:?usage: scripts/qa.sh SLOT SCENARIO [OUT.png]}"
OUT="${3:-artifacts/captures/$SLOT/$SCENARIO.png}"
case "$OUT" in /*) ;; *) OUT="$ROOT/$OUT" ;; esac
EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity}"
COPY="$ROOT/artifacts/qa/$SLOT"
mkdir -p "$COPY" "$(dirname "$OUT")" "$COPY/artifacts/logs"

rsync -a --delete --exclude='*.meta.tmp' "$ROOT/Assets/" "$COPY/Assets/"
rsync -a --delete "$ROOT/Packages/" "$COPY/Packages/"
rsync -a --delete "$ROOT/ProjectSettings/" "$COPY/ProjectSettings/"

"$EDITOR" -batchmode -nographics -quit -projectPath "$COPY" -buildTarget OSXUniversal \
  -executeMethod Showdown.EditorTools.ShowdownBuilds.Mac -logFile "$COPY/artifacts/logs/build.log" \
  || { echo "Unity build failed; see $COPY/artifacts/logs/build.log" >&2; grep -E "error CS|Exception" "$COPY/artifacts/logs/build.log" | head -20 >&2; exit 1; }
grep -q "Showdown build: Succeeded" "$COPY/artifacts/logs/build.log" \
  || { echo "Unity build did not succeed; see $COPY/artifacts/logs/build.log" >&2; grep -E "error CS" "$COPY/artifacts/logs/build.log" | head -20 >&2; exit 1; }

rm -f "$OUT"
perl -e 'alarm 120; exec @ARGV' "$COPY/artifacts/macos/Showdown.app/Contents/MacOS/Poker vs Dealer" \
  -screen-width 780 -screen-height 1688 -screen-fullscreen 0 -logFile "$COPY/artifacts/logs/run.log" \
  --scenario "$SCENARIO" --capture "$OUT" >/dev/null 2>&1 || true
[ -f "$OUT" ] && echo "$OUT" || { echo "No capture produced; see $COPY/artifacts/logs/run.log" >&2; grep -iE "exception|error" "$COPY/artifacts/logs/run.log" | head -10 >&2; exit 1; }
