# ============================================================================
# NUMERIC SAFETY / PROBABILITY PROPERTIES
# Pure deterministic assertions complement the source-contract audit. No game RNG,
# state mutation or sampled simulation is used here.
# ============================================================================

$runtimeMathText = (Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Runtime.cs')) + (Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.LootProbabilityMath.cs'))
$containerMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.LootContainerChanceMath.cs')
$lootPresentationMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.LootPresentation.cs')
$lootModifiersMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.LootModifiers.cs')
$baronSpecialMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.LootBaronSpecial.cs')
$scavengerPresentationMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.ScavengerMissionPresentation.cs')
$weaponPresentationMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.WeaponModePresentation.cs')
$datadiskMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.DatadiskRuntime.cs')
$itemDropMathCurrentText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.ItemDropRandomizeMath.cs')
$scavengerMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.ScavengerMissionChance.cs')
$weaponDamageMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.WeaponModeDamagePerAP.cs')
$weaponCriticalMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.WeaponModeCriticalDamagePerAP.cs')
$weaponScatterMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.WeaponModeScatter.cs')
$numericProjectionSafetyText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.NumericProjectionSafety.cs')
$factionMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Factions.cs')

foreach ($token in @(
    'double.IsNaN(result) || double.IsInfinity(result)',
    'if (raw > int.MaxValue) return false;',
    'wide < int.MinValue || wide > int.MaxValue',
    'double.IsNaN(bonusExpected) || double.IsInfinity(bonusExpected)',
    'double.IsNaN(perRoll) || double.IsInfinity(perRoll)',
    'float.IsNaN(modeMult) || float.IsInfinity(modeMult)',
    'float.IsNaN(ammoMult) || float.IsInfinity(ammoMult)',
    'float.IsNaN(scatter) || float.IsInfinity(scatter)',
    '(double)value > int.MaxValue || firstCount <= 0 || secondCount <= 0',
    'long wide = (long)rounded * firstCount;',
    'float percent = float.NaN;',
    'if (float.IsNaN(value) || float.IsInfinity(value)) return "—";',
    'stats.DamageMult.HasValue && !float.IsNaN(stats.DamageMult.Value)',
    '!resolved || float.IsNaN(minPercent) || float.IsInfinity(minPercent)',
    'value < 0.0 || double.IsNaN(value) || double.IsInfinity(value)',
    'if (float.IsNaN(percent) || float.IsInfinity(percent)) return "—";')) {
    if (($runtimeMathText + $containerMathText + $lootPresentationMathText + $lootModifiersMathText + $baronSpecialMathText + $scavengerPresentationMathText + $weaponPresentationMathText + $datadiskMathText + $weaponDamageMathText + $weaponScatterMathText + $numericProjectionSafetyText + $factionMathText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "Numeric fail-closed guard missing: $token"
    }
}
# Container save estimates must be visually unambiguous: a range separator is
# typographic, never a minus-sign-like ASCII hyphen, and RU decimal output uses commas.
foreach ($token in @('"≈ " + maxText + percentSuffix','"≈ " + minText + "–" + maxText + percentSuffix',
    'string percentSuffix = ru ? " %" : "%";','return russian ? text.Replace(''.'', '','') : text;')) {
    if ($containerMathText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "Container estimate presentation contract missing: $token"
    }
}

foreach ($token in @(
    '1.0 - Math.Pow(1.0 - basePerRoll, baseRolls)',
    'CorpseBonusAtLeastOnceChance(bonusPerRoll, bonusExpected)',
    '1.0 - (1.0 - baseChance) * (1.0 - bonusChance)',
    'targetWeight / totalWeight',
    'missAll *= Math.Pow(perRollMiss, rolls)',
    'TryResolveStrictlyPositiveItemDropTotal',
    'TryRoundAndScaleDamage(baseMin * perFragmentMult, fragments, casts, out totalMin)',
    'TryRoundAndScaleDamage(normalPerHitMin * critMult, fragments, casts, out totalMin)',
    'int rounded = Mathf.RoundToInt(value);')) {
    if (($runtimeMathText + $containerMathText + $itemDropMathCurrentText + $scavengerMathText + $weaponDamageMathText + $weaponCriticalMathText + $numericProjectionSafetyText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "Audited math formula token missing: $token"
    }
}

$rngNeutralOwners = @(
    'ModMain.LootContainerChanceMath.cs','ModMain.LootContainerSaveEstimate.cs',
    'ModMain.ScavengerMissionChance.cs','ModMain.ScavengerMissionPoolMath.cs',
    'ModMain.LootBaronUltimateData.cs')
foreach ($owner in $rngNeutralOwners) {
    $text = Read-Utf8Strict -Path (Join-Path $sourceDir $owner)
    foreach ($forbidden in @('UnityEngine.Random.','System.Random(','DropManager.GenerateDrop','CreateForInventory(')) {
        if ($text.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "RNG-neutral math owner invokes gameplay/sampling API: $owner -> $forbidden" }
    }
}

# Execute actual production C#; duplicated PowerShell formulas cannot validate it.
& (Join-Path $root 'Tests/Run-BehaviorTests.ps1') -SourceRoot $root
& (Join-Path $root 'Tests/Run-ReleaseTests.ps1')
