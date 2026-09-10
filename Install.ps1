#requires -Version 7.0
param(
    [string]$GameRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$PublicWorkshopId = '3780078201'
$stage = 'C:\QM_Workshop\ItemIntelligence'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'BUILD_AND_STAGE.ps1'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) { throw 'BUILD_AND_STAGE.ps1 not found.' }

Write-Host 'Item Intelligence v1.7.42.5 - Stable Release' -ForegroundColor Cyan
Write-Host 'Builds the stable source and prepares the existing public Workshop payload.' -ForegroundColor DarkGray
Write-Host 'No Steam upload is performed automatically.' -ForegroundColor DarkGray
Write-Host 'This installer does not overwrite the live subscribed Workshop copy.' -ForegroundColor DarkGray
Write-Host ''

$buildArguments = @{ Mode = 'RELEASE'; WorkshopStage = $stage }
if ($GameRoot) { $buildArguments.GameRoot = $GameRoot }
& $buildScript @buildArguments
if ($LASTEXITCODE -ne 0) { throw "RELEASE build/stage failed with exit code $LASTEXITCODE" }

$stageDll = Join-Path $stage 'ItemIntelligence.dll'
$stageManifest = Join-Path $stage 'modmanifest.json'
if (-not (Test-Path -LiteralPath $stageDll -PathType Leaf)) { throw 'RELEASE stage validation failed: ItemIntelligence.dll missing.' }
if (-not (Test-Path -LiteralPath $stageManifest -PathType Leaf)) { throw 'RELEASE stage validation failed: modmanifest.json missing.' }
$stageHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $stageDll).Hash

Write-Host ''
Write-Host 'RELEASE BUILD + STAGING OK.' -ForegroundColor Green
Write-Host ('Stage: ' + $stage) -ForegroundColor Green
Write-Host ('Stage DLL SHA256: ' + $stageHash) -ForegroundColor Green
Write-Host 'Expected runtime marker:' -ForegroundColor Yellow
Write-Host '[ItemIntelligence] ACTIVE VERSION 1.7.42.5 (StableRelease17425).' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Upload to the existing public item, then enable the public copy in the game:' -ForegroundColor Yellow
Write-Host ('mod_updateworkshopitem ' + $PublicWorkshopId + ' ' + $stage + ' FALSE') -ForegroundColor Cyan

# Current 1.0.4 game evidence has been reviewed. Keep the collector optional for future updates.
