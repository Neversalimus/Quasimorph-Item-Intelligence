# ============================================================================
# ARCHITECTURE / OWNERSHIP / CORE INVARIANTS
# Current invariants only. Historical test/build provenance intentionally omitted.
# ============================================================================

$sourceFiles = @(Get-ChildItem -LiteralPath $sourceDir -Filter '*.cs' -File -Recurse | Sort-Object FullName)
# Whole-source snapshot is an Architecture-owned prerequisite used by early retired-symbol/global-state guards.
$sourceText = ($sourceFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
if ($sourceFiles.Count -lt 30) { throw "Source decomposition contract failed: expected at least 30 C# files, found $($sourceFiles.Count)." }
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ItemIntelligence.cs')) { throw 'Monolithic ItemIntelligence.cs must not return in the hardening branch.' }
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.AmmoDisassembly.cs')) { throw 'Architecture regression: Ammo and Disassembly must remain separate feature files.' }
foreach ($moduleFile in @(
    'ModMain.Ammo.cs','ModMain.WeaponModes.cs','ModMain.WeaponModePresentation.cs','ModMain.WeaponModeLocalization.cs','ModMain.WeaponModeScatter.cs','ModMain.WeaponModeDamagePerAP.cs','ModMain.WeaponModeCriticalDamagePerAP.cs','ModMain.NumericProjectionSafety.cs','ModMain.Disassembly.cs','ModMain.BrowserModels.cs','ModMain.BrowserState.cs',
    'ModMain.BrowserCatalog.cs','ModMain.BrowserCatalogLabels.cs','ModMain.BrowserPresentation.cs','ModMain.OverviewDashboard.cs','ModMain.AdaptiveEntry.cs','ModMain.BrowserTextLayout.cs','ModMain.BrowserCatalogPresentation.cs','ModMain.BrowserLazyUi.cs',
    'ModMain.InterfaceIcons.cs',
    'ModMain.LootIndexes.cs','ModMain.LootContainerProfiles.cs','ModMain.LootContainerIcons.cs','ModMain.LootContainerSaveEstimate.cs','ModMain.LootContainerChanceMath.cs',
    'ModMain.LootPresentation.cs','ModMain.LootEnemyPresentation.cs','ModMain.LootGeneralSpawn.cs','ModMain.LootSpecialSources.cs','ModMain.LootRewardSources.cs','ModMain.ItemDropRandomizeMath.cs','ModMain.LootBaronSpecial.cs','ModMain.LootBaronUltimateData.cs','ModMain.LootBaronPactPool.cs','ModMain.LootBaronData.cs','ModMain.OverviewBaronSpecial.cs','ModMain.LootModifiers.cs','ModMain.LootModifierRuntime.cs',
    'ModMain.Configuration.cs','ModMain.CoreIndexes.cs','ModMain.DataAccess.cs','ModMain.FeatureLifecycle.cs','ModMain.CompatibilityFeatureGates.cs',
    'ModMain.Icons.cs','ModMain.Information.cs','ModMain.Magnum.cs','ModMain.RuntimeServices.cs','ModMain.TradeMissionStatus.cs','ModMain.TradeFreshness.cs','ModMain.TradePresentation.cs','ModMain.TradeLayoutCompatibility.cs','ModMain.TradeLayoutControls.cs','ModMain.TradeBatchPricing103.cs','ModMain.StationProduction.cs',
    'ModMain.StarmapUiResolution.cs','ModMain.FactionTechnologyNavigation.cs','ModMain.FactionTechnologyPanelResolver.cs','ModMain.ScavengerMissionRewards.cs','ModMain.ScavengerMissionChance.cs','ModMain.ScavengerMissionPoolMath.cs','ModMain.ScavengerMissionPresentation.cs','ModMain.ScavengerMissionTiming.cs','ModMain.BrowserAdvancedSearch.cs','ModMain.ModderMode.cs',
    'ModMain.ModderSpawnRuntime.cs','ModMain.ModderCargoSpawn103.cs','ModMain.ModderSpawnPanel.cs','ModMain.BrowserLinkPresentation.cs')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir $moduleFile) -PathType Leaf)) {
        throw "Architecture contract missing source module: $moduleFile"
    }
}
$runtimePath = Join-Path $sourceDir 'ModMain.Runtime.cs'
if (-not (Test-Path -LiteralPath $runtimePath -PathType Leaf)) { throw 'ModMain.Runtime.cs missing.' }
$runtimeLines = (Get-Content -LiteralPath $runtimePath).Count
if ($runtimeLines -gt 4500) { throw "Source decomposition regressed: ModMain.Runtime.cs has $runtimeLines lines; current requires <= 4500." }
$browserUiPath = Join-Path $sourceDir 'ModMain.BrowserUI.cs'
$browserUiLines = (Get-Content -LiteralPath $browserUiPath).Count
$browserPresentationPath = Join-Path $sourceDir 'ModMain.BrowserPresentation.cs'
$browserPresentationLines = (Get-Content -LiteralPath $browserPresentationPath).Count
$overviewDashboardPath = Join-Path $sourceDir 'ModMain.OverviewDashboard.cs'
$overviewDashboardLines = (Get-Content -LiteralPath $overviewDashboardPath).Count
$adaptiveEntryPath = Join-Path $sourceDir 'ModMain.AdaptiveEntry.cs'
$adaptiveEntryLines = (Get-Content -LiteralPath $adaptiveEntryPath).Count
$browserCatalogPath = Join-Path $sourceDir 'ModMain.BrowserCatalog.cs'
$browserCatalogLines = (Get-Content -LiteralPath $browserCatalogPath).Count
$browserCatalogLabelsPath = Join-Path $sourceDir 'ModMain.BrowserCatalogLabels.cs'
$browserCatalogLabelsLines = (Get-Content -LiteralPath $browserCatalogLabelsPath).Count
$browserCatalogPresentationPath = Join-Path $sourceDir 'ModMain.BrowserCatalogPresentation.cs'
$browserCatalogPresentationLines = (Get-Content -LiteralPath $browserCatalogPresentationPath).Count
$interfaceIconsPath = Join-Path $sourceDir 'ModMain.InterfaceIcons.cs'
$interfaceIconsLines = (Get-Content -LiteralPath $interfaceIconsPath).Count
$lootPath = Join-Path $sourceDir 'ModMain.Loot.cs'
$lootLines = (Get-Content -LiteralPath $lootPath).Count
$lootIndexesPath = Join-Path $sourceDir 'ModMain.LootIndexes.cs'
$lootIndexesLines = (Get-Content -LiteralPath $lootIndexesPath).Count
$lootContainerProfilesPath = Join-Path $sourceDir 'ModMain.LootContainerProfiles.cs'
$lootContainerProfilesLines = (Get-Content -LiteralPath $lootContainerProfilesPath).Count
$lootContainerIconsPath = Join-Path $sourceDir 'ModMain.LootContainerIcons.cs'
$lootContainerIconsLines = (Get-Content -LiteralPath $lootContainerIconsPath).Count
$lootContainerSaveEstimatePath = Join-Path $sourceDir 'ModMain.LootContainerSaveEstimate.cs'
$lootContainerSaveEstimateLines = (Get-Content -LiteralPath $lootContainerSaveEstimatePath).Count
$lootContainerChanceMathPath = Join-Path $sourceDir 'ModMain.LootContainerChanceMath.cs'
$lootContainerChanceMathLines = (Get-Content -LiteralPath $lootContainerChanceMathPath).Count
$lootPresentationPath = Join-Path $sourceDir 'ModMain.LootPresentation.cs'
$lootPresentationLines = (Get-Content -LiteralPath $lootPresentationPath).Count
$lootBaronSpecialPath = Join-Path $sourceDir 'ModMain.LootBaronSpecial.cs'
$lootBaronSpecialLines = (Get-Content -LiteralPath $lootBaronSpecialPath).Count
$lootBaronUltimateDataPath = Join-Path $sourceDir 'ModMain.LootBaronUltimateData.cs'
$lootBaronUltimateDataLines = (Get-Content -LiteralPath $lootBaronUltimateDataPath).Count
$lootBaronDataPath = Join-Path $sourceDir 'ModMain.LootBaronData.cs'
$lootBaronDataLines = (Get-Content -LiteralPath $lootBaronDataPath).Count
$overviewBaronSpecialPath = Join-Path $sourceDir 'ModMain.OverviewBaronSpecial.cs'
$overviewBaronSpecialLines = (Get-Content -LiteralPath $overviewBaronSpecialPath).Count
$lootModifiersPath = Join-Path $sourceDir 'ModMain.LootModifiers.cs'
$lootModifiersLines = (Get-Content -LiteralPath $lootModifiersPath).Count
$lootEnemyPresentationPath = Join-Path $sourceDir 'ModMain.LootEnemyPresentation.cs'
$lootEnemyPresentationLines = (Get-Content -LiteralPath $lootEnemyPresentationPath).Count
$lootModifierRuntimePath = Join-Path $sourceDir 'ModMain.LootModifierRuntime.cs'
$lootModifierRuntimeLines = (Get-Content -LiteralPath $lootModifierRuntimePath).Count
$tradePath = Join-Path $sourceDir 'ModMain.Trade.cs'
$tradeLines = (Get-Content -LiteralPath $tradePath).Count
$tradeFreshnessPath = Join-Path $sourceDir 'ModMain.TradeFreshness.cs'
$tradeFreshnessLines = (Get-Content -LiteralPath $tradeFreshnessPath).Count
$disassemblyPath = Join-Path $sourceDir 'ModMain.Disassembly.cs'
$disassemblyLines = (Get-Content -LiteralPath $disassemblyPath).Count
$weaponModesPath = Join-Path $sourceDir 'ModMain.WeaponModes.cs'
$weaponModesLines = (Get-Content -LiteralPath $weaponModesPath).Count
$weaponModePresentationPath = Join-Path $sourceDir 'ModMain.WeaponModePresentation.cs'
$weaponModePresentationLines = (Get-Content -LiteralPath $weaponModePresentationPath).Count
$weaponModeLocalizationPath = Join-Path $sourceDir 'ModMain.WeaponModeLocalization.cs'
$weaponModeLocalizationLines = (Get-Content -LiteralPath $weaponModeLocalizationPath).Count
$weaponModeScatterPath = Join-Path $sourceDir 'ModMain.WeaponModeScatter.cs'
$weaponModeDamagePerApOwnershipPath = Join-Path $sourceDir 'ModMain.WeaponModeDamagePerAP.cs'
$weaponModeScatterLines = (Get-Content -LiteralPath $weaponModeScatterPath).Count
$weaponModeDamagePerApLines = (Get-Content -LiteralPath $weaponModeDamagePerApOwnershipPath).Count
$weaponModeCriticalDamagePerApOwnershipPath = Join-Path $sourceDir 'ModMain.WeaponModeCriticalDamagePerAP.cs'
$weaponModeCriticalDamagePerApLines = (Get-Content -LiteralPath $weaponModeCriticalDamagePerApOwnershipPath).Count
$numericProjectionSafetyLines = (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.NumericProjectionSafety.cs')).Count
$factionTechnologyNavigationPath = Join-Path $sourceDir 'ModMain.FactionTechnologyNavigation.cs'
$factionTechnologyPanelResolverPath = Join-Path $sourceDir 'ModMain.FactionTechnologyPanelResolver.cs'
$factionTechnologyNavigationLines = (Get-Content -LiteralPath $factionTechnologyNavigationPath).Count
$factionTechnologyPanelResolverLines = (Get-Content -LiteralPath $factionTechnologyPanelResolverPath).Count
$scavengerMissionRewardsPath = Join-Path $sourceDir 'ModMain.ScavengerMissionRewards.cs'
$scavengerMissionRewardsLines = (Get-Content -LiteralPath $scavengerMissionRewardsPath).Count
$scavengerMissionChancePath = Join-Path $sourceDir 'ModMain.ScavengerMissionChance.cs'
$scavengerMissionChanceLines = (Get-Content -LiteralPath $scavengerMissionChancePath).Count
$scavengerMissionPoolMathPath = Join-Path $sourceDir 'ModMain.ScavengerMissionPoolMath.cs'
$scavengerMissionPoolMathLines = (Get-Content -LiteralPath $scavengerMissionPoolMathPath).Count
$scavengerMissionPresentationPath = Join-Path $sourceDir 'ModMain.ScavengerMissionPresentation.cs'
$scavengerMissionPresentationLines = (Get-Content -LiteralPath $scavengerMissionPresentationPath).Count
$scavengerMissionTimingPath = Join-Path $sourceDir 'ModMain.ScavengerMissionTiming.cs'
$scavengerMissionTimingLines = (Get-Content -LiteralPath $scavengerMissionTimingPath).Count
if ($browserUiLines -gt 1650) { throw "Browser controller ownership regressed: ModMain.BrowserUI.cs has $browserUiLines lines; current requires <= 1650." }
$browserUiText = [IO.File]::ReadAllText($browserUiPath)
foreach ($token in @('HasObservedInspectorItemUi','directMissionHotkeyBootstrap','!directMissionHotkeyBootstrap')) {
    if ($browserUiText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Direct-mission first-hotkey regression guard missing: $token" }
}
if ($browserPresentationLines -gt 1700) { throw "Browser presentation ownership regressed: ModMain.BrowserPresentation.cs has $browserPresentationLines lines; current requires <= 1700." }
if ($overviewDashboardLines -gt 460) { throw "Overview dashboard ownership regressed: ModMain.OverviewDashboard.cs has $overviewDashboardLines lines; current requires <= 460." }
if ($adaptiveEntryLines -gt 90) { throw "Adaptive entry ownership regressed: ModMain.AdaptiveEntry.cs has $adaptiveEntryLines lines; current requires <= 90." }
$browserLazyUiPath = Join-Path $sourceDir 'ModMain.BrowserLazyUi.cs'
$browserLazyUiText = [IO.File]::ReadAllText($browserLazyUiPath)
$browserLazyUiLines = (Get-Content -LiteralPath $browserLazyUiPath).Count
if ($browserLazyUiLines -gt 180) { throw "Browser lazy-UI ownership regressed: ModMain.BrowserLazyUi.cs has $browserLazyUiLines lines; current requires <= 180." }
if ($browserCatalogLines -gt 550) { throw "Browser catalog controller ownership regressed: ModMain.BrowserCatalog.cs has $browserCatalogLines lines; current requires <= 550." }
if ($browserCatalogLabelsLines -gt 90) { throw "Browser catalog label/metadata ownership regressed: ModMain.BrowserCatalogLabels.cs has $browserCatalogLabelsLines lines; current requires <= 90." }
$browserCatalogLabelsText = [IO.File]::ReadAllText($browserCatalogLabelsPath)
foreach ($token in @('GetBrowserCatalogScopeLabel','GetBrowserCatalogDataFilterLabel','GetBrowserCatalogSortLabel','GetBrowserCatalogRowMetadata')) {
    if ($browserCatalogLabelsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Browser catalog label owner missing helper: $token" }
    if ([IO.File]::ReadAllText($browserCatalogPath).IndexOf('private static string ' + $token,[StringComparison]::Ordinal) -ge 0) { throw "Browser catalog controller regression: presentation helper returned to BrowserCatalog.cs: $token" }
}
if ($browserCatalogPresentationLines -gt 575) { throw "Browser catalog presentation ownership regressed: ModMain.BrowserCatalogPresentation.cs has $browserCatalogPresentationLines lines; current requires <= 575." }
if ($interfaceIconsLines -gt 875) { throw "Interface-icon presentation ownership regressed: ModMain.InterfaceIcons.cs has $interfaceIconsLines lines; current requires <= 875." }
if ($lootLines -gt 550) { throw "Loot facade/model ownership regressed: ModMain.Loot.cs has $lootLines lines; current requires <= 550." }
if ($lootIndexesLines -gt 1350) { throw "Loot index ownership regressed: ModMain.LootIndexes.cs has $lootIndexesLines lines; current requires <= 1350." }
if ($lootContainerProfilesLines -gt 300) { throw "Loot container-profile ownership regressed: ModMain.LootContainerProfiles.cs has $lootContainerProfilesLines lines; current requires <= 300." }
if ($lootContainerIconsLines -gt 1400) { throw "Loot container-icon ownership regressed: ModMain.LootContainerIcons.cs has $lootContainerIconsLines lines; current requires <= 1400." }
if ($lootContainerSaveEstimateLines -gt 320) { throw "Loot container save-estimate ownership regressed: ModMain.LootContainerSaveEstimate.cs has $lootContainerSaveEstimateLines lines; current requires <= 320." }
if ($lootContainerChanceMathLines -gt 220) { throw "Loot container chance-math ownership regressed: $lootContainerChanceMathLines/220 lines." }
if ($lootPresentationLines -gt 950) { throw "Loot presentation ownership regressed: ModMain.LootPresentation.cs has $lootPresentationLines lines; current requires <= 950." }
if ($lootBaronSpecialLines -gt 220) { throw "Baron Loot presentation/model ownership regressed: ModMain.LootBaronSpecial.cs has $lootBaronSpecialLines lines; current requires <= 220." }
if ($lootBaronUltimateDataLines -gt 500) { throw "Baron Ultimate data/math ownership regressed: ModMain.LootBaronUltimateData.cs has $lootBaronUltimateDataLines lines; current requires <= 500." }
if ($lootBaronDataLines -gt 100) { throw "Baron Qmorphos data resolver ownership regressed: ModMain.LootBaronData.cs has $lootBaronDataLines lines; current requires <= 100." }
if ($overviewBaronSpecialLines -gt 80) { throw "Baron Overview ownership regressed: ModMain.OverviewBaronSpecial.cs has $overviewBaronSpecialLines lines; current requires <= 80." }
if ($lootModifiersLines -gt 320) { throw "Loot modifier UI ownership regressed: ModMain.LootModifiers.cs has $lootModifiersLines lines; current requires <= 320." }
if ($lootEnemyPresentationLines -gt 220) { throw "Loot enemy presentation ownership regressed: ModMain.LootEnemyPresentation.cs has $lootEnemyPresentationLines lines; current requires <= 220." }
if ($lootModifierRuntimeLines -gt 240) { throw "Loot modifier runtime ownership regressed: ModMain.LootModifierRuntime.cs has $lootModifierRuntimeLines lines; current requires <= 240." }
if ($tradeLines -gt 900) { throw "Trade ownership regressed: ModMain.Trade.cs has $tradeLines lines; current requires <= 900." }
if ($tradeFreshnessLines -gt 100) { throw "TradeFreshness line budget exceeded: $tradeFreshnessLines/100" }
if ($disassemblyLines -gt 460) { throw "Disassembly ownership regressed: ModMain.Disassembly.cs has $disassemblyLines lines; current requires <= 460 after legacy scanner removal." }
if ($weaponModesLines -gt 160) { throw "Weapon-mode data ownership regressed: ModMain.WeaponModes.cs has $weaponModesLines lines; current requires <= 160." }
if ($weaponModePresentationLines -gt 300) { throw "Weapon-mode presentation ownership regressed: ModMain.WeaponModePresentation.cs has $weaponModePresentationLines lines; current requires <= 300." }
if ($weaponModeLocalizationLines -gt 180) { throw "Weapon-mode localization ownership regressed: ModMain.WeaponModeLocalization.cs has $weaponModeLocalizationLines lines; current requires <= 180." }
if ($weaponModeScatterLines -gt 190) { throw "Weapon-mode scatter ownership regressed: ModMain.WeaponModeScatter.cs has $weaponModeScatterLines lines; current requires <= 190." }
if ($weaponModeDamagePerApLines -gt 260) { throw "Weapon-mode Damage/AP ownership regressed: ModMain.WeaponModeDamagePerAP.cs has $weaponModeDamagePerApLines lines; current requires <= 260." }
if ($weaponModeCriticalDamagePerApLines -gt 220) { throw "Weapon-mode critical Damage/AP ownership regressed: ModMain.WeaponModeCriticalDamagePerAP.cs has $weaponModeCriticalDamagePerApLines lines; current requires <= 220." }
if ($numericProjectionSafetyLines -gt 60) { throw "Numeric projection safety ownership regressed: $numericProjectionSafetyLines/60 lines." }
if ($factionTechnologyNavigationLines -gt 400) { throw "Faction technology navigation ownership regressed: ModMain.FactionTechnologyNavigation.cs has $factionTechnologyNavigationLines lines; current requires <= 400." }
if ($factionTechnologyPanelResolverLines -gt 220) { throw "Faction technology panel resolver ownership regressed: ModMain.FactionTechnologyPanelResolver.cs has $factionTechnologyPanelResolverLines lines; current requires <= 220." }
if ($scavengerMissionRewardsLines -gt 120) { throw "Scavengers predicate ownership regressed: ModMain.ScavengerMissionRewards.cs has $scavengerMissionRewardsLines lines; current requires <= 120." }
if ($scavengerMissionChanceLines -gt 220) { throw "Scavengers exact-chance ownership regressed: ModMain.ScavengerMissionChance.cs has $scavengerMissionChanceLines lines; current requires <= 220." }
if ($scavengerMissionPoolMathLines -gt 140) { throw "Scavengers pool-math ownership regressed: ModMain.ScavengerMissionPoolMath.cs has $scavengerMissionPoolMathLines lines; current requires <= 140." }
if ($scavengerMissionPresentationLines -gt 120) { throw "Scavengers presentation ownership regressed: ModMain.ScavengerMissionPresentation.cs has $scavengerMissionPresentationLines lines; current requires <= 120." }
if ($scavengerMissionTimingLines -gt 120) { throw "Scavengers mission-timing ownership regressed: ModMain.ScavengerMissionTiming.cs has $scavengerMissionTimingLines lines; current requires <= 120." }

$runtimeText = Get-Content -LiteralPath $runtimePath -Raw
$coreIndexesPath = Join-Path $sourceDir 'ModMain.CoreIndexes.cs'
$coreIndexesText = Get-Content -LiteralPath $coreIndexesPath -Raw
$informationPath = Join-Path $sourceDir 'ModMain.Information.cs'
$informationText = Get-Content -LiteralPath $informationPath -Raw
$coreIndexesLines = (Get-Content -LiteralPath $coreIndexesPath).Count
$informationLines = (Get-Content -LiteralPath $informationPath).Count
if ($coreIndexesLines -gt 650) { throw "Core-index ownership regressed: ModMain.CoreIndexes.cs has $coreIndexesLines lines; current requires <= 650." }
if ($informationLines -gt 220) { throw "Information policy regressed: ModMain.Information.cs has $informationLines lines; current requires <= 220." }
$stateOwnershipContracts = @{
    'ModMain.Starmap.cs' = @('_pendingStarmapTargetId','StarmapSourceViewVisualStates')
    'ModMain.Trade.cs' = @('_marketItemId','MarketStations','MarketFactionRelations','_stationsState','_stationSystem','_tradeSystem','_worldPricesSystem','_itemsPrices','_marketEmptyRetryCooldown','_stationSchemaLogged')
    'ModMain.StationProduction.cs' = @('StationProductionByInputItem','StationProductionByOutputItem')
    'ModMain.Factions.cs' = @('RuntimeFactionsById','FactionTechUnlocksByItem','_secretDataSelectedFactionId','_secretDataContractLogged','_factionTradeSchemaLogged','_factionsState','_difficultyState')
    'ModMain.Magnum.cs' = @('_magnumProgression','_magnumLightLookupAttempted','_runtimeMagnumIndexBuilt','MagnumUses')
    'ModMain.RuntimeServices.cs' = @('_customResources','_runtimeResolveOwnerTypes','_runtimeFallbackResolveActive','_stateServicesResolved')
    'ModMain.Ammo.cs' = @('WeaponsByItem','WeaponModeRecordsById','AmmoWarmupItems','WeaponModeItemIdByKey')
    'ModMain.WeaponModeScatter.cs' = @('WeaponModeWeaponRecordsByItem','_weaponModeScatterFormulaLogged','_weaponModeCreatures','WeaponModeScatterLoggedKeys')
    'ModMain.WeaponModes.cs' = @('WeaponModeStatsByRawId','WeaponModeStatsByKey')
    'ModMain.Disassembly.cs' = @('DisassemblyOutputsByItem','DisassemblySourcesByOutputItem','DisassemblyWarmupItems')
    'ModMain.LootIndexes.cs' = @('LootContainerSourcesByItem','_lootEnemyContextIndexReady','_lootWarmupActive','_lootContainerDropCollection')
    'ModMain.LootContainerProfiles.cs' = @('_lootContainerProfileCount','_lootContainerMappedProfileCount','_lootContainerUnmappedProfileCount','_lootContainerIndexedProfileCount','_lootContainerItemLinkCount','LootMultiProfilePhysicalContainerIds','LootUnmappedContainerProfileIds')
    'ModMain.LootContainerIcons.cs' = @('LootContainerIconsById','_lootContainerRendererCatalog','LootContainerRecordsById')
    'ModMain.LootPresentation.cs' = @('_lootProgressRoot','_lootProgressLastVisible','LootDisplayNameCache')
    'ModMain.LootEnemyPresentation.cs' = @('LootEnemyRegularPresentationBuffer','LootEnemyCorpseBonusPresentationBuffer')
    'ModMain.LootGeneralSpawn.cs' = @('LootGeneralSpawnContainersByItem','_lootGeneralSpawnPairCount','LootGeneralSpawnManualContainerBuffer','LootGeneralSpawnAdditionalContainerBuffer')
    'ModMain.LootModifiers.cs' = @('_lootModifierUseManual','_lootManualMarauderLevel','_lootManualOrganization','_lootManualFieldMedic','LootActiveContainerPresentationBuffer')
    'ModMain.LootModifierRuntime.cs' = @('_lootModifierTypesResolved','_lootPerkSumMethod','_lootImplantBaseProgression')
    'ModMain.BrowserState.cs' = @('BrowserNavigation','_browserSearchInput','_browserCatalogOpen','_browserCatalogScope','BrowserFavoriteItemIds','BrowserRecentItemIds','_browserBackButton','BrowserLines','BrowserRowActionIcons','_lastHoveredItemId','_itemPointerScope','_inspectorRoot')
    'ModMain.Configuration.cs' = @('_configLoaded','_mcmRegistered','EnableItemIntelligence','InspectorKeyCode','ShowInterfaceIcons','UsePreviousTradeLayout')
    'ModMain.InterfaceIcons.cs' = @('BrowserInterfaceIconSprites','BrowserInterfaceIconBindings','_browserInterfaceSearchIcon')
    'ModMain.DataAccess.cs' = @('InstanceFlags','StaticFlags','ReadableMemberCache','InstanceMemberLookupCache')
    'ModMain.Icons.cs' = @('ItemSmallIcons','ItemSmallIconMisses','VanillaObservedItemIcons')
    'ModMain.Compatibility.cs' = @('_compatStaticChecked','_compatCore','CompatibilityReasons','RuntimeBoundaryWarningLogs')
    'ModMain.CompatibilityFeatureGates.cs' = @('_lootManualProjectionContractState','_containerSaveEstimateContractState','_scavengerChanceContractState','_sourceFamilyContractState')
    'ModMain.Localization.cs' = @('LocalizationCache','ExternalUiTranslations','ResolvedUiTextCache')
    'ModMain.CoreIndexes.cs' = @('_indexesBuilt','PriceByItem','UsedInRecipes','CraftedFromRecipes','RecipesById','KnownItemIds','BarterItemIds','ItemRecordsById','SpaceObjectRecordsById','ItemDataSourceNames')
    'ModMain.ItemMetadata.cs' = @('CanonicalItemMetadataRecordsById','ExactItemTechLevelsById','UnresolvedItemMetadataIds')
    'ModMain.ModderSpawnRuntime.cs' = @('_modderMissionActive','_modderCloneInventory','_modderCargoState','_modderInventoryTryAddMethod','_modderSpawnLastFrame')
    'ModMain.ModderSpawnPanel.cs' = @('_modderSpawnPanelRoot','_modderSpawnButton','_modderSpawnTargetIcon','_modderSpawnStatusKey')
}
foreach ($moduleName in $stateOwnershipContracts.Keys) {
    $modulePath = Join-Path $sourceDir $moduleName
    $moduleText = Get-Content -LiteralPath $modulePath -Raw
    foreach ($token in $stateOwnershipContracts[$moduleName]) {
        $declPattern = '(?m)^\s*private\s+static(?:\s+readonly)?\s+[^;=\r\n]+\s+' + [regex]::Escape($token) + '\s*(?:=|;)'
        if ($runtimeText -match $declPattern) {
            throw "Architecture regression: feature-owned state declaration '$token' returned to ModMain.Runtime.cs."
        }
        if ($moduleText -notmatch $declPattern) {
            throw "Architecture ownership contract missing declaration '$token' from $moduleName."
        }
    }
}


# Browser line/action type-safety and gradual global-state reduction.
$browserModelsPath = Join-Path $sourceDir 'ModMain.BrowserModels.cs'
$browserModelsText = Get-Content -LiteralPath $browserModelsPath -Raw
$browserModelsLines = (Get-Content -LiteralPath $browserModelsPath).Count
if ($browserModelsLines -gt 700) { throw "Browser model/type ownership regressed: ModMain.BrowserModels.cs has $browserModelsLines lines; current requires <= 700." }
$browserUiText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserUI.cs') -Raw
$browserStateText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserState.cs') -Raw
foreach ($token in @(
    'private enum BrowserActionKind',
    'private struct BrowserAction',
    'public readonly BrowserActionKind Kind;',
    'public readonly string Payload;',
    'public readonly BrowserTabId Tab;',
    'public readonly BrowserLootModifierCommand LootModifierCommand;',
    'public readonly BrowserAction Action',
    'private enum BrowserLineStyle',
    'private enum BrowserLeftContentKind',
    'private enum BrowserChipStatus',
    'private enum BrowserFactionRelation',
    'private enum BrowserTradeArrivalState',
    'private sealed class BrowserNavigationSessionState',
    'private static readonly BrowserNavigationSessionState BrowserNavigation')) {
    if (($browserModelsText + $browserStateText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "Browser type-safety/state ownership contract missing: $token"
    }
}
foreach ($token in @('switch (action.Kind)','BrowserActionKind.OpenStarmap','BrowserActionKind.OpenItem','BrowserActionKind.CopyText','BrowserActionKind.SwitchTab','BrowserActionKind.ToggleLootSection','BrowserActionKind.LootModifier','BrowserActionKind.FactionTechnology','BrowserActionKind.SecretDataBack','BrowserActionKind.SecretDataFaction')) {
    if ($browserUiText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Browser typed action routing contract missing: $token" }
}
foreach ($retired in @('ActionSpaceObjectId','BrowserItemActionPrefix','BrowserItemBackAction','BrowserCopyTextActionPrefix','BrowserTabActionPrefix','LootSectionToggleActionPrefix','LootModifierActionPrefix','FactionTechnologyActionPrefix','SecretDataFactionActionPrefix','SecretDataBackAction','QII_BROWSER_ITEM','QII_BROWSER_COPY','QII_BROWSER_TAB','QII_LOOT_SECTION','QII_LOOT_MODIFIER','QII_FACTION_TECH','QII_SECRET_DATA')) {
    if ($sourceText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "Retired string action protocol returned: $retired" }
}
foreach ($retired in @('private static int _browserTab','private static int _browserScrollOffset','BrowserItemNavigationHistory','BrowserScrollOffsetByTab')) {
    if ($sourceText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "Browser navigation global-state regression returned: $retired" }
}

$runtimeDecompositionContracts = @{
    'ModMain.Configuration.cs' = @('EnsureConfigLoaded','SaveConfig','TryRegisterMcm','OnMcmConfigSaved')
    'ModMain.DataAccess.cs' = @('GetStaticMember','GetMember','FindCachedMember','EnumerateData','ExtractKnownItemQuantitiesDeep','ExtractItemQuantities','GetReadableMembers')
    'ModMain.Icons.cs' = @('TryResolveItemSmallIcon','TryResolveCanonicalItemSmallIcon','TryResolveCompositeInventoryIcon','CaptureVanillaItemSlotIcon')
    'ModMain.StarmapUiResolution.cs' = @('FindActiveUnityObject','IsUiObjectActuallyUsable')
    'ModMain.CoreIndexes.cs' = @('BuildIndexesSafe','RunIndexStage','EnsureRuntimeIndexesReady','BuildSpaceObjectIndex','ClearIndexes','BuildItemCoverageIndex','IndexItemRecords','BuildMagnumIndex','AddMagnumUseUnique','BuildRecipeIndex','BuildStationProductionIndex')
    'ModMain.Information.cs' = @('HasInspectorData','HasVisibleMagnumUses','GetVisibleMagnumRequired','GetMagnumSnapshot')
}
foreach ($moduleName in $runtimeDecompositionContracts.Keys) {
    $modulePath = Join-Path $sourceDir $moduleName
    $moduleText = Get-Content -LiteralPath $modulePath -Raw
    foreach ($methodName in $runtimeDecompositionContracts[$moduleName]) {
        $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
        if ($runtimeText -match $declPattern) {
            throw "Architecture regression: decomposed method $methodName returned to ModMain.Runtime.cs."
        }
        if ($moduleText -notmatch $declPattern) {
            throw "Architecture decomposition contract missing $methodName from $moduleName."
        }
    }
}

$starmapUiResolutionText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.StarmapUiResolution.cs') -Raw
if ($runtimeText -match '\bclass\s+StarmapSourceViewVisualState\b') {
    throw 'Architecture regression: StarmapSourceViewVisualState returned to ModMain.Runtime.cs.'
}
if ($starmapUiResolutionText -notmatch '\bclass\s+StarmapSourceViewVisualState\b') {
    throw 'Architecture decomposition contract missing StarmapSourceViewVisualState.'
}

foreach ($dataAccessType in @('DataEntry','ReferenceComparer')) {
    if ($runtimeText -match ('\bclass\s+' + [regex]::Escape($dataAccessType) + '\b')) {
        throw "Architecture regression: DataAccess type $dataAccessType returned to ModMain.Runtime.cs."
    }
    $dataAccessText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.DataAccess.cs') -Raw
    if ($dataAccessText -notmatch ('\bclass\s+' + [regex]::Escape($dataAccessType) + '\b')) {
        throw "Architecture decomposition contract missing DataAccess type $dataAccessType."
    }
}

$resetOwnershipContracts = @{
    'ModMain.Ammo.cs' = @('ResetAmmoKnowledgeIndexState','ResetAmmoWeaponIndexState')
    'ModMain.Disassembly.cs' = @('ResetDisassemblyIndexState')
    'ModMain.Magnum.cs' = @('ResetMagnumIndexState')
    'ModMain.Trade.cs' = @('ResetTradeIndexState')
    'ModMain.Factions.cs' = @('ResetFactionIndexState')
    'ModMain.Loot.cs' = @('ResetLootIndexState')
    'ModMain.BrowserState.cs' = @('ResetBrowserIndexState')
}
foreach ($moduleName in $resetOwnershipContracts.Keys) {
    $modulePath = Join-Path $sourceDir $moduleName
    $moduleText = Get-Content -LiteralPath $modulePath -Raw
    foreach ($methodName in $resetOwnershipContracts[$moduleName]) {
        $methodToken = 'private static void ' + $methodName + '()'
        if ($moduleText.IndexOf($methodToken,[StringComparison]::Ordinal) -lt 0) {
            throw "Architecture reset ownership contract missing $methodName from $moduleName."
        }
        $callToken = $methodName + '();'
        if ($coreIndexesText.IndexOf($callToken,[StringComparison]::Ordinal) -lt 0) {
            throw "Architecture reset delegation missing call $callToken from ModMain.CoreIndexes.cs."
        }
    }
}

$sessionOwnershipContracts = @{
    'ModMain.Starmap.cs' = @('ResetStarmapRuntimeSessionState')
    'ModMain.Trade.cs' = @('InitializeTradeSpaceSessionState','ResetTradeMenuSessionState')
    'ModMain.Ammo.cs' = @('ResetAmmoRuntimeSessionState')
    'ModMain.Factions.cs' = @('InitializeFactionSpaceSessionState','ResetFactionMenuSessionState')
    'ModMain.Magnum.cs' = @('ResetMagnumRuntimeSessionState')
    'ModMain.BrowserState.cs' = @('InitializeBrowserSpaceSessionState','ResetBrowserMenuSessionState')
    'ModMain.RuntimeServices.cs' = @('ResetRuntimeServiceResolverSessionState')
}
foreach ($moduleName in $sessionOwnershipContracts.Keys) {
    $modulePath = Join-Path $sourceDir $moduleName
    $moduleText = Get-Content -LiteralPath $modulePath -Raw
    foreach ($methodName in $sessionOwnershipContracts[$moduleName]) {
        $methodToken = 'private static void ' + $methodName + '()'
        if ($moduleText.IndexOf($methodToken,[StringComparison]::Ordinal) -lt 0) {
            throw "Architecture session ownership contract missing $methodName from $moduleName."
        }
        $callToken = $methodName + '();'
        if ($runtimeText.IndexOf($callToken,[StringComparison]::Ordinal) -lt 0) {
            throw "Architecture session delegation missing call $callToken from ModMain.Runtime.cs."
        }
    }
}

$featureLifecyclePath = Join-Path $sourceDir 'ModMain.FeatureLifecycle.cs'
$featureLifecycleText = Get-Content -LiteralPath $featureLifecyclePath -Raw
$browserUiText = Get-Content -LiteralPath $browserUiPath -Raw
$browserPresentationText = Get-Content -LiteralPath $browserPresentationPath -Raw
$overviewDashboardText = Get-Content -LiteralPath $overviewDashboardPath -Raw
$weaponModesText = Get-Content -LiteralPath $weaponModesPath -Raw
$weaponModePresentationText = Get-Content -LiteralPath $weaponModePresentationPath -Raw
$weaponModeLocalizationText = Get-Content -LiteralPath $weaponModeLocalizationPath -Raw
$weaponModeScatterText = Get-Content -LiteralPath $weaponModeScatterPath -Raw
$browserCatalogText = Get-Content -LiteralPath $browserCatalogPath -Raw
$browserCatalogPresentationText = Get-Content -LiteralPath $browserCatalogPresentationPath -Raw
$interfaceIconsText = Get-Content -LiteralPath $interfaceIconsPath -Raw
$browserAllText = $browserUiText + $browserPresentationText + $overviewDashboardText + $browserCatalogText + $browserCatalogPresentationText

$weaponModeDataOwnershipContracts = @(
    'BuildWeaponModeStatsIndex','ProjectWeaponModeStats','GetWeaponModeFloatMember'
)
foreach ($methodName in $weaponModeDataOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($weaponModesText -notmatch $declPattern) {
        throw "Weapon-mode data ownership contract missing $methodName from ModMain.WeaponModes.cs."
    }
    if ($browserPresentationText -match $declPattern -or $runtimeText -match $declPattern) {
        throw "Weapon-mode data method $methodName escaped ModMain.WeaponModes.cs."
    }
}
$weaponModePresentationOwnershipContracts = @(
    'AttachBrowserWeaponModeTooltipTarget','SetBrowserWeaponModeTooltipTarget','BuildWeaponModeTooltipRows','EnsureBrowserWeaponModeTooltip'
)
foreach ($methodName in $weaponModePresentationOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($weaponModePresentationText -notmatch $declPattern) {
        throw "Weapon-mode presentation ownership contract missing $methodName from ModMain.WeaponModePresentation.cs."
    }
    if ($browserUiText -match $declPattern -or $runtimeText -match $declPattern) {
        throw "Weapon-mode presentation method $methodName escaped ModMain.WeaponModePresentation.cs."
    }
}


$weaponModeLocalizationOwnershipContracts = @(
    'ResolveWeaponModeDisplayLabel','BuildWeaponModeLanguageSafeFallback','IsWeaponModeLabelCompatibleWithCurrentLanguage'
)
foreach ($methodName in $weaponModeLocalizationOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($weaponModeLocalizationText -notmatch $declPattern) {
        throw "Weapon-mode localization ownership contract missing $methodName from ModMain.WeaponModeLocalization.cs."
    }
    if ($browserPresentationText -match $declPattern -or $runtimeText -match $declPattern) {
        throw "Weapon-mode localization method $methodName escaped ModMain.WeaponModeLocalization.cs."
    }
}
if ($overviewDashboardText -match 'BrowserLine\.WeaponMode\(mode\.Label') {
    throw 'Weapon-mode localization regression: cached warmup-time mode.Label must never be rendered directly.'
}
if ($overviewDashboardText -notmatch 'ResolveWeaponModeDisplayLabel\(mode\)') {
    throw 'Weapon-mode localization regression: Overview must resolve each mode label at render time.'
}
if ($weaponModeLocalizationText -notmatch 'EnsureLocalizationCacheLanguage\(\)' -or
    $weaponModeLocalizationText -notmatch 'LocalizeCandidates\(' -or
    $weaponModeLocalizationText -notmatch 'IsEnglishLanguage\(\)' -or
    $weaponModeLocalizationText -notmatch 'IsRussian\(\)') {
    throw 'Weapon-mode localization regression: dynamic language projection contract is incomplete.'
}

if ($browserPresentationText -match 'BrowserLine\.WeaponModeStats\(' -or
    $browserPresentationText -match 'TryFormatWeaponModeStats\(' -or
    $browserPresentationText -match 'FormatWeaponModeHeaderRight\(') {
    throw 'Weapon-mode inline stat rows returned; current requires hover-only stats.'
}
if ($weaponModePresentationText -match 'ui\.mode_shot_delay') {
    throw 'Weapon-mode hover tooltip must not expose non-vanilla shot-delay presentation.'
}
if ($weaponModePresentationText -notmatch 'TryCalculateVanillaFiremodeScatter\(modeKey, stats, out scatter\)' -or
    $weaponModePresentationText -notmatch 'ui\.mode_scatter') {
    throw 'Weapon-mode scatter regression: current must present the exact audited vanilla scatter path from the hover tooltip.'
}
if ($weaponModeScatterText -notmatch 'FireMode\.ScatterAngle\+WeaponRecord\.BonusScatterAngle' -or
    $weaponModeScatterText -notmatch 'GetScatterAngleMult\(record\)' -or
    $weaponModeScatterText -notmatch 'GetSimpleRecord<WeaponRecord>') {
    throw 'Weapon-mode scatter regression: exact vanilla IL formula/resolver contract is incomplete.'
}
if ($weaponModeScatterText -match 'BuildRelevantItemGraph|GetReadableMembers|GetProperties\(|GetFields\(') {
    throw 'Weapon-mode scatter performance regression: current forbids catalog/graph/reflection scans.'
}

$weaponModeScatterOwnershipContracts = @(
    'ResetWeaponModeScatterCache','IsWeaponModeMelee','TryCalculateVanillaFiremodeScatter',
    'ResolveWeaponModeWeaponRecord','ResolveWeaponModePlayer','FormatWeaponModeScatter'
)
foreach ($methodName in $weaponModeScatterOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($weaponModeScatterText -notmatch $declPattern) {
        throw "Weapon-mode scatter ownership contract missing $methodName from ModMain.WeaponModeScatter.cs."
    }
    if ($runtimeText -match $declPattern -or $browserPresentationText -match $declPattern) {
        throw "Weapon-mode scatter method $methodName escaped ModMain.WeaponModeScatter.cs."
    }
}

$browserPresentationOwnershipContracts = @(
    'EnsureInspectorPanel','CreateBrowserSearchUi','CreateBrowserSearchDropdown',
    'CreateBrowserText','CreateBrowserPageScrollbar','RenderBrowser',
    'UpdateBrowserChromeLocalization','UpdateBrowserTabs','BuildBrowserMagnum',
    'BuildBrowserRecipes','ConfigureInspectorText'
)
foreach ($methodName in $browserPresentationOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($browserUiText -match $declPattern) {
        throw "Architecture regression: presentation method $methodName returned to ModMain.BrowserUI.cs."
    }
    if ($browserPresentationText -notmatch $declPattern) {
        throw "Architecture presentation contract missing $methodName from ModMain.BrowserPresentation.cs."
    }
}
$browserRowRendererText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserRowRenderer.cs')
$browserRowRendererPartsText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserRowRendererParts.cs')
$browserRowRendererTradeText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserRowRendererTrade.cs')
$browserRowRenderCombinedText = $browserRowRendererText + "`n" + $browserRowRendererPartsText + "`n" + $browserRowRendererTradeText
$browserRowLayoutText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserRowLayout.cs')
if ($browserRowRendererText -notmatch '(?m)^\s*private\s+static\s+void\s+RenderBrowserRowsOnly\s*\(') {
    throw 'current row-render ownership contract missing RenderBrowserRowsOnly from ModMain.BrowserRowRenderer.cs.'
}
if ($browserPresentationText -match '(?m)^\s*private\s+static\s+void\s+RenderBrowserRowsOnly\s*\(') {
    throw 'current architecture regression: RenderBrowserRowsOnly returned to BrowserPresentation.'
}
if (($browserRowRendererPartsText + "`n" + $browserRowRendererTradeText) -match '(?m)^\s*private\s+static\s+void\s+RenderBrowserRowsOnly\s*\(') {
    throw 'current architecture regression: RenderBrowserRowsOnly must remain the orchestration owner, not a specialized renderer method.'
}
$rendererOrchestratorLines = (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserRowRenderer.cs')).Count
if ($rendererOrchestratorLines -gt 120) { throw "current renderer orchestration budget exceeded: $rendererOrchestratorLines/120" }
foreach ($token in @(
    'private struct BrowserRowRenderContext',
    'InitializeBrowserRowRenderContext',
    'PrepareBrowserRowForRender',
    'RenderBrowserRowContent',
    'RenderBrowserLootRow',
    'RenderBrowserLootSixColumnRow',
    'RenderBrowserBaronLootRow',
    'ApplyBrowserRowFinalStyle',
    'UpdateBrowserRowScrollChrome')) {
    if ($browserRowRendererPartsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current renderer decomposition contract missing: $token"
    }
}
if ($browserRowRendererTradeText.IndexOf('private static void RenderBrowserTradeRow',[StringComparison]::Ordinal) -lt 0) {
    throw 'current renderer decomposition contract missing Trade-owned RenderBrowserTradeRow.'
}
if ($browserRowRendererPartsText.IndexOf('private static void RenderBrowserTradeRow',[StringComparison]::Ordinal) -ge 0) {
    throw 'current renderer ownership regression: Trade renderer returned to BrowserRowRendererParts.cs.'
}
if ($browserRowRendererText.IndexOf('BrowserRowKind.',[StringComparison]::Ordinal) -ge 0) {
    throw 'current renderer decomposition regression: specialized row-kind rendering returned to the orchestration owner.'
}
$overviewOwnershipContracts = @(
    'BuildBrowserOverview','AppendOverviewCombat','AppendOverviewRelationships',
    'ResolveOverviewPreview','AppendOverviewPreview','UpdateBrowserStats'
)
foreach ($methodName in $overviewOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($overviewDashboardText -notmatch $declPattern) {
        throw "Overview dashboard contract missing $methodName from ModMain.OverviewDashboard.cs."
    }
    if ($browserUiText -match $declPattern -or $browserPresentationText -match $declPattern -or $runtimeText -match $declPattern) {
        throw "Overview dashboard method $methodName escaped ModMain.OverviewDashboard.cs."
    }
}
$browserCatalogOwnershipContracts = @(
    'EnsureCatalogPreferencesLoaded','SaveCatalogFavorites','ToggleBrowserFavorite','RecordBrowserItemVisit',
    'NavigateBrowserToItem','NavigateBrowserBack','OpenBrowserCatalog','CloseBrowserCatalog',
    'RefreshBrowserCatalog','CompareBrowserCatalogItems','BrowserCatalogItemPassesDataFilter',
    'ClassifyCatalogItem'
)
foreach ($methodName in $browserCatalogOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($browserUiText -match $declPattern -or $browserPresentationText -match $declPattern -or
        $browserCatalogPresentationText -match $declPattern) {
        throw "Architecture regression: catalog controller method $methodName escaped ModMain.BrowserCatalog.cs."
    }
    if ($browserCatalogText -notmatch $declPattern) {
        throw "Architecture catalog controller contract missing $methodName from ModMain.BrowserCatalog.cs."
    }
}
$browserCatalogPresentationOwnershipContracts = @(
    'CreateBrowserHeaderNavigationControls','UpdateBrowserHeaderActions','CreateBrowserCatalogButton',
    'CreateBrowserCatalogUi','RenderBrowserCatalogRows','UpdateBrowserCatalogControls',
    'UpdateBrowserCatalogButtonStyle','GetBrowserCatalogCategoryLabel'
)
foreach ($methodName in $browserCatalogPresentationOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($browserUiText -match $declPattern -or $browserPresentationText -match $declPattern -or
        $browserCatalogText -match $declPattern) {
        throw "Architecture regression: catalog presentation method $methodName escaped ModMain.BrowserCatalogPresentation.cs."
    }
    if ($browserCatalogPresentationText -notmatch $declPattern) {
        throw "Architecture catalog presentation contract missing $methodName from ModMain.BrowserCatalogPresentation.cs."
    }
}
$interfaceIconOwnershipContracts = @(
    'ResetBrowserInterfaceIconPresentation','FinalizeBrowserInterfaceIconPresentation',
    'RefreshBrowserInterfaceIconSetting','ApplyBrowserInterfaceIconVisibility',
    'CreateBrowserInterfaceIcon','ConfigureBrowserInterfaceIconControlLayout',
    'GetBrowserInterfaceIconSprite','DrawBrowserInterfaceIcon'
)
foreach ($methodName in $interfaceIconOwnershipContracts) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if ($browserAllText -match $declPattern) {
        throw "Architecture regression: interface-icon method $methodName escaped ModMain.InterfaceIcons.cs."
    }
    if ($interfaceIconsText -notmatch $declPattern) {
        throw "Architecture interface-icon presentation contract missing $methodName from ModMain.InterfaceIcons.cs."
    }
}
foreach ($iconContractToken in @(
    'BrowserInterfaceIconBindingLimit = 64',
    'BrowserInterfaceIconBindings.Count >= BrowserInterfaceIconBindingLimit',
    'new Texture2D(size, size, TextureFormat.ARGB32, false)',
    'texture.filterMode = FilterMode.Point',
    'image.raycastTarget = false',
    'ShowInterfaceIcons && !_browserInterfaceIconGenerationFailed',
    'offset.x = enabled && _browserInterfaceSearchIcon != null',
    'binding.ControlRect.anchoredPosition = iconVisible',
    'new Vector2(100f, 34f)',
    'new Vector2(92f, 24f)',
    'ApplyBrowserInterfaceIconVisibility(true)',
    'Item sprites and research/status markers deliberately remain separate owners'
)) {
    if ($interfaceIconsText.IndexOf($iconContractToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Interface-icon semantic contract missing: $iconContractToken"
    }
}
if ($interfaceIconsText.IndexOf('Resources.FindObjectsOfTypeAll',[StringComparison]::Ordinal) -ge 0 -or
    $interfaceIconsText.IndexOf('Update()',[StringComparison]::Ordinal) -ge 0) {
    throw 'Interface-icon performance contract failed: resource enumeration or per-frame owner returned.'
}
$browserStateText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserState.cs') -Raw
foreach ($catalogStateToken in @(
    'BrowserRecentItemLimit = 32','BrowserNavigationHistoryLimit = 64',
    'BrowserCatalogVisibleRows = 8','BrowserFavoriteItemIds','BrowserRecentItemIds')) {
    if ($browserStateText.IndexOf($catalogStateToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Catalog bounded-state contract missing: $catalogStateToken"
    }
}
foreach ($catalogSemanticToken in @(
    'favorites.txt','new UTF8Encoding(false, true)','File.Replace(',
    'BrowserCatalogScope.Favorites','BrowserCatalogScope.Recent',
    'BrowserCatalogDataFilter.Recipes','BrowserCatalogDataFilter.Sources',
    'BrowserCatalogDataFilter.Consumers','BrowserCatalogDataFilter.Magnum',
    'BrowserCatalogDataFilter.Factions','BrowserCatalogDataFilter.Ammo',
    'BrowserCatalogDataFilter.Disassembly','ShowRecipes','ShowSources',
    'ShowTradeInformation','ShowMagnumUses','ShowAmmoRelations')) {
    if ($browserCatalogText.IndexOf($catalogSemanticToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Catalog semantic contract missing: $catalogSemanticToken"
    }
}
$frameOwnershipContracts = @{
    'ModMain.Ammo.cs' = @('StartAmmoFeatureWarmup','TickAmmoFeatureFrameWork','StopAmmoFeatureFrameWork','GetAmmoWarmupStatus')
    'ModMain.Disassembly.cs' = @('StartDisassemblyFeatureWarmup','TickDisassemblyFeatureFrameWork','StopDisassemblyFeatureFrameWork','GetDisassemblyWarmupStatus')
    'ModMain.Factions.cs' = @('StartFactionFeatureWarmup','TickFactionFeatureFrameWork','StopFactionFeatureFrameWork','GetFactionWarmupStatus')
    'ModMain.Loot.cs' = @('StartLootFeatureWarmup','TickLootFeatureFrameWork','StopLootFeatureFrameWork','GetLootWarmupStatus')
}
foreach ($moduleName in $frameOwnershipContracts.Keys) {
    $moduleText = Get-Content -LiteralPath (Join-Path $sourceDir $moduleName) -Raw
    foreach ($methodName in $frameOwnershipContracts[$moduleName]) {
        if ($moduleText.IndexOf($methodName + '(',[StringComparison]::Ordinal) -lt 0) {
            throw "Architecture feature-frame ownership contract missing $methodName from $moduleName."
        }
        if ($featureLifecycleText.IndexOf($methodName + '();',[StringComparison]::Ordinal) -lt 0 -and
            $featureLifecycleText.IndexOf($methodName + '()',[StringComparison]::Ordinal) -lt 0) {
            throw "Architecture feature-frame coordinator missing $methodName."
        }
    }
}
foreach ($coordinatorMethod in @('StartFeatureWarmupsAfterCoreIndexes','TickFeatureFrameWork','StopFeatureFrameWorkForApplicationQuit','DescribeFeatureWarmupStates')) {
    if ($featureLifecycleText.IndexOf($coordinatorMethod + '(',[StringComparison]::Ordinal) -lt 0) {
        throw "Architecture feature-frame coordinator contract missing $coordinatorMethod."
    }
}
if ($coreIndexesText.IndexOf('StartFeatureWarmupsAfterCoreIndexes();',[StringComparison]::Ordinal) -lt 0 -or
    $coreIndexesText.IndexOf('DescribeFeatureWarmupStates()',[StringComparison]::Ordinal) -lt 0 -or
    $runtimeText.IndexOf('StopFeatureFrameWorkForApplicationQuit();',[StringComparison]::Ordinal) -lt 0) {
    throw 'Architecture feature-frame delegation missing from CoreIndexes/Runtime owners.'
}
if ($browserUiText.IndexOf('TickFeatureFrameWork();',[StringComparison]::Ordinal) -lt 0) {
    throw 'Architecture feature-frame delegation missing from ModMain.BrowserUI.cs.'
}
foreach ($rawTick in @('TickAmmoIndexWarmup();','TickDisassemblyWarmup();','TickFactionTechWarmup();','TickLootSourcesWarmup();','TickStationBarterWarmup();')) {
    if ($browserUiText.IndexOf($rawTick,[StringComparison]::Ordinal) -ge 0) {
        throw "Architecture regression: BrowserUI resumed direct feature scheduling through $rawTick"
    }
}
foreach ($rawStart in @('StartAmmoIndexWarmup();','StartDisassemblyWarmup();','StartFactionTechWarmup();','StartLootSourcesWarmup();','StartStationBarterWarmup();')) {
    if ($runtimeText.IndexOf($rawStart,[StringComparison]::Ordinal) -ge 0) {
        throw "Architecture regression: Runtime resumed direct feature warmup startup through $rawStart"
    }
}
foreach ($rawWarmupState in @('_ammoWarmupActive','_disassemblyWarmupActive','_factionTechWarmupActive','_lootWarmupActive','_lootWarmupRequested','_stationBarterWarmupActive','_runtimeFallbackResolveActive')) {
    if ($runtimeText.IndexOf($rawWarmupState,[StringComparison]::Ordinal) -ge 0) {
        throw "Architecture regression: feature warmup state $rawWarmupState leaked back into ModMain.Runtime.cs."
    }
}
if ($featureLifecycleText -match '(?m)^\s*private\s+static(?:\s+readonly)?\s+[^;\r\n()]+\s+_[A-Za-z0-9_]+\s*(?:=|;)') {
    throw 'Architecture regression: ModMain.FeatureLifecycle.cs must remain a stateless explicit coordinator.'
}

$runtimeServicesText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.RuntimeServices.cs') -Raw
foreach ($runtimeServiceMethod in @('TickStateServiceResolver','ResolveStateModule','BeginRuntimeFallbackResolver','TickRuntimeFallbackResolver','TryResolveRuntimeServicesLightweight','FindNestedRuntimeObject','StopRuntimeServiceFrameWork')) {
    $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($runtimeServiceMethod) + '\s*\('
    if ($runtimeText -match $declPattern) {
        throw "Architecture regression: runtime-service method $runtimeServiceMethod returned to ModMain.Runtime.cs."
    }
    if ($runtimeServicesText.IndexOf($runtimeServiceMethod + '(',[StringComparison]::Ordinal) -lt 0) {
        throw "Architecture runtime-services contract missing $runtimeServiceMethod from ModMain.RuntimeServices.cs."
    }
}

$lootText = Get-Content -LiteralPath $lootPath -Raw
$lootIndexesText = Get-Content -LiteralPath $lootIndexesPath -Raw
$lootContainerProfilesText = Get-Content -LiteralPath $lootContainerProfilesPath -Raw
$lootContainerIconsText = Get-Content -LiteralPath $lootContainerIconsPath -Raw
$lootContainerSaveEstimateText = Get-Content -LiteralPath $lootContainerSaveEstimatePath -Raw
$lootContainerChanceMathText = Get-Content -LiteralPath $lootContainerChanceMathPath -Raw
$lootPresentationText = Get-Content -LiteralPath $lootPresentationPath -Raw
$lootAllText = $lootText + $lootIndexesText + $lootContainerProfilesText + $lootContainerIconsText + $lootContainerSaveEstimateText + $lootContainerChanceMathText + $lootPresentationText
if ($lootText -match '(?m)^\s*private\s+static(?:\s+readonly)?\s+[^;\r\n()]+\s+[A-Za-z_][A-Za-z0-9_]*\s*(?:=|;)') {
    throw 'Architecture regression: ModMain.Loot.cs facade must not own mutable static state.'
}
foreach ($lootType in @('LootWeightedItem','LootContainerDescriptor','LootContainerSource','LootEnemySource','LootAmputationSource','EnemyLootContext','EnemyChanceAccumulator','LootItemMeta','LootMissionSource')) {
    $typePattern = '\bclass\s+' + [regex]::Escape($lootType) + '\b'
    if ($runtimeText -match $typePattern) {
        throw "Architecture regression: Loot model '$lootType' returned to ModMain.Runtime.cs."
    }
    if ($lootText -notmatch $typePattern) {
        throw "Architecture ownership contract missing Loot model '$lootType' from ModMain.Loot.cs."
    }
}

$lootBehaviorOwnershipContracts = @{
    'ModMain.LootIndexes.cs' = @('EnsureLootWarmupStarted','TickLootSourcesWarmup','IndexLootContainerDrop','TickLootMobClassSlice','IndexLootItemMeta','FindLootItemRecord')
    'ModMain.LootContainerProfiles.cs' = @('BuildLootContainerDescriptors','CollectLootContainerDropReferences','ResolveLootContainerRollRange','AddLootContainerDescriptor','RecordLootContainerProfileIndexResult','BuildLootMultiProfilePhysicalContainerSet')
    'ModMain.LootContainerIcons.cs' = @('EnsureLootContainerIconsResolved','TryResolveLootContainerVisual','TryResolveIndexedCanonicalContainerIcon','TryResolveGenericContainerFamilyFallback','TryResolveLootContainerSmallIcon')
    'ModMain.LootContainerSaveEstimate.cs' = @('ResetLootContainerSaveEstimateIndex','RecordLootContainerWeightedPool','GetLootContainerSaveEstimateSnapshot','BuildLootContainerSaveEstimateSnapshot','TryGetExactContainerItemTechLevel')
    'ModMain.LootContainerChanceMath.cs' = @('TryGetExactContainerBonusEligibility','FormatLootContainerEffectiveChance','TryAverageContainerChance','TryResolveContainerPerRollChance')
    'ModMain.LootPresentation.cs' = @('CreateLootProgressUi','UpdateLootProgressUi','BuildBrowserLootSources','FormatEnemyLootChance','ResolveLootContainerName','ResolveLootSourceName','FormatLootPercent')
    'ModMain.LootEnemyPresentation.cs' = @('AppendLootEnemySections','CompareLootEnemySourcesForPresentation','FormatEnemyLootPerRollChance','FormatCorpseBonusRollDistribution')
    'ModMain.LootModifiers.cs' = @('GetLootModifierSnapshot','AppendLootModifierControlLines','HandleLootModifierAction','FilterActiveLootContainerSources','FormatLootContainerRolls','GetEnemyLootResultLabelWithModifiers')
    'ModMain.LootModifierRuntime.cs' = @('ResolveCurrentLootModifierCreatureData','GetLootPerkParameterSum','GetCurrentAdditionalImplantDropChance','ResolveImplantRecoveryChance')
}
foreach ($moduleName in $lootBehaviorOwnershipContracts.Keys) {
    $moduleText = Get-Content -LiteralPath (Join-Path $sourceDir $moduleName) -Raw
    foreach ($methodName in $lootBehaviorOwnershipContracts[$moduleName]) {
        $declPattern = '(?m)^\s*private\s+static\s+[^\r\n]+\s+' + [regex]::Escape($methodName) + '\s*\('
        if ($lootText -match $declPattern) {
            throw "Architecture regression: decomposed Loot method $methodName returned to ModMain.Loot.cs."
        }
        if ($moduleText -notmatch $declPattern) {
            throw "Architecture Loot ownership contract missing $methodName from $moduleName."
        }
    }
}
foreach ($estimateType in @('LootContainerWeightedPool','LootContainerSaveEstimateSnapshot')) {
    if ($lootContainerSaveEstimateText -notmatch ('\bclass\s+' + [regex]::Escape($estimateType) + '\b')) { throw "Architecture container save-estimate type contract missing $estimateType." }
}
foreach ($iconType in @('LootContainerRendererSnapshot','LootContainerIconCandidate')) {
    if ($lootText -match ('\bclass\s+' + [regex]::Escape($iconType) + '\b')) {
        throw "Architecture regression: container-icon type $iconType returned to ModMain.Loot.cs."
    }
    if ($lootContainerIconsText -notmatch ('\bclass\s+' + [regex]::Escape($iconType) + '\b')) {
        throw "Architecture container-icon type contract missing $iconType."
    }
}

$disassemblyText = Get-Content -LiteralPath $disassemblyPath -Raw
foreach ($disassemblyContractToken in @(
    'DisassemblySourcesByOutputItem',
    'IndexCanonicalDisassemblySources',
    'ValidateDisassemblyIndexSymmetry',
    'GetDisassemblySourceCount',
    'FormatDisassemblySource'
)) {
    if ($disassemblyText.IndexOf($disassemblyContractToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Reverse-disassembly contract missing: $disassemblyContractToken"
    }
}
foreach ($retiredDisassemblyToken in @(
    'DisassemblyDropRecordsById','DisassemblyDropMemberWarmup','_disassemblyDropReferencesBuilt',
    'QueueDisassemblyDropReferenceMembers','IndexDisassemblyDropReferenceMember',
    'BuildDisassemblySourceGraph','ExtractDisassemblyOutputsDeep','FinalizeDisassemblySemantics',
    'ReadDisassemblyWeight','ReadDisassemblyPoolChance','LogDisassemblySchema'
)) {
    if ($disassemblyText.IndexOf($retiredDisassemblyToken,[StringComparison]::Ordinal) -ge 0) {
        throw "Disassembly architecture regression: retired legacy deep scanner returned through $retiredDisassemblyToken"
    }
}
if ($browserPresentationText.IndexOf('Ui("ui.obtained_by_disassembling")',[StringComparison]::Ordinal) -lt 0 -or
    $browserPresentationText.IndexOf('FormatDisassemblySource(source, ru)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Reverse-disassembly presentation contract missing from Recipes.'
}
if ($browserCatalogText.IndexOf('GetDisassemblySourceCount(itemId) > 0',[StringComparison]::Ordinal) -lt 0) {
    throw 'Reverse-disassembly catalog filter contract missing.'
}
if ($sourceText -match '\bLayoutKind\b') { throw 'Architecture regression: anonymous LayoutKind integers must not return; use BrowserRowKind.' }
if ($sourceText -match '(?:_browserTab|BrowserNavigation\.Tab)\s*(?:==|!=)\s*[0-6]\b') { throw 'Architecture regression: raw browser tab numbers must not return; use BrowserTabId.' }
if ($sourceText -match 'rawMany\.Count\s*>\s*currentRaw\.Count') { throw 'Chip probability regression: longest UnlockIds pool heuristic must not return.' }
if ($sourceText -match '(?i)longest candidate once per chip') { throw 'Chip probability regression: legacy longest-pool comment/path returned.' }
$silentCatchCount = ([regex]::Matches($sourceText, 'catch\s*\{\s*\}')).Count
if ($silentCatchCount -gt 120) { throw "Silent catch budget regressed: $silentCatchCount > 120. Classify critical boundaries instead of adding opaque catches." }
foreach ($architectureToken in @('enum BrowserTabId','enum BrowserRowKind','BrowserRowKind.ChipNote','BrowserTabId.Loot')) {
    if ($sourceText -notmatch ([regex]::Escape($architectureToken))) { throw "Architecture contract token missing: $architectureToken" }
}

$ammoText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Ammo.cs') -Raw
$tradeText = Get-Content -LiteralPath $tradePath -Raw
$configurationText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Configuration.cs') -Raw
# Feature-owned Trade presentation/recipe clarity tokens are validated by TradeArchitecture.ps1.
# Keep this module focused on cross-feature ownership and retired-symbol guards.
$stationProductionPath = Join-Path $sourceDir 'ModMain.StationProduction.cs'
$stationProductionText = Get-Content -LiteralPath $stationProductionPath -Raw
if ($runtimeText -match '\bclass\s+StationProductionRelation\b' -or
    $tradeText -match '\bclass\s+StationProductionRelation\b' -or
    $stationProductionText -notmatch '\bclass\s+StationProductionRelation\b') {
    throw 'Station production ownership regression: StationProductionRelation must remain owned by ModMain.StationProduction.cs.'
}
if ($runtimeText.IndexOf('GetUniqueRelationCount(',[StringComparison]::Ordinal) -ge 0 -or
    $informationText.IndexOf('GetUniqueRelationCount(',[StringComparison]::Ordinal) -ge 0 -or
    $overviewDashboardText.IndexOf('GetUniqueRelationCount(',[StringComparison]::Ordinal) -ge 0) {
    throw 'Smart Overview regression: retired visible Trade relation-count helper returned.'
}
foreach ($retiredTradeUiToken in @('ui.static_sources_barter','ui.static_consumers','ui.barter_use_this_item','AddBrowserRelations(')) {
    if ($sourceText.IndexOf($retiredTradeUiToken,[StringComparison]::Ordinal) -ge 0) {
        throw "Trade clarity regression: retired static relation UI returned through $retiredTradeUiToken"
    }
}
if ($tradeText.IndexOf('StationSources.TryGetValue(itemId',[StringComparison]::Ordinal) -ge 0 -or
    $tradeText.IndexOf('StationConsumers.TryGetValue(itemId',[StringComparison]::Ordinal) -ge 0) {
    throw 'Trade clarity regression: broad StationBarter classifications leaked back into the player-facing Trade tab.'
}
foreach ($retiredTradeArchitectureToken in @(
    'StationBarterWorkerRequest','StationBarterWorkerSync','StartStationBarterWarmup',
    'TickStationBarterWarmup','StationSources','StationConsumers',
    'ExtractStationQuantitiesWorker','StationGroupLabelWorker'
)) {
    if ($sourceText.IndexOf($retiredTradeArchitectureToken,[StringComparison]::Ordinal) -ge 0) {
        throw "Trade architecture regression: retired broad StationBarter subsystem returned through $retiredTradeArchitectureToken"
    }
}
if ($sourceText.IndexOf('JoinLocalizedItemNames',[StringComparison]::Ordinal) -ge 0 -or
    $coreIndexesText.IndexOf('new StationProductionRelation(id, input.Value, outputs)',[StringComparison]::Ordinal) -lt 0 -or
    $coreIndexesText.IndexOf('new StationProductionRelation(id, output.Value, inputs)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Station production localization contract failed: recipe index must store stable related item ids, never localized labels.'
}
$interfaceIconConfigurationContracts = @(
    'string.Equals(key, "ShowInterfaceIcons", StringComparison.OrdinalIgnoreCase)',
    'AddMcmBool(add, list, configValueType, "ShowInterfaceIcons",',
    'ApplyMcmBool(currentConfig, "ShowInterfaceIcons", ref ShowInterfaceIcons)',
    '"ShowInterfaceIcons=" + ShowInterfaceIcons',
    'RefreshBrowserInterfaceIconSetting();',
    'Ui("mcm.show_interface_icons")',
    'Ui("mcm.show_interface_icons_tip")'
)
foreach ($iconConfigToken in $interfaceIconConfigurationContracts) {
    if ($configurationText.IndexOf($iconConfigToken,[StringComparison]::Ordinal) -lt 0) {
        throw "MCM interface-icon contract missing: $iconConfigToken"
    }
}
$informationRendererContracts = @{
    'ShowMagnumUses' = $informationText + $browserAllText
    'ShowFutureMagnumUses' = $informationText + $browserAllText + $runtimeText
    'ShowRecipes' = $informationText + $browserAllText
    'ShowSources' = $informationText + $browserAllText + $tradeText + $lootAllText
    'ShowTradeInformation' = $informationText + $browserAllText + $tradeText
    'ShowMagnumSurplus' = $browserAllText
    'ShowAmmoRelations' = $informationText + $browserAllText + $ammoText
}
foreach ($setting in $informationRendererContracts.Keys) {
    $configurationContracts = @{
        'INI load' = 'string.Equals(key, "' + $setting + '", StringComparison.OrdinalIgnoreCase)'
        'MCM registration' = 'AddMcmBool(add, list, configValueType, "' + $setting + '",'
        'MCM apply' = 'ApplyMcmBool(currentConfig, "' + $setting + '", ref '
        'INI save' = '"' + $setting + '=" + '
    }
    foreach ($contractName in $configurationContracts.Keys) {
        if ($configurationText.IndexOf($configurationContracts[$contractName],[StringComparison]::Ordinal) -lt 0) {
            throw "MCM Information contract missing $contractName path for $setting."
        }
    }
    if ($informationRendererContracts[$setting].IndexOf($setting,[StringComparison]::Ordinal) -lt 0) {
        throw "MCM Information renderer contract missing for $setting."
    }
}
foreach ($informationToken in @(
    'Information={MagnumUses=',
    'if (ShowMagnumUses) { EnsureBrowserFactionColumnsUi(); BuildBrowserMagnum(itemId); }',
    'if (ShowAmmoRelations) BuildBrowserAmmo(itemId);',
    'if (ShowSources) BuildBrowserLootSources(itemId);',
    'bool recipesAvailable = ShowRecipes && _compatRecipes;',
    'if (!ShowFutureMagnumUses) continue;'
)) {
    if ($sourceText.IndexOf($informationToken,[StringComparison]::Ordinal) -lt 0) {
        throw "MCM Information visibility contract token missing: $informationToken"
    }
}
if ($ammoText.IndexOf('if (!ShowAmmoRelations) return;',[StringComparison]::Ordinal) -lt 0 -or
    $lootPresentationText.IndexOf('if (!ShowSources) return;',[StringComparison]::Ordinal) -lt 0) {
    throw 'MCM Information non-Trade detail-renderer hard gate is incomplete.'
}
if ($tradeText.IndexOf('if (!IsStarmapExperimentSpaceContext(out spaceContextReason)) return "—";',[StringComparison]::Ordinal) -lt 0) {
    throw 'Trade travel regression: Dungeon must fail closed before the vanilla space-only calculation.'
}
if ($sourceText.IndexOf('ItemSlot hover had no resolvable item id',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('(record != null || item != null) && _itemHoverResolveWarnings < 4',[StringComparison]::Ordinal) -lt 0) {
    throw 'Hover diagnostic regression: empty/unbound vanilla controls must not emit false compatibility warnings.'
}
foreach ($legacyOverviewTradeToken in @(
    'FormatVisibleTradeCounts(',
    'overview.trade.sources_prefix',
    'overview.trade.consumers_separator'
)) {
    if (($overviewDashboardText + $informationText).IndexOf($legacyOverviewTradeToken,[StringComparison]::Ordinal) -ge 0) {
        throw "Smart Overview regression: legacy Trade sources/consumers summary returned: $legacyOverviewTradeToken"
    }
}
foreach ($ambiguousContainerId in @('medical_container','medical_holder')) {
    if ($lootContainerIconsText.IndexOf($ambiguousContainerId,[StringComparison]::Ordinal) -lt 0) {
        throw "Container-icon frame-time contract missing audited ambiguous id: $ambiguousContainerId"
    }
}
foreach ($weaponCaseIconToken in @(
    'case "weapon_case_big": return new string[] { "GreenCaseBig", "WeaponCaseBig" };',
    'case "weapon_case_small": return new string[] { "GreenCaseSmall", "WeaponCaseSmall" };',
    'IsLootContainerVisualCompatible(containerId, entry.Source, entry.SpriteName)',
    'BuildWeaponCaseTargetAudit(containerId)'
)) {
    if ($lootContainerIconsText.IndexOf($weaponCaseIconToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Weapon-case icon identity contract missing: $weaponCaseIconToken"
    }
}
foreach ($containerCoverageToken in @(
    'LootWarmupContainerDropIds',
    'relationCoverage=',
    'additionalDropMembers=',
    'multiProfilePhysical=',
    'string.Equals(existing.DropId, source.DropId, StringComparison.OrdinalIgnoreCase)',
    'loot.column.container_profile'
)) {
    if ($lootAllText.IndexOf($containerCoverageToken,[StringComparison]::Ordinal) -lt 0 -and
        $sourceText.IndexOf($containerCoverageToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Loot container-profile coverage contract missing: $containerCoverageToken"
    }
}
foreach ($releaseCandidateToken in @(
    'FullSection = 13',
    'BrowserLine.FullSection(Ui("ui.secret_data_rewards"))',
    'AddWrappedBrowserNote("ui.secret_data_story_effect", 72, 86)',
    'private const int BrowserCatalogVisibleRows = 8;',
    'new Vector2(categoryX[i], i < 5 ? -40f : -75f)',
    'GetBrowserCatalogScopeLabel(scope, false)',
    'GetBrowserCatalogCategoryLabel(i, false)',
    'buttonWidth - 28f',
    'WrapBrowserFullWidthText(placementText, placementLimit)',
    'ScanRuntimeMagnumNodeSafe',
    'skippedNodes=',
    'ex.GetType().Name'
)) {
    if ($sourceText.IndexOf($releaseCandidateToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Release-candidate layout/runtime contract missing: $releaseCandidateToken"
    }
}
if ($sourceText.IndexOf('WrapLootText',[StringComparison]::Ordinal) -ge 0) {
    throw 'Release-candidate wrapper migration incomplete: retired WrapLootText reference remains.'
}
if (([regex]::Matches($ammoText, 'AmmoWarmupItems\.Clear\(\);')).Count -lt 3) {
    throw 'Warmup-memory regression: Ammo full-item buffer is not released on reset, stop and successful completion.'
}
if (([regex]::Matches($lootText, 'LootContainerSourcesByItem\.Clear\(\);')).Count -ne 1) {
    throw 'Loot reset regression: StartLootSourcesWarmup must reuse the single ResetLootIndexState contract.'
}
$hardeningText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Hardening.cs') -Raw
foreach ($bufferDiagnostic in @('AmmoWarmupItemBuffer','AmmoWarmupWeaponBuffer','DisassemblyWarmupBuffer','FactionTechWarmupBuffer')) {
    if ($hardeningText.IndexOf($bufferDiagnostic,[StringComparison]::Ordinal) -lt 0) {
        throw "Warmup-memory diagnostic contract missing: $bufferDiagnostic"
    }
}
foreach ($iconDiagnostic in @('InterfaceIconsEnabled','InterfaceIconLayoutActive','InterfaceIconBindings','InterfaceIconSprites')) {
    if ($hardeningText.IndexOf($iconDiagnostic,[StringComparison]::Ordinal) -lt 0) {
        throw "Interface-icon diagnostic contract missing: $iconDiagnostic"
    }
}
foreach ($lootProfileDiagnostic in @(
    'LootContainerProfiles','LootContainerMappedProfiles','LootContainerUnmappedProfiles','LootContainerUnmappedProfileIds',
    'LootContainerIndexedProfiles','LootContainerEmptyProfiles','LootContainerDescriptorLinks','LootContainerItemLinks',
    'LootMultiProfilePhysicalContainers'
)) {
    if ($hardeningText.IndexOf($lootProfileDiagnostic,[StringComparison]::Ordinal) -lt 0) {
        throw "Loot container-profile diagnostic contract missing: $lootProfileDiagnostic"
    }
}
