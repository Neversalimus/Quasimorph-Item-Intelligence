#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
# Compile the monitor with System only. A dependency on ModMain, MGSC or Unity
# must fail here even though the integration fixtures below expose those types.
$monitorIdentity = 'QiiShipCargoMonitor_' + [guid]::NewGuid().ToString('N')
$monitorCode = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ShipCargoMonitor.cs')) + "`n" +
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ShipCargoMonitorCases.cs'))
$monitorCode = $monitorCode.Replace('namespace ItemIntelligence', ('namespace ' + $monitorIdentity))
Add-Type -TypeDefinition $monitorCode -WarningAction Stop
$monitorType = ($monitorIdentity + '.ShipCargoMonitorCases') -as [type]
$monitorCount = $monitorType::Run()
Write-Host "Production ship cargo monitor: PASS ($monitorCount assertions; independent instances, polling and session lifecycle; no game/UI fixtures)." -ForegroundColor Green

$whole = foreach ($name in @('ShipCargoReader.cs','ShipCargoMonitor.cs','ModMain.OverviewShipCargo.cs')) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $name))))
    $errors = @($tree.GetDiagnostics() | Where-Object Severity -eq 'Error')
    if ($errors.Count) { throw ($errors -join [Environment]::NewLine) }
    $tree.GetRoot().Members | ForEach-Object ToFullString
}
$overview = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.OverviewDashboard.cs')))
$build = @($overview.GetRoot().DescendantNodes() | Where-Object {
    $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq 'BuildBrowserOverview'
})
if ($build.Count -ne 1) { throw 'Expected one production BuildBrowserOverview.' }
$identity = 'QiiShipCargo_' + [guid]::NewGuid().ToString('N')
$code = "using System; using System.Collections.Generic; using System.Globalization; using MGSC; using UnityEngine;`n" +
    ($whole -join "`n") + "`nnamespace ItemIntelligence { public static partial class ModMain {`n" +
    $build[0].ToFullString() + "`n} }`n" + [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ShipCargoCases.cs'))
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity)).
    Replace('namespace MGSC', ('namespace ' + $identity + '.MGSC')).
    Replace('namespace UnityEngine', ('namespace ' + $identity + '.UnityEngine')).
    Replace('using MGSC;', ('using ' + $identity + '.MGSC;')).
    Replace('using UnityEngine;', ('using ' + $identity + '.UnityEngine;'))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunShipCargoCases()
Write-Host "Production ship cargo: PASS ($count assertions; storage scope, stacks, exact IDs, live overview and session changes; game API fixtures)." -ForegroundColor Green
