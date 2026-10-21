#!/usr/bin/env bash
# Local checks only. No installation, packaging, SSH, publishing or game launch.
set -euo pipefail
cd "$(dirname "$0")/.."
mode="${1:-}"
if [[ "$mode" != "" && "$mode" != "--localization-only" ]]; then
  echo 'Usage: scripts/verify.sh [--localization-only]' >&2
  exit 2
fi
python3 scripts/localization.py --check
python3 -m unittest discover -s tests -v
scripts/dotnet.sh run --project tests/LocalizationHarness -c Release
scripts/dotnet.sh build tests/GameApiCompile -c Release --nologo
if [[ "$mode" == "--localization-only" ]]; then
  echo 'PASS: localization checks only; not a full mod build or game test.'
else
  echo 'Attempting full mod build. Missing/publicized references may block it; failure is not hidden.'
  scripts/dotnet.sh build IsekaiMod.Local.csproj -c Release --nologo
fi
