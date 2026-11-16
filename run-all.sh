#!/bin/bash
# Start all three GalgameOnline services.
# SdkServer listens on TCP 5000 + Unix socket /tmp/sdk.sock.
# GameServer reaches SdkServer over the Unix socket (loopback TCP between
# server processes is blocked in this sandbox; on a real host set
# GameServer__SdkServerUrl=http://localhost:5000 instead).
set -e
export PATH="$HOME/.dotnet:$PATH"
cd "$(dirname "$0")/server"

dotnet build -v q --nologo 2>&1 | tail -2

rm -f /tmp/sdk.sock
dotnet run --project src/GalgameOnline.SdkServer --no-build > /tmp/sdk.log 2>&1 &
dotnet run --project src/GalgameOnline.QueryGateway --no-build > /tmp/gw.log 2>&1 &
GameServer__SdkServerUrl="unix:///tmp/sdk.sock" dotnet run --project src/GalgameOnline.GameServer --no-build > /tmp/game.log 2>&1 &

sleep 8
echo "SdkServer:     $(curl -s http://127.0.0.1:5000/api/sdk/status)"
echo "GameServer:    $(curl -s http://127.0.0.1:5100/api/game/status)"
echo "QueryGateway:  $(curl -s http://127.0.0.1:5200/api/gateway/status)"
echo "Logs: /tmp/sdk.log /tmp/game.log /tmp/gw.log"
