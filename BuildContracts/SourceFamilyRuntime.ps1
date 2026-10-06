# ============================================================================
# SOURCE FAMILY / RUNTIME POLICY CONTRACT
# Runtime verification owns random-start pool policy and prevents narrow
# feature fingerprints from leaking into the broad compatibility gate.
# ============================================================================

foreach ($token in @(
    'if (!IsCurrentSourceFamilyAssembly() || !_compatLoot)',
    'GetRawData',
    'map == null || !map.Contains(10)',
    '_sourceFamilyContractState = 1;')) {
    if ($sourceFamilyFeatureGateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "source-family runtime verification contract missing: $token"
    }
}

$sourceFamilyRewardText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootRewardSources.cs'))
if (-not $sourceFamilyRewardText.Contains('GetRandomStartingPoolKeys(IsCurrent104SourceFamilyAssembly())')) {
    throw 'random-start sources must select pools for the audited game version.'
}
$sourceFamilyPolicyText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.SourceFamilyPolicy.cs'))
foreach ($pool in @('RandomStart_rewardEquipment','RandomStart_rewardConsumables','General_rewardEquipment','General_rewardConsumables')) {
    if (-not $sourceFamilyPolicyText.Contains($pool)) { throw "random-start pool policy missing: $pool" }
}
foreach ($alias in @('Trade','CargoSpawn','LootModifiers','ContainerSaveEstimate','Scavenger','SourceFamily')) {
    if ($auditedFeatureBody.Contains(('Audited' + $alias + 'AssemblySha104'))) {
        throw 'feature-specific 1.0.4 audits must not promote the broad compatibility gate.'
    }
}
foreach ($fingerprint in @(
    '9D0C784A764D75EC6616BD3B5744C0E581BB1CE148FD175BD8CABA8195854006',
    'D72CD91F98DE4D6CCE8EE420AC1A906D89E68BA0CB164694D266B56F46339EBE',
    'BE78036434737521BB43DABBD934B579A08DC3E8370B834AEE36E40932F56FE0')) {
    if ($auditedFeatureBody.Contains($fingerprint)) {
        throw 'narrow feature fingerprint must not enter the broad compatibility gate.'
    }
}
if ($sourceFamilyFeatureGateText.IndexOf('randomStartPools=verified',[StringComparison]::Ordinal) -lt 0 -or
    $sourceFamilyFeatureGateText.IndexOf('map == null || !map.Contains(10)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current source-family runtime random-start verification contract missing.'
}
