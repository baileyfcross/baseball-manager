Param()

$ErrorActionPreference = "Stop"

dotnet publish src/BaseballManager.Game/BaseballManager.Game.csproj -c Release -r win-x64 --self-contained false
