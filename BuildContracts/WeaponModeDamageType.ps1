# ============================================================================
# WEAPON MODE DAMAGE TYPE / AMMO SLOT CONTRACT
# ============================================================================
$damageTypePath = Join-Path $sourceDir 'ModMain.WeaponModeDamageType.cs'
if (-not (Test-Path -LiteralPath $damageTypePath -PathType Leaf)) { throw 'Weapon-mode damage-type source missing.' }
$damageTypeText = Get-Content -LiteralPath $damageTypePath -Raw
foreach ($token in @(
    'ResolveWeaponModeAmmoIdFromSlots',
    'ReadWeaponModeStringSlots',
    '"Firemodes", "Firemode1", "Firemode2"',
    '"OverrideAmmo", "OverrideAmmo1", "OverrideAmmo2"',
    'GetStringMember(ammo, "DmgType")',
    'ui.mode_damage_type_default',
    'ui.mode_damage_type',
    'WeaponModeDamageTypeMisses')) {
    if ($damageTypeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "weapon damage-type contract missing: $token" }
}
if ($damageTypeText.IndexOf('Contains("slash"',[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
    $damageTypeText.IndexOf('Contains("stab"',[StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'weapon damage type must never be inferred from mode names.'
}
if ($weaponModePresentationText.IndexOf('TryResolveWeaponModeDamageType',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModePresentationText.IndexOf('WeaponModeTooltipMaxRows = 8',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModePresentationText.IndexOf('WeaponModeTooltipWidth = 420f',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModePresentationText.IndexOf('new Vector2(132f, 30f)',[StringComparison]::Ordinal) -lt 0) {
    throw 'weapon damage-type tooltip row/value-width contract missing.'
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    foreach ($token in @('ui.mode_damage_type','ui.mode_damage_type_default','ui.damage_type.blunt','ui.damage_type.pierce','ui.damage_type.lacer')) {
        if ($locText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "weapon damage-type localization missing: $token" }
    }
}
