#requires -Version 7.0
param(
    [string]$GameRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$DevWorkshopId = '3781927679'
$stage = 'C:\QM_Workshop\ItemIntelligence_DEV'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'BUILD_AND_STAGE.ps1'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) { throw 'BUILD_AND_STAGE.ps1 not found.' }

Write-Host 'Item Intelligence v1.7.43.1-test1 - Weapon damage type test' -ForegroundColor Cyan
Write-Host 'Builds against the currently installed game and stages ONLY the existing DEV Workshop item.' -ForegroundColor DarkGray
Write-Host 'No Steam upload is performed automatically.' -ForegroundColor DarkGray
Write-Host 'PUBLIC Workshop item 3780078201 is protected and is not targeted by this TEST installer.' -ForegroundColor Yellow
Write-Host ''

$buildArguments = @{ Mode = 'TEST'; WorkshopStage = $stage }
if ($GameRoot) { $buildArguments.GameRoot = $GameRoot }
& $buildScript @buildArguments
if ($LASTEXITCODE -ne 0) { throw "TEST build/stage failed with exit code $LASTEXITCODE" }

$stageDll = Join-Path $stage 'ItemIntelligence.dll'
$stageManifest = Join-Path $stage 'modmanifest.json'
if (-not (Test-Path -LiteralPath $stageDll -PathType Leaf)) { throw 'TEST stage validation failed: ItemIntelligence.dll missing.' }
if (-not (Test-Path -LiteralPath $stageManifest -PathType Leaf)) { throw 'TEST stage validation failed: modmanifest.json missing.' }
$stageHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $stageDll).Hash

Write-Host ''
Write-Host 'TEST BUILD + STAGING OK.' -ForegroundColor Green
Write-Host ('Stage: ' + $stage) -ForegroundColor Green
Write-Host ('Stage DLL SHA256: ' + $stageHash) -ForegroundColor Green
Write-Host 'Expected runtime marker:' -ForegroundColor Yellow
Write-Host '[ItemIntelligence] ACTIVE VERSION 1.7.43.1-test1 (WeaponDamageTypeTest1).' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Upload to the existing DEV item only:' -ForegroundColor Yellow
Write-Host ('mod_updateworkshopitem ' + $DevWorkshopId + ' ' + $stage + ' FALSE') -ForegroundColor Cyan
