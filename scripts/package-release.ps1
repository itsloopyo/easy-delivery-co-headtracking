#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper: calls shared packaging script with Easy Delivery Co values.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

$modName = "EasyDeliveryCoHeadTracking"
$buildOutputDir = "src/EasyDeliveryCoHeadTracking/bin/Release/net48"
$modDlls = @("EasyDeliveryCoHeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")

$packaged = & "$projectDir/cameraunlock-core/scripts/package-bepinex-mod.ps1" `
    -ModName $modName `
    -CsprojPath "src/EasyDeliveryCoHeadTracking/EasyDeliveryCoHeadTracking.csproj" `
    -BuildOutputDir $buildOutputDir `
    -ModDlls $modDlls `
    -ProjectRoot $projectDir

# cameraunlock-core is MIT under a different copyright holder from this repo's
# LICENSE and is compiled into two of the shipped DLLs, so its notice travels
# with the binaries in both ZIPs. The shared script does not stage it, and this
# repo pins a submodule commit, so correcting it upstream would not reach the
# ZIP until that pointer moved.
$coreLicense = Join-Path $projectDir "cameraunlock-core/LICENSE"
if (-not (Test-Path $coreLicense)) {
    throw "cameraunlock-core/LICENSE is missing - run 'git submodule update --init'"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$installerZip = [System.IO.Compression.ZipFile]::Open($packaged.GithubZip, 'Update')
try {
    $entryName = "licenses/cameraunlock-core-LICENSE.txt"
    $existing = $installerZip.GetEntry($entryName)
    if ($existing) { $existing.Delete() }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($installerZip, $coreLicense, $entryName) | Out-Null
} finally {
    $installerZip.Dispose()
}
Write-Host "  $entryName (installer ZIP)" -ForegroundColor Green

# The Nexus ZIP is built here rather than through the shared script's
# -CreateNexusZip switch. That switch stages the payload subtree and nothing
# else, so the ZIP went out carrying three DLLs with no licence text at all.
# Both our own MIT licence and cameraunlock-core's require the notice to travel
# with the binary, and a Nexus download is a binary distribution like any other.
# The fix cannot live only in the submodule: this repo pins a commit, so a
# corrected shared script would not reach the ZIP until that pointer moved.
Write-Host ""
Write-Host "=== Creating NexusMods ZIP ===" -ForegroundColor Magenta
Write-Host ""

[xml]$csproj = Get-Content (Join-Path $projectDir "src/EasyDeliveryCoHeadTracking/EasyDeliveryCoHeadTracking.csproj")
$version = $csproj.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
if (-not $version) { throw "No <Version> found in the csproj" }

$releaseDir = Join-Path $projectDir "release"
$stagingDir = Join-Path $releaseDir "staging-nexus"
if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }

$pluginsDir = Join-Path $stagingDir "BepInEx/plugins"
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    $dllPath = Join-Path $projectDir (Join-Path $buildOutputDir $dll)
    if (-not (Test-Path $dllPath)) { throw "Mod DLL not found: $dllPath" }
    Copy-Item $dllPath -Destination $pluginsDir -Force
    Write-Host "  BepInEx/plugins/$dll" -ForegroundColor Green
}

# throw, never a Test-Path guard. A skipped licence copy turns a compliance
# failure into a green build, which is how a ZIP ships without one.
foreach ($doc in @("LICENSE", "THIRD-PARTY-NOTICES.md", "README.md")) {
    $docPath = Join-Path $projectDir $doc
    if (-not (Test-Path $docPath)) { throw "$doc is missing - it must ship in the Nexus ZIP" }
    Copy-Item $docPath -Destination $stagingDir -Force
    Write-Host "  $doc" -ForegroundColor Green
}

$licensesDir = Join-Path $stagingDir "licenses"
New-Item -ItemType Directory -Path $licensesDir -Force | Out-Null
Copy-Item $coreLicense -Destination (Join-Path $licensesDir "cameraunlock-core-LICENSE.txt") -Force
Write-Host "  licenses/cameraunlock-core-LICENSE.txt" -ForegroundColor Green

$nexusZipPath = Join-Path $releaseDir "$modName-v$version-nexus.zip"
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }

Push-Location $stagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $nexusZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $stagingDir

Write-Host ""
Write-Host "NexusMods archive: $nexusZipPath" -ForegroundColor Green
Write-Host ("Size: {0:N1} KB" -f ((Get-Item $nexusZipPath).Length / 1KB)) -ForegroundColor White
