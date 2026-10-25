# Validate staging destinations before any recursive removal. No writes on import.
function Test-QiiPathWithin([string]$Path, [string]$Root) {
    $base = $Root.TrimEnd('\','/')
    return $Path.Equals($base, [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-QiiNoLinkedPath([string]$Path) {
    for ($current = $Path; $current; $current = [IO.Path]::GetDirectoryName($current)) {
        if (Test-Path -LiteralPath $current) {
            $entry = Get-Item -LiteralPath $current -Force -ErrorAction Stop
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Staging through a linked path is not allowed: $current"
            }
        }
    }
}

function Assert-QiiStageContents([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { throw "Stage is not a directory: $Path" }
    $entries = @(Get-ChildItem -LiteralPath $Path -Force)
    if ($entries.Count -eq 0) { return }
    $manifest = Join-Path $Path 'modmanifest.json'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
        throw "Stage is not an Item Intelligence package: $Path"
    }
    # A previous stage can contain community translations, but never unrelated
    # files beside the package. Reject unknown contents instead of deleting them.
    foreach ($entry in $entries) {
        if ($entry.PSIsContainer) {
            if ($entry.Name -ne 'Localization') { throw "Unexpected stage directory: $($entry.FullName)" }
        } elseif ($entry.Name -notin @('ItemIntelligence.dll','modmanifest.json','thumbnail.png')) {
            throw "Unexpected stage file: $($entry.FullName)"
        }
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked stage entry: $($entry.FullName)" }
    }
    foreach ($entry in (Get-ChildItem -LiteralPath $Path -Force -Recurse)) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked stage entry: $($entry.FullName)" }
    }
    $identity = Get-Content -LiteralPath $manifest -Raw -Encoding utf8 | ConvertFrom-Json -ErrorAction Stop
    if (-not $identity.PSObject.Properties['UniqueModName'] -or $identity.UniqueModName -ne 'ItemIntelligence') {
        throw "Unexpected stage mod identity: $Path"
    }
}

function Resolve-QiiStagePath([string]$Path, [string]$SourceRoot, [string]$GameRoot) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Stage path is empty.' }
    $resolved = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
    $rootPath = [IO.Path]::GetPathRoot($resolved)
    if ($resolved.TrimEnd('\','/') -eq $rootPath.TrimEnd('\','/')) { throw 'A drive root cannot be used as a stage.' }
    $resolved = $resolved.TrimEnd('\','/')
    $source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\','/')
    $game = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')
    $localStages = Join-Path $source '_stage'
    if ((Test-QiiPathWithin $source $resolved) -or
        ((Test-QiiPathWithin $resolved $source) -and
         ($resolved -eq $localStages -or -not (Test-QiiPathWithin $resolved $localStages)))) {
        throw 'Stage overlaps the source tree. Use its _stage directory or a separate package directory.'
    }
    if ((Test-QiiPathWithin $resolved $game) -or (Test-QiiPathWithin $game $resolved) -or
        $resolved -match '(?i)[\\/]steamapps[\\/](?:common|workshop)(?:[\\/]|$)') {
        throw 'Stage must not overlap a game installation or live Steam content.'
    }
    foreach ($protected in @($env:USERPROFILE, $env:WINDIR, [IO.Path]::GetTempPath())) {
        if ($protected -and $resolved -eq [IO.Path]::GetFullPath($protected).TrimEnd('\','/')) {
            throw "Protected directory cannot be a stage: $resolved"
        }
    }
    Assert-QiiNoLinkedPath $resolved
    Assert-QiiStageContents $resolved
    return $resolved
}
