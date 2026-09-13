#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$trees = @{}
function Read-Members([string]$file, [string]$kind, [string[]]$names) {
    if (-not $trees.ContainsKey($file)) {
        $trees[$file] = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
            [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/ModMain.' + $file + '.cs'))))
    }
    foreach ($name in $names) {
        $nodes = @($trees[$file].GetRoot().DescendantNodes() | Where-Object {
            $_.GetType().Name -eq $kind -and $_.Identifier.ValueText -eq $name
        })
        if (-not $nodes.Count) { throw "Production member missing: $file / $name" }
        foreach ($node in $nodes) {
            if ($kind -eq 'VariableDeclaratorSyntax') { $node.Parent.Parent.ToFullString() }
            else { $node.ToFullString() }
        }
    }
}
$whole = foreach ($file in @('BrowserReadability','BrowserRowRenderCache','BrowserTextLayout',
    'BrowserViewportMath','BrowserModels','LocalizationTextLayout')) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/ModMain.' + $file + '.cs'))))
    $tree.GetRoot().Members | ForEach-Object ToFullString
}
$members = @(
    Read-Members 'Configuration' 'VariableDeclaratorSyntax' @('_configLoaded','EnableItemIntelligence',
        'QuickIntelligence','InspectorEnabled','ShowInspectorHint','ShowInterfaceIcons','EnhancedReadability',
        'ModderMode','ShowMagnumUses','ShowFutureMagnumUses','ShowRecipes','ShowSources','ShowTradeInformation',
        'UsePreviousTradeLayout','ShowMagnumSurplus','ShowAmmoRelations','UiLanguagePreference','InspectorKeyName')
    Read-Members 'Configuration' 'PropertyDeclarationSyntax' @('ConfigDirectory','ConfigPath')
    Read-Members 'Configuration' 'MethodDeclarationSyntax' @('EnsureConfigLoaded','ApplyConfigValue',
        'ApplyConfigTextValue','SaveConfig','OnMcmConfigSaved','ApplyMcmBool','TryReadMcmBool','TryReadMcmString')
    Read-Members 'BrowserState' 'VariableDeclaratorSyntax' @('BrowserRowCapacity','BrowserCatalogRowCapacity',
        'BrowserTabCount','BrowserVisibleRows','BrowserRowLeft','BrowserRowRight','BrowserRowRoots','BrowserRowButtons',
        'BrowserLines','BrowserNavigation','_browserHelpText','_inspectorOpen','_inspectorItemId')
    Read-Members 'BrowserUiDirtySuppression' 'MethodDeclarationSyntax' @('SetBrowserFontSizeIfChanged',
        'SetBrowserGraphicColorIfChanged','SetBrowserFontStyleIfChanged','SetBrowserActiveIfChanged',
        'SetBrowserInteractableIfChanged','SetBrowserRaycastTargetIfChanged')
    Read-Members 'LocalizationInternational' 'MethodDeclarationSyntax' @('ContainsHanScript','ContainsKanaScript','ContainsHangulScript')
    Read-Members 'InterfaceIcons' 'MethodDeclarationSyntax' @('GetBrowserInterfaceTabFontSize')
) -join "`n"
$identity = 'QiiReadability_' + [guid]::NewGuid().ToString('N')
# Pooled state unrelated to this fixture remains at its declared defaults.
$code = "#pragma warning disable 0649`nusing System; using System.IO; using System.Text; using System.Globalization; using System.Collections.Generic; using TMPro; using UnityEngine; using UnityEngine.UI;`n" +
    ($whole -join "`n") + "`nnamespace ItemIntelligence { public static partial class ModMain {`n" +
    $members + "`n} }`n" + [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ReadabilityCases.cs'))
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
# Other contract suites use their own Unity/TMP fixtures in the same process.
$code = $code.Replace('UnityEngine', ($identity + '.UnityEngine')).Replace('TMPro', ($identity + '.TMPro'))
$fixture = Join-Path ([IO.Path]::GetTempPath()) $identity
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    Add-Type -TypeDefinition $code -WarningAction Stop
    $type = ($identity + '.ModMain') -as [type]
    $count = $type::RunReadabilityCases($fixture, (Join-Path $SourceRoot 'WORKSHOP_CONTENT/Localization'))
    Write-Host "Production browser readability: PASS ($count assertions; config/cache and synthetic font metrics, not Unity rendering)." -ForegroundColor Green
} finally { Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue }
