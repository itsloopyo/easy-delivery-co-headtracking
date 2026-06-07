#!/usr/bin/env pwsh
# Populates src/EasyDeliveryCoHeadTracking/libs/ for a game-free build.
# BepInEx DLLs come from the committed vendor zip. Unity reference stubs are
# compiled from the checked-in UnityStubs.cs. No Easy Delivery Co installation needed.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot  = Split-Path -Parent $scriptDir
$libsPath     = Join-Path $projectRoot 'src\EasyDeliveryCoHeadTracking\libs'
$vendorZip    = Join-Path $projectRoot 'vendor\bepinex\BepInEx_win_x64.zip'
$stubSource   = Join-Path $libsPath 'UnityStubs.cs'

if (-not (Test-Path $vendorZip)) { throw "Vendored BepInEx not found at $vendorZip" }
if (-not (Test-Path $stubSource)) { throw "UnityStubs.cs not found at $libsPath" }

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null

Write-Host "Bootstrapping build dependencies (no game install required)..." -ForegroundColor Cyan

# Wipe libs/ except the tracked stub source so stale game DLLs can't mask CI parity.
Get-ChildItem -Path $libsPath -Force |
    Where-Object { $_.Name -ne 'UnityStubs.cs' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# BepInEx from vendor zip
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

# Unity reference stubs compiled from UnityStubs.cs
function Build-Stub([string]$assemblyName, [string]$compileItem) {
    $proj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <AssemblyName>$assemblyName</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0169;CS0649;CS0067;CS0660;CS0661</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$compileItem" />
  </ItemGroup>
</Project>
"@
    $projPath = Join-Path $libsPath "Stub_$assemblyName.csproj"
    $proj | Out-File -FilePath $projPath -Encoding utf8
    dotnet build $projPath -c Release -o $libsPath --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build stub $assemblyName" }
    Remove-Item $projPath -ErrorAction SilentlyContinue
    Write-Host "  Stub: $assemblyName.dll" -ForegroundColor Gray
}

Build-Stub 'UnityEngine' 'UnityStubs.cs'

$emptySource = Join-Path $libsPath 'EmptyStub.cs'
'// Empty stub assembly' | Out-File -FilePath $emptySource -Encoding utf8
foreach ($m in @(
    'UnityEngine.CoreModule', 'UnityEngine.IMGUIModule', 'UnityEngine.PhysicsModule',
    'UnityEngine.TextRenderingModule', 'UnityEngine.InputLegacyModule'
)) { Build-Stub $m 'EmptyStub.cs' }

Remove-Item $emptySource -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libsPath '*.deps.json') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libsPath '*.pdb')        -Force -ErrorAction SilentlyContinue

Write-Host "Build dependencies ready." -ForegroundColor Green
