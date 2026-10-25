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
    'WeaponModeDamageTypeMisses',
    'CacheIntrinsicWeaponModeDamageType')) {
    if ($damageTypeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "weapon damage-type contract missing: $token" }
}
if ($damageTypeText.IndexOf('Contains("slash"',[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
    $damageTypeText.IndexOf('Contains("stab"',[StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'weapon damage type must never be inferred from mode names.'
}
if ($weaponModePresentationText.IndexOf('TryResolveWeaponModeDamageType',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModePresentationText.IndexOf('WeaponModeTooltipMaxRows = 12',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModePresentationText.IndexOf('WeaponModeTooltipWidth = 420f',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModePresentationText.IndexOf('new Vector2(132f, 30f)',[StringComparison]::Ordinal) -lt 0) {
    throw 'weapon damage-type tooltip row/value-width contract missing.'
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    foreach ($token in @('ui.mode_damage_type','ui.mode_damage_type_default','ui.damage_type.blunt','ui.damage_type.pierce','ui.damage_type.lacer')) {
        if ($locText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "weapon damage-type localization missing: $token" }
    }
}

# Player-reported launcher, mode effect, and installed-arm regressions.
$grenadeText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.GrenadeAmmoRelations.cs')
foreach ($token in @('record.DefaultGrenadeId','record.AllowedGrenadeIds',
    'ResolveExplicitLauncherGrenades','CompatibleWeaponsByAmmo.Remove(oldId)',
    'weapon.CompatibleAmmo.Clear()')) {
    if ($grenadeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "explicit grenade allowlist contract missing: $token"
    }
}
$ammoAuditText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.AmmoSanityAudit.cs')
if ($ammoAuditText.IndexOf('CorrectExplicitGrenadeLauncherRelations();',[StringComparison]::Ordinal) -lt 0) {
    throw 'grenade correction must run after ammo warmup.'
}

$effectsText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.WeaponModePayloadEffects.cs')
foreach ($token in @('ResolveWeaponModeAmmoId','ammo.Traits','ammo.StatusEffectId',
    'ui.mode_trait_wounding','ui.mode_effect','ui.mode_status_effect')) {
    if ($effectsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "per-mode effects contract missing: $token"
    }
}
if ($weaponModePresentationText.IndexOf('AppendWeaponModePayloadEffectRows(modeKey, rows);',
    [StringComparison]::Ordinal) -lt 0) {
    throw 'per-mode effects are not shown in weapon tooltip.'
}

$armLinkText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.ArmWeaponLinks.cs')
foreach ($token in @('GetSimpleRecord<AugmentationRecord>',
    'GetSimpleRecord<WeaponRecord>','KnownItemIds.Contains','ui.arm_builtin_weapon',
    'ui.arm_source_augmentation')) {
    if ($armLinkText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "validated limb link contract missing: $token"
    }
}
$overviewText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.OverviewDashboard.cs')
if ($overviewText.IndexOf('AppendOverviewArmWeaponLinks(itemId);',[StringComparison]::Ordinal) -lt 0) {
    throw 'validated limb link not displayed in Overview.'
}
