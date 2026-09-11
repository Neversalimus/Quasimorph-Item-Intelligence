# ============================================================================
# SOURCE FAMILY / VERSIONED OWNERSHIP CONTRACT
# Independent source-family audits own hardcoded story acquisition and
# random-start pool exposure. 1.0.4 requires its own RandomStart_* selection.
# ============================================================================

$sourceFamilyFeatureGateText = [IO.File]::ReadAllText(
    (Join-Path $sourceDir 'ModMain.CompatibilityFeatureGates.cs'))

foreach ($token in @(
    'AuditedSourceFamilyAssemblySha103Hotfix',
    'AuditedSourceFamilyAssemblySha104',
    'AuditedSourceFamilyAssemblySha104582',
    'IsCurrentSourceFamilyAssembly()',
    'IsAuditedSourceFamilyContractVerified()',
    'A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1')) {
    if ($sourceFamilyFeatureGateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current 1.0.3.578 source-family narrow hotfix gate missing: $token"
    }
}

$sourceFamilyGateBody = [regex]::Match(
    $sourceFamilyFeatureGateText,
    'private static bool IsCurrentSourceFamilyAssembly\(\).*?\n        \}',
    [Text.RegularExpressions.RegexOptions]::Singleline).Value
if ([string]::IsNullOrEmpty($sourceFamilyGateBody) -or
    $sourceFamilyGateBody.IndexOf('AuditedSourceFamilyAssemblySha103Hotfix',[StringComparison]::Ordinal) -lt 0 -or
    $sourceFamilyGateBody.IndexOf('AuditedSourceFamilyAssemblySha104',[StringComparison]::Ordinal) -lt 0 -or
    $sourceFamilyGateBody.IndexOf('AuditedSourceFamilyAssemblySha104582',[StringComparison]::Ordinal) -lt 0) {
    throw 'current source-family helper must own the independently audited fingerprints.'
}

$auditedFeatureBody = [regex]::Match(
    $sourceFamilyFeatureGateText,
    'private static bool IsAuditedFeatureAssembly\(\).*?\n        \}',
    [Text.RegularExpressions.RegexOptions]::Singleline).Value
if ($auditedFeatureBody.IndexOf(
        'A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1',
        [StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'source-family hotfix must not promote A38 into broad IsAuditedFeatureAssembly ownership.'
}

if ($sourceFamilyFeatureGateText.IndexOf(
        'IsCurrentSourceFamilyAssembly() && _compatLoot ? 1 : -1;',
        [StringComparison]::Ordinal) -lt 0) {
    throw 'source-family contract state is not bound to the narrow current-build ownership helper.'
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
if ($auditedFeatureBody.Contains('9D0C784A764D75EC6616BD3B5744C0E581BB1CE148FD175BD8CABA8195854006')) {
    throw 'the current patch fingerprint must not enter the broad compatibility gate.'
}
if ($auditedFeatureBody.Contains('BE78036434737521BB43DABBD934B579A08DC3E8370B834AEE36E40932F56FE0')) {
    throw 'the 1.0.4 fingerprint must not enter the broad compatibility gate.'
}
