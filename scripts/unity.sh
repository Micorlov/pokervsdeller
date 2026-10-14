#!/usr/bin/env bash
# Headless Unity entry points for Poker vs Dealer.
#   ./scripts/unity.sh configure     apply project settings, generate panel asset + scene
#   ./scripts/unity.sh test          run EditMode tests (results: artifacts/editmode-results.xml)
#   ./scripts/unity.sh apk           development APK  -> artifacts/android/showdown-dev.apk
#   ./scripts/unity.sh aab           signed release AAB -> artifacts/android/showdown-release.aab
#                                    (needs SHOWDOWN_KEYSTORE_PROPERTIES=<path to keystore.properties>)
#   ./scripts/unity.sh mac           QA player -> artifacts/macos/Showdown.app
#   ./scripts/unity.sh capture OUT.png [delay]   run the Mac player and save a screenshot
# Only one Unity process can open the project at a time.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity}"
mkdir -p "$ROOT/artifacts/logs"
COMMAND="${1:-configure}"

run_method() { # method, [extra editor args...]
  local method="$1"; shift
  exec "$EDITOR" -batchmode -nographics -quit -projectPath "$ROOT" "$@" \
    -executeMethod "Showdown.EditorTools.$method" -logFile "$ROOT/artifacts/logs/$COMMAND.log"
}

case "$COMMAND" in
  configure) run_method ShowdownProject.Configure ;;
  apk)       run_method ShowdownBuilds.AndroidApk -buildTarget Android ;;
  aab)       run_method ShowdownBuilds.AndroidBundle -buildTarget Android ;;
  mac)       run_method ShowdownBuilds.Mac -buildTarget OSXUniversal ;;
  test)
    exec "$EDITOR" -batchmode -nographics -projectPath "$ROOT" -runTests -testPlatform EditMode \
      -testResults "$ROOT/artifacts/editmode-results.xml" -logFile "$ROOT/artifacts/logs/test.log"
    ;;
  capture)
    OUT="${2:?usage: unity.sh capture OUT.png [delay-seconds]}"
    # A Mac player runs with its own bundle as the working directory, so a relative path would land inside it.
    case "$OUT" in /*) ;; *) OUT="$PWD/$OUT" ;; esac
    mkdir -p "$(dirname "$OUT")"
    DELAY="${3:-2.5}"
    exec "$ROOT/artifacts/macos/Showdown.app/Contents/MacOS/Poker vs Dealer" \
      -screen-width 780 -screen-height 1688 -screen-fullscreen 0 -logFile "$ROOT/artifacts/logs/capture.log" \
      --capture "$OUT" --capture-delay "$DELAY"
    ;;
  *) echo "Usage: $0 configure|test|apk|aab|mac|capture" >&2; exit 2 ;;
esac
