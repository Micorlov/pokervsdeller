#!/usr/bin/env bash
# Type-checks Assets/Showdown/{Domain,Runtime} against Unity's engine assemblies without opening Unity.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet}"
export DOTNET_CLI_HOME="$ROOT/tests/Check/.dotnet" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
exec "$DOTNET" build "$ROOT/tests/Check/Editor.Check.csproj" --nologo -v q -o "$ROOT/tests/Check/out" "$@"
