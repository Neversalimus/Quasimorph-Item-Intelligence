#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Roslyn ships with PowerShell 7. Parse declarations, never match method bodies by regex.
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$trees = @{}
function Get-ProductionMember([string]$file, [string]$kind, [string]$name) {
    if (-not $trees.ContainsKey($file)) {
        $text = [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $file)))
        $trees[$file] = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text)
        $errors = @($trees[$file].GetDiagnostics() | Where-Object Severity -eq 'Error')
        if ($errors.Count) { throw ($errors -join "`n") }
    }
    $found = @($trees[$file].GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -eq $kind -and $_.Identifier.ValueText -eq $name
    })
    if ($found.Count -ne 1) { throw "Expected one production declaration: $file / $name; found $($found.Count)" }
    if ($kind -eq 'VariableDeclaratorSyntax') { return $found[0].Parent.Parent.ToFullString() }
    return $found[0].ToFullString()
}
$members = @(
    (Get-ProductionMember 'ModMain.Runtime.cs' 'VariableDeclaratorSyntax' 'ModifiedItemMarker'),
    (Get-ProductionMember 'ModMain.ProductionUnlockRuntime.cs' 'MethodDeclarationSyntax' 'ConsolidateRecipeUseFamilies'),
    (Get-ProductionMember 'ModMain.Runtime.cs' 'ClassDeclarationSyntax' 'RecipeUseGroup'),
    (Get-ProductionMember 'ModMain.Runtime.cs' 'MethodDeclarationSyntax' 'IsModifiedItemId'),
    (Get-ProductionMember 'ModMain.Runtime.cs' 'MethodDeclarationSyntax' 'ResolveStaticRelationItemId'),
    (Get-ProductionMember 'ModMain.Runtime.cs' 'MethodDeclarationSyntax' 'NormalizeGameText'),
    (Get-ProductionMember 'ModMain.ItemDropRandomizeMath.cs' 'MethodDeclarationSyntax' 'GetItemDropCategoryWeight'),
    (Get-ProductionMember 'ModMain.ItemDropRandomizeMath.cs' 'MethodDeclarationSyntax' 'TryResolveStrictlyPositiveItemDropTotal'),
    (Get-ProductionMember 'ModMain.NumericProjectionSafety.cs' 'MethodDeclarationSyntax' 'TryRoundAndScaleDamage'),
    (Get-ProductionMember 'ModMain.Loot.cs' 'ClassDeclarationSyntax' 'LootWeightedItem'),
    (Get-ProductionMember 'ModMain.LootContainerSaveEstimate.cs' 'ClassDeclarationSyntax' 'LootContainerWeightedPool'),
    (Get-ProductionMember 'ModMain.LootContainerSaveEstimate.cs' 'MethodDeclarationSyntax' 'TryGetExactContainerItemTechLevel'),
    (Get-ProductionMember 'ModMain.LootContainerChanceMath.cs' 'MethodDeclarationSyntax' 'TryResolveContainerPerRollChance'),
    (Get-ProductionMember 'ModMain.LootContainerChanceMath.cs' 'MethodDeclarationSyntax' 'TryAverageContainerChance'),
    (Get-ProductionMember 'ModMain.ScavengerMissionRewards.cs' 'EnumDeclarationSyntax' 'ScavengerRewardClass'),
    (Get-ProductionMember 'ModMain.ScavengerMissionRewards.cs' 'MethodDeclarationSyntax' 'MatchesScavengerRewardClass'),
    (Get-ProductionMember 'ModMain.ScavengerMissionPoolMath.cs' 'StructDeclarationSyntax' 'ScavengerPoolStats'),
    (Get-ProductionMember 'ModMain.ScavengerMissionPoolMath.cs' 'MethodDeclarationSyntax' 'CountExactScavengerPool'),
    (Get-ProductionMember 'ModMain.ScavengerMissionPoolMath.cs' 'MethodDeclarationSyntax' 'IsExactScavengerCandidate'),
    (Get-ProductionMember 'ModMain.ScavengerMissionPoolMath.cs' 'MethodDeclarationSyntax' 'TryBuildExactScavengerWhitelist'),
    (Get-ProductionMember 'ModMain.LootAmputationWeights.cs' 'MethodDeclarationSyntax' 'TryExtractAmputationDropWeights'),
    (Get-ProductionMember 'ModMain.DataAccess.cs' 'ClassDeclarationSyntax' 'DataEntry'),
    (Get-ProductionMember 'ModMain.Loot.cs' 'ClassDeclarationSyntax' 'LootAmputationSource'),
    (Get-ProductionMember 'ModMain.LootIndexes.cs' 'VariableDeclaratorSyntax' 'LootAmputationSourcesByItem'),
    (Get-ProductionMember 'ModMain.LootIndexes.cs' 'MethodDeclarationSyntax' 'IndexLootAmputationSlot')
) -join "`n"
$gateMembers = foreach ($name in @('IsAuditedFeatureAssembly','IsCurrent103CargoSpawnAssembly','IsCurrent103TradeAssembly',
    'IsLegacy102FeatureAssembly','IsCurrentLootModifiersAssembly','IsCurrentContainerSaveEstimateAssembly',
    'IsCurrentScavengerAssembly','IsCurrentSourceFamilyAssembly','IsCurrent104SourceFamilyAssembly')) {
    Get-ProductionMember 'ModMain.CompatibilityFeatureGates.cs' 'MethodDeclarationSyntax' $name
}
$gateConstants = $trees['ModMain.CompatibilityFeatureGates.cs'].GetRoot().DescendantNodes() | Where-Object {
    $_.GetType().Name -eq 'FieldDeclarationSyntax' -and $_.Modifiers.ToString() -match '\bconst\b'
} | ForEach-Object { $_.ToFullString() }
$members += "`n" + ($gateMembers -join "`n") + "`n" + ($gateConstants -join "`n")
$cases = (@('BehaviorCases.cs','CompatibilityCases.cs','GameModelFixtures.cs','AmputationCases.cs') | ForEach-Object {
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot $_))
}) -join "`n"
$pure = foreach ($name in @('ModMain.LootProbabilityMath.cs','ModMain.LootAvailability.cs','ModMain.SourceFamilyPolicy.cs')) {
    [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $name)))
}
# Fresh namespace prevents Add-Type's cache from hiding source changes on repeated runs.
$testNamespace = 'QiiBehavior_' + [guid]::NewGuid().ToString('N')
$code = ($pure -join "`n") + "`nnamespace ItemIntelligence { public static partial class ModMain {`n" + $members + "`n} }`n" + $cases
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $testNamespace))
$code = $code.Replace('namespace UnityEngine', ('namespace ' + $testNamespace + '.UnityFixture'))
$code = $code.Replace('namespace MGSC', ('namespace ' + $testNamespace + '.GameFixture'))
# All using directives must precede namespace declarations. Fixtures use fully qualified
# names outside the core, while production members inherit these compilation-unit imports.
$code = "using System.Collections;`nusing System.Collections.Generic;`nusing $testNamespace.UnityFixture;`nusing $testNamespace.GameFixture;`n" + $code
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($testNamespace + '.ModMain') -as [type]
$count = $type::RunBehaviorCases()
Write-Host ("Production C# behavior: PASS ($count assertions).") -ForegroundColor Green
