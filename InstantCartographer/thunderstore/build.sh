#!/usr/bin/env bash
# Builds the DLL and zips the Thunderstore package. Usage: ./build.sh "/path/to/Valheim"
set -euo pipefail
cd "$(dirname "$0")"
VP="${1:?pass the Valheim install path}"
dotnet build ../InstantCartographer.csproj -c Release -p:ValheimPath="$VP"
cp ../bin/Release/net462/InstantCartographer.dll pkg/
rm -f InstantCartographer-thunderstore.zip
(cd pkg && zip -q ../InstantCartographer-thunderstore.zip manifest.json README.md icon.png InstantCartographer.dll)
echo "Wrote InstantCartographer-thunderstore.zip"
