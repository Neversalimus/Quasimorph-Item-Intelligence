param(
    [string]$GameRoot = '',
    [string]$WorkshopStage = 'C:\QM_Workshop\ItemIntelligence'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'BUILD_AND_STAGE.ps1'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) { throw 'BUILD_AND_STAGE.ps1 not found.' }

Write-Host 'Item Intelligence v1.7.39 - Stable Release' -ForegroundColor Cyan
Write-Host 'Builds v1.7.39 and stages it for the existing PUBLIC Workshop item 3780078201.' -ForegroundColor DarkGray
Write-Host 'This script updates only the existing public staging folder and does not create a new Workshop item.' -ForegroundColor DarkGray
Write-Host ''

$buildArguments = @{ Mode = 'RELEASE'; WorkshopStage = $WorkshopStage }
if ($GameRoot) { $buildArguments.GameRoot = $GameRoot }
& pwsh -NoProfile -ExecutionPolicy Bypass -File $buildScript @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Release build/stage failed with exit code $LASTEXITCODE" }

$stageDll = Join-Path $WorkshopStage 'ItemIntelligence.dll'
$stageManifest = Join-Path $WorkshopStage 'modmanifest.json'
if (-not (Test-Path -LiteralPath $stageDll -PathType Leaf)) { throw 'Release stage validation failed: ItemIntelligence.dll missing.' }
if (-not (Test-Path -LiteralPath $stageManifest -PathType Leaf)) { throw 'Release stage validation failed: modmanifest.json missing.' }

Write-Host ''
Write-Host 'PUBLIC RELEASE build + Workshop staging OK.' -ForegroundColor Green
Write-Host ('Stage: ' + $WorkshopStage) -ForegroundColor Green
Write-Host ('DLL SHA256: ' + (Get-FileHash -Algorithm SHA256 -LiteralPath $stageDll).Hash) -ForegroundColor Green
Write-Host ''
Write-Host 'Update the EXISTING PUBLIC Workshop item from the Quasimorph developer console:' -ForegroundColor Yellow
Write-Host ('mod_updateworkshopitem 3780078201 ' + $WorkshopStage + ' FALSE') -ForegroundColor Cyan
Write-Host ''
Write-Host 'Expected runtime marker:' -ForegroundColor Yellow
Write-Host '[ItemIntelligence] ACTIVE VERSION 1.7.39 (StableRelease1739).' -ForegroundColor Cyan
