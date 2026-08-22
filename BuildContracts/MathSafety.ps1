# ============================================================================
# NUMERIC SAFETY / PROBABILITY PROPERTIES
# Pure deterministic assertions complement the source-contract audit. No game RNG,
# state mutation or sampled simulation is used here.
# ============================================================================

$runtimeMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Runtime.cs')
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

function Get-FractionalExpectedChance([double]$p, [double]$expected) {
    $p = [Math]::Max(0.0,[Math]::Min(1.0,$p))
    $expected = [Math]::Max(0.0,$expected)
    $floor = [Math]::Floor($expected)
    $fraction = $expected - $floor
    $a = 1.0 - [Math]::Pow(1.0 - $p,$floor)
    $b = 1.0 - [Math]::Pow(1.0 - $p,$floor + 1.0)
    return (1.0 - $fraction) * $a + $fraction * $b
}

foreach ($p in @(0.0,0.0001,0.01,0.1,0.5,0.9999,1.0)) {
    $previous = -1.0
    foreach ($expected in @(0.0,0.1,0.3,0.9,1.0,1.2,2.7,5.0,12.75)) {
        $actual = Get-FractionalExpectedChance $p $expected
        $floor = [Math]::Floor($expected)
        $fraction = $expected - $floor
        $closed = 1.0 - [Math]::Pow(1.0 - $p,$floor) * (1.0 - $fraction * $p)
        if ([double]::IsNaN($actual) -or $actual -lt -1e-12 -or $actual -gt 1.0 + 1e-12 -or
            [Math]::Abs($actual - $closed) -gt 1e-12 -or $actual + 1e-12 -lt $previous) {
            throw "Fractional expected-roll probability property failed: p=$p expected=$expected actual=$actual closed=$closed"
        }
        $previous = $actual
    }
}

$random = [Random]::new(174125)
for ($i = 0; $i -lt 1000; $i++) {
    $base = $random.NextDouble()
    $bonus = $random.NextDouble()
    $combined = 1.0 - (1.0 - $base) * (1.0 - $bonus)
    if ($combined -lt $base - 1e-12 -or $combined -lt $bonus - 1e-12 -or $combined -gt 1.0 + 1e-12) {
        throw "Independent probability composition property failed: base=$base bonus=$bonus"
    }
}

# Rounding must happen per damage instance, matching the vanilla pipeline.
$perFragment = [Math]::Round(11.0 / 3.0)
$rangedTotal = [int]$perFragment * 3 * 2
if ($rangedTotal -ne 24) { throw "Per-fragment damage rounding property failed: $rangedTotal" }
$criticalTotal = [int][Math]::Round($perFragment * 1.5) * 3 * 2
if ($criticalTotal -ne 36) { throw "Per-hit critical rounding property failed: $criticalTotal" }

function Test-DamageScaleProjection([double]$value, [int]$first, [int]$second, [ref]$result) {
    $result.Value = 0
    if ([double]::IsNaN($value) -or [double]::IsInfinity($value) -or $value -lt 0.0 -or
        $value -gt [int]::MaxValue -or $first -le 0 -or $second -le 0) { return $false }
    $rounded = [long][Math]::Round($value,[MidpointRounding]::ToEven)
    $wide = $rounded * [long]$first
    if ($wide -gt [int]::MaxValue) { return $false }
    $wide *= [long]$second
    if ($wide -gt [int]::MaxValue) { return $false }
    $result.Value = [int]$wide
    return $true
}
$damageScaleCases = @(
    @{ Value=(11.0/3.0); First=3; Second=2; Ok=$true; Result=24 },
    @{ Value=2.5; First=1; Second=1; Ok=$true; Result=2 },
    @{ Value=1.0; First=[int]::MaxValue; Second=2; Ok=$false; Result=0 },
    @{ Value=2147483648.0; First=1; Second=1; Ok=$false; Result=0 },
    @{ Value=[double]::NaN; First=1; Second=1; Ok=$false; Result=0 },
    @{ Value=[double]::PositiveInfinity; First=1; Second=1; Ok=$false; Result=0 })
foreach ($case in $damageScaleCases) {
    $scaled = 0
    $ok = Test-DamageScaleProjection $case.Value $case.First $case.Second ([ref]$scaled)
    if ($ok -ne $case.Ok -or $scaled -ne $case.Result) {
        throw "Damage scale overflow/rounding property failed: value=$($case.Value), first=$($case.First), second=$($case.Second), ok=$ok, result=$scaled"
    }
}
