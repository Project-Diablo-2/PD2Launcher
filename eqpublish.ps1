Push-Location $PSScriptRoot

$publishPath = "C:\Users\Pritc\OneDrive\Desktop\testPublish"

dotnet publish /p:DebugType=None /p:DebugSymbols=false -o "$publishPath\PD2Launcher" PD2Launcherv2\PD2Launcherv2.csproj
dotnet publish /p:DebugType=None /p:DebugSymbols=false -o "$publishPath\SteamPD2" SteamPD2\SteamPD2.csproj
dotnet publish /p:DebugType=None /p:DebugSymbols=false -o "$publishPath\UpdateUtility" UpdateUtility\UpdateUtility.csproj

# Optionally remove XML files from all published folders
Get-ChildItem "$publishPath" -Recurse -Filter *.xml | Remove-Item -Force

Pop-Location