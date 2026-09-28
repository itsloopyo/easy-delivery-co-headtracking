#!/usr/bin/env pwsh
#Requires -Version 5.1
param(
    [Parameter(Position=0)]
    [string]$Version = "",
    # Ship a release even when there are no user-facing commits since the
    # last tag (writes a maintenance changelog entry instead of aborting).
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# THIRD-PARTY-NOTICES.md names the cameraunlock-core commit compiled into the
# release ZIPs, and bumping the submodule does not touch it. Packaging refuses
# to ship that mismatch, so a bump with no notices edit stopped the release
# here, or in CI once the tag had already been pushed. Re-sync it and let this
# release carry the correction.
$noticesRoot = Split-Path -Parent $PSScriptRoot
& git -C $noticesRoot diff --quiet -- THIRD-PARTY-NOTICES.md
if ($LASTEXITCODE -ne 0) { throw "THIRD-PARTY-NOTICES.md has uncommitted edits. Commit or discard them, then re-run." }
& (Join-Path $noticesRoot 'cameraunlock-core\scripts\sync-core-notices.ps1') -Repo $noticesRoot
if ($LASTEXITCODE -ne 0) { throw "sync-core-notices.ps1 exited $LASTEXITCODE - fix THIRD-PARTY-NOTICES.md before releasing." }
& git -C $noticesRoot diff --quiet -- THIRD-PARTY-NOTICES.md
if ($LASTEXITCODE -ne 0) {
    & git -C $noticesRoot commit -q -m 'chore: record the cameraunlock-core commit this build compiles' -- THIRD-PARTY-NOTICES.md
    if ($LASTEXITCODE -ne 0) { throw "Could not commit the re-synced THIRD-PARTY-NOTICES.md." }
    Write-Host 'THIRD-PARTY-NOTICES.md re-synced to the pinned cameraunlock-core commit.' -ForegroundColor Yellow
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\EasyDeliveryCoHeadTracking\EasyDeliveryCoHeadTracking.csproj"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

Write-Host "=== Easy Delivery Co Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release <major|minor|patch|X.Y.Z>" -ForegroundColor White
    Write-Host ""
    Write-Host "Example: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release patch" -ForegroundColor White
    exit 0
}

try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Assert-ReleaseNotBelowCanonicalSince -RepoRoot $projectDir -Version $Version

$tagName = "v$Version"

$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

$status = git status --porcelain -- ':!prebuilt/'
if ($status) {
    Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
    Write-Host $status -ForegroundColor Gray
    exit 1
}

$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Running the full test suite..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    pixi run test
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: pixi run test failed. Nothing was changed." -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Step 1: generate CHANGELOG from commits since last tag. This is the gate
# that aborts when there are no user-facing commits, so run it BEFORE
# mutating any version files or building - a failure here then leaves a
# clean tree instead of stranding a half-applied version bump with no tag.
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
try {
    New-ChangelogFromCommits `
        -ChangelogPath $changelogPath `
        -Version $Version `
        -ArtifactPaths @(
            "src/EasyDeliveryCoHeadTracking/",
            "cameraunlock-core",
            "scripts/install.cmd",
            "scripts/uninstall.cmd",
            "prebuilt/"
        ) `
        -Maintenance:$Force
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $Force) {
        Write-Host "No user-facing changes to release. Re-run with -Force for a maintenance release." -ForegroundColor Yellow
    }
    exit 1
}

# Step 2: Update version
Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

$pluginPath = Join-Path $projectDir "src\EasyDeliveryCoHeadTracking\Core\HeadTrackingPlugin.cs"
$pluginContent = Get-Content $pluginPath -Raw
$pluginContent = $pluginContent -replace 'PluginVersion = "[^"]+"', "PluginVersion = `"$Version`""
$pluginContent | Set-Content $pluginPath -NoNewline
Write-Host "  Updated HeadTrackingPlugin.cs" -ForegroundColor Gray

# install.cmd's MOD_VERSION is what the install writes into the launcher's
# state file, which is where the launcher looks to spot a stale install.
$installCmdPath = Join-Path $projectDir "scripts\install.cmd"
$installCmdContent = Get-Content $installCmdPath -Raw
if ($installCmdContent -notmatch 'set "MOD_VERSION=[^"]+"') { throw "MOD_VERSION line not found in $installCmdPath" }
$installCmdContent = $installCmdContent -replace 'set "MOD_VERSION=[^"]+"', "set `"MOD_VERSION=$Version`""
$installCmdContent | Set-Content $installCmdPath -NoNewline
Write-Host "  Updated install.cmd" -ForegroundColor Gray

# Step 3: Build
Write-Host "Building release..." -ForegroundColor Cyan
Push-Location $projectDir
dotnet build src/EasyDeliveryCoHeadTracking/EasyDeliveryCoHeadTracking.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}

$prebuiltDir = Join-Path $projectDir "prebuilt"
if (-not (Test-Path $prebuiltDir)) {
    New-Item -ItemType Directory -Path $prebuiltDir -Force | Out-Null
}
# Named explicitly, never a *.dll glob. The build output directory is also
# where a copy-local reference would land, so a glob commits whatever the
# compiler happened to stage - including a game or engine assembly the moment
# one reference loses its <Private>false</Private>. Only our own binaries may
# enter this repository.
foreach ($dll in @("EasyDeliveryCoHeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")) {
    $dllPath = "src/EasyDeliveryCoHeadTracking/bin/Release/net48/$dll"
    if (-not (Test-Path $dllPath)) { throw "Expected build output not found: $dllPath" }
    Copy-Item $dllPath $prebuiltDir -Force
}
Write-Host "  Updated prebuilt DLLs" -ForegroundColor Gray
Pop-Location

# Step 4: Commit
Write-Host "Committing changes..." -ForegroundColor Cyan
git add $csprojPath
git add $pluginPath
git add $installCmdPath
git add "$projectDir/prebuilt"
git add $changelogPath
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Commit failed!" -ForegroundColor Red
    exit 1
}

# Step 5: Create tag
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"

# Step 6: Push
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
git push origin $tagName

Write-Host ""
Write-Host "Release $tagName initiated!" -ForegroundColor Green
