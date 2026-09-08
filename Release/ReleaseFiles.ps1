# Shared deterministic file-set verification. No network or publication on import.
Set-StrictMode -Version Latest

function Get-PayloadInventory([string]$Path) {
    $base = (Get-Item -LiteralPath $Path -Force -ErrorAction Stop).FullName.TrimEnd('\','/')
    if ((Get-Item -LiteralPath $base -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked payload root: $base" }
    $entries = [Collections.Generic.List[object]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($item in (Get-ChildItem -LiteralPath $base -Recurse -Force | Sort-Object FullName)) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked payload entry: $($item.FullName)" }
        if ($item.PSIsContainer) { continue }
        $relative = $item.FullName.Substring($base.Length + 1).Replace('\','/')
        if (-not $seen.Add($relative)) { throw "Case-colliding payload path: $relative" }
        $entries.Add([ordered]@{ Path = $relative; SHA256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash; Length = $item.Length })
    }
    if ($entries.Count -eq 0) { throw 'Payload is empty.' }
    return $entries.ToArray()
}

function Assert-PayloadInventory([string]$Path, [object[]]$Expected) {
    $actual = @(Get-PayloadInventory $Path)
    Assert-InventoryEntries $actual $Expected $Path
}

function Assert-InventoryEntries([object[]]$Actual, [object[]]$Expected, [string]$Context) {
    $map = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Expected) {
        if ($map.ContainsKey([string]$entry.Path)) { throw "Duplicate receipt path: $($entry.Path)" }
        $map.Add([string]$entry.Path, $entry)
    }
    if ($Actual.Count -ne $map.Count) { throw "Payload file-set changed: $Context" }
    foreach ($entry in $Actual) {
        if (-not $map.ContainsKey([string]$entry.Path)) { throw "Unexpected payload file: $($entry.Path)" }
        $wanted = $map[[string]$entry.Path]
        if ($entry.SHA256 -ne $wanted.SHA256 -or $entry.Length -ne $wanted.Length) { throw "Payload changed: $($entry.Path)" }
    }
}

function Get-TrackedSourceInventory([string]$Source) {
    $names = ([string]::Join('', @(Invoke-ReleaseNative git @('-C',$Source,'ls-files','-z')))) -split "`0"
    foreach ($name in $names) {
        if (-not $name) { continue }
        $file = Get-Item -LiteralPath (Join-Path $Source $name) -Force
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked source file: $name" }
        [ordered]@{ Path = $name; SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash; Length = $file.Length }
    }
}

function Get-RemoteReleaseRefs([string]$Source, [string]$Tag) {
    $lines = @(Invoke-ReleaseNative git @('-C',$Source,'ls-remote','origin','refs/heads/main',"refs/tags/$Tag","refs/tags/$Tag^{}"))
    $state = @{ Main = ''; TagCommit = ''; TagObject = '' }
    foreach ($line in $lines) {
        $parts = $line -split '\s+'
        if ($parts[1] -eq 'refs/heads/main') { $state.Main = $parts[0] }
        elseif ($parts[1] -eq "refs/tags/$Tag^{}") { $state.TagCommit = $parts[0] }
        elseif ($parts[1] -eq "refs/tags/$Tag") { $state.TagObject = $parts[0] }
    }
    if (-not $state.TagCommit) { $state.TagCommit = $state.TagObject }
    return $state
}

function Write-ReleaseJson([object]$Value, [string]$Path) {
    $tmp = $Path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($tmp, ($Value | ConvertTo-Json -Depth 16), [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $tmp -Destination $Path -Force
    } finally { if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force } }
}

function New-FrozenPayload([string]$Stage, [string]$Directory, [string]$Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'A stable four-part version is required.' }
    if (Test-Path -LiteralPath $Directory) { throw 'Freeze directory already exists; resume using its receipt or choose a new directory.' }
    $inventory = @(Get-PayloadInventory $Stage)
    foreach ($required in @('ItemIntelligence.dll','modmanifest.json','thumbnail.png','Localization/en.lang','Localization/ru.lang')) {
        if ($inventory.Path -cnotcontains $required) { throw "Required payload file missing: $required" }
    }
    New-Item -ItemType Directory -Path $Directory -ErrorAction Stop | Out-Null
    $payload = Join-Path $Directory 'payload'
    New-Item -ItemType Directory -Path $payload | Out-Null
    foreach ($entry in $inventory) {
        $target = Join-Path $payload $entry.Path
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $Stage $entry.Path) -Destination $target
    }
    Assert-PayloadInventory $Stage $inventory
    Assert-PayloadInventory $payload $inventory
    $asset = Join-Path $Directory ("ItemIntelligence-v$Version.zip")
    # ZipFile includes hidden files; Compress-Archive may silently omit them.
    [IO.Compression.ZipFile]::CreateFromDirectory($payload, $asset)
    $checksums = Join-Path $Directory 'SHA256SUMS.txt'
    $lines = @(((Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash + '  ' + (Split-Path -Leaf $asset)))
    $lines += $inventory | ForEach-Object { $_.SHA256 + '  payload/' + $_.Path }
    [IO.File]::WriteAllLines($checksums, $lines, [Text.UTF8Encoding]::new($false))
    return [ordered]@{
        Payload = $inventory
        Assets = @($asset, $checksums | ForEach-Object {
            [ordered]@{ Path = (Split-Path -Leaf $_); SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
        })
    }
}

function Assert-FrozenRelease([object]$Receipt, [string]$Directory) {
    if ($Receipt.Schema -ne 2) { throw 'Unsupported release receipt schema.' }
    Assert-PayloadInventory $Receipt.Stage $Receipt.Payload
    Assert-PayloadInventory (Join-Path $Directory 'payload') $Receipt.Payload
    foreach ($asset in $Receipt.Assets) {
        if ([IO.Path]::GetFileName([string]$asset.Path) -cne $asset.Path) { throw 'Asset name must be a leaf filename.' }
        if ((Get-FileHash -LiteralPath (Join-Path $Directory $asset.Path) -Algorithm SHA256).Hash -ne $asset.SHA256) { throw "Frozen asset changed: $($asset.Path)" }
    }
}

function Invoke-ReleaseNative([string]$Command, [string[]]$Arguments) {
    $result = & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE). Frozen files and receipt are retained for retry." }
    return $result
}

function Get-ReleaseRefPlan([string]$RemoteMain, [string]$RemoteTag, [string]$BaseMain, [string]$Commit) {
    if ($RemoteMain -ne $BaseMain -and $RemoteMain -ne $Commit) { throw 'Remote main changed since preparation; review the new commits before proceeding.' }
    if ($RemoteTag -and $RemoteTag -ne $Commit) { throw 'The release tag points to different source.' }
    return [ordered]@{ PushMain = ($RemoteMain -ne $Commit); PushTag = (-not $RemoteTag) }
}
