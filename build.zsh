#!/usr/bin/env zsh
set -euo pipefail

script_dir="${0:A:h}"

cd "$script_dir"

dotnet build AutoStatus/AutoStatus.csproj -c Release

echo "Build complete. Output in AutoStatus/bin/Release/"
