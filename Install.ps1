# Item Intelligence v1.7.41.1 stable release builder/stager.
# Builds RELEASE and prepares the existing PUBLIC Workshop payload.
# It does not upload to Steam and does not overwrite the live subscribed Workshop copy.

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$PublicWorkshopId = '3780078201'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'BUILD_AND_STAGE.ps1'
$stage = 'C:\QM_Workshop\ItemIntelligence'

if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
    throw 'BUILD_AND_STAGE.ps1 not found.'
}

Write-Host 'Item Intelligence v1.7.41.1 - Stable Release' -ForegroundColor Cyan
Write-Host ('Builds RELEASE and stages the existing PUBLIC Workshop item ' + $PublicWorkshopId + '.') -ForegroundColor DarkGray
Write-Host 'No Steam upload is performed automatically.' -ForegroundColor DarkGray
Write-Host ''

$buildArguments = @{ Mode = 'RELEASE'; WorkshopStage = $stage }
& $buildScript @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Stable release build/staging failed with exit code $LASTEXITCODE"
}

$dll = Join-Path $stage 'ItemIntelligence.dll'
$manifest = Join-Path $stage 'modmanifest.json'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Stable stage validation failed: ItemIntelligence.dll missing.' }
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Stable stage validation failed: modmanifest.json missing.' }
if (Test-Path -LiteralPath (Join-Path $stage 'ItemIntelligenceAutoTests.dll')) { throw 'Stable stage validation failed: AutoTests leaked into stage.' }

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash
Write-Host ''
Write-Host 'STABLE RELEASE BUILD + STAGING OK.' -ForegroundColor Green
Write-Host ('Stage: ' + $stage) -ForegroundColor Green
Write-Host ('DLL SHA256: ' + $hash) -ForegroundColor Green
Write-Host ''
Write-Host 'Expected runtime marker:' -ForegroundColor Yellow
Write-Host '[ItemIntelligence] ACTIVE VERSION 1.7.41.1 (StableRelease17411).' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Publish the prepared stage to the EXISTING public Workshop item with:' -ForegroundColor Yellow
Write-Host ('mod_updateworkshopitem ' + $PublicWorkshopId + ' ' + $stage + ' FALSE') -ForegroundColor Cyan
