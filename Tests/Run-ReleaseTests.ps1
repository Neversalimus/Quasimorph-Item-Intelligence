#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'Release/ReleaseFiles.ps1')
$temp = Join-Path ([IO.Path]::GetTempPath()) ('qii-release-tests-' + [guid]::NewGuid().ToString('N'))
$qiiTestState = @{ Checks = 0 }
function Check([bool]$ok, [string]$message) {
    $qiiTestState.Checks++
    if (-not $ok) { throw "Release regression: $message" }
}
function Reject([scriptblock]$action, [string]$message) {
    $rejected = $false
    try { & $action } catch { $rejected = $true; Write-Verbose ("Expected rejection [$message]: " + $_.Exception.Message) }
    Check $rejected $message
}
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $stage = Join-Path $temp 'stage'
    New-Item -ItemType Directory -Path (Join-Path $stage 'Localization') -Force | Out-Null
    foreach ($file in @('ItemIntelligence.dll','modmanifest.json','thumbnail.png','Localization/en.lang','Localization/ru.lang','.hidden-payload')) {
        [IO.File]::WriteAllText((Join-Path $stage $file), $file)
    }
    $frozenDirectory = Join-Path $temp 'frozen'
    $frozen = New-FrozenPayload $stage $frozenDirectory '1.7.42.4'
    Check ($frozen.Payload.Count -eq 6) 'hidden files are included'
    $receipt = [pscustomobject]@{
        Schema = 2; Version = '1.7.42.4'; Tag = 'v1.7.42.4'
        Repo = 'Neversalimus/Quasimorph-Item-Intelligence'; WorkshopId = '3780078201'
        SourceRoot = (Join-Path $temp 'source'); SourceCommit = ('b' * 40); BaseMain = ('a' * 40)
        GameSHA256 = ('c' * 64); Stage = $stage; Payload = $frozen.Payload; Assets = $frozen.Assets
    }
    New-Item -ItemType Directory -Path $receipt.SourceRoot | Out-Null
    [IO.File]::WriteAllText((Join-Path $receipt.SourceRoot 'source.txt'), 'source')
    $receipt | Add-Member -NotePropertyName SourceFiles -NotePropertyValue @(Get-PayloadInventory $receipt.SourceRoot)
    $receiptPath = Join-Path $frozenDirectory 'release-receipt.json'
    Write-ReleaseJson $receipt $receiptPath
    Assert-FrozenRelease $receipt $frozenDirectory
    $ru = Join-Path $stage 'Localization/ru.lang'
    [IO.File]::AppendAllText($ru, 'changed')
    Reject { Assert-FrozenRelease $receipt $frozenDirectory } 'localization tampering blocked'
    [IO.File]::WriteAllText($ru, 'Localization/ru.lang')
    Remove-Item -LiteralPath $ru
    Reject { Assert-FrozenRelease $receipt $frozenDirectory } 'missing file blocked'
    [IO.File]::WriteAllText($ru, 'Localization/ru.lang')
    [IO.File]::WriteAllText((Join-Path $stage 'extra.txt'), 'extra')
    Reject { Assert-FrozenRelease $receipt $frozenDirectory } 'extra file blocked'
    Remove-Item -LiteralPath (Join-Path $stage 'extra.txt')
    $frozenRu = Join-Path $frozenDirectory 'payload/Localization/ru.lang'
    [IO.File]::AppendAllText($frozenRu, 'changed')
    Reject { Assert-FrozenRelease $receipt $frozenDirectory } 'frozen copy tampering blocked'
    [IO.File]::WriteAllText($frozenRu, 'Localization/ru.lang')
    $asset = Join-Path $frozenDirectory $receipt.Assets[0].Path
    $bytes = [IO.File]::ReadAllBytes($asset)
    [IO.File]::AppendAllText($asset, 'changed')
    Reject { Assert-FrozenRelease $receipt $frozenDirectory } 'ZIP tampering blocked'
    [IO.File]::WriteAllBytes($asset, $bytes)
    Reject { New-FrozenPayload $stage $frozenDirectory '1.7.42.4' } 'existing frozen evidence cannot be overwritten'
    Reject { New-FrozenPayload $stage (Join-Path $temp 'test-version') '1.7.42.4-test1' } 'DEV version cannot freeze as release'
    $unpacked = Join-Path $temp 'unpacked'
    [IO.Compression.ZipFile]::ExtractToDirectory($asset, $unpacked)
    Assert-PayloadInventory $unpacked $receipt.Payload
    Check $true 'ZIP contains exact frozen file set'
    $plan = Get-ReleaseRefPlan ('a' * 40) '' ('a' * 40) ('b' * 40)
    Check ($plan.PushMain -and $plan.PushTag) 'fresh publication updates both refs'
    $plan = Get-ReleaseRefPlan ('b' * 40) '' ('a' * 40) ('b' * 40)
    Check (-not $plan.PushMain -and $plan.PushTag) 'old partial main push is resumable'
    $plan = Get-ReleaseRefPlan ('a' * 40) ('b' * 40) ('a' * 40) ('b' * 40)
    Check ($plan.PushMain -and -not $plan.PushTag) 'partial tag state is resumable'
    $plan = Get-ReleaseRefPlan ('b' * 40) ('b' * 40) ('a' * 40) ('b' * 40)
    Check (-not $plan.PushMain -and -not $plan.PushTag) 'matching refs are idempotent'
    Reject { Get-ReleaseRefPlan ('d' * 40) '' ('a' * 40) ('b' * 40) } 'concurrent main change blocks push'
    Reject { Get-ReleaseRefPlan ('a' * 40) ('d' * 40) ('a' * 40) ('b' * 40) } 'conflicting tag blocks push'

    # Execute the real publisher with in-memory git/gh transports. These functions
    # intercept every native call; no remote command or network request is executed.
    $mock = @{ remoteMain = $receipt.SourceCommit; remoteTag = $receipt.SourceCommit; release = [pscustomobject]@{ id = 17424; tag_name = $receipt.Tag; draft = $true; assets = @() }; pushes = 0; uploads = 0; edits = 0; creates = 0; tagQueries = 0; failList = $false; duplicateList = $false; failCreate = $false; failUpload = $false; remoteFiles = (Join-Path $temp 'remote-resume-assets') }
    New-Item -ItemType Directory -Path $mock.remoteFiles | Out-Null
    function git {
        $v = @($args); $global:LASTEXITCODE = 0
        if ($v[2] -eq 'rev-parse') { return $receipt.SourceCommit }
        if ($v[2] -eq 'status') { return }
        if ($v[2] -eq 'ls-files') { return "source.txt`0" }
        if ($v[2] -eq 'merge-base') { return }
        if ($v[2] -eq 'remote') { return 'https://github.com/Neversalimus/Quasimorph-Item-Intelligence.git' }
        if ($v[2] -eq 'ls-remote') {
            "$($mock.remoteMain)`trefs/heads/main"
            if ($mock.remoteTag) { "$($mock.remoteTag)`trefs/tags/v1.7.42.4" }
            return
        }
        if ($v[2] -eq 'push') {
            Check ($v -contains '--atomic') 'publisher uses atomic push'
            Check ($v -contains ("--force-with-lease=refs/heads/main:" + $mock.remoteMain)) 'publisher leases observed main'
            $mock.pushes++; $mock.remoteMain = $receipt.SourceCommit; $mock.remoteTag = $receipt.SourceCommit
            return
        }
        throw ('Unmocked git call: ' + ($v -join ' '))
    }
    function gh {
        $v = @($args); $global:LASTEXITCODE = 0
        if ($v[0] -eq 'auth') { return }
        if ($v[0] -eq 'api') {
            if ($v[1] -eq ('repos/' + $receipt.Repo + '/releases') -and $v -contains '--slurp') {
                if ($mock.failList) { $global:LASTEXITCODE = 1; return '{"message":"Not Found"}' }
                $older = '{"id":17422,"tag_name":"v1.7.42.2","draft":false,"assets":[]}'
                if ($null -eq $mock.release) { return '[[' + $older + '],[]]' }
                $current = $mock.release | ConvertTo-Json -Depth 8 -Compress
                if ($mock.duplicateList) { return '[[' + $current + '],[' + $current + ']]' }
                return '[[' + $older + '],[' + $current + ']]'
            }
            if ($v[1] -eq ('repos/' + $receipt.Repo + '/releases/tags/' + $receipt.Tag)) {
                $mock.tagQueries++
                # Published-tag API must not stand in for authenticated draft discovery.
                if ($null -eq $mock.release -or $mock.release.draft) {
                    $global:LASTEXITCODE = 1; return '{"message":"Not Found"}'
                }
                return $mock.release | ConvertTo-Json -Depth 8
            }
            throw ('Unmocked gh API endpoint: ' + $v[1])
        }
        if ($v[1] -eq 'create') {
            if ($mock.failCreate) { $mock.failCreate = $false; $global:LASTEXITCODE = 9; return }
            if ($null -ne $mock.release) { throw 'Attempted to recreate an existing release.' }
            $mock.release = [pscustomobject]@{ id = 17424; tag_name = $receipt.Tag; draft = $true; assets = @() }
            $mock.creates++
            return
        }
        if ($v[1] -eq 'upload') {
            if ($mock.failUpload -and $mock.uploads -eq 1) { $mock.failUpload = $false; $global:LASTEXITCODE = 9; return }
            $name = Split-Path -Leaf $v[3]
            Copy-Item -LiteralPath $v[3] -Destination (Join-Path $mock.remoteFiles $name)
            $mock.release.assets += [pscustomobject]@{ name = $name }; $mock.uploads++
            return
        }
        if ($v[1] -eq 'download') {
            $destination = $v[[array]::IndexOf($v, '--dir') + 1]
            Get-ChildItem -LiteralPath $mock.remoteFiles -File | Copy-Item -Destination $destination
            return
        }
        if ($v[1] -eq 'edit') { $mock.edits++; $mock.release.draft = $false; return }
        throw ('Unmocked gh call: ' + ($v -join ' '))
    }
    $publisher = Join-Path $root 'Release/Publish-Release.ps1'
    # Reproduce the reported state: main/tag already pushed, draft exists, no assets.
    $beforeReceipt = (Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash
    & $publisher -ReceiptPath $receiptPath -WorkshopPublished 6>$null
    Check ($mock.pushes -eq 0 -and $mock.creates -eq 0 -and $mock.uploads -eq 2 -and $mock.edits -eq 1) 'existing draft resumes without ref updates or duplicate creation'
    Check ($mock.tagQueries -eq 0) 'draft lookup does not call published-tag endpoint'
    Check ((Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash -eq $beforeReceipt) 'recovery keeps frozen receipt bytes'
    $mock.failList = $true
    Reject { & $publisher -ReceiptPath $receiptPath -WorkshopPublished } 'release-list API failure is not treated as absence'
    Check ($mock.creates -eq 0 -and $mock.uploads -eq 2) 'failed list does not create or upload'
    $mock.failList = $false; $mock.duplicateList = $true
    Reject { & $publisher -ReceiptPath $receiptPath -WorkshopPublished } 'ambiguous same-tag release list blocks continuation'
    $mock.duplicateList = $false

    # Fresh publication still handles failures before creation and during asset upload.
    $mock.remoteMain = $receipt.BaseMain; $mock.remoteTag = ''; $mock.release = $null
    $mock.pushes = 0; $mock.uploads = 0; $mock.edits = 0; $mock.creates = 0
    $mock.failCreate = $true; $mock.failUpload = $true
    $mock.remoteFiles = Join-Path $temp 'remote-assets'
    New-Item -ItemType Directory -Path $mock.remoteFiles | Out-Null
    Reject { & $publisher -ReceiptPath $receiptPath } 'Steam acknowledgement required before publication'
    Reject { & $publisher -ReceiptPath $receiptPath -WorkshopPublished } 'interruption after atomic push reported'
    Check ((Test-Path -LiteralPath $asset) -and (Test-Path -LiteralPath $receiptPath)) 'frozen evidence survives failure'
    Reject { & $publisher -ReceiptPath $receiptPath -WorkshopPublished } 'partial asset upload interruption reported'
    Check ($mock.uploads -eq 1 -and $mock.release.draft) 'incomplete release remains draft'
    & $publisher -ReceiptPath $receiptPath -WorkshopPublished 6>$null
    Check ($mock.pushes -eq 1 -and $mock.uploads -eq 2 -and $mock.edits -eq 1) 'resume uploads only missing assets'
    & $publisher -ReceiptPath $receiptPath -WorkshopPublished 6>$null
    Check ($mock.pushes -eq 1 -and $mock.uploads -eq 2 -and $mock.edits -eq 1) 'completed rerun has no remote mutations'
    [IO.File]::AppendAllText((Join-Path $mock.remoteFiles $receipt.Assets[0].Path), 'changed')
    Reject { & $publisher -ReceiptPath $receiptPath -WorkshopPublished } 'mismatching remote asset is blocked'
    Check ($mock.uploads -eq 2) 'remote asset is never silently replaced'
    Write-Host ("Release behavior: PASS ($($qiiTestState.Checks) assertions; native transports mocked).") -ForegroundColor Green
} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
