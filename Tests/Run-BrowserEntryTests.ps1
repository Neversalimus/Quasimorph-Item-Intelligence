#requires -Version 7.0
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$verboseLoggingFixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VerboseLoggingFixture.cs'))
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$entry = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.BrowserEntry.cs')))
$ui = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.BrowserUI.cs')))
$methods = foreach ($name in @('HasObservedInspectorItemUi','ResolveInspectorTargetOnDemand','ShouldCaptureInspectorHotkeyOpeningFrame')) {
    $found = @($ui.GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq $name
    })
    if ($found.Count -ne 1) { throw "Missing production method: $name" }
    $found[0].ToFullString()
}
$code = "using System; using System.Collections.Generic; using System.Globalization; using UnityEngine; using MGSC;`n" +
    (($entry.GetRoot().Members | ForEach-Object ToFullString) -join "`n") +
    "`nnamespace ItemIntelligence { public static partial class ModMain {`n" + ($methods -join "`n") + "`n} }`n" +
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'BrowserEntryCases.cs'))
$identity = 'QiiBrowserEntry_' + [guid]::NewGuid().ToString('N')
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity)).
    Replace('namespace MGSC', ('namespace ' + $identity + '.MGSC')).
    Replace('namespace UnityEngine', ('namespace ' + $identity + '.UnityEngine')).
    Replace('using MGSC;', ('using ' + $identity + '.MGSC;')).
    Replace('using UnityEngine;', ('using ' + $identity + '.UnityEngine;'))
$code += "`n" + $verboseLoggingFixture.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunBrowserEntryCases()
Write-Host "Production browser entry: PASS ($count assertions; fresh space, mission resume, modal capture, settings and menu idle; Unity input fixtures)." -ForegroundColor Green
