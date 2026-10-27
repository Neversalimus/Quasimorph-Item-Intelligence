#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($assemblyName in @('Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $assemblyName)
}
$navigationSource = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/BrowserNavigationSessionState.cs'))
$identity = 'QiiNavigationState_' + [guid]::NewGuid().ToString('N')
$code = $navigationSource + "`n" + [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'BrowserNavigationCases.cs'))
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
# Compile the owner with System only, without ModMain, game or Unity fixtures.
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.BrowserNavigationCases') -as [type]
$count = $type::Run()
Write-Host "Production browser navigation state: PASS ($count assertions; bounded history, tab scroll and session lifecycle; no game/UI fixtures)." -ForegroundColor Green

$methods = foreach ($selection in @(
    @{File='BrowserCatalog'; Names=@('PushBrowserNavigationState','NavigateBrowserToItem','NavigateBrowserBack')},
    @{File='BrowserUI'; Names=@('SetBrowserTab','CycleBrowserTab','ScrollBrowserRows')},
    @{File='LootNavigation'; Names=@('HandleLootSectionToggleAction')}
)) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/ModMain.' + $selection.File + '.cs'))))
    foreach ($methodName in $selection.Names) {
        $found = @($tree.GetRoot().DescendantNodes() | Where-Object {
            $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq $methodName
        })
        if ($found.Count -ne 1) { throw "Expected one production method: $methodName" }
        $found[0].ToFullString()
    }
}
$identity = 'QiiNavigationAdapter_' + [guid]::NewGuid().ToString('N')
$code = "using System; using System.Collections.Generic; using UnityEngine;`n" +
    (([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($navigationSource).GetRoot().Members |
        ForEach-Object ToFullString) -join "`n") +
    "`nnamespace ItemIntelligence { public static partial class ModMain {`n" +
    ($methods -join "`n") + "`n} }`n" +
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'BrowserNavigationAdapterCases.cs'))
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity)).
    Replace('namespace UnityEngine', ('namespace ' + $identity + '.UnityEngine')).
    Replace('using UnityEngine;', ('using ' + $identity + '.UnityEngine;'))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunNavigationAdapterCases()
Write-Host "Production browser navigation adapter: PASS ($count assertions; item/back, trade refresh, tab cycling and loot section callbacks; UI fixtures)." -ForegroundColor Green
