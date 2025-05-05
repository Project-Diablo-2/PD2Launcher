Push-Location $PSScriptRoot

dotnet publish /p:DebugType=None /p:DebugSymbols=false PD2Launcherv2\PD2Launcherv2.csproj
dotnet publish /p:DebugType=None /p:DebugSymbols=false SteamPD2\SteamPD2.csproj
dotnet publish /p:DebugType=None /p:DebugSymbols=false UpdateUtility\UpdateUtility.csproj

Remove-Item -Path bin/publish/*.xml -Force

Pop-Location
