#!/usr/bin/env bash
# Build (if needed), deploy this pageless Global.asax into the OpenWebForms standalone
# host, and run it. Usage:  bash run.sh [port]
# Run from the OpenWebForms repo root, e.g.
#   cd '/mnt/d/Claude Files/OpenWebForms' && bash 'samples/small pageless web forms test/run.sh' 8080
set -e
PORT="${1:-8080}"
export DOTNET_ROLL_FORWARD=Major

HOSTDLL="samples/host/bin/Release/net8.0/SampleHost.dll"
WWWROOT="samples/host/bin/Release/net8.0/wwwroot"
SRC="samples/small pageless web forms test/Global.asax"

# Build the host (and clean-room System.Web via project reference) if not present.
if [ ! -f "$HOSTDLL" ]; then
  echo "Building OpenWebForms host..."
  dotnet build src/System.Web.csproj -c Release -v quiet
  dotnet build samples/host/SampleHost.csproj -c Release -v quiet
fi

# Deploy: the host discovers Global.asax in its app root (wwwroot).
mkdir -p "$WWWROOT"
rm -f "$WWWROOT"/default.aspx "$WWWROOT"/index.aspx 2>/dev/null || true
cp "$SRC" "$WWWROOT/Global.asax"

echo "Starting pageless host on http://localhost:$PORT/  (Ctrl+C to stop)"
exec dotnet "$HOSTDLL" "$PORT"
