$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot

try {
    $mainWindowPath = Join-Path $PSScriptRoot "PD2Launcherv2\MainWindow.xaml"

    if (-not (Test-Path $mainWindowPath)) {
        throw "Could not find MainWindow.xaml at: $mainWindowPath"
    }

    $xamlContent = Get-Content $mainWindowPath -Raw

    $versionMatch = [regex]::Match($xamlContent, 'Text="v\s*([0-9]+\.[0-9]+\.[0-9]+)"')
    if (-not $versionMatch.Success) {
        throw "Could not find version text in MainWindow.xaml"
    }

    $version = $versionMatch.Groups[1].Value

    $publishPath = "C:\Users\Pritc\OneDrive\Desktop\testPublish\dist"
    $zipPath = "C:\Users\Pritc\OneDrive\Desktop\testPublish\PD2Launcher_v$version`_ReleaseCandidate.zip"

    Write-Host "Version: $version"
    Write-Host "Publish path: $publishPath"
    Write-Host "Zip path: $zipPath"

    if (Test-Path $publishPath) {
        Remove-Item $publishPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishPath | Out-Null

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    dotnet publish -c Release /p:DebugType=None /p:DebugSymbols=false -o $publishPath PD2Launcherv2\PD2Launcherv2.csproj
    dotnet publish -c Release /p:DebugType=None /p:DebugSymbols=false -o $publishPath SteamPD2\SteamPD2.csproj
    dotnet publish -c Release /p:DebugType=None /p:DebugSymbols=false -o $publishPath UpdateUtility\UpdateUtility.csproj

    Get-ChildItem $publishPath -Recurse -Filter *.xml |
        Remove-Item -Force -ErrorAction SilentlyContinue

    $publishedFiles = Get-ChildItem $publishPath -File -Recurse
    if (-not $publishedFiles -or $publishedFiles.Count -eq 0) {
        throw "No published artifacts were found in $publishPath"
    }

    Write-Host "Found $($publishedFiles.Count) files to zip."

    Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath -Force

    if (-not (Test-Path $zipPath)) {
        throw "Zip file was not created: $zipPath"
    }

    Write-Host ""
    Write-Host "Build complete."
    Write-Host "Version: $version"
    Write-Host "Artifacts folder: $publishPath"
    Write-Host "Zip created: $zipPath"
}
catch {
    Write-Host ""
    Write-Host "Build failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Pop-Location
}