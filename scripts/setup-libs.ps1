#!/usr/bin/env pwsh
# Provisions the BepInEx reference assemblies into src/EasyDeliveryCoHeadTracking/libs/
# from the committed vendor zip. The Unity reference stubs are built separately by
# cameraunlock-core/csharp/stubs/build-unity-stubs.ps1, which `pixi run setup` runs
# after this. No Easy Delivery Co installation needed.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot  = Split-Path -Parent $scriptDir
$libsPath     = Join-Path $projectRoot 'src\EasyDeliveryCoHeadTracking\libs'
$vendorZip    = Join-Path $projectRoot 'vendor\bepinex\BepInEx_win_x64.zip'

if (-not (Test-Path $vendorZip)) { throw "Vendored BepInEx not found at $vendorZip" }

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null

Write-Host "Bootstrapping build dependencies (no game install required)..." -ForegroundColor Cyan

# Wipe libs/ so a game DLL copied in by hand can't mask CI parity. Everything the
# build needs is regenerated: BepInEx below, the Unity stubs by the shared script.
Get-ChildItem -Path $libsPath -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Add-Type -AssemblyName System.IO.Compression.FileSystem
$tempDir = Join-Path $env:TEMP ("edc-bep-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($vendorZip, $tempDir)
    foreach ($dll in @('BepInEx.dll', '0Harmony.dll')) {
        $src = Join-Path $tempDir "BepInEx\core\$dll"
        if (-not (Test-Path $src)) { throw "$dll not found in vendor zip at BepInEx\core\" }
        Copy-Item $src (Join-Path $libsPath $dll) -Force
        Write-Host "  BepInEx: $dll" -ForegroundColor Gray
    }
} finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Loader assemblies ready." -ForegroundColor Green
