#!/usr/bin/env bash
set -euo pipefail
DOTNET="${DOTNET_BIN:-$HOME/.local/share/tactical-virtuoso/dotnet/dotnet}"
export DOTNET_ROOT="$(dirname "$DOTNET")"
exec "$DOTNET" "$@"
