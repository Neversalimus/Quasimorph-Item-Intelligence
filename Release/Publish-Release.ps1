#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReceiptPath, [switch]$WorkshopPublished, [switch]$ExistingReleaseOnly)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ReleaseFiles.ps1')
if (-not $WorkshopPublished) { throw 'Use -WorkshopPublished only after the frozen payload has passed the Steam acceptance gate.' }
$receipt = Get-Content -LiteralPath $ReceiptPath -Raw | ConvertFrom-Json
$directory = Split-Path -Parent (Resolve-Path -LiteralPath $ReceiptPath).Path
Assert-FrozenRelease $receipt $directory
if ($receipt.Repo -ne 'Neversalimus/Quasimorph-Item-Intelligence' -or $receipt.WorkshopId -ne '3780078201') { throw 'Unexpected release destination.' }
if ($receipt.Version -notmatch '^\d+\.\d+\.\d+\.\d+$' -or $receipt.Tag -cne ('v' + $receipt.Version)) { throw 'Only a stable version tag can be published.' }
$source = $receipt.SourceRoot; $repo = $receipt.Repo; $tag = $receipt.Tag; $commit = $receipt.SourceCommit
$ghRepo = 'github.com/' + $repo
if ([string](Invoke-ReleaseNative git @('-C',$source,'rev-parse','HEAD')) -ne $commit) { throw 'Source HEAD differs from the frozen commit.' }
if (@(Invoke-ReleaseNative git @('-C',$source,'status','--porcelain','--untracked-files=all')).Count) { throw 'Source changed since preparation.' }
Assert-InventoryEntries @(Get-TrackedSourceInventory $source) $receipt.SourceFiles 'frozen source'
Invoke-ReleaseNative git @('-C',$source,'merge-base','--is-ancestor',$receipt.BaseMain,$commit) | Out-Null
$remote = [string](Invoke-ReleaseNative git @('-C',$source,'remote','get-url','origin'))
if ($remote -notmatch '^https://github\.com/Neversalimus/Quasimorph-Item-Intelligence(?:\.git)?/?$|^git@github\.com:Neversalimus/Quasimorph-Item-Intelligence(?:\.git)?$') { throw 'Unexpected source origin.' }
Invoke-ReleaseNative gh @('auth','status','--hostname','github.com') | Out-Null
$refs = Get-RemoteReleaseRefs $source $tag
$main = $refs.Main
$plan = Get-ReleaseRefPlan $main $refs.TagCommit $receipt.BaseMain $commit
if ($plan.PushMain -or $plan.PushTag) {
    if ($ExistingReleaseOnly) { throw 'Recovery requires matching remote main and tag; no refs were changed.' }
    # Explicit leases also reject races occurring between ls-remote and push. No fallback
    # to sequential pushes: a server without atomic push must stop safely.
    $push = @('-C',$source,'push','--atomic',"--force-with-lease=refs/heads/main:$main")
    if ($plan.PushTag) { $push += "--force-with-lease=refs/tags/${tag}:" }
    $push += 'origin'
    if ($plan.PushMain) { $push += "${commit}:refs/heads/main" }
    if ($plan.PushTag) { $push += "${commit}:refs/tags/$tag" }
    Invoke-ReleaseNative git $push | Out-Null
}

# Remote state is the retry journal: existing matching refs and assets are accepted.
# Query releases with a checked API call; authentication/network failure is never treated
# as absence. Include drafts so an interrupted upload can resume.
$release = $null
for ($attempt = 0; $attempt -lt 3; $attempt++) {
    $release = Find-ReleaseIncludingDrafts $repo $tag
    if ($null -ne $release -or -not $ExistingReleaseOnly) { break }
    if ($attempt -lt 2) { Start-Sleep -Seconds 2 }
}
if ($null -eq $release) {
    if ($ExistingReleaseOnly) { throw 'Existing release is still unavailable through authenticated list and pending-tag lookup. No release was created and frozen files were retained. Check gh access to github.com, then retry this recovery.' }
    $notes = Join-Path $directory 'release-notes.md'
    if (-not (Test-Path -LiteralPath $notes)) {
        [IO.File]::WriteAllText($notes, ("Item Intelligence " + $receipt.Version + "`n`nSource commit: " + $commit + "`nGame SHA256: " + $receipt.GameSHA256 + "`n"), [Text.UTF8Encoding]::new($false))
    }
    Invoke-ReleaseNative gh @('release','create',$tag,'--repo',$ghRepo,'--verify-tag','--draft','--title',"Item Intelligence $($receipt.Version)",'--notes-file',$notes) | Out-Null
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        $release = Find-ReleaseIncludingDrafts $repo $tag
        if ($null -ne $release) { break }
        if ($attempt -lt 2) { Start-Sleep -Seconds 2 }
    }
    if ($null -eq $release) { throw 'Created release is not yet visible through authenticated lookup. Frozen files are retained. Resume with -ExistingReleaseOnly; do not create another draft.' }
}
$assetNames = @($receipt.Assets | ForEach-Object Path)
foreach ($remoteAsset in $release.assets) {
    if ($assetNames -cnotcontains $remoteAsset.name) { throw "Unexpected remote asset: $($remoteAsset.name)" }
}
foreach ($asset in $receipt.Assets) {
    if (@($release.assets | Where-Object name -CEQ $asset.Path).Count -eq 0) {
        if (-not $release.draft) { throw 'A published release is incomplete; refusing to change its assets.' }
        Invoke-ReleaseNative gh @('release','upload',$tag,(Join-Path $directory $asset.Path),'--repo',$ghRepo) | Out-Null
    }
}
$verifyDir = Join-Path $directory ('remote-verification-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verifyDir | Out-Null
try {
    Invoke-ReleaseNative gh @('release','download',$tag,'--repo',$ghRepo,'--dir',$verifyDir) | Out-Null
    foreach ($asset in $receipt.Assets) {
        if ((Get-FileHash -LiteralPath (Join-Path $verifyDir $asset.Path) -Algorithm SHA256).Hash -ne $asset.SHA256) { throw "Remote asset differs: $($asset.Path); no published assets were overwritten." }
    }
    Assert-FrozenRelease $receipt $directory
    $finalRefs = Get-RemoteReleaseRefs $source $tag
    if ($finalRefs.Main -ne $commit -or $finalRefs.TagCommit -ne $commit) { throw 'Remote source refs changed during upload; the release was not promoted.' }
    if ($release.draft) { Invoke-ReleaseNative gh @('release','edit',$tag,'--repo',$ghRepo,'--draft=false') | Out-Null }
    Write-Host "Release verified: https://github.com/$repo/releases/tag/$tag" -ForegroundColor Green
} finally {
    Remove-Item -LiteralPath $verifyDir -Recurse -Force
    # Frozen payload, ZIP, checksums and receipt always remain available for retry.
}
