$ErrorActionPreference = "Stop"
$buildFailed = $false

Push-Location $PSScriptRoot

try {
    $mainWindowPath = Join-Path `
        $PSScriptRoot `
        "PD2Launcherv2\MainWindow.xaml"

    if (-not (Test-Path $mainWindowPath -PathType Leaf)) {
        throw "Could not find MainWindow.xaml at: $mainWindowPath"
    }

    $xamlContent = Get-Content $mainWindowPath -Raw

    # Accepts versions such as:
    # v 2.14.1
    # v 2.14.AWS1
    # v 2.14.SUCCESS
    $versionMatch = [regex]::Match(
        $xamlContent,
        'Text="v\s*([^"]+)"'
    )

    if (-not $versionMatch.Success) {
        throw "Could not find version text in MainWindow.xaml"
    }

    $version = $versionMatch.Groups[1].Value.Trim()
    $safeVersion = $version -replace '[\\/:*?"<>|]', '-'

    $outputRoot = "C:\Users\Pritc\OneDrive\Desktop\testPublish"
    $publishPath = Join-Path $outputRoot "dist"

    $zipPath = Join-Path `
        $outputRoot `
        "PD2Launcher_v$safeVersion`_ReleaseCandidate.zip"

    $launcherBaseUrl =
        "https://pd2-launcher.projectdiablo2.com/"

    $manifestFileName =
        "launcher_manifest.json"

    $manifestPath = Join-Path `
        $publishPath `
        $manifestFileName

    Write-Host ""
    Write-Host "Version: $version"
    Write-Host "Publish path: $publishPath"
    Write-Host "Manifest path: $manifestPath"
    Write-Host "Zip path: $zipPath"
    Write-Host ""

    # Start with a completely clean output directory.
    if (Test-Path $publishPath) {
        Remove-Item `
            $publishPath `
            -Recurse `
            -Force
    }

    New-Item `
        -ItemType Directory `
        -Path $publishPath `
        -Force |
        Out-Null

    if (Test-Path $zipPath) {
        Remove-Item `
            $zipPath `
            -Force
    }

    Write-Host "Publishing PD2Launcher..."

    dotnet publish `
        PD2Launcherv2\PD2Launcherv2.csproj `
        -c Release `
        /p:DebugType=None `
        /p:DebugSymbols=false `
        -o $publishPath

    if ($LASTEXITCODE -ne 0) {
        throw (
            "PD2Launcher publish failed with exit code " +
            "$LASTEXITCODE"
        )
    }

    Write-Host ""
    Write-Host "Publishing SteamPD2..."

    dotnet publish `
        SteamPD2\SteamPD2.csproj `
        -c Release `
        /p:DebugType=None `
        /p:DebugSymbols=false `
        -o $publishPath

    if ($LASTEXITCODE -ne 0) {
        throw (
            "SteamPD2 publish failed with exit code " +
            "$LASTEXITCODE"
        )
    }

    Write-Host ""
    Write-Host "Publishing UpdateUtility..."

    dotnet publish `
        UpdateUtility\UpdateUtility.csproj `
        -c Release `
        /p:DebugType=None `
        /p:DebugSymbols=false `
        -o $publishPath

    if ($LASTEXITCODE -ne 0) {
        throw (
            "UpdateUtility publish failed with exit code " +
            "$LASTEXITCODE"
        )
    }

    # PD2Shared is compiled into the published single-file executables.
    # Do not distribute the standalone DLL.
    $legacySharedDllPath = Join-Path `
        $publishPath `
        "PD2Shared.dll"

    if (Test-Path $legacySharedDllPath -PathType Leaf) {
        Remove-Item `
            $legacySharedDllPath `
            -Force

        Write-Host ""
        Write-Host "Removed standalone PD2Shared.dll"
    }

    # Remove generated XML documentation.
    Get-ChildItem `
        -Path $publishPath `
        -Recurse `
        -Filter "*.xml" `
        -ErrorAction SilentlyContinue |
        Remove-Item `
            -Force `
            -ErrorAction SilentlyContinue

    # These are the only launcher-update binaries.
    $requiredFiles = @(
        "PD2Launcher.exe",
        "SteamPD2.exe",
        "UpdateUtility.exe"
    )

    foreach ($requiredFile in $requiredFiles) {
        $requiredPath = Join-Path `
            $publishPath `
            $requiredFile

        if (-not (Test-Path $requiredPath -PathType Leaf)) {
            throw "Required artifact was not found: $requiredPath"
        }
    }

    if (Test-Path $legacySharedDllPath -PathType Leaf) {
        throw (
            "PD2Shared.dll still exists in the publish folder " +
            "and must not be distributed."
        )
    }

    Write-Host ""
    Write-Host "Generating $manifestFileName..."

    if (-not ("Pd2LauncherManifestCrc32C" -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.IO;

public static class Pd2LauncherManifestCrc32C
{
    private const uint Polynomial = 0x82F63B78u;

    private static readonly uint[] Table =
        BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (uint i = 0; i < table.Length; i++)
        {
            uint value = i;

            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1u) != 0
                    ? Polynomial ^ (value >> 1)
                    : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }

    public static string ComputeBytesBase64(byte[] bytes)
    {
        uint crc = uint.MaxValue;

        for (int i = 0; i < bytes.Length; i++)
        {
            int tableIndex =
                (int)((crc ^ bytes[i]) & 0xffu);

            crc =
                Table[tableIndex] ^
                (crc >> 8);
        }

        return ToBase64BigEndian(~crc);
    }

    public static string ComputeFileBase64(
        string filePath)
    {
        uint crc = uint.MaxValue;
        byte[] buffer = new byte[1024 * 1024];

        using (var stream = File.OpenRead(filePath))
        {
            int bytesRead;

            while (
                (bytesRead =
                    stream.Read(
                        buffer,
                        0,
                        buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    int tableIndex =
                        (int)((crc ^ buffer[i]) & 0xffu);

                    crc =
                        Table[tableIndex] ^
                        (crc >> 8);
                }
            }
        }

        return ToBase64BigEndian(~crc);
    }

    private static string ToBase64BigEndian(uint crc)
    {
        byte[] bytes =
        {
            (byte)(crc >> 24),
            (byte)(crc >> 16),
            (byte)(crc >> 8),
            (byte)crc
        };

        return Convert.ToBase64String(bytes);
    }
}
"@
    }

    # Validate the CRC32C implementation.
    $crcTestBytes =
        [System.Text.Encoding]::ASCII.GetBytes(
            "123456789"
        )

    $crcTestResult =
        [Pd2LauncherManifestCrc32C]::ComputeBytesBase64(
            $crcTestBytes
        )

    if ($crcTestResult -ne "4waSgw==") {
        throw (
            "CRC32C self-test failed. " +
            "Expected 4waSgw== but received " +
            "$crcTestResult"
        )
    }

    $normalizedBaseUrl =
        $launcherBaseUrl.TrimEnd("/") + "/"

    $manifestItems = foreach (
        $fileName in $requiredFiles
    ) {
        $filePath = Join-Path `
            $publishPath `
            $fileName

        $fileInfo = Get-Item $filePath

        [ordered]@{
            name = $fileName

            mediaLink = (
                $normalizedBaseUrl +
                [Uri]::EscapeDataString($fileName)
            )

            crc32c = (
                [Pd2LauncherManifestCrc32C]::ComputeFileBase64(
                    $filePath
                )
            )

            size = $fileInfo.Length
        }
    }

    $manifest = [ordered]@{
        items = @($manifestItems)
    }

    $manifestJson =
        $manifest |
        ConvertTo-Json -Depth 4

    $utf8WithoutBom =
        New-Object `
            System.Text.UTF8Encoding($false)

    [System.IO.File]::WriteAllText(
        $manifestPath,
        $manifestJson,
        $utf8WithoutBom
    )

    if (-not (Test-Path $manifestPath -PathType Leaf)) {
        throw "Manifest was not created: $manifestPath"
    }

    Write-Host "Created manifest: $manifestPath"

    Write-Host ""
    Write-Host "Manifest contents:"

    $manifestItems |
        ForEach-Object {
            [PSCustomObject]@{
                Name = $_.name
                SizeBytes = $_.size
                Crc32c = $_.crc32c
                MediaLink = $_.mediaLink
            }
        } |
        Format-Table -AutoSize

    # Read the manifest back and verify it.
    $generatedManifest =
        Get-Content `
            $manifestPath `
            -Raw |
        ConvertFrom-Json

    $generatedNames = @(
        $generatedManifest.items |
        ForEach-Object {
            $_.name
        }
    )

    if ($generatedNames.Count -ne 3) {
        throw (
            "Manifest contains " +
            "$($generatedNames.Count) files. " +
            "Expected exactly 3."
        )
    }

    foreach ($requiredFile in $requiredFiles) {
        if ($generatedNames -notcontains $requiredFile) {
            throw (
                "Manifest is missing required file: " +
                "$requiredFile"
            )
        }
    }

    if ($generatedNames -contains "PD2Shared.dll") {
        throw "Manifest must not contain PD2Shared.dll."
    }

    $publishedFiles = @(
        Get-ChildItem `
            -Path $publishPath `
            -File `
            -Recurse
    )

    if ($publishedFiles.Count -eq 0) {
        throw (
            "No published artifacts were found in " +
            "$publishPath"
        )
    }

    Write-Host ""
    Write-Host (
        "Found $($publishedFiles.Count) files to zip."
    )

    $publishedFiles |
        Select-Object `
            Name,
            Length,
            LastWriteTime |
        Format-Table -AutoSize

    Compress-Archive `
        -Path (Join-Path $publishPath "*") `
        -DestinationPath $zipPath `
        -Force

    if (-not (Test-Path $zipPath -PathType Leaf)) {
        throw "Zip file was not created: $zipPath"
    }

    Write-Host ""
    Write-Host "Build complete." -ForegroundColor Green
    Write-Host "Version: $version"
    Write-Host "Artifacts folder: $publishPath"
    Write-Host "Manifest: $manifestPath"
    Write-Host "Zip created: $zipPath"

    Write-Host ""
    Write-Host "Release files:" -ForegroundColor Green

    Get-ChildItem `
        -Path $publishPath `
        -File |
        Select-Object `
            Name,
            Length |
        Format-Table -AutoSize
}
catch {
    $buildFailed = $true

    Write-Host ""
    Write-Host "BUILD FAILED" -ForegroundColor Red
    Write-Host ""

    Write-Host "Message:" -ForegroundColor Yellow
    Write-Host $_.Exception.Message

    Write-Host ""
    Write-Host "Location:" -ForegroundColor Yellow
    Write-Host $_.InvocationInfo.PositionMessage

    Write-Host ""
    Write-Host "Full error:" -ForegroundColor Yellow
    Write-Host ($_ | Out-String)
}
finally {
    Pop-Location
}

Write-Host ""

if ($buildFailed) {
    Read-Host "Build failed. Press Enter to close"
    exit 1
}

Read-Host "Build complete. Press Enter to close"
exit 0