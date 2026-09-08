#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$SourceRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Stage = 'C:\QM_Workshop\ItemIntelligence'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ReleaseFiles.ps1')
$repo = 'Neversalimus/Quasimorph-Item-Intelligence'
$SourceRoot = (Resolve-Path -LiteralPath $SourceRoot).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Freeze output already exists. Use its receipt to resume, or choose a new directory.' }
$runtime = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.Runtime.cs'))
$version = [regex]::Match($runtime, 'public const string Version = "([^"]+)";').Groups[1].Value
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'DEV source cannot be published. Use reviewed stable source after game acceptance.' }
$status = @(Invoke-ReleaseNative git @('-C',$SourceRoot,'status','--porcelain','--untracked-files=all'))
if ($status.Count) { throw 'Commit the reviewed source before freezing a release; the source tree must be clean.' }
if ($OutputDirectory.StartsWith($SourceRoot.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Freeze output must be outside the source tree.' }
$remote = [string](Invoke-ReleaseNative git @('-C',$SourceRoot,'remote','get-url','origin'))
if ($remote -notmatch '^https://github\.com/Neversalimus/Quasimorph-Item-Intelligence(?:\.git)?/?$|^git@github\.com:Neversalimus/Quasimorph-Item-Intelligence(?:\.git)?$') { throw 'Unexpected source origin.' }
$commit = [string](Invoke-ReleaseNative git @('-C',$SourceRoot,'rev-parse','HEAD'))
$mainLine = [string](Invoke-ReleaseNative git @('-C',$SourceRoot,'ls-remote','origin','refs/heads/main'))
$baseMain = ($mainLine -split '\s+')[0]
if ($baseMain -notmatch '^[0-9a-f]{40}$') { throw 'Cannot resolve main.' }
Invoke-ReleaseNative git @('-C',$SourceRoot,'fetch','origin','main') | Out-Null
Invoke-ReleaseNative git @('-C',$SourceRoot,'merge-base','--is-ancestor',$baseMain,$commit) | Out-Null
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $SourceRoot 'Source') -Recurse -File -Filter '*.cs')) {
    Invoke-ReleaseNative git @('-C',$SourceRoot,'ls-files','--error-unmatch','--',$file.FullName) | Out-Null
}
$sourceFiles = @(Get-TrackedSourceInventory $SourceRoot)
& (Join-Path $SourceRoot 'BUILD_AND_STAGE.ps1') -Mode RELEASE -GameRoot $GameRoot -WorkshopStage $Stage
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
Assert-InventoryEntries @(Get-TrackedSourceInventory $SourceRoot) $sourceFiles 'source during compilation'
$frozen = New-FrozenPayload $Stage $OutputDirectory $version
$receipt = [ordered]@{
    Schema = 2; Version = $version; Tag = "v$version"; Repo = $repo
    SourceRoot = $SourceRoot; SourceCommit = $commit; BaseMain = $baseMain
    SourceFiles = $sourceFiles
    WorkshopId = '3780078201'; Stage = [IO.Path]::GetFullPath($Stage)
    GameSHA256 = (Get-FileHash -LiteralPath (Join-Path $GameRoot 'Quasimorph_Data/Managed/Assembly-CSharp.dll') -Algorithm SHA256).Hash
    Payload = $frozen.Payload; Assets = $frozen.Assets
    PreparedAt = [DateTime]::UtcNow.ToString('o')
}
$receiptPath = Join-Path $OutputDirectory 'release-receipt.json'
Write-ReleaseJson $receipt $receiptPath
Write-Host "Frozen release: $receiptPath" -ForegroundColor Green
# Resolve the actual next script instead of constructing a version-dependent filename.
$publisher = Join-Path $PSScriptRoot 'Publish-Release.ps1'
if (-not (Test-Path -LiteralPath $publisher)) { throw 'Publish-Release.ps1 is missing.' }
Write-Host ('After Steam acceptance: & "' + $publisher + '" -ReceiptPath "' + $receiptPath + '" -WorkshopPublished')
