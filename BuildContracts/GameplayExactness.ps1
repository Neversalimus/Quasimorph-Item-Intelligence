# ============================================================================
# GAMEPLAY EXACTNESS / FAIL-CLOSED CONTRACTS
# Current invariants only. Historical test/build provenance intentionally omitted.
# ============================================================================

# critical Damage/AP, enemy Loot clarity, and faction Technology navigation gates.
$weaponModeCriticalDamagePerApText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.WeaponModeCriticalDamagePerAP.cs') -Raw
$factionTechnologyNavigationText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.FactionTechnologyNavigation.cs') -Raw
$factionTechnologyPanelResolverText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.FactionTechnologyPanelResolver.cs') -Raw
$browserModelsText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserModels.cs') -Raw
foreach ($token in @(
    'TryCalculateWeaponModeCriticalDamagePerAp',
    'GetWeaponModeFloatMember((object)weapon.Damage, "critDmg")',
    'TryRoundAndScaleDamage(normalPerHitMin * critMult, fragments, casts, out totalMin)',
    'GetCritDamageBonus/backstab/perks/effects excluded',
    'ResetWeaponModeCriticalDamagePerApCache')) {
    if ($weaponModeCriticalDamagePerApText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current critical Damage/AP token missing: $token" }
}
if ($weaponModePresentationText.IndexOf('ui.mode_critical_damage_per_ap_default',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModePresentationText.IndexOf('WeaponModeTooltipMaxRows = 7',[StringComparison]::Ordinal) -lt 0) {
    throw 'current critical Damage/AP presentation row/height contract missing.'
}
if ($lootEnemyPresentationText.IndexOf('loot.note.enemy_bonus_separate',[StringComparison]::Ordinal) -lt 0 -or
    $lootEnemyPresentationText.IndexOf('AddWrappedLootNote("loot.note.random_equipment")',[StringComparison]::Ordinal) -ge 0) {
    throw 'current enemy Loot explanation contract missing or redundant old note returned.'
}
foreach ($token in @(
    'BrowserAction.FactionTechnology',
    'BeginFactionTechnologyNavigation',
    'IsStarmapNavigationForbiddenByTravelState',
    'IsStarmapExperimentSpaceContext',
    'ResolveStarmapExperimentFallbackType',
    'IsRaidPreparationStarmapFallback',
    'UI.Show',
    'FactionPanelOnSelected',
    'FactionWindowOnShowTechnologyWindow',
    'FactionTechnologyWindow')) {
    if (($factionTechnologyNavigationText + $browserUiText + $browserModelsText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current faction Technology navigation token missing: $token" }
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    foreach ($token in @('ui.mode_critical_damage_per_ap_default','loot.note.enemy_bonus_separate','ui.faction_technology_navigation_unavailable')) {
        if ($locText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current localization token missing: $token" }
    }
}

# direct faction Technology open + Overview/Loot clarity gates.
foreach ($token in @(
    'TryResolveFactionPanelByVanillaOnEnableOrder',
    '<OnEnable>b__41_0',
    'IsEnabledFaction',
    'target panel resolved by exact FactionsScreen.OnEnable order')) {
    if (($factionTechnologyNavigationText + $factionTechnologyPanelResolverText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current faction panel resolver token missing: $token" }
}
if ($overviewDashboardText.IndexOf('private static void BuildBrowserOverview',[StringComparison]::Ordinal) -lt 0) { throw 'current Overview dashboard entry point unavailable.' }
if ($overviewDashboardText.IndexOf('Ui("ui.trade_links")',[StringComparison]::Ordinal) -ge 0) { throw 'current regression: Trade links returned to Overview summary.' }
if ($overviewDashboardText.IndexOf('Ui("ui.faction_technology")',[StringComparison]::Ordinal) -ge 0) { throw 'current regression: Faction technologies returned to Overview summary.' }
foreach ($token in @('AddWrappedBrowserNoteGroup(','"loot.note.tech"','"loot.note.enemy_chance"','"loot.note.corpse_transfer"','"loot.note.enemy_bonus_separate"')) { if ($lootEnemyPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current enemy Loot page-safe explanation group missing: $token" } }
if ($ruLocalizationText.IndexOf('перk Марики',[StringComparison]::OrdinalIgnoreCase) -ge 0) { throw 'current localization typo guard triggered.' }
if ($ruLocalizationText.IndexOf("ui.loot_marika_organization`tМарика — Организация",[StringComparison]::Ordinal) -lt 0) { throw 'current RU Organization attribution missing.' }
if ($enLocalizationText.IndexOf("ui.loot_marika_organization`tMarika — Organization",[StringComparison]::Ordinal) -lt 0) { throw 'current EN Organization attribution missing.' }

# exact Scavengers closure + continuous-scroll UX.
$scavengerMissionRewardsText = Get-Content -LiteralPath $scavengerMissionRewardsPath -Raw
$scavengerMissionChanceText = Get-Content -LiteralPath $scavengerMissionChancePath -Raw
$scavengerMissionPoolMathText = Get-Content -LiteralPath $scavengerMissionPoolMathPath -Raw
$scavengerMissionPresentationText = Get-Content -LiteralPath $scavengerMissionPresentationPath -Raw
$scavengerMissionTimingText = Get-Content -LiteralPath $scavengerMissionTimingPath -Raw
$browserTextLayoutPath = Join-Path $sourceDir 'ModMain.BrowserTextLayout.cs'
$browserTextLayoutText = Get-Content -LiteralPath $browserTextLayoutPath -Raw
foreach ($token in @(
    'TryResolveScavengerRewardClass',
    'MatchesScavengerRewardClass',
    'composite.GetRecord<TrashRecord>()',
    'composite.GetRecord<WeaponRecord>()',
    'composite.GetRecord<HelmetRecord>()',
    'composite.GetRecord<ArmorRecord>()',
    'composite.GetRecord<LeggingsRecord>()',
    'composite.GetRecord<BootsRecord>()',
    'composite.GetRecord<ConsumableRecord>()',
    'composite.GetRecord<FixationMedicineRecord>()',
    'composite.GetRecord<AmmoRecord>()',
    'composite.GetRecord<GrenadeRecord>()')) {
    if ($scavengerMissionRewardsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current exact Scavengers predicate token missing: $token" }
}
foreach ($token in @(
    'ResolveTradeMissionsState()',
    'mission.IsStoryMission',
    'mission.IsBlocked',
    'IsScavengerMissionExpiredAtSnapshot(mission)',
    '(int)mission.ProcMissionType == 12 && progression.HasProxyCompanyDepartment',
    'station.Record.StationType',
    'stationType.ItemDropCategories',
    'victim.Record.ItemDropCategories',
    'victim.CurrentTechLevel',
    'progression.HasPurgeBrigadeDepartment',
    'progression.PurgeBrigadeResourcesBonus',
    'progression.PurgeBrigadeArmorWeaponBonus',
    'progression.PurgeBrigadeFoodMedsBonus',
    'progression.PurgeBrigadeAmmoGrenadesBonus',
    'stats.TargetCandidates / stats.TotalCandidates',
    'Math.Pow(perRollMiss, rolls)',
    'IsScavengerChanceContractVerified()',
    '!TryEnsureTradeTravelState()',
    '!inBramfatura.HasValue || inBramfatura.Value',
    'AddBrowserScavengerMissionRows(rows)')) {
    if ($scavengerMissionChanceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current exact Scavengers chance token missing: $token" }
}
foreach ($token in @(
    'Data.Items.Records',
    'primary.TechLevel > techLimit',
    'whitelist.Contains("Faction")',
    'primary.Categories.Contains(factionTag)',
    'stats.TotalCandidates++',
    'stats.TargetCandidates++',
    'Dictionary<string,float> with Add()',
    '!whitelist.Add(category)')) {
    if ($scavengerMissionPoolMathText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Scavengers pool-math token missing: $token" }
}
if ($scavengerMissionChanceText.IndexOf('TryEnsureTradeTravelState() && GetBoolMember(_tradeTravelMetadata, "IsInBramfatura")',[StringComparison]::Ordinal) -ge 0) {
    throw 'current Bramfatura fail-closed regression: old fail-open guard returned.'
}
$scavengerFeatureText = $scavengerMissionRewardsText + $scavengerMissionChanceText + $scavengerMissionPoolMathText + $scavengerMissionPresentationText + $scavengerMissionTimingText
if ($scavengerFeatureText.IndexOf('ItemDropSystem.Randomize(',[StringComparison]::Ordinal) -ge 0 -or
    $scavengerFeatureText.IndexOf('TryRandomize(',[StringComparison]::Ordinal) -ge 0 -or
    $scavengerFeatureText.IndexOf('UnityEngine.Random',[StringComparison]::Ordinal) -ge 0) {
    throw 'current read-only regression: Item Intelligence must reconstruct chance without rolling vanilla RNG.'
}
foreach ($token in @(
    'BrowserLine.ScavengerMissionHeader',
    'BrowserLine.ScavengerMissionRow',
    'Ui("ui.scavenger_current_missions")',
    'Ui("ui.scavenger_best_chance")',
    'FormatScavengerPercent(rows[0].ChancePercent)',
    'Ui("ui.travel")',
    'Ui("ui.scavenger_time_left")',
    'row.SpaceObjectId',
    'value.ToString("0.##"',
    'Ui("ui.rolls")')) {
    if ($scavengerMissionPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Scavengers presentation token missing: $token" }
}
if ($scavengerMissionPresentationText.IndexOf('FirstNonEmpty(row.SpaceObjectId, row.StationId)',[StringComparison]::Ordinal) -ge 0 -or
    $scavengerMissionPresentationText.IndexOf('row.StationId',[StringComparison]::Ordinal) -ge 0) {
    throw 'current Starmap exact-target regression: StationId fallback returned.'
}
foreach ($token in @(
    'ResetScavengerMissionTimingSnapshot()',
    'GetTradeTravelTimeSafe(row.SpaceObjectId, out travelHours)',
    'mission.ExpireTime - _scavengerMissionTimingNow.Value',
    'ResolveTradeSpaceTimeState()',
    'ScavengerTravelBySpaceObject.TryGetValue',
    'FormatTradeMissionRemainingVanilla(hours)',
    'string.Equals(state, "Idle", StringComparison.OrdinalIgnoreCase)',
    'mission.ExpireTime <= _scavengerMissionTimingNow.Value',
    'row.TravelHours.Value <= row.RemainingHours.Value ? 1 : 2')) {
    if ($scavengerMissionTimingText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Scavengers timing token missing: $token" }
}
if ($scavengerMissionTimingText.IndexOf('Math.Max(0d, (mission.ExpireTime - _scavengerMissionTimingNow.Value).TotalHours)',[StringComparison]::Ordinal) -ge 0) {
    throw 'current expiry regression: expired missions must be omitted, not clamped to zero.'
}
foreach ($token in @(
    'ToScavengerLineStyle(arrivalState)',
    'BrowserRowKind.LootRow, opponent, chance, travel, timeLeft')) {
    if ($browserModelsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Scavengers timing-row model token missing: $token" }
}
foreach ($token in @(
    'bool scavengerTimingRow = line.Style == BrowserLineStyle.ScavengerUnknown ||',
    'ConfigureLootColumn(factionCurrent, 488f, 88f, line.ColumnCurrent, 12.5f);',
    'ConfigureLootColumn(factionState, 576f, 112f, line.ColumnState, 12.5f);',
    'line.Style == BrowserLineStyle.ScavengerExpiresBeforeArrival')) {
    if ($browserRowRenderCombinedText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Scavengers timing-row geometry token missing from pooled renderer owners: $token" }
}
$factionHeaderIndex = $factionsText.IndexOf('BrowserLines.Add(BrowserLine.Section(Ui("ui.faction_rewards")))',[StringComparison]::Ordinal)
$scavengerHookIndex = $factionsText.IndexOf('BuildBrowserScavengerMissionRewards(itemId);',[StringComparison]::Ordinal)
if ($factionHeaderIndex -lt 0 -or $scavengerHookIndex -lt 0 -or $factionHeaderIndex -gt $scavengerHookIndex) {
    throw 'current Factions ordering regression: FACTION REWARDS must render before Scavengers.'
}

# Continuous virtualized scrolling: no fixed page slicing or page-alignment filler.
if ($browserRowRendererText.IndexOf('int startIndex = BrowserNavigation.ScrollOffset;',[StringComparison]::Ordinal) -lt 0) {
    throw 'current main scroll renderer contract missing continuous row offset in orchestration owner.'
}
if ($browserRowRenderCombinedText.IndexOf('SyncBrowserContinuousScrollbar(_browserScrollScrollbar, total, BrowserVisibleRows, BrowserNavigation.ScrollOffset)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current main scroll renderer contract missing continuous scrollbar sync in pooled renderer owners.'
}
foreach ($token in @(
    'BrowserOffsetFromScrollbarValue',
    'float nextSize = Mathf.Clamp((float)visibleRows / Math.Max(1, totalRows)',
    'scrollbar.numberOfSteps = 0')) {
    if ($browserPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current main scroll infrastructure contract missing: $token" }
}
foreach ($token in @(
    'ScrollBrowserRows(-3)',
    'ScrollBrowserRows(3)',
    'ScrollBrowserRows(-(BrowserVisibleRows - 1))',
    'ScrollBrowserRows(BrowserVisibleRows - 1)',
    'int index = BrowserNavigation.ScrollOffset + visibleRow')) {
    if ($browserUiText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current browser input scroll contract missing: $token" }
}
if ($browserPresentationText.IndexOf('BrowserNavigation.ScrollOffset * BrowserVisibleRows',[StringComparison]::Ordinal) -ge 0 -or
    $browserPresentationText.IndexOf('_browserScrollOffset * BrowserVisibleRows',[StringComparison]::Ordinal) -ge 0 -or
    $browserCatalogPresentationText.IndexOf('_browserCatalogScrollOffset * BrowserCatalogVisibleRows',[StringComparison]::Ordinal) -ge 0 -or
    $browserPresentationText.IndexOf('_browserSearchScrollOffset * BrowserSearchVisibleRows',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: fixed page slicing returned to a virtualized scrolling list.'
}
if ($browserTextLayoutText.IndexOf('BrowserLines.Count % BrowserVisibleRows',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: page-alignment blank-row padding returned.'
}
foreach ($token in @(
    'int start = _browserCatalogScrollOffset;',
    'SyncBrowserContinuousScrollbar(')) {
    if ($browserCatalogPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current catalog scroll contract missing: $token" }
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    foreach ($token in @(
        'ui.scavenger_mission_rewards',
        'ui.scavenger_current_missions',
        'ui.scavenger_best_chance',
        'ui.rows_visible',
        'ui.opponent',
        'ui.scavenger_time_left')) {
        if ($locText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current localization token missing: $token" }
    }
    if ($locText.IndexOf('ui.scavenger_rolls_note',[StringComparison]::Ordinal) -ge 0 -or
        $locText.IndexOf('ui.scavenger_context_note',[StringComparison]::Ordinal) -ge 0) {
        throw 'current regression: generic Scavengers explanatory notes returned.'
    }
}
if ($ruLocalizationText.IndexOf("ui.scavenger_current_missions`tПОДХОДЯЩИХ МИССИЙ: ",[StringComparison]::Ordinal) -lt 0 -or
    $enLocalizationText.IndexOf("ui.scavenger_current_missions`tELIGIBLE MISSIONS: ",[StringComparison]::Ordinal) -lt 0) {
    throw 'current Scavengers summary semantics regression: eligible mission count label is not exact.'
}
if ($ruLocalizationText.IndexOf('ШАНС ≥1',[StringComparison]::Ordinal) -ge 0 -or
    $enLocalizationText.IndexOf('≥1 CHANCE',[StringComparison]::Ordinal) -ge 0) {
    throw 'current font-safety regression: unsupported Scavengers heading glyph returned.'
}
if ($runtimeText.IndexOf('public const string Version = "1.7.41.1";',[StringComparison]::Ordinal) -lt 0 -or
    $runtimeText.IndexOf('StableRelease17411',[StringComparison]::Ordinal) -lt 0) {
    throw 'current runtime version/marker contract missing.'
}

foreach ($token in @('BuildLootBaronPresentationGroups','Dictionary<string, BaronHabitatNode> nodesById','[ItemIntelligence][BaronHabitatGroup]')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Baron dedup/tree-identity contract missing: $token" }
}
foreach ($retired in @('BuildGenericMagnumCostIndex','GenericMagnumFallback')) {
    if ($sourceText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "current Magnum fail-closed regression: broad fallback returned: $retired" }
}

$itemDropMathText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.ItemDropRandomizeMath.cs'))
foreach ($token in @('TryResolveStrictlyPositiveItemDropTotal','WeightedList/DropManager do not reject zero or negative weights')) {
    if ($itemDropMathText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current ItemDrop non-positive-weight fail-closed contract missing: $token" }
}
foreach ($owner in @('ModMain.Runtime.cs','ModMain.LootBaronUltimateData.cs')) {
    $ownerText = [IO.File]::ReadAllText((Join-Path $sourceDir $owner))
    if ($ownerText.IndexOf('finalWeight <= 0.0) continue;',[StringComparison]::Ordinal) -ge 0) {
        throw "current ItemDrop regression: $owner silently discards non-positive vanilla candidates."
    }
}
$datadiskText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.DatadiskRuntime.cs'))
foreach ($token in @('MGSC.DatadiskRecord','SetCanonicalDatadiskUnlockPool','UnlockPoolSizeByDatadisk')) {
    if ($datadiskText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current canonical datadisk contract missing: $token" }
}
foreach ($token in @('if (rawUnlockType == null) continue;','Convert.ToInt32(rawUnlockType, CultureInfo.InvariantCulture)','if (unlockType != 0) continue;','string outputItemId = rawPool[n];')) {
    if ($datadiskText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current production-datadisk exact contract missing: $token" }
}
if ($datadiskText.IndexOf('RecipesById.TryGetValue(unlockId',[StringComparison]::Ordinal) -ge 0) { throw 'current datadisk regression: vanilla production UnlockId must not be remapped through recipes.' }
foreach ($forbidden in @('ObserveDatadiskUnlockPool','FallbackUnlockPoolFingerprintByDatadisk','AmbiguousUnlockPoolDatadisks','RawUnlockPoolByDatadisk')) {
    if ($sourceText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "current datadisk regression: graph-only probability fallback returned: $forbidden" }
}
$containerProfileText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootContainerProfiles.cs'))
foreach ($token in @('LootUnmappedContainerProfileIds','unmappedIds=','relationCoverage=')) {
    if ($containerProfileText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current unmapped-container fail-closed contract missing: $token" }
}
if ($containerProfileText.IndexOf('AddLootContainerDescriptor(dropId, dropId, 0, 0, false)',[StringComparison]::Ordinal) -ge 0) {
    throw 'current container regression: unmapped ContainerItemDrop profile is fabricated as a physical descriptor.'
}
$specialSourceText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootSpecialSources.cs')) + [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootRewardSources.cs'))
foreach ($token in @('StoryMissions','PrizeItems','StartingItems','AnComDataRewards','ConvertedItemId','FailedRitualItemId','GarbageItemId','DeathGiftId','InvokeFactionRewardTradeItems','General_rewardEquipment','General_rewardConsumables','CurrentReceipts','IsAuditedSourceFamilyContractVerified()')) {
    if ($specialSourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current special-source exactness contract missing: $token" }
}
$featureGateText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.CompatibilityFeatureGates.cs'))
foreach ($token in @('AuditedFeatureAssemblySha102','AuditedFeatureAssemblySha103','FE68E4355D4ED9CBAB7F8B1BA7717DBC1CC3FD749D0D11A644A9A3DB5EAB478F','IsAuditedSourceFamilyContractVerified()')) {
    if ($featureGateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current feature-owned special-source gate contract missing: $token" }
}
$factionChanceText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.Factions.cs'))
foreach ($token in @('float percent = float.NaN;','Percentage intentionally remains NaN until current-build panel math is','GetFactionRewardRecordItemId','float.IsNaN(view.RewardPercent)')) {
    if ($factionChanceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current faction reward chance fail-closed contract missing: $token" }
}
foreach ($retired in @('FactionRewardPoolSnapshot','FactionRewardPoolCache','GetFactionRewardPoolSnapshot')) {
    if ($sourceText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "current faction reward regression: unused full-pool model returned: $retired" }
}
foreach ($token in @('TryResolveStrictlyPositiveItemDropTotal','"amputation." + slotId')) {
    if ($lootIndexesText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current amputation chance fail-closed contract missing: $token" }
}
if ($sourceText.IndexOf('BuildMagnumProjectPriceIndex',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('RunCompatibilityIndexStage(`n                    "MagnumProjectPrices"',[StringComparison]::Ordinal) -ge 0) {
    throw 'current Magnum regression: unresolved MagnumProjectPrice.ItemsGrades returned to the static item-use index.'
}
$coreIndexText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.CoreIndexes.cs'))
if ($coreIndexText.IndexOf('BuildMagnumPriceRecordLookup',[StringComparison]::Ordinal) -ge 0 -or
    $coreIndexText.IndexOf('IsCostLikeMemberName(name)',[StringComparison]::Ordinal) -ge 0) {
    throw 'current Magnum regression: static perk index returned to generic cost-like fallback.'
}

$baronHabitatText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.BaronHabitat.cs'))
foreach ($token in @('TryReadBaronHabitatField','TryReadBaronHabitatProperty','baron.habitat.reverse.type.','LogRuntimeBoundaryWarningOnce')) {
    if ($baronHabitatText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current Baron habitat reflection-boundary contract missing: $token"
    }
}
foreach ($forbidden in @('try { value = fields[i].GetValue(record); } catch { }','try { value = property.GetValue(record, null); } catch { }')) {
    if ($baronHabitatText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "current regression: opaque Baron habitat reflection catch returned: $forbidden"
    }
}


# Loot accordion replaces Quick Jump navigation.
$lootNavigationText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootNavigation.cs'))
foreach ($token in @(
    'LootSectionExpanded',
    'ApplyLootCollapsibleSections',
    'HandleLootSectionToggleAction',
    'ResetLootAccordionState',
    'BrowserAction.ToggleLootSection',
    'BrowserLine.CollapsibleSection',
    '[ItemIntelligence][LootAccordion]')) {
    if (($lootNavigationText + $browserUiText + $browserModelsText + $sourceText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current Loot accordion contract missing: $token"
    }
}
foreach ($forbidden in @('LootJumpActionPrefix','InsertLootQuickJumps','HandleLootJumpAction','AddLootJumpIfSectionAdded')) {
    if ($sourceText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "current regression: current Quick Jump implementation returned: $forbidden"
    }
}
if ($browserUiText.IndexOf('ResetLootAccordionState();',[StringComparison]::Ordinal) -lt 0) {
    throw 'current Loot accordion state must reset when the inspector closes.'
}

# retained semantics. supersedes only habitat presentation and accordion affordance.
foreach ($token in @(
    'GetOverviewMagnumState',
    'FormatOverviewMagnumStatus',
    'CurrentRequired',
    'ui.overview_magnum_available_now',
    'ResolveBaronHabitatTree',
    'BaronHabitatMemberNames',
    'ui.baron_habitat',
    'ui.baron_guaranteed',
    'ui.baron_one_pact',
    'weapon.CompatibleAmmo.Clear()')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0 -and
        ($ruLocalizationText + $enLocalizationText + $templateLocalizationText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current contract missing: $token"
    }
}
if ($overviewDashboardText.IndexOf('magnum.ToString(CultureInfo.InvariantCulture) + Ui("ui.remaining")',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: Overview returned to misleading Magnum total + remaining wording.'
}
if ($sourceText.IndexOf('1 pact guaranteed',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('1 пакт гарантированно',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: Baron guarantee must not be hardcoded in source.'
}

# accordion affordance is self-evident; Baron habitat is structured by exact satellite ParentId.
foreach ($token in @(
    'disclosureLabel = (expanded ? "-  " : "+  ") + label',
    'BrowserRowKind.LootSectionHeader',
    'ResolveBaronHabitatTree',
    'IsBaronSatelliteRecord',
    'GetStringMember(record, "ParentId")',
    'AppendOverviewBaronHabitat',
    'AppendOverviewBaronHabitatNode')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current contract missing: $token"
    }
}
if ($sourceText.IndexOf('Ui("ui.loot_accordion_hint")',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: accordion must be understandable without an explanatory instruction row.'
}
foreach ($forbidden in @('▼  ','▶  ')) {
    if ($lootNavigationText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "current font-safety regression: unsupported disclosure glyph returned: $forbidden"
    }
}

# retained contracts. Habitat diagnostics were superseded by 's
# Station+Mission raid union, so do not require the removed exactStationSource token here.
foreach ($token in @(
    'CollectBaronHabitatFromRuntimeStations',
    'GetStringMember(station, "BramfaturaId")',
    'GetStringMember(station, "SpaceObjectId")',
    'stationMatches=',
    '[ItemIntelligence][AmmoSanity]',
    'AuditAmmoRelationsAfterWarmup',
    '[ItemIntelligence][LootAccordionAudit]',
    'MagnumSnapshot snapshot = GetMagnumSnapshot(itemId)')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current final-consistency contract missing: $token"
    }
}
# External IL audit artifacts are evidence, not runtime/build dependencies.
# Current exactness is enforced by the source contracts in this module.

if ($lootNavigationText.IndexOf('BrowserNavigation.ScrollOffset = Math.Max(0, clickedRowIndex - 1);',[StringComparison]::Ordinal) -lt 0) {
    throw 'current Loot accordion toggle must keep the clicked header in view.'
}

# Adaptive Entry / Smart Overview regression gates.
foreach ($token in @(
    'AppendOverviewCombat',
    'TryCalculateWeaponModeDamagePerAp',
    'TryCalculateWeaponModeCriticalDamagePerAp',
    'AppendOverviewRelationships',
    'ResolveOverviewPreview',
    'BrowserAction.SwitchTab',
    'GetOverviewRecipeRelationCount')) {
    if (($overviewDashboardText + $browserUiText + $sourceText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current Overview dashboard token missing: $token"
    }
}
foreach ($forbidden in @('GetUniqueRelationCount(itemId, true)','GetUniqueRelationCount(itemId, false)','stats.trade')) {
    if ($overviewDashboardText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "current regression: legacy/ambiguous trade summary returned to Overview: $forbidden"
    }
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    foreach ($token in @('stats.tech','stats.modes','stats.weapons','ui.overview_combat','ui.overview_relationships','ui.overview_obtainable_by_disassembly')) {
        if ($locText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current localization token missing: $token" }
    }
}


if (-not (Test-Path -LiteralPath $adaptiveEntryPath -PathType Leaf)) { throw 'current adaptive-entry owner missing.' }
$adaptiveEntryText = [IO.File]::ReadAllText($adaptiveEntryPath)
# Ownership is intentional: policy/selection lives in AdaptiveEntry, while the runtime
# diagnostic is emitted by BrowserUI at the point where the resolved landing tab is applied.
foreach ($token in @('ResolveAdaptiveEntryTab','EvaluateOverviewSignals','BrowserTabId.Recipes','BrowserTabId.Magnum')) {
    if (($adaptiveEntryText + $sourceText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Adaptive-entry policy token missing: $token" }
}
if ($browserUiText.IndexOf('[ItemIntelligence][AdaptiveEntry]',[StringComparison]::Ordinal) -lt 0) {
    throw 'current Adaptive Entry runtime marker missing from BrowserUI application point.'
}
foreach ($token in @('OverviewCombatHeader','OverviewCombatRow','OverviewRelationKind','shownNames')) {
    if (($overviewDashboardText + $sourceText + $browserPresentationText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current Smart Overview token missing: $token"
    }
}
foreach ($token in @('ShouldSuppressDescriptorAmmoForMelee','suppressMeleeInference','record.IsMelee','mode.Stats.AmmoPerShot > 0','weapon.CompatibleAmmo.Clear()','meleeDescriptorSuppressedWeapons')) {
    if ($ammoText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current melee-ammo exactness token missing: $token" }
}
if ($ammoText.IndexOf('foreach (string overrideId in weapon.OverrideAmmo.Keys)',[StringComparison]::Ordinal) -lt 0 -or
    $ammoText.IndexOf('if (!suppressMeleeInference)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current energy/ranged ammo preservation contract missing.'
}

# TMP_InputField is the sole owner of text editing.
# A second physical-key polling path caused intermittent two-character Backspace deletion.
foreach ($forbidden in @(
    'PollBrowserPhysicalBackspace',
    'DeleteOneBrowserSearchCharacter',
    'GetAsyncKeyState',
    'VkBackspace',
    'BrowserBackspaceInitialRepeatDelay',
    'BrowserBackspaceRepeatInterval',
    '_browserBackspaceWasDown',
    '_browserNextBackspaceRepeat',
    '_browserWin32BackspaceUnavailable'
)) {
    if ($sourceText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "current Backspace ownership regression: forbidden second editing path returned: $forbidden"
    }
}
if ($browserPresentationText.IndexOf('TMP_InputField input = searchGo.AddComponent<TMP_InputField>();',[StringComparison]::Ordinal) -lt 0 -or
    $browserPresentationText.IndexOf('input.onValueChanged.AddListener',[StringComparison]::Ordinal) -lt 0) {
    throw 'current Backspace ownership contract missing: TMP_InputField must remain the search text editor.'
}

# UX/architecture ownership gates.
foreach ($owner in @('ModMain.BrowserRowRenderer.cs','ModMain.BrowserRowLayout.cs','ModMain.OverviewPolicy.cs','ModMain.LootNavigation.cs','ModMain.BrowserPerfBudget.cs','ModMain.DatadiskRuntime.cs','ModMain.ProductionUnlockRuntime.cs')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir $owner) -PathType Leaf)) { throw "current feature owner missing: $owner" }
}
$runtimeLineCount = (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Runtime.cs')).Count
$presentationLineCount = (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserPresentation.cs')).Count
if ($runtimeLineCount -gt 4500) { throw "current Runtime architecture regression: $runtimeLineCount > 4500" }
if ($presentationLineCount -gt 1700) { throw "current BrowserPresentation architecture regression: $presentationLineCount > 1700" }
foreach ($token in @('ApplyBrowserStandardRowLayout','ApplyBrowserFullWidthRow','ResetBrowserRowTextFit','BrowserContentWidth')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current universal row layout token missing: $token" }
}
foreach ($token in @('OverviewSignalSnapshot','StrongOverview','MeaningfulGroups','ItemsUnlockedByDatadisk')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current adaptive overview token missing: $token" }
}
# supersedes Quick Jump with the in-page accordion while retaining
# LootNavigation as the feature owner.
foreach ($token in @('BrowserAction.ToggleLootSection','ApplyLootCollapsibleSections','HandleLootSectionToggleAction')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Loot accordion owner token missing: $token" }
}
if ($overviewDashboardText.IndexOf('List<int> unlockStatuses = new List<int>(chipUnlockItems.Count);',[StringComparison]::Ordinal) -lt 0 -or
    $overviewDashboardText.IndexOf('BrowserLine.ChipUnlockAction(unlockedItemId, right, unlockStatuses[i])',[StringComparison]::Ordinal) -lt 0) {
    throw 'current chip Overview performance regression: unlock status must be resolved once per item.'
}
foreach ($token in @('BrowserColdOpenBudgetMs','BrowserWarmOpenBudgetMs','BrowserRenderBudgetMs','[ItemIntelligence][PerfBudget]')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current performance budget token missing: $token" }
}
# supersedes the single-line guarantee with a structured Baron block.
# Validate the current player-facing tokens instead of keeping the retired combined label alive via localization only.
foreach ($token in @('ui.baron_habitat','ui.baron_guaranteed','ui.baron_one_pact','ui.baron_this_pact','ui.baron_depends_on_mission_tech')) {
    if (($sourceText + $ruLocalizationText + $enLocalizationText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Baron overview token missing: $token" }
}

# Manual container weights feed a save-aware neutral estimate, never a timeless exact percentage.
$lootIndexesText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootIndexes.cs'))
$lootPresentationText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootPresentation.cs'))
$lootContainerSaveEstimateText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootContainerSaveEstimate.cs')) + [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootContainerChanceMath.cs'))
$allSourceTextForContainerGate = (($sourceFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n")
if ($lootIndexesText.IndexOf('float.NaN,',[StringComparison]::Ordinal) -lt 0 -or
    $lootIndexesText.IndexOf('RecordLootContainerWeightedPool(dropId, biomeId, parsed, schemaResolved)',[StringComparison]::Ordinal) -lt 0) {
    throw 'manual container membership/raw-pool preservation contract missing'
}
if ($allSourceTextForContainerGate.IndexOf('new LootContainerDescriptor(' + "`n" + '                        dropId,',[StringComparison]::Ordinal) -ge 0 -or
    $allSourceTextForContainerGate.IndexOf('dropId, dropId, 0, 0, false',[StringComparison]::Ordinal) -ge 0) {
    throw 'fabricated unmapped ContainerItemDrop physical descriptor returned'
}
foreach ($token in @('MissionTechByStationType','mission.IsStoryMission','Math.Max(mission.MinTechLevel, victim.CurrentTechLevel)','ResolveFactionAvailabilityForCurrentSave(factionId) != 1','TryGetExactContainerItemTechLevel','TryGetExactContainerBonusEligibility','GetMember(trash, "SubType")','BonusEligibilityResolved','composite.PrimaryRecord as ItemRecord','entry.Weight <= 0.0','1.0 - Math.Pow(1.0 - basePerRoll, baseRolls)','CorpseBonusAtLeastOnceChance(bonusPerRoll, bonusExpected)','missionPointBudget=excluded.','return "—";')) {
    if ($lootContainerSaveEstimateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "manual container save-estimate exactness contract missing: $token" }
}
if ($lootContainerSaveEstimateText.IndexOf('Random.Range',[StringComparison]::Ordinal) -ge 0 -or
    $lootContainerSaveEstimateText.IndexOf('RollExpectedCount(',[StringComparison]::Ordinal) -ge 0) {
    throw 'manual container estimate must integrate expected rolls without advancing gameplay RNG'
}
$runtimeChanceText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.Runtime.cs'))
if ($runtimeChanceText.IndexOf('TryToDoubleSafe(entry.Value, out weight) && weight > 0.0',[StringComparison]::Ordinal) -ge 0 -or
    $runtimeChanceText.IndexOf('TryToDoubleSafe(GetMember(entry, "Value"), out weight) && weight > 0.0',[StringComparison]::Ordinal) -ge 0) {
    throw 'enemy class-weight parser must not silently discard non-positive vanilla weights'
}
