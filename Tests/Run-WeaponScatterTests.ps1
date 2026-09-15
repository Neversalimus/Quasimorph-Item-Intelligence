#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$verboseLoggingFixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VerboseLoggingFixture.cs'))
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.WeaponModeScatter.cs')))
$production = ($tree.GetRoot().Members | ForEach-Object ToFullString) -join "`n"
$fixtures = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'WeaponScatterCases.cs'))
$count = 0
foreach ($variant in @('MODERN','LEGACY','INVALID')) {
    $identity = 'QiiScatter_' + [guid]::NewGuid().ToString('N')
    $code = "#define $variant`nusing System; using System.Collections.Generic; using System.Globalization; using System.Reflection; using MGSC; using UnityEngine;`n" + $production + "`n" + $fixtures
    $code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
    foreach ($ns in @('MGSC','UnityEngine')) {
        $code = $code.Replace(('namespace ' + $ns), ('namespace ' + $identity + '.' + $ns))
        $code = $code.Replace(('using ' + $ns + ';'), ('using ' + $identity + '.' + $ns + ';'))
    }
    $code += "`n" + $verboseLoggingFixture.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
Add-Type -TypeDefinition $code -WarningAction Stop
    $type = ($identity + '.ModMain') -as [type]
    $count += $type::RunScatterCases()
}
Write-Host "Production weapon scatter: PASS ($count assertions; modern, legacy and incompatible API fixtures)." -ForegroundColor Green
