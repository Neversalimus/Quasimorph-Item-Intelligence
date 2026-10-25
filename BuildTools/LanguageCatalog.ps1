# Compile the same immutable registry used by the mod. A content-based namespace
# prevents stale types when several source revisions are checked in one process.
function Get-QiiLanguageCatalog([string]$SourceRoot) {
    $text = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/LanguageCatalog.cs'))
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-','') }
    finally { $hasher.Dispose() }
    $namespace = 'QiiLanguages_' + $hash
    $typeName = $namespace + '.LanguageCatalog'
    if (-not ($typeName -as [type])) {
        Add-Type -TypeDefinition $text.Replace('namespace ItemIntelligence', ('namespace ' + $namespace)) -WarningAction Stop
    }
    $type = $typeName -as [type]
    return $type::All
}
