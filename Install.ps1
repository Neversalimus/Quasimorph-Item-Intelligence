#requires -Version 7.0
param(
    [string]$GameRoot = '',
    [string]$WorkshopStage = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$stage = if ($WorkshopStage) { $WorkshopStage } else { Join-Path $root '_stage\ItemIntelligence' }
$buildScript = Join-Path $root 'BUILD_AND_STAGE.ps1'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) { throw 'BUILD_AND_STAGE.ps1 not found.' }

Write-Host 'Item Intelligence v1.7.43.3-test1 - Official Languages' -ForegroundColor Cyan
Write-Host 'Builds the test source and prepares a local package.' -ForegroundColor DarkGray
Write-Host 'No Steam upload is performed automatically.' -ForegroundColor DarkGray
Write-Host 'This installer does not overwrite the live subscribed Workshop copy.' -ForegroundColor DarkGray
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
Write-Host '[ItemIntelligence] ACTIVE VERSION 1.7.43.3-test1 (OfficialLanguagesTest1).' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Package ready for local testing. Keep a backup before replacing an installed copy.' -ForegroundColor Yellow
