# Item Intelligence static build-contract coordinator.
#
# BUILD_AND_STAGE.ps1 owns environment discovery, C# compilation and staging.
# Files in BuildContracts/ own only CURRENT static invariants. They are dot-sourced in
# dependency order so later feature contracts may reuse source snapshots from earlier ones.

$buildContractRoot = Join-Path $root 'BuildContracts'
if (-not (Test-Path -LiteralPath $buildContractRoot -PathType Container)) {
    throw 'BuildContracts directory is missing.'
}

$buildContractBudgets = @{
    'Architecture.ps1' = 950
    'TradeArchitecture.ps1' = 120
    'StationProductionArchitecture.ps1' = 120
    'FeatureSemantics.ps1' = 850
    'GameplayExactness.ps1' = 550
    'SourceFamilyHotfix.ps1' = 80
    'MathSafety.ps1' = 180
    'ModderActions.ps1' = 220
    'CodeHygiene.ps1' = 180
    'ReleaseSafety.ps1' = 380
    'InstallSafety.ps1' = 80
    'TextSafety.ps1' = 120
    'Performance.ps1' = 160
}
$buildContractModules = @(
    'Architecture.ps1',
    'TradeArchitecture.ps1',
    'StationProductionArchitecture.ps1',
    'FeatureSemantics.ps1',
    'GameplayExactness.ps1',
    'SourceFamilyHotfix.ps1',
    'MathSafety.ps1',
    'ModderActions.ps1',
    'CodeHygiene.ps1',
    'ReleaseSafety.ps1',
    'InstallSafety.ps1',
    'TextSafety.ps1',
    'Performance.ps1'
)

$runtimeContractText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Runtime.cs') -Raw
$currentVersionToken = [regex]::Match($runtimeContractText, 'public const string Version = "([^"]+)";').Groups[1].Value
$currentMarkerToken = [regex]::Match($runtimeContractText, 'ACTIVE VERSION.*?\(([^)"]+)\)\.').Groups[1].Value

foreach ($moduleName in $buildContractModules) {
    $modulePath = Join-Path $buildContractRoot $moduleName
    if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
        throw "Build contract module missing: $moduleName"
    }
    Assert-No-PowerShellVariableColonHazards -Path $modulePath

    $moduleLines = (Get-Content -LiteralPath $modulePath).Count
    $moduleBudget = [int]$buildContractBudgets[$moduleName]
    if ($moduleLines -gt $moduleBudget) {
        throw "Build contract module budget exceeded: $moduleName = $moduleLines/$moduleBudget lines. Split by responsibility instead of growing a new monolith."
    }

    # Current contracts must explain invariants, not preserve experiment chronology.
    # The current test identity is allowed only where Runtime identity is explicitly checked.
    $contractText = Get-Content -LiteralPath $modulePath -Raw
    $historyProbe = $contractText
    if ($moduleName -eq 'GameplayExactness.ps1') { $historyProbe = $historyProbe.Replace($currentVersionToken,'').Replace($currentMarkerToken,'') }
    if ($historyProbe -match '(?i)v1\.7\.(?:3[0-9]|40)(?:\.\d+)?(?:-test\d+)?|\btest\d+\b|\bBuildFix\d*\b') {
        throw "Historical test/build provenance returned to current build contracts: $moduleName"
    }
}

$unexpectedModules = @(Get-ChildItem -LiteralPath $buildContractRoot -File -Filter '*.ps1' |
    Where-Object { $buildContractModules -notcontains $_.Name })
if ($unexpectedModules.Count -gt 0) {
    throw ('Unowned build contract module(s): ' + (($unexpectedModules | ForEach-Object Name) -join ', '))
}

foreach ($moduleName in $buildContractModules) {
    . (Join-Path $buildContractRoot $moduleName)
}
