param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $SourceRoot 'BuildTools/SourceReferences.ps1')
$probe = 'Alpha Alpha() "Alpha" // Alpha alpha AlphaBeta _Alpha 0Alpha ЯAlpha AlphaЯ; beta beta2 _two _two'
$counts = Get-QiiIdentifierCounts $probe
$expected = @(@('Alpha',4), @('alpha',1), @('AlphaBeta',1), @('_Alpha',1), @('AlphaЯ',1), @('beta',1), @('beta2',1), @('_two',2))
foreach ($entry in $expected) {
    if (-not $counts.ContainsKey($entry[0]) -or $counts[$entry[0]] -ne $entry[1]) {
        throw "Source-reference regression: $($entry[0])"
    }
}
if ((Get-QiiIdentifierCounts '').Count -ne 0) { throw 'Empty source should have no identifiers.' }
if ($counts.ContainsKey('0Alpha') -or $counts.ContainsKey('ЯAlpha')) { throw 'Identifier start boundary changed.' }
$declarations = Get-QiiDeclaredIdentifiers @'
class Live {
    // void Retired() {}
    string Message = "Retired";
    void Alive() { var Other = "x"; }
}
'@
if ($declarations.Contains('Retired')) { throw 'Comments and strings are not declarations.' }
foreach ($name in @('Live','Message','Alive','Other')) {
    if (-not $declarations.Contains($name)) { throw "Missing C# declaration: $name" }
}
if (-not (Get-QiiDeclaredIdentifiers 'class X { void Retired() {} }').Contains('Retired')) { throw 'Retired method declaration missed.' }
Write-Host 'Production source-reference index: PASS (16 assertions; token boundaries and C# declarations).' -ForegroundColor Green
