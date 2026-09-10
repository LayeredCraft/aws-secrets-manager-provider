#!/usr/bin/env bash
set -euo pipefail

case "$(uname -s)/$(uname -m)" in
  Darwin/arm64) RID=osx-arm64 ;;
  Darwin/x86_64) RID=osx-x64 ;;
  Linux/x86_64) RID=linux-x64 ;;
  Linux/aarch64) RID=linux-arm64 ;;
  MINGW*/x86_64|CYGWIN*/x86_64) RID=win-x64 ;;
  *) echo "Unsupported host: $(uname -s)/$(uname -m)"; exit 1 ;;
esac

project=tests/AotCompatibility.TestApp/AotCompatibility.TestApp.csproj
log=$(mktemp)
trap 'rm -f "$log"' EXIT

if ! dotnet publish "$project" -c Release -r "$RID" >"$log" 2>&1; then
  cat "$log"
  exit 1
fi

if grep -E 'warning IL(2|3)[0-9]{3}' "$log" | sort -u; then
  echo "AOT compatibility warnings found."
  exit 1
fi

"tests/AotCompatibility.TestApp/bin/Release/net8.0/$RID/publish/AotCompatibility.TestApp"
echo "AOT compatibility: clean."
