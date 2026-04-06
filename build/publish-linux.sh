#!/usr/bin/env bash
set -euo pipefail

dotnet publish src/BaseballManager.Game/BaseballManager.Game.csproj -c Release -r linux-x64 --self-contained false
