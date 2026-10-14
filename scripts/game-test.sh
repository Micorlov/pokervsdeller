#!/usr/bin/env bash
# Builds and runs the Showdown game-layer tests with the .NET SDK bundled with Unity,
# so nothing needs Unity to be open (only one Unity process may use the project).
#
#   scripts/game-test.sh [-v] [--include-ignored] [--filter <text>]
#
# Override the SDK with DOTNET=/path/to/dotnet, or the Editor with UNITY_EDITOR_ROOT.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR_ROOT="${UNITY_EDITOR_ROOT:-/Applications/Unity/Hub/Editor/6000.5.8f1}"
DEFAULT_DOTNET="$UNITY_EDITOR_ROOT/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"
DOTNET="${DOTNET:-$DEFAULT_DOTNET}"

if [[ ! -x "$DOTNET" ]]; then
  if command -v dotnet >/dev/null 2>&1; then
    DOTNET="$(command -v dotnet)"
  else
    echo "No dotnet found. Expected Unity's bundled SDK at: $DEFAULT_DOTNET" >&2
    exit 2
  fi
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export MSBUILDDISABLENODEREUSE=1

PROJECT="$ROOT/tests/Game/GameTests.csproj"
OUTPUT="$ROOT/tests/Game/bin/Debug/net8.0"

"$DOTNET" build "$PROJECT" --nologo -v quiet -nodeReuse:false \
  -p:UseSharedCompilation=false -p:UnityEditorRoot="$UNITY_EDITOR_ROOT"

exec "$DOTNET" "$OUTPUT/ShowdownGameTests.dll" "$@"
