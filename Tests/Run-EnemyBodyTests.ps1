#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$verboseLoggingFixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VerboseLoggingFixture.cs'))
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$pieces = foreach ($file in @('ModMain.EnemyImplantMath.cs','ModMain.EnemyBodySources.cs')) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $file))))
    $tree.GetRoot().Members | ForEach-Object ToFullString
}
function Read-BodyTestMember([string]$file, [string]$kind, [string]$name) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $file))))
    $nodes = @($tree.GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -eq $kind -and $_.Identifier.ValueText -eq $name
    })
    if ($nodes.Count -ne 1) { throw "Expected one production declaration: $file / $name" }
    $nodes[0].ToFullString()
}
$members = @(
    Read-BodyTestMember 'ModMain.ItemDropRandomizeMath.cs' 'MethodDeclarationSyntax' 'GetItemDropCategoryWeight'
    Read-BodyTestMember 'ModMain.Loot.cs' 'ClassDeclarationSyntax' 'EnemyLootContext'
    Read-BodyTestMember 'ModMain.Loot.cs' 'ClassDeclarationSyntax' 'LootEnemySource'
    Read-BodyTestMember 'ModMain.LootIndexes.cs' 'MethodDeclarationSyntax' 'AddLootEnemySource'
    Read-BodyTestMember 'ModMain.LootPresentation.cs' 'MethodDeclarationSyntax' 'FormatEnemyLootChance'
) -join "`n"
$cases = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EnemyBodyCases.cs'))
$fixtures = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EnemyBodyFixtures.cs'))
$code = "using System; using System.Collections; using System.Collections.Generic; using System.Globalization; using MGSC;`n" +
    ($pieces -join "`n") + "`nnamespace ItemIntelligence { public static partial class ModMain {`n" +
    $members + "`n} }`n" + $fixtures + "`n" + $cases
$identity = 'QiiEnemyBody_' + [guid]::NewGuid().ToString('N')
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
$code = $code.Replace('namespace MGSC', ('namespace ' + $identity + '.GameFixture'))
$code = $code.Replace('using MGSC;', ('using ' + $identity + '.GameFixture;'))
$code = $code.Replace('namespace UnityEngine', ('namespace ' + $identity + '.UnityFixture'))
$code = $code.Replace('UnityEngine.', ($identity + '.UnityFixture.'))
# The fixtures model record access only. Math and runtime integration execute the
# production declarations above; the independent oracle enumerates complete draws.
$code += "`n" + $verboseLoggingFixture.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunEnemyBodyCases()
Write-Host "Production enemy body/implant behavior: PASS ($count assertions)." -ForegroundColor Green
