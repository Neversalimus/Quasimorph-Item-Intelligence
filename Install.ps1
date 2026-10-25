#requires -Version 7.0
param([string]$GameRoot = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$DevWorkshopId = '3781927679'
$stage = 'C:\QM_Workshop\ItemIntelligence_DEV'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'BUILD_AND_STAGE.ps1'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) { throw 'BUILD_AND_STAGE.ps1 missing.' }

Write-Host 'Item Intelligence 1.7.43.2-test5 - DEV validation only' -ForegroundColor Cyan
Write-Host 'No Steam upload is performed automatically.'
Write-Host 'PUBLIC Workshop item 3780078201 is protected and not targeted.' -ForegroundColor Yellow

$buildArguments = @{ Mode = 'TEST'; WorkshopStage = $stage }
if ($GameRoot) { $buildArguments.GameRoot = $GameRoot }
& $buildScript @buildArguments
if ($LASTEXITCODE -ne 0) { throw "TEST build/stage failed: $LASTEXITCODE" }
$stageDll = Join-Path $stage 'ItemIntelligence.dll'
$stageManifest = Join-Path $stage 'modmanifest.json'
if (-not (Test-Path -LiteralPath $stageDll -PathType Leaf)) { throw 'TEST DLL missing.' }
if (-not (Test-Path -LiteralPath $stageManifest -PathType Leaf)) { throw 'TEST manifest missing.' }

$stageHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $stageDll).Hash
Write-Host 'TEST BUILD + STAGING OK.' -ForegroundColor Green
Write-Host ('Stage: ' + $stage)
Write-Host ('Stage DLL SHA256: ' + $stageHash)
Write-Host '[ItemIntelligence] ACTIVE VERSION 1.7.43.2-test5 (UpgradeUsesTest5).'
Write-Host ('mod_updateworkshopitem ' + $DevWorkshopId + ' ' + $stage + ' FALSE') -ForegroundColor Cyan
