#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper - dev-deploy orchestration lives in
# cameraunlock-core/powershell/DevDeploy.psm1.

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory=$false, Position=1)]
    [string]$GivenPath,
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\DevDeploy.psm1") -Force
Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ModDeployment.psm1") -Force
$buildOutput = Join-Path $projectRoot "src\EasyDeliveryCoHeadTracking\bin\$Configuration\net48"
$result = Invoke-DevDeployBepInEx `
    -GameId 'easy-delivery-co' `
    -GameDisplayName 'Easy Delivery Co' `
    -BuildOutputPath $buildOutput `
    -ModDllName 'EasyDeliveryCoHeadTracking.dll' `
    -ExtraDlls @('CameraUnlock.Core.dll', 'CameraUnlock.Core.Unity.dll') `
    -GivenPath $GivenPath `
    -EnsureLoader

Write-DeploymentSuccess `
    -ModName "Head Tracking mod" `
    -DeployPath $result.DeployedDllPath `
    -RecenterKey "Home" `
    -ToggleKey "End"