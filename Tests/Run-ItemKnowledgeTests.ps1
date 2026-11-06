#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) { Add-Type -Path (Join-Path $PSHOME $name) }
$identity = 'QiiItemKnowledge_' + [guid]::NewGuid().ToString('N')
$trees = @{}
function Get-KnowledgeMember([string]$file, [string]$name) {
    if (-not $trees.ContainsKey($file)) {
        $trees[$file] = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $file))))
    }
    $members = @($trees[$file].GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -in @('MethodDeclarationSyntax','ClassDeclarationSyntax','VariableDeclaratorSyntax') -and $_.Identifier.ValueText -eq $name
    })
    if ($members.Count -ne 1) { throw "Expected one production member: $file/$name" }
    if ($members[0].GetType().Name -eq 'VariableDeclaratorSyntax') { return $members[0].Parent.Parent.ToFullString() }
    return $members[0].ToFullString()
}
$pieces = foreach ($name in @('ModMain.DatadiskUnlockExpansion.cs','ModMain.DatadiskRuntime.cs','ModMain.WeaponSearch.cs')) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $name))))
    $tree.GetRoot().Members | ForEach-Object ToFullString
}
$members = @()
foreach ($name in @('DatadisksByUnlockedItem','ItemsUnlockedByDatadisk','UnlockHitCountsByDatadisk','UnlockPoolSizeByDatadisk','ProductionDatadiskItemIds','_chipUnlockChanceContractVerified')) {
    $members += Get-KnowledgeMember 'ModMain.Ammo.cs' $name
}
foreach ($name in @('BrowserAdvancedSearchQuery','BrowserTechConstraint','BrowserSearchRecordTypes','BrowserSearchRelationIds','BrowserSearchTechLevels',
    'ResetBrowserAdvancedSearchIndexState','IndexBrowserAdvancedSearchMetadata','NormalizeBrowserSearchText','TokenizeBrowserAdvancedSearch',
    'ParseBrowserAdvancedSearchQuery','TryParseBrowserTechConstraint','ResolveBrowserSearchCategory','BrowserSearchWordStartsWith',
    'ScoreBrowserSearchMatch','BrowserAdvancedSearchMatches')) {
    $members += Get-KnowledgeMember 'ModMain.BrowserAdvancedSearch.cs' $name
}
foreach ($name in @('GetPrimaryDatadiskForItem','GetDatadiskStatus')) { $members += Get-KnowledgeMember 'ModMain.ProductionUnlockRuntime.cs' $name }
$code = "using System; using System.Collections; using System.Collections.Generic; using System.Globalization; using System.Reflection; using System.Text; using MGSC;`n" +
    ($pieces -join "`n") + "`nnamespace ItemIntelligence { public static partial class ModMain {`n" + ($members -join "`n") + "`n} }`n" +
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ItemKnowledgeCases.cs'))
# Only the fixture namespace changes; the exact DatadiskRecord type check remains exercised.
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity)).
    Replace('namespace MGSC', ('namespace ' + $identity + '.MGSC')).
    Replace('using MGSC;', ('using ' + $identity + '.MGSC;')).
    Replace('"MGSC.DatadiskRecord"', ('"' + $identity + '.MGSC.DatadiskRecord"'))
Add-Type -TypeDefinition $code -WarningAction Stop
$owner = ($identity + '.ModMain') -as [type]
$counts = $owner::RunItemKnowledgeCases()
Write-Host "Production chip unlock graph: PASS ($($counts[0]) assertions; whole armor sets, default payloads, weighted/shared outcomes and reset)." -ForegroundColor Green
Write-Host "Production weapon search: PASS ($($counts[1]) assertions; real query parser/matcher, translated classes/perks and catalog filtering)." -ForegroundColor Green
