#requires -Version 7.0
[CmdletBinding()]
param([string]$GameRoot = '', [string]$OutputPath = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
if (-not $GameRoot) {
    $validation = Join-Path $root 'LAST_BUILD_VALIDATION.txt'
    if (-not (Test-Path -LiteralPath $validation)) { throw 'Run Install.ps1 first, or pass -GameRoot.' }
    $line = Get-Content -LiteralPath $validation | Where-Object { $_.StartsWith('GameRoot=') } | Select-Object -First 1
    if (-not $line) { throw 'GameRoot is missing from the build receipt.' }
    $GameRoot = $line.Substring('GameRoot='.Length)
}
$managed = Join-Path $GameRoot 'Quasimorph_Data/Managed'
$assembly = Join-Path $managed 'Assembly-CSharp.dll'
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'Assembly-CSharp.dll not found.' }
if (-not $OutputPath) { $OutputPath = Join-Path $env:USERPROFILE 'Downloads/QII_17424_GameEvidence.zip' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$gamePrefix = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if ($OutputPath.StartsWith($gamePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence output must be outside the game installation.' }
$temp = Join-Path ([IO.Path]::GetTempPath()) ('qii-game-evidence-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $temp 'Managed') -Force | Out-Null
try {
    $files = [Collections.Generic.List[object]]::new()
    foreach ($file in (Get-ChildItem -LiteralPath $managed -File -Filter '*.dll' | Sort-Object Name)) {
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $copy = Join-Path $temp ('Managed/' + $file.Name)
        Copy-Item -LiteralPath $file.FullName -Destination $copy
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $hash) { throw "Game file changed during collection: $($file.Name)" }
        $files.Add([ordered]@{ Path = ('Managed/' + $file.Name); SHA256 = $hash; Length = $file.Length })
    }
    $gameHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    if ($gameHash -ne ($files | Where-Object Path -eq 'Managed/Assembly-CSharp.dll').SHA256) { throw 'Game updated during evidence collection. Retry after the update finishes.' }
    $manifest = [ordered]@{
        Purpose = 'Current game metadata for Loot/Scavenger/source-family compatibility review. Collection does not verify compatibility.'
        CollectedAt = [DateTime]::UtcNow.ToString('o'); GameSHA256 = $gameHash
        GameCodeInvoked = $false; SavesIncluded = $false; RuntimeAcceptanceIncluded = $false
        Files = $files.ToArray()
    }
    [IO.File]::WriteAllText((Join-Path $temp 'evidence.json'), ($manifest | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    $validation = Join-Path $root 'LAST_BUILD_VALIDATION.txt'
    if (Test-Path -LiteralPath $validation) { Copy-Item -LiteralPath $validation -Destination $temp }
    New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
    $zipTemp = $OutputPath + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.Compression.ZipFile]::CreateFromDirectory($temp, $zipTemp)
        Move-Item -LiteralPath $zipTemp -Destination $OutputPath -Force
    } finally { if (Test-Path -LiteralPath $zipTemp) { Remove-Item -LiteralPath $zipTemp -Force } }
    Write-Host ('Game evidence saved: ' + $OutputPath) -ForegroundColor Green
    Write-Host ('Game SHA256: ' + $gameHash) -ForegroundColor DarkGray
} finally { Remove-Item -LiteralPath $temp -Recurse -Force }
