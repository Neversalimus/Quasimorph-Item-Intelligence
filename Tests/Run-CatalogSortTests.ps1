#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) { Add-Type -Path (Join-Path $PSHOME $name) }
$sortSource = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/CatalogSortCache.cs'))
$identity = 'QiiCatalogSort_' + [guid]::NewGuid().ToString('N')
$code = $sortSource + "`n" + [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CatalogSortCases.cs'))
Add-Type -TypeDefinition $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity)) -WarningAction Stop
$type = ($identity + '.CatalogSortCases') -as [type]
$count = $type::Run()
Write-Host "Production catalog sort cache: PASS ($count assertions; comparison counts, invalidation and localized ordering; no game/UI fixtures)." -ForegroundColor Green

$tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.BrowserCatalog.cs')))
$methods = foreach ($methodName in @('CompareBrowserCatalogItems','RefreshBrowserCatalog')) {
    $found = @($tree.GetRoot().DescendantNodes() | Where-Object { $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq $methodName })
    if ($found.Count -ne 1) { throw "Expected one production catalog method: $methodName" }
    $found[0].ToFullString()
}
$identity = 'QiiCatalogAdapter_' + [guid]::NewGuid().ToString('N')
$code = "using System; using System.Collections.Generic; using System.Globalization;`n" +
    (([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($sortSource).GetRoot().Members | ForEach-Object ToFullString) -join "`n") +
    "`nnamespace ItemIntelligence { public static partial class ModMain {`n" + ($methods -join "`n") + "`n} }`n" +
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CatalogAdapterCases.cs'))
Add-Type -TypeDefinition $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity)) -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunCatalogAdapterCases()
Write-Host "Production catalog refresh adapter: PASS ($count assertions; live filters, warmup, favorites, recent scope and Tech sorting)." -ForegroundColor Green
