#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [string]$Python = 'python',
    [string]$SourceRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReportPath = (Join-Path $SourceRoot '_build/font-coverage.json')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $SourceRoot 'BuildTools/LanguageCatalog.ps1')
$languages = @(Get-QiiLanguageCatalog $SourceRoot)
$catalogType = ($languages[0].GetType().Namespace + '.LanguageCatalog') -as [type]
$managed = Join-Path $GameRoot 'Quasimorph_Data/Managed'
$assemblyPath = Join-Path $managed 'Assembly-CSharp.dll'
# Read enum metadata without loading or executing game code.
$harmony = [Reflection.Assembly]::LoadFrom((Join-Path $managed '0Harmony.dll'))
$read = $harmony.GetType('Mono.Cecil.AssemblyDefinition').GetMethod('ReadAssembly', [type[]]@([string]))
$assembly = $read.Invoke($null, [object[]]@([string]$assemblyPath))
$request = Join-Path ([IO.Path]::GetTempPath()) ('qii-fonts-' + [guid]::NewGuid().ToString('N') + '.json')
try {
    $localization = $assembly.MainModule.Types | Where-Object FullName -eq 'MGSC.Localization'
    $enum = $localization.NestedTypes | Where-Object Name -eq 'Lang'
    if (-not $enum -or -not $enum.IsEnum) { throw 'Game language enum unavailable; font audit cannot infer language assignments.' }
    $entries = @()
    foreach ($field in $enum.Fields | Where-Object HasConstant) {
        $language = $catalogType::Find($field.Name)
        if (-not $language) { throw "Game language missing from the mod registry: $($field.Name)" }
        $entries += @{ id=$language.Id; file=$language.FileName; game_name=$field.Name; game_value=[int]$field.Constant }
    }
    if ($entries.Count -ne $languages.Count) { throw 'Game and mod language counts differ; audit requires review.' }
    @{
        languages=$entries
        game_assembly_sha256=(Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $request -Encoding utf8
    & $Python (Join-Path $PSScriptRoot 'font_coverage.py') --game-root $GameRoot --source-root $SourceRoot --catalog $request --report $ReportPath
    if ($LASTEXITCODE -ne 0) { throw "Font coverage failed (exit $LASTEXITCODE). See $ReportPath" }
} finally {
    $assembly.Dispose()
    # This is a single freshly created file, never a recursive directory cleanup.
    Remove-Item -LiteralPath $request -Force -ErrorAction SilentlyContinue
}
