#requires -Version 7.0
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $SourceRoot 'BuildTools/StagePaths.ps1')
$identity = 'QiiStagePaths_' + [guid]::NewGuid().ToString('N')
$fixture = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $identity))
$state = @{ Checks = 0 }
function Check([bool]$condition, [string]$reason) {
    $state.Checks++
    if (-not $condition) { throw "Stage path regression: $reason" }
}
function Reject([string]$path, [string]$reason) {
    $rejected = $false
    try { $null = Resolve-QiiStagePath $path $source $game } catch { $rejected = $true }
    Check $rejected $reason
}
function Write-FixturePackage([string]$path) {
    [IO.Directory]::CreateDirectory((Join-Path $path 'Localization')) | Out-Null
    [IO.File]::WriteAllText((Join-Path $path 'modmanifest.json'), '{"UniqueModName":"ItemIntelligence"}')
    [IO.File]::WriteAllText((Join-Path $path 'ItemIntelligence.dll'), 'fixture assembly')
    [IO.File]::WriteAllText((Join-Path $path 'Localization/en.lang'), 'fixture translation')
}
try {
    $source = Join-Path $fixture 'repo'
    $game = Join-Path $fixture 'game'
    [IO.Directory]::CreateDirectory($source) | Out-Null
    [IO.Directory]::CreateDirectory($game) | Out-Null
    $stage = Join-Path $fixture 'package'
    Check ((Resolve-QiiStagePath $stage $source $game) -eq $stage) 'new sibling package accepted'
    $local = Join-Path $source '_stage/ItemIntelligence'
    Check ((Resolve-QiiStagePath $local $source $game) -eq $local) 'documented local package accepted'
    [IO.Directory]::CreateDirectory($stage) | Out-Null
    Check ((Resolve-QiiStagePath $stage $source $game) -eq $stage) 'empty package accepted'
    Write-FixturePackage $stage
    Check ((Resolve-QiiStagePath ($stage + '/.') $source $game) -eq $stage) 'canonical owned package accepted'
    $before = Get-Content -LiteralPath (Join-Path $stage 'ItemIntelligence.dll') -Raw
    foreach ($path in @('', [IO.Path]::GetPathRoot($fixture), $fixture, $source,
        (Join-Path $source 'Source'), (Join-Path $source 'WORKSHOP_CONTENT'), (Join-Path $source '_stage'),
        (Join-Path $source '_stage/../../repo'), $game, (Join-Path $game 'Mods/ItemIntelligence'),
        (Join-Path $fixture 'steamapps/workshop/content/2059170/3780078201'),
        (Join-Path $fixture 'SteamApps/Common/AnotherGame'), [IO.Path]::GetTempPath())) {
        Reject $path 'protected or overlapping destination rejected'
    }
    $unrelated = Join-Path $fixture 'unrelated'
    [IO.Directory]::CreateDirectory($unrelated) | Out-Null
    [IO.File]::WriteAllText((Join-Path $unrelated 'keep.txt'), 'preserve me')
    Reject $unrelated 'unowned nonempty directory rejected'
    Check ((Get-Content -LiteralPath (Join-Path $unrelated 'keep.txt') -Raw) -eq 'preserve me') 'unrelated contents preserved'
    Reject (Join-Path $unrelated 'keep.txt') 'file target rejected'
    [IO.File]::WriteAllText((Join-Path $stage 'save.dat'), 'unrelated save')
    Reject $stage 'foreign file beside valid manifest rejected'
    Remove-Item -LiteralPath (Join-Path $stage 'save.dat')
    $manifest = Join-Path $stage 'modmanifest.json'
    [IO.File]::WriteAllText($manifest, '{"UniqueModName":"AnotherMod"}')
    Reject $stage 'other mod rejected'
    [IO.File]::WriteAllText($manifest, '{}')
    Reject $stage 'missing identity rejected'
    [IO.File]::WriteAllText($manifest, 'malformed')
    Reject $stage 'malformed identity rejected'
    [IO.File]::WriteAllText($manifest, '{"UniqueModName":"ItemIntelligence"}')
    $stale = $stage + '.tmp'
    [IO.Directory]::CreateDirectory($stale) | Out-Null
    [IO.File]::WriteAllText((Join-Path $stale 'keep.txt'), 'preserve tmp')
    Reject $stale 'unowned temporary directory rejected'
    Check ((Get-Content -LiteralPath (Join-Path $stale 'keep.txt') -Raw) -eq 'preserve tmp') 'temporary contents preserved'
    $link = Join-Path $fixture 'linked'
    New-Item -ItemType Junction -Path $link -Target $stage | Out-Null
    Reject $link 'junction target rejected'
    Reject (Join-Path $link 'new-package') 'junction ancestor rejected'
    # Remove the junction itself, never traverse the linked directory.
    [IO.Directory]::Delete($link)
    $nested = Join-Path $stage 'Localization/link'
    New-Item -ItemType Junction -Path $nested -Target $unrelated | Out-Null
    Reject $stage 'nested package junction rejected'
    [IO.Directory]::Delete($nested)
    Check ((Get-Content -LiteralPath (Join-Path $stage 'ItemIntelligence.dll') -Raw) -eq $before) 'validation never writes package files'
    Write-Host "Production stage paths: PASS ($($state.Checks) assertions; temporary filesystem fixtures)." -ForegroundColor Green
} finally {
    $expected = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $identity))
    if ($fixture -ne $expected -or (Split-Path -Leaf $fixture) -notmatch '^QiiStagePaths_[0-9a-f]{32}$') {
        throw 'Refusing cleanup outside the unique staging fixture.'
    }
    # A failed assertion must not leave a traversable junction for recursive cleanup.
    if (Test-Path -LiteralPath $fixture) {
        foreach ($entry in (Get-ChildItem -LiteralPath $fixture -Recurse -Force)) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { [IO.Directory]::Delete($entry.FullName) }
        }
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
}
