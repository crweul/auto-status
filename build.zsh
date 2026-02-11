#!/usr/bin/env zsh
set -euo pipefail

script_dir="${0:A:h}"

cd "$script_dir"

dotnet build auto-status/AutoStatus.csproj -c Release

echo "Build complete. Output in auto-status/bin/Release/"
