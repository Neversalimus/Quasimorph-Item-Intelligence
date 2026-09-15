#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$verboseLoggingFixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VerboseLoggingFixture.cs'))
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$pieces = foreach ($file in @('ModMain.BrowserViewportMath.cs','ModMain.BrowserViewportChrome.cs')) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $file))))
    $tree.GetRoot().Members | ForEach-Object ToFullString
}
$tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.BrowserState.cs')))
$constants = foreach ($name in @('BrowserRowCapacity','BrowserCatalogRowCapacity')) {
    $nodes = @($tree.GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -eq 'VariableDeclaratorSyntax' -and $_.Identifier.ValueText -eq $name
    })
    if ($nodes.Count -ne 1) { throw "Expected one production capacity: $name" }
    $nodes[0].Parent.Parent.ToFullString()
}
$cases = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ViewportCases.cs'))
$identity = 'QiiViewport_' + [guid]::NewGuid().ToString('N')
# Rect fixtures capture production chrome writes, not Unity rendering/font metrics.
$code = "using System; using System.Collections.Generic;`n" + ($pieces -join "`n") +
    "`nnamespace ItemIntelligence { public static partial class ModMain {`n" +
    ($constants -join "`n") + "`n} }`n" + $cases
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
$code += "`n" + $verboseLoggingFixture.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunViewportCases()
Write-Host "Production browser viewport geometry: PASS ($count assertions)." -ForegroundColor Green
