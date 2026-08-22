# ============================================================================
# FEATURE SEMANTICS / UI TRUTHFULNESS / NAVIGATION
# Current invariants only. Historical test/build provenance intentionally omitted.
# ============================================================================

# Travel-safety contract: direct Starmap navigation is allowed only when the
# vanilla TravelSystem observer is installed and no observed spaceship travel is active.
# A QII-owned Starmap also carries a DepartureButton backstop, so even a failed/partial
# Starmap OnEnable cannot start a second TravelSystem transition.
$starmapText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Starmap.cs') -Raw
foreach ($travelSafetyToken in @(
    'InstallStarmapTravelSafetyPatches',
    'TravelSystemStartObserverPrefix',
    'TravelSystemStartObserverPostfix',
    'QiiStarmapDepartureSafetyPrefix',
    'IsObservedSpaceshipTravelActive',
    'TryAdoptLoadedVanillaTravelState',
    'ResetStarmapTravelSafetySession',
    '_observedSpaceshipTravelTargetId',
    '_observedSpaceshipTravelState',
    'ui.starmap_unavailable_during_travel'
)) {
    if ($sourceText.IndexOf($travelSafetyToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Travel-safety contract token missing: $travelSafetyToken"
    }
}
if ($starmapText.IndexOf('adopted active vanilla travel from loaded session',[StringComparison]::Ordinal) -lt 0) {
    throw 'Travel-safety regression: mid-flight save/session adoption diagnostic is missing.'
}
if ($starmapText.IndexOf('RestoreStarmapSourceViewVisuals("session reset")',[StringComparison]::Ordinal) -lt 0 -or
    $starmapText.IndexOf('_pendingStarmapFallbackType = null;',[StringComparison]::Ordinal) -lt 0) {
    throw 'Starmap session-reset regression: fallback/visual suspension cleanup is incomplete.'
}
if ($starmapText.IndexOf('if (!_starmapTravelSafetyPatchesReady)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Travel-safety regression: QII Starmap no longer fails closed when the vanilla TravelSystem observer is unavailable.'
}
if ($starmapText.IndexOf('if (_qiiStarmapShowFailedUnsafe)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Travel-safety regression: failed Starmap show no longer hard-blocks QII Departure.'
}
if ($starmapText.IndexOf('CaptureTravelMetadataScalarSnapshot',[StringComparison]::Ordinal) -ge 0 -or
    $starmapText.IndexOf('ObservedSpaceshipTravelChangedMembers',[StringComparison]::Ordinal) -ge 0 -or
    $starmapText.IndexOf('markersReturned=',[StringComparison]::Ordinal) -ge 0) {
    throw 'Travel-safety architecture regression: generic scalar-delta observer returned; keep the proven TargetSpaceObject/State/current-destination contract.'
}
# release-polish lazy UI gates.
if ($browserPresentationText.IndexOf('CreateBrowserSearchDropdown();',[StringComparison]::Ordinal) -ge 0 -or
    $browserPresentationText.IndexOf('CreateBrowserCatalogUi();',[StringComparison]::Ordinal) -ge 0 -or
    $browserPresentationText.IndexOf('CreateLootProgressUi();',[StringComparison]::Ordinal) -ge 0 -or
    $browserPresentationText.IndexOf('EnsureQiiMarkerSprites();',[StringComparison]::Ordinal) -ge 0) {
    throw 'First-open performance regression: deferred Search/Catalog/Loot/marker UI returned to EnsureInspectorPanel.'
}
if ($browserUiText -match 'EnsureBrowserSearchIndexWarmup\(\);\s*ClearBrowserSearchField\(\);') {
    throw 'First-open performance regression: global item search index is warming eagerly during OpenInspector.'
}
foreach ($lazyToken in @('EnsureBrowserSearchDropdownUi','EnsureBrowserCatalogUi','EnsureBrowserFactionColumnsUi','EnsureBrowserRecipeContextUi','EnsureBrowserLootProgressUi','[ItemIntelligence][FirstOpenPerf]','targetResolve=','coreBuild=','recipeContext=','lootProgress=')) {
    if (($browserLazyUiText + $browserUiText + $browserCatalogText + $browserPresentationText).IndexOf($lazyToken,[StringComparison]::Ordinal) -lt 0) {
        throw "First-open lazy UI contract token missing: $lazyToken"
    }
}
$tradeCaseStart = $browserPresentationText.IndexOf('case BrowserTabId.Trade:',[StringComparison]::Ordinal)
$tradeCaseEnd = $browserPresentationText.IndexOf('case BrowserTabId.Ammo:',[StringComparison]::Ordinal)
if ($tradeCaseStart -lt 0 -or $tradeCaseEnd -le $tradeCaseStart) { throw 'Trade lazy-column regression: Trade switch case was not found.' }
$tradeCaseText = $browserPresentationText.Substring($tradeCaseStart, $tradeCaseEnd - $tradeCaseStart)
$tradeEnsure = $tradeCaseText.IndexOf('EnsureBrowserFactionColumnsUi();',[StringComparison]::Ordinal)
$tradeBuild = $tradeCaseText.IndexOf('BuildBrowserTrade(itemId);',[StringComparison]::Ordinal)
if ($tradeEnsure -lt 0 -or $tradeBuild -lt 0 -or $tradeEnsure -gt $tradeBuild) {
    throw 'Trade lazy-column regression: Trade must materialize the shared price/stock/travel columns before BuildBrowserTrade.'
}
$lootCaseStart = $browserPresentationText.IndexOf('case BrowserTabId.Loot:',[StringComparison]::Ordinal)
$lootCaseEnd = $browserPresentationText.IndexOf('default:', $lootCaseStart, [StringComparison]::Ordinal)
if ($lootCaseStart -lt 0 -or $lootCaseEnd -le $lootCaseStart) { throw 'Loot lazy-column regression: Loot switch case was not found.' }
$lootCaseText = $browserPresentationText.Substring($lootCaseStart, $lootCaseEnd - $lootCaseStart)
$lootEnsureColumns = $lootCaseText.IndexOf('EnsureBrowserFactionColumnsUi();',[StringComparison]::Ordinal)
$lootBuild = $lootCaseText.IndexOf('BuildBrowserLootSources(itemId);',[StringComparison]::Ordinal)
if ($lootEnsureColumns -lt 0 -or $lootBuild -lt 0 -or $lootEnsureColumns -gt $lootBuild) {
    throw 'Loot lazy-column regression: Loot must materialize the shared table columns before BuildBrowserLootSources.'
}
foreach ($itemIdToken in @('_inspectorItemIdText','_inspectorItemIdText.raycastTarget = true','Ui("ui.item_id")','GUIUtility.systemCopyBuffer','Item ID copied:')) {
    if ($browserPresentationText.IndexOf($itemIdToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Item-ID header contract token missing: $itemIdToken"
    }
}
foreach ($titleHotfixToken in @('_inspectorTitle.enableAutoSizing = true','_inspectorTitle.fontSizeMin = 18f','_inspectorTitle.fontSizeMax = 27f','new Vector2(78f, -5f), new Vector2(326f, 36f)','new Vector2(78f, -42f), new Vector2(326f, 16f)')) {
    if ($browserPresentationText.IndexOf($titleHotfixToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Item-name header hotfix contract token missing: $titleHotfixToken"
    }
}

# runtime audit writers removed after acceptance; no automatic Downloads output.

# general-spawn Loot coverage gates.
$lootGeneralSpawnPath = Join-Path $sourceDir 'ModMain.LootGeneralSpawn.cs'
if (-not (Test-Path -LiteralPath $lootGeneralSpawnPath -PathType Leaf)) { throw 'Loot general-spawn source missing.' }
$lootGeneralSpawnText = Get-Content -LiteralPath $lootGeneralSpawnPath -Raw
foreach ($token in @(
    'LootGeneralSpawnContainersByItem',
    'UseForSpawnItems',
    'AllowedItemClasses',
    'HasNormalLootGenerationSource',
    'TickLootGeneralSpawnIndexSlice',
    'ResolveLootGeneralSpawnContainersForItem',
    'HasDirectNormalLootGenerationSource',
    '[LootGeneralSpawn][OnDemand]',
    'GetBoolMember(record, "UseForSpawnItems")'
)) {
    if ($lootGeneralSpawnText.IndexOf($token, [StringComparison]::Ordinal) -lt 0) { throw "Loot general-spawn contract token missing: $token" }
}
$lootPresentationPath = Join-Path $sourceDir 'ModMain.LootPresentation.cs'
$lootPresentationText = Get-Content -LiteralPath $lootPresentationPath -Raw
foreach ($token in @('AppendLootGeneralSpawnContainerLines')) {
    if ($lootPresentationText.IndexOf($token, [StringComparison]::Ordinal) -lt 0) { throw "Loot general-spawn presentation token missing: $token" }
}

# release-polish gates.
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.LootFullAudit.cs')) { throw 'Release polish regression: LootFullAudit runtime writer must stay removed.' }
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.LootContextAudit.cs')) { throw 'Release polish regression: LootContextAudit runtime writer must stay removed.' }
$lootFacadeText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Loot.cs') -Raw
foreach ($forbiddenAuditToken in @('TryWriteFullLootAuditReports','ResetLootFullAuditState','QII_LOOT_CONTEXT_AUDIT_TEST4')) {
    if ($lootFacadeText.IndexOf($forbiddenAuditToken,[StringComparison]::Ordinal) -ge 0) { throw "Release polish regression: automatic Loot audit token returned: $forbiddenAuditToken" }
}
foreach ($obsoleteUiToken in @('loot.note.container_identity')) {
    if ($lootPresentationText.IndexOf($obsoleteUiToken,[StringComparison]::Ordinal) -ge 0) { throw "Release polish regression: obsolete Loot note returned: $obsoleteUiToken" }
}

# / Loot modifier simulation gates.
$lootModifiersText = Get-Content -LiteralPath $lootModifiersPath -Raw
$lootModifierRuntimeText = Get-Content -LiteralPath $lootModifierRuntimePath -Raw
$lootModifierAllText = $lootModifiersText + "`n" + $lootModifierRuntimeText
foreach ($token in @(
    'BrowserLootModifierCommand',
    'FLootStorageItem',
    'FLootCorpseItem',
    'FImplantDropChance',
    'GetAdditionalImplantDropChance',
    'GetImplantGainChance',
    'GetManualMarauderExpectedBonus',
    'case 1: return 0.3',
    'case 2: return 0.6',
    'case 3: return 0.9',
    'case 4: return 1.2',
    'FilterActiveLootContainerSources',
    'GetEnemyLootResultLabelWithModifiers'
)) {
    if ($lootModifierAllText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Loot modifier contract token missing: $token" }
}
foreach ($token in @(
    'AppendLootModifierControlLines(lootModifiers)',
    'FormatLootContainerRolls(',
    'loot.note.container_context_chance',
    'AppendLootEnemySections(rawEnemies, lootModifiers, ru, ref any)'
)) {
    if ($lootPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Loot modifier presentation token missing: $token" }
}
$browserUiText = Get-Content -LiteralPath $browserUiPath -Raw
foreach ($token in @('BrowserActionKind.LootModifier','HandleLootModifierAction(action.LootModifierCommand)')) {
    if ($browserUiText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Loot modifier action-routing token missing: $token" }
}
if ($lootModifierAllText.IndexOf('GetAdditionalCorpseDropBonus(',[StringComparison]::Ordinal) -ge 0 -or
    $lootModifierAllText.IndexOf('RollExpectedCount(',[StringComparison]::Ordinal) -ge 0) {
    throw 'Loot modifier RNG regression: information UI must never call the vanilla corpse roll method or RollExpectedCount.'
}
if ($runtimeText.IndexOf('GetCurrentCorpseBonusExpectedRolls(',[StringComparison]::Ordinal) -ge 0) {
    throw 'Loot modifier architecture regression: legacy corpse-only live resolver must stay removed from Runtime.cs.'
}
if ($runtimeText.IndexOf('ResetLootModifierSessionState();',[StringComparison]::Ordinal) -lt 0) {
    throw 'Loot modifier lifecycle regression: manual simulation state must reset at the Main Menu session boundary.'
}
if ($browserUiText.IndexOf('if (_indexesBuilt || _inspectorOpen)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Menu idle performance regression: state-service resolver must stay gated outside active gameplay/browser sessions.'
}
if ($lootModifiersText.IndexOf('NoteLootModifierProbeFailure(',[StringComparison]::Ordinal) -ge 0) {
    throw 'Loot modifier ownership regression: reflection hardening must live in ModMain.LootModifierRuntime.cs.'
}

# corpse-bonus split + render/runtime performance gates.
$lootEnemyPresentationText = Get-Content -LiteralPath $lootEnemyPresentationPath -Raw
foreach ($token in @(
    'AppendLootEnemySections',
    'LootEnemyRegularPresentationBuffer',
    'LootEnemyCorpseBonusPresentationBuffer',
    'ui.bonus_corpse_loot',
    'ui.per_roll',
    'ui.final_chance',
    'FormatCorpseBonusRollDistribution(corpseExpected)'
)) {
    if ($lootEnemyPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Loot corpse split contract token missing: $token" }
}
foreach ($token in @(
    'EnsureLootModifierRuntimeContracts',
    '_lootPerkSumMethod',
    '_lootImplantChanceMethod',
    '_lootImplantGainChanceMethod',
    '_lootImplantBaseProgression',
    'NoteLootModifierProbeFailure'
)) {
    if ($lootModifierRuntimeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Loot modifier runtime cache contract token missing: $token" }
}
foreach ($token in @('new List<LootEnemySource>(sources.Count)','FilterActiveLootEnemySources(')) {
    if ($lootModifiersText.IndexOf($token,[StringComparison]::Ordinal) -ge 0) { throw "Loot modifier allocation regression returned: $token" }
}
if ($lootGeneralSpawnText.IndexOf('LootGeneralSpawnManualContainerBuffer',[StringComparison]::Ordinal) -lt 0 -or
    $lootGeneralSpawnText.IndexOf('LootGeneralSpawnAdditionalContainerBuffer',[StringComparison]::Ordinal) -lt 0) {
    throw 'Loot general-spawn render-buffer optimization contract missing.'
}
if ($lootPresentationText.IndexOf('AppendLootEnemySections(rawEnemies, lootModifiers, ru, ref any)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Loot enemy presentation delegation contract missing.'
}

# performance hardening gates.
$advancedSearchPath = Join-Path $sourceDir 'ModMain.BrowserAdvancedSearch.cs'
$advancedSearchText = Get-Content -LiteralPath $advancedSearchPath -Raw
foreach ($token in @('_browserSearchLastResultRevision','_browserSearchIndexRevision','_browserSearchLastResultLanguage')) {
    if ($browserStateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Search cache revision contract missing: $token" }
}
foreach ($token in @('PlainTokens','PlainJoined','PerformanceBudgetExceeded(started, frameBudgetMs)','_browserSearchLastResultRevision == _browserSearchIndexRevision')) {
    if ($advancedSearchText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Search performance contract token missing: $token" }
}
if ($advancedSearchText.IndexOf('string.Join(" ", query.PlainTerms.ToArray())',[StringComparison]::Ordinal) -ge 0) {
    throw 'Search allocation regression: PlainTerms.ToArray/string.Join returned to the per-item match loop.'
}
if ($browserUiText.IndexOf('(_browserSearchDropdown == null || !_browserSearchDropdown.activeSelf)',[StringComparison]::Ordinal) -ge 0) {
    throw 'Search P0 regression: dropdown visibility must not trigger a full query rescan every frame.'
}
if ($runtimeText.IndexOf('InputControllerModalActionPrefix(object[] __args',[StringComparison]::Ordinal) -ge 0 -or
    $runtimeText.IndexOf('InputControllerModalActionPrefix(ref bool __result)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Input hot-path regression: unused Harmony __args allocation returned.'
}
foreach ($token in @('FinalizeAmmoWarmupWeapon','_ammoWarmupPhase','PerformanceBudgetExceeded(started, frameBudgetMs)','AmmoFinalizeCompatibleBuffer')) {
    if ($ammoText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Ammo incremental-finalize contract token missing: $token" }
}
if ($ammoText.IndexOf('FinalizeAmmoWarmup();',[StringComparison]::Ordinal) -ge 0) {
    throw 'Ammo performance regression: monolithic FinalizeAmmoWarmup returned.'
}
foreach ($token in @('TickLootAmputationIndexSlice','LootAmputationWarmupSlots','LootWarmupFrameTimer')) {
    if ($lootIndexesText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Loot incremental-amputation contract token missing: $token" }
}
foreach ($token in @('TickLootGeneralSpawnIndexSlice','LootGeneralSpawnContainersByClassWork','_lootGeneralSpawnBuildStage')) {
    if ($lootGeneralSpawnText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Loot incremental-general-spawn contract token missing: $token" }
}
if ($lootIndexesText.IndexOf('BuildLootAmputationIndex();',[StringComparison]::Ordinal) -ge 0 -or
    $lootIndexesText.IndexOf('BuildLootGeneralSpawnIndex();',[StringComparison]::Ordinal) -ge 0) {
    throw 'Loot performance regression: monolithic final phases returned.'
}
# the legacy generic container-count probe was unreachable and existed
# only because an old performance gate required its implementation tokens. Keep the
# actually-live DataAccess invariants above, and make resurrection of that zombie path
# a build failure instead of preserving dead code.
foreach ($retiredToken in @(
    'ContainerHasItem',
    'TryGetContainerItemCountFast',
    'TryGetContainerItemCountDeep',
    'TryFindContainerItemDeep',
    'ExtractMatchedContainerCount',
    'GetCachedContainerCountMethods',
    'ContainerCountMethodsByType',
    'ContainerCountInvokeArgs',
    'ContainerDeepSearchVisited')) {
    if ($dataAccessText.IndexOf($retiredToken,[StringComparison]::Ordinal) -ge 0) {
        throw "Dead-code hygiene regression: retired container-count probe returned: $retiredToken"
    }
}
if ($dataAccessText.IndexOf('ItemIdNestedMemberNames',[StringComparison]::Ordinal) -lt 0 -or
    $dataAccessText.IndexOf('GetItemIdDeep',[StringComparison]::Ordinal) -lt 0) {
    throw 'Data-access live item-id resolver contract missing.'
}
if ($coreIndexesText.IndexOf('if (_indexesBuilt && KnownItemIds.Count > 0)',[StringComparison]::Ordinal) -lt 0) {
    throw 'F2 readiness performance regression: healthy fast path is missing.'
}
if ($disassemblyText.IndexOf('PerformanceBudgetExceeded(started, frameBudgetMs)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Disassembly performance regression: count-only warmup budget returned.'
}
if ($lootContainerIconsText.IndexOf('directRecord=true',[StringComparison]::Ordinal) -lt 0 -or
    $lootContainerIconsText.IndexOf('EnsureLootContainerIconsResolved();',[StringComparison]::Ordinal) -lt 0) {
    throw 'Container-icon fallback-order contract missing.'
}
$directVisualIndex = $lootContainerIconsText.IndexOf('TryResolveLootContainerVisual(', $lootContainerIconsText.IndexOf('TryResolveLootContainerSmallIcon'), [StringComparison]::Ordinal)
$globalCatalogIndex = $lootContainerIconsText.IndexOf('EnsureLootContainerIconsResolved();', $lootContainerIconsText.IndexOf('TryResolveLootContainerSmallIcon'), [StringComparison]::Ordinal)
if ($directVisualIndex -lt 0 -or $globalCatalogIndex -lt 0 -or $directVisualIndex -gt $globalCatalogIndex) {
    throw 'Container-icon performance regression: global SpriteRenderer catalog must remain a fallback after direct-record resolution.'
}
foreach ($token in @('ShouldWriteAutomaticDiagnostics','ModderMode || string.Equals','"DEGRADED"')) {
    if ($hardeningText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Diagnostics gating contract token missing: $token" }
}
foreach ($token in @('EnsureStateServiceTypesResolved','_stateServiceTypesResolved','_stateResolveAttempts <= 6','_stateResolveCooldown = 300')) {
    if ($runtimeServicesText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Runtime-service performance contract token missing: $token" }
}

# production runtime cleanup gates.
$starmapText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Starmap.cs') -Raw
$starmapUiResolutionText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.StarmapUiResolution.cs') -Raw
foreach ($forbiddenDevToken in @('StarmapRuntimeAuditEnabled','StarmapAuditSnapshot','InstallStarmapRuntimeAuditPatches','WriteRussianNameResolverAuditSnapshot','WriteRussianNamesAuditSnapshot','QII_RussianNameResolverAudit_Runtime','QII_RussianNamesAudit_Runtime')) {
    if ($sourceText.IndexOf($forbiddenDevToken,[StringComparison]::Ordinal) -ge 0) { throw "Runtime cleanup regression: DEV token present in release source: $forbiddenDevToken" }
}
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.StarmapAudit.cs')) { throw 'Runtime cleanup regression: DEV StarmapAudit.cs returned.' }
foreach ($token in @('if (!_indexesBuilt && !_inspectorOpen','StarmapSourceViewVisualStates.Count == 0')) {
    if ($browserUiText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Main-menu idle fast-path token missing: $token" }
}
$iconsCleanupText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Icons.cs') -Raw
if ($iconsCleanupText.IndexOf('if (!ModderMode || root == null || _iconMissingAuditCount >= 12) return;',[StringComparison]::Ordinal) -lt 0) {
    throw 'Runtime cleanup regression: deep missing-icon audit must remain ModderMode-only.'
}
if ($lootContainerIconsText.IndexOf('if (ModderMode)',[StringComparison]::Ordinal) -lt 0 -or
    $lootContainerIconsText.IndexOf('BuildGenericContainerNeighborhoodAudit',[StringComparison]::Ordinal) -lt 0) {
    throw 'Runtime cleanup regression: container diagnostics gating contract missing.'
}
foreach ($token in @('MarketRenderStationBatch = 10','MarketRenderNewEntriesBatch = 3','_marketEntriesAtLastRender')) {
    if ($tradeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade render-throttle cleanup token missing: $token" }
}
foreach ($token in @('PrepareTradePresentationEntries();','public double? TravelHours','GetTradeTravelTimeSafe(string destinationSpaceObjectId, out double? travelHours)','travelHours = 0d','entry.TravelHours = travelHours')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade travel-sort contract token missing: $token" }
}

foreach ($token in @('AttachBrowserItemIconNavigation','AttachBrowserItemTextNavigation','HandleBrowserItemIconClick','ShowBrowserItemNavigationHint','ui.lmb_open_item','navigationTarget = false','NavigateBrowserToItem(targetItemId, false')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Contextual item-navigation contract token missing: $token" }
}
# Row hit-target ownership moved with RenderBrowserRowsOnly in .
if ($browserRowRenderCombinedText.IndexOf('SetBrowserRaycastTargetIfChanged(left, line != null && line.LeftContentKind == BrowserLeftContentKind.Item && IsKnownItemId(line.Left));',[StringComparison]::Ordinal) -lt 0) {
    throw 'Contextual item-navigation regression: item-name hit target is missing from BrowserRowRenderer.'
}
if ($weaponModeScatterText.IndexOf('if (ModderMode)',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModeScatterText.IndexOf('WeaponModeScatterLoggedKeys',[StringComparison]::Ordinal) -lt 0) {
    throw 'Runtime cleanup regression: weapon-mode scatter diagnostics must remain ModderMode-only.'
}
if ($hardeningText.IndexOf('HasLocalizationHealthProblems',[StringComparison]::Ordinal) -lt 0 -or
    $hardeningText.IndexOf('WriteLocalizationHealthReportSafe(bool force)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Healthy localization report I/O cleanup contract missing.'
}
$compatibilityText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Compatibility.cs') -Raw
if ($compatibilityText.IndexOf('IsHealthyVerifiedCompatibilityState',[StringComparison]::Ordinal) -lt 0 -or
    $compatibilityText.IndexOf('Path.Combine(Application.dataPath, "Managed", "Assembly-CSharp.dll")',[StringComparison]::Ordinal) -lt 0 -or
    $compatibilityText.IndexOf('ComputeFileSha256Safe(managedPath)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Healthy compatibility/fingerprint contract missing.'
}
# Player-facing development/version markers are rejected centrally by Assert-No-PlayerFacingDevText.

# stable release-polish gates.
# Current Quasimorph 1.0.2.575s.d02a8d8 was runtime/IL-audited against this exact Assembly-CSharp hash.
if ($sourceText.IndexOf('EFF608C5118735359CD07FEAD8A8219E1CFB557E3A5A57517DB4428F04834B8B',[StringComparison]::Ordinal) -lt 0 -or
    $hardeningText.IndexOf('1.0.2.575s.d02a8d8',[StringComparison]::Ordinal) -lt 0) { throw 'Release regression: current verified game/hash identity is missing.' }
if ($sourceText.IndexOf('FE68E4355D4ED9CBAB7F8B1BA7717DBC1CC3FD749D0D11A644A9A3DB5EAB478F',[StringComparison]::Ordinal) -lt 0) { throw 'Feature compatibility regression: audited 1.0.3 assembly identity is missing.' }
if ($lootGeneralSpawnText.IndexOf('_lootGeneralSpawnPairCount = CountLootGeneralSpawnPairs();',[StringComparison]::Ordinal) -lt 0 -or
    $lootGeneralSpawnText.IndexOf('private static int CountLootGeneralSpawnPairs()',[StringComparison]::Ordinal) -lt 0) {
    throw 'Release regression: general-spawn diagnostics must recount the final authoritative dictionary.'
}
if ($lootModifiersText.IndexOf('if (ModderMode)',[StringComparison]::Ordinal) -lt 0 -or
    $lootModifiersText.IndexOf('[ItemIntelligence][LootModifiers][Perf]',[StringComparison]::Ordinal) -lt 0) {
    throw 'Release regression: Loot modifier performance diagnostics must remain ModderMode-gated.'
}

# Modder Mode / advanced-search semantic gates.
$advancedSearchPath = Join-Path $sourceDir 'ModMain.BrowserAdvancedSearch.cs'
$advancedSearchText = Get-Content -LiteralPath $advancedSearchPath -Raw
$modderModePath = Join-Path $sourceDir 'ModMain.ModderMode.cs'
$modderModeText = Get-Content -LiteralPath $modderModePath -Raw
$configText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Configuration.cs') -Raw
# Verify the parser implementation itself rather than UI/example literals such as
# 'tech:' or 'cat:'. Those prefixes are assembled by parsing field/value around ':'
# and therefore do not need to exist as contiguous strings in the C# source.
foreach ($token in @(
    'ParseBrowserAdvancedSearchQuery',
    'IndexBrowserAdvancedSearchMetadata',
    'field == "tech"',
    'field == "cat" || field == "category"',
    'field == "type"',
    'field == "rel" || field == "relation"',
    'TryParseBrowserTechConstraint',
    'ResolveBrowserSearchCategory',
    'ResetBrowserAdvancedSearchIndexState'
)) {
    if ($advancedSearchText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Advanced-search contract token missing: $token" }
}
foreach ($token in @('ModderMode','AppendBrowserModderOverview','BrowserLine.CopyValue','ui.modder_record_type','ui.modder_firemode_ids')) {
    if (($modderModeText + $configText + $browserPresentationText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Modder-mode contract token missing: $token" }
}
if ($advancedSearchText.IndexOf('StartLootSourcesWarmup',[StringComparison]::Ordinal) -ge 0 -or
    $advancedSearchText.IndexOf('BuildRelevantItemGraph',[StringComparison]::Ordinal) -ge 0) {
    throw 'Advanced-search performance regression: search must reuse existing time-sliced catalog metadata and must not start Loot or graph scans itself.'
}

# shared TechLevel resolver gates.
$itemMetadataPath = Join-Path $sourceDir 'ModMain.ItemMetadata.cs'
if (-not (Test-Path -LiteralPath $itemMetadataPath -PathType Leaf)) { throw 'Shared item metadata resolver module is missing.' }
$itemMetadataText = Get-Content -LiteralPath $itemMetadataPath -Raw
foreach ($token in @(
    'ResolveCanonicalItemMetadataRecord',
    'TryGetExactItemTechLevel',
    'ObserveCanonicalItemMetadataNode',
    'ExactItemTechLevelsById',
    'UnresolvedItemMetadataIds',
    'FindCachedMember(type, "Categories", false)',
    'FindCachedMember(type, "TechLevel", false)'
)) {
    if ($itemMetadataText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "TechLevel resolver contract token missing: $token" }
}
if ($advancedSearchText.IndexOf('GetIntMember(record, "TechLevel"',[StringComparison]::Ordinal) -ge 0 -or
    $modderModeText.IndexOf('GetIntMember(record, "TechLevel"',[StringComparison]::Ordinal) -ge 0) {
    throw 'TechLevel resolver regression: browser features must not read TechLevel from the root wrapper record.'
}
if ($coreIndexesText.IndexOf('ResetItemMetadataResolverState();',[StringComparison]::Ordinal) -lt 0) {
    throw 'TechLevel resolver lifecycle regression: cache must reset with core indexes.'
}

# exact nominal weapon damage/AP gates.
$weaponModeDamagePerApPath = Join-Path $sourceDir 'ModMain.WeaponModeDamagePerAP.cs'
$weaponModeDamagePerApText = Get-Content -LiteralPath $weaponModeDamagePerApPath -Raw
foreach ($token in @(
    'TryCalculateWeaponModeDamagePerAp',
    'weapon.DefaultAmmoId',
    'ammo.DamageMult',
    'ammo.BulletCastsPerShot',
    'stats.WeaponCastsCount',
    'TryRoundAndScaleDamage(baseMin * perFragmentMult, fragments, casts, out totalMin)',
    'ResetWeaponModeDamagePerApCache',
    '[ItemIntelligence][WeaponModeDamageAP]'
)) {
    if ($weaponModeDamagePerApText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Weapon damage/AP contract token missing: $token" }
}
if ($weaponModePresentationText.IndexOf('ui.mode_damage_per_ap_default',[StringComparison]::Ordinal) -lt 0) {
    throw 'Weapon damage/AP presentation row is missing.'
}
if ($weaponModeDamagePerApText.IndexOf('OverallRangeDamageMult',[StringComparison]::Ordinal) -ge 0 -or
    $weaponModeDamagePerApText.IndexOf('GetTotalPerkRangeDamageBonus',[StringComparison]::Ordinal) -ge 0) {
    throw 'Weapon damage/AP neutral-stat regression: current character damage modifiers must not enter the default-ammo metric.'
}

# user-facing polish gates.
$lootPresentationText = Get-Content -LiteralPath $lootPresentationPath -Raw
if ($browserPresentationText.IndexOf('BrowserLine.FullNote(Ui("ui.no_related_magnum_research_was_found"))',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: Magnum empty-state must use the full-width note row.'
}
if ($lootPresentationText.IndexOf('AddWrappedBrowserNote(key, 110, 120);',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: Loot helper-note wrap width is not the widened full-row contract.'
}
$ruLocalizationText = Get-Content -LiteralPath (Join-Path $content 'Localization\ru.lang') -Raw
$enLocalizationText = Get-Content -LiteralPath (Join-Path $content 'Localization\en.lang') -Raw
foreach ($forbiddenVisibleToken in @('AdditItemClasses','RollExpectedCount')) {
    if ($ruLocalizationText.IndexOf($forbiddenVisibleToken,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $enLocalizationText.IndexOf($forbiddenVisibleToken,[StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "current user-facing text regression: internal token remains visible: $forbiddenVisibleToken"
    }
}
foreach ($retainedToken in @('ui.item_id','ui.modder_search_syntax','ui.faction_technology','ui.barter_give_this_item','ui.barter_receive_this_item')) {
    if (($ruLocalizationText + $enLocalizationText).IndexOf($retainedToken,[StringComparison]::Ordinal) -lt 0) {
        throw "current scope regression: intentionally retained UI contract missing: $retainedToken"
    }
}

# exact direct-disassembly chance gates.
$disassemblyChancePath = Join-Path $sourceDir 'ModMain.DisassemblyChance.cs'
if (-not (Test-Path -LiteralPath $disassemblyChancePath)) { throw 'current exact disassembly chance owner is missing.' }
$disassemblyChanceText = Get-Content -LiteralPath $disassemblyChancePath -Raw
foreach ($token in @('DeathGiftId','GetDirectDisassemblyChancePercent','IsRandomDirectDisassemblyItem','return 100f')) {
    if ($disassemblyChanceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current exact disassembly chance token missing: $token" }
}
if ($disassemblyText.IndexOf('GetDirectDisassemblyChancePercent(itemId)',[StringComparison]::Ordinal) -lt 0 -or
    $disassemblyText.IndexOf('IsRandomDirectDisassemblyItem(itemId)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current exact disassembly chance is not connected to canonical forward presentation data.'
}
if ($sourceText.IndexOf('LogTest5DisassemblyExactAudit();',[StringComparison]::Ordinal) -ge 0 -or
    (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.Test5DisassemblyExactAudit.cs'))) {
    throw 'current regression: accepted current runtime probe must be removed.'
}

# player-facing Loot/Trade clarity gates.
$tradeText = (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Trade.cs') -Raw) + (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.TradePresentation.cs') -Raw) + (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.TradeBatchPricing103.cs') -Raw)
if ($enLocalizationText.IndexOf("ui.context`tLOCATION TYPE",[StringComparison]::Ordinal) -lt 0 -or
    $ruLocalizationText.IndexOf("ui.context`tТИП ЛОКАЦИИ",[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: Loot context header must be player-facing LOCATION TYPE / ТИП ЛОКАЦИИ.'
}
foreach ($token in @('ui.trade_repricing_note','BrowserLine.FullSection')) {
    if ($tradeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current trade clarity token missing: $token" }
}
# Full-width text fitting is centralized in BrowserRowLayout since .
if ($browserRowRenderCombinedText.IndexOf('ApplyBrowserFullWidthRow(left, right, leftRt, 17f, 12.5f, true);',[StringComparison]::Ordinal) -lt 0 -or
    $browserRowLayoutText.IndexOf('SetBrowserAutoSizingIfChanged(left, autoSize);',[StringComparison]::Ordinal) -lt 0 -or
    $browserRowLayoutText.IndexOf('SetBrowserFontSizeMinIfChanged(left, minFontSize);',[StringComparison]::Ordinal) -lt 0 -or
    $browserRowLayoutText.IndexOf('SetBrowserFontSizeMaxIfChanged(left, fontSize);',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: full-width localized section no longer uses fit-safe centralized TMP auto-sizing.'
}

# safe player-facing polish gates.
$specialVisualPath = Join-Path $sourceDir 'ModMain.LootContainerSpecialVisuals.cs'
if (-not (Test-Path -LiteralPath $specialVisualPath)) { throw 'current special visual owner is missing.' }
$specialVisualText = Get-Content -LiteralPath $specialVisualPath -Raw
foreach ($token in @('AztecAltar','exact-altar-renderer-not-loaded','exactSemantic=true')) {
    if ($specialVisualText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current AztecAltar safe visual token missing: $token" }
}
foreach ($token in @("ui.qty_rolls`tКОЛ./БРОСКИ", "ui.rolls_2`t броск.", "ui.per_roll`tЗА БРОСОК")) {
    if ($ruLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current RU Loot terminology token missing: $token" }
}
# Match standalone Russian transliteration of gameplay "roll" (including inflections),
# but do not false-positive on unrelated words such as "скролл"/"прокрутка" hints.
if ([regex]::IsMatch($ruLocalizationText, '(?i)(?<![\p{L}])ролл[а-яё]*(?![\p{L}])')) {
    throw 'current regression: player-facing Russian Loot terminology still contains roll transliteration.'
}
if ($browserRowRenderCombinedText.IndexOf('bool actionable = !line.Action.IsNone;',[StringComparison]::Ordinal) -lt 0 -or
    $browserRowRenderCombinedText.IndexOf('SetBrowserInteractableIfChanged(rowButton, actionable);',[StringComparison]::Ordinal) -lt 0 -or
    $browserRowRenderCombinedText.IndexOf('SetBrowserOutlineEnabledIfChanged(rowOutline, actionable);',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: clickable browser rows no longer share the same interactable/outline affordance in BrowserRowRenderer.'
}

# Loot clarity gates retained.
$tradeMissionPath = Join-Path $sourceDir 'ModMain.TradeMissionStatus.cs'
if (-not (Test-Path -LiteralPath $tradeMissionPath)) { throw 'Trade mission status owner is missing.' }
$tradeMissionText = Get-Content -LiteralPath $tradeMissionPath -Raw
if ($sourceText.IndexOf('LootMissionRow(string source, string type, string tech, bool eligible)',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('eligible ? "eligible" : "ineligible"',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: redundant mission-pool eligible/status payload still exists.'
}
# Keep Loot clarity contracts semantic rather than pinning the full prose.
# Player-facing wording may be polished without invalidating the build as long as
# the key remains present and the fail-closed meaning is preserved below.
foreach ($token in @("loot.note.mission_pools`t",'normal loot pool of these missions','container or on the floor')) {
    if ($enLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current EN Loot clarity semantic missing: $token" }
}
foreach ($token in @("loot.note.mission_pools`t",'обычном пуле добычи','контейнере или на полу')) {
    if ($ruLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current RU Loot clarity semantic missing: $token" }
}

# exact Trade mission countdown gates.
foreach ($token in @(
    'MGSC.Missions',
    'GetMember(missions, "Values")',
    'GetStringMember(mission, "StationId")',
    'GetTradeDateTimeMember(mission, "ExpireTime")',
    'MGSC.SpaceTime',
    'GetTradeDateTimeMember(spaceTime, "Time")',
    'ApplyTradeMissionState',
    'RefreshTradeMissionArrivalState',
    'GetTradeMissionDisplay',
    'ToLocalizedDaysAndHours',
    'typeof(TimeSpan)',
    'exactGate=Missions.Values/StationId')) {
    if ($tradeMissionText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current exact mission token missing: $token" }
}
foreach ($forbidden in @('TradeMissionStationIds','IsTradeMissionTerminal','ResolveTradeMissionStationId','terminalByTest9','LogTradeMissionExactAuditOnce')) {
    if ($tradeMissionText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "current stale heuristic/audit token remains: $forbidden" }
}
foreach ($token in @("ui.mission`tMISSION", "ui.yes`tYES")) {
    if ($enLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current EN localization token missing: $token" }
}
foreach ($token in @("ui.mission`tМИССИЯ", "ui.yes`tДА")) {
    if ($ruLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current RU localization token missing: $token" }
}
if ($tradeText.IndexOf('GetTradeMissionDisplay(entry)',[StringComparison]::Ordinal) -lt 0 -or
    $tradeText.IndexOf('entry.MissionArrivalState',[StringComparison]::Ordinal) -lt 0 -or
    $browserRowRenderCombinedText.IndexOf('line.RowKind == BrowserRowKind.TradeStationCard',[StringComparison]::Ordinal) -lt 0 -or
    $browserRowRenderCombinedText.IndexOf('GetTradeMissionColor(line.TradeArrivalState)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: exact mission countdown is not connected to pooled Trade rows in BrowserRowRenderer.'
}
$tradeMissionLines = (Get-Content -LiteralPath $tradeMissionPath).Count
if ($tradeMissionLines -gt 260) { throw "current TradeMissionStatus line budget exceeded: $tradeMissionLines/260" }
if ($sourceText.IndexOf('SellItems(',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('BuyItems(',[StringComparison]::Ordinal) -ge 0) {
    throw 'current read-only regression: mission countdown must not invoke trade mutation APIs.'
}

# Trade freshness / cleanup gates.
$tradeFreshnessText = Get-Content -LiteralPath $tradeFreshnessPath -Raw
$templateLocalizationText = Get-Content -LiteralPath (Join-Path $content 'Localization\TranslationTemplate.lang') -Raw
if ($tradeText.IndexOf('BuildLiveStationMarket',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: obsolete BuildLiveStationMarket dead implementation returned.'
}
foreach ($token in @(
    'StartMarketScan(string itemId, bool forceRefresh = false)',
    '!forceRefresh && string.Equals(_marketItemId, itemId',
    'TickTradeMissionCountdownUiRefresh();',
    'MarkTradeMissionCountdownUiRendered();')) {
    if ($tradeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Trade freshness token missing: $token" }
}
if ($browserUiText.IndexOf('StartMarketScan(_inspectorItemId, true);',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: explicit Trade-tab entry/re-click no longer forces a fresh market snapshot.'
}
foreach ($token in @(
    'TradeMissionUiCheckFrames = 300',
    'TradeMissionUiRefreshMinutes = 5d',
    'RefreshTradeMissionStatusSnapshot();',
    'TradeMissionsByStationId.ContainsKey(entry.StationId)',
    'missionChanged || timeChanged')) {
    if ($tradeFreshnessText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current mission live-refresh token missing: $token" }
}
if ($tradeFreshnessText.IndexOf('BuildLiveMarketEntry(',[StringComparison]::Ordinal) -ge 0 -or
    $tradeFreshnessText.IndexOf('GetRuntimeStations',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: cheap mission countdown refresh must not rescan the station market.'
}
foreach ($token in @('_tradeMissionsTypeChecked','_tradeSpaceTimeTypeChecked')) {
    if ($tradeMissionText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current type-lookup cache token missing: $token" }
}
if ($lootPresentationText.IndexOf('Ui("ui.tech")',[StringComparison]::Ordinal) -lt 0 -or
    $lootPresentationText.IndexOf('"TECH"',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: Mission Pools TECH header must use localized ui.tech, never a raw literal.'
}
foreach ($token in @('const int maxShown = 64;','int remaining = totalValid - shown;','Ui("ui.more_rows_format")')) {
    if ($lootPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Mission Pools truncation token missing: $token" }
}
if ($enLocalizationText.IndexOf("ui.more_rows_format`t+ {0} more ({1})",[StringComparison]::Ordinal) -lt 0 -or
    $templateLocalizationText.IndexOf("ui.more_rows_format`t+ {0} more ({1})",[StringComparison]::Ordinal) -lt 0) {
    throw 'current EN/template Mission Pools overflow localization missing.'
}
if ($ruLocalizationText.IndexOf("ui.more_rows_format`t+ ещё {0} ({1})",[StringComparison]::Ordinal) -lt 0) {
    throw 'current RU Mission Pools overflow localization missing.'
}

# Legacy Loot code-hygiene gates retained from the pre-1.7.40 hardening lineage.
if ($sourceText.IndexOf('"TECH"',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: raw "TECH" source literal returned; use Ui("ui.tech").'
}
if ($lootIndexesText.IndexOf('IndexLootMobClass',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: retired synchronous IndexLootMobClass dead path returned.'
}
foreach ($token in @(
    'cached != null)',
    'Do not negative-cache a temporarily unavailable vanilla table.',
    'LootGeneralSpawnContainersByItem[itemId] = result;')) {
    if ($lootGeneralSpawnText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current negative-cache token missing: $token" }
}
if ($lootGeneralSpawnText.IndexOf('cached != null && cached.Count > 0',[StringComparison]::Ordinal) -ge 0 -or
    $lootGeneralSpawnText.IndexOf('if (result.Count == 0) return null;',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: empty general-spawn results are no longer cached.'
}

# Loot unknown-semantics gates retained from the container-chance hardening lineage. Unknown roll count must never
# silently become a proven zero or a bonus-only total probability.
$lootFacadeText = Get-Content -LiteralPath $lootPath -Raw
foreach ($token in @('public readonly bool RollRangeResolved;','bool rollRangeResolved')) {
    if ($lootFacadeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Loot model token missing: $token" }
}
foreach ($token in @(
    'private static bool ResolveLootContainerRollRange(',
    'if (range == null) return false;',
    'if (!minResolved || !maxResolved) return false;',
    'resolvedContainerId, dropId, min, max, rollRangeResolved')) {
    if ($lootContainerProfilesText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current roll-range resolver token missing: $token" }
}
foreach ($token in @(
    '!source.RollRangeResolved || source.MaxRolls > 0',
    'return "? +" + FormatExpectedNumber(storageExpected, IsRussian());',
    'return "?";')) {
    if ($lootModifiersText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current unknown-roll presentation token missing: $token" }
}
# Manual-container rows expose a save-aware estimate only when Tech, pool weights
# and roll inputs are resolved; missing inputs remain visibly unknown.
$lootContainerSaveEstimateText = (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.LootContainerSaveEstimate.cs') -Raw) + (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.LootContainerChanceMath.cs') -Raw)
foreach ($token in @('IsContainerSaveEstimateContractVerified()','FormatLootContainerEffectiveChance(','GetLootContainerSaveEstimateSnapshot()','Ui("loot.column.save_estimate")','itemId, rawContainers, containerItemTech, ref any','AddWrappedLootNote("loot.note.unknown_container_rolls")')) {
    if (($lootPresentationText + $lootContainerSaveEstimateText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current container save-estimate/dedup token missing: $token" }
}
foreach ($token in @('GetTechContexts(source.BiomeId)','TryGetExactContainerItemTechLevel','TryGetExactContainerBonusEligibility','CorpseBonusAtLeastOnceChance','targetTech > contextTech','source.MinRolls','source.MaxRolls','return "—";')) {
    if ($lootContainerSaveEstimateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current container estimate fail-closed token missing: $token" }
}
if ($lootPresentationText.IndexOf('itemId, containers, containerItemTech, ref any',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: Other Containers dedup must use raw profile sources, not filtered presentation rows.'
}
if ($lootIndexesText.IndexOf('descriptor.RollRangeResolved',[StringComparison]::Ordinal) -lt 0 -or
    $lootIndexesText.IndexOf('!existing.RollRangeResolved && source.RollRangeResolved',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: roll-range resolution state is not preserved/preferred by the weighted reverse index.'
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    if ($locText.IndexOf('loot.note.unknown_container_rolls',[StringComparison]::Ordinal) -lt 0) {
        throw 'current unknown-roll explanatory localization missing.'
    }
}

# exact SELL TO STATIONS contract. Vanilla TradeWindow.Configure discovers
# consumer stations by live Station.ConsumableItems.ContainsKey(itemId).
# TradeSystem.IsValidItem is a broader transaction gate (tutorial/strategy fallback)
# and must never be used as a synonym for consumer membership.
foreach ($token in @(
    'TryEvaluateVanillaConsumerMembership',
    'IDictionary consumableItems = GetMember(typedStation, "ConsumableItems") as IDictionary;',
    'accepts = consumableItems.Contains(itemId);',
    'exactMembership=Station.ConsumableItems.ContainsKey(itemId)',
    'SELL TO STATIONS relations are omitted rather than guessed.',
    'bool sells = stock > 0;',
    'TryGetExactStationPrice(station, itemId, true, out price)')) {
    if ($tradeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current exact sell-contract token missing: $token" }
}
foreach ($forbidden in @(
    'TryEvaluateVanillaSellToStationGate',
    '_tradeIsValidItemMethod',
    'tradeType, "IsValidItem", new Type[] { typeof(Faction), typeof(Station), typeof(string) }',
    'exactGate=TradeSystem.IsValidItem(Faction,Station,string)',
    'GetMember(station, "AdditionalConsumableItems")',
    'GetMember(station, "ConsumeItemsRating")',
    'GetMember(station, "ItemsRequirement")',
    'if (!buys && priceRecord != null) buys = true;')) {
    if ($tradeText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "current retired sell heuristic returned: $forbidden" }
}
# Exact price presentation is feature-versioned: 1.0.3 mirrors TradeStationPanel, while the audited 1.0.2 path is retained separately.
foreach ($token in @('IsCurrent103TradeAssembly()','TryGetExactStationPanelPrice103','GetMember(preset, "BarterValue")','Mathf.RoundToInt(displayed)','"GetBuyPrice"','Dictionary<string, int> oneItem','{ itemId, 1 }','TryGetLegacyExactStationPrice102','string methodName = stationBuys ? "GetItemSellPrice" : "GetItemBuyPrice";')) {
    if ($runtimeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current exact trade-price token missing: $token" }
}

# Quasimorph 1.0.3 per-unit stock repricing is shown as one two-line station card:
# station + first→last marginal price, batch total, travel and mission stay readable.
foreach ($token in @('TryGetExactStationBatchPrice103','GetTradeBatchSampleQuantity','lastUnitPrice','"GetItemSellTradePoints"','"GetBuyPrice"','TradeStationCard103','FormatTradePriceRange','FormatTradeBuyBatchCard','FormatTradeSellBatchCard','UsePreviousTradeLayout','AddTradeStationTable103','Ui(previousLayout ? "ui.trade_previous_note" : "ui.trade_repricing_note")')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade 1.0.3 card-price contract token missing: $token" }
}
$browserModelsTradeText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.BrowserModels.cs'))
if ($browserModelsTradeText.IndexOf('BrowserAction.OpenStarmap(spaceObjectId)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Trade navigation regression: station cards must keep OpenStarmap(spaceObjectId).'
}
if ($sourceText.IndexOf('ui.trade_batch_note_buy',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('ui.trade_batch_note_sell',[StringComparison]::Ordinal) -ge 0) {
    throw 'Trade 1.0.3 UX regression: retired dense NEXT/BATCH explanation notes returned.'
}

if ($tradeText.IndexOf('SellItems(',[StringComparison]::Ordinal) -ge 0 -or
    $tradeText.IndexOf('BuyItems(',[StringComparison]::Ordinal) -ge 0) {
    throw 'current read-only regression: exact relation resolver must not invoke trade mutation APIs.'
}

# semantic truthfulness gates. Unknown values must stay unknown,
# and station-economy recipe relations must not masquerade as direct player barter.
$factionsText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Factions.cs') -Raw
$disassemblyText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Disassembly.cs') -Raw
$browserPresentationText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserPresentation.cs') -Raw
foreach ($token in @(
    'FormatFactionRewardPercent(view)',
    'float.IsNaN(view.RewardPercent)',
    'float.IsInfinity(view.RewardPercent)',
    'float value = Mathf.Clamp(view.RewardPercent, 0f, 100f);',
    'return value.ToString("0.###", CultureInfo.InvariantCulture) + "%";',
    '// 2 = unresolved. Keep it distinct from a proven neutral relation (0).')) {
    if ($factionsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current faction truthfulness token missing: $token" }
}
if ($factionsText.IndexOf('view.RewardPercent.ToString(CultureInfo.InvariantCulture) + "%"',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: faction locked/unknown chance can again render as numeric percent.'
}
foreach ($token in @('MissionTechByStationType','EnabledFactionTech','mission.IsStoryMission','Math.Max(mission.MinTechLevel, victim.CurrentTechLevel)','mission.ExpireTime <= now.Value','neutralTechContext=true','missionPointBudget=excluded.')) {
    if ($lootContainerSaveEstimateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Loot save-aware neutral estimate token missing: $token" }
}
if ($browserPresentationText.IndexOf('if (ShowMagnumUses) { EnsureBrowserFactionColumnsUi(); BuildBrowserMagnum(itemId); }',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: Magnum must materialize the shared quantity/state columns before its first render.'
}
foreach ($token in @('100%" + Ui("ui.roll")','chance + Ui("ui.roll")')) {
    if ($disassemblyText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current disassembly per-roll token missing: $token" }
}
if ($tradeText.IndexOf('object storage = GetMember(station, "InternalStorage");',[StringComparison]::Ordinal) -lt 0) {
    throw 'current Trade live-stock truthfulness token missing.'
}
foreach ($forbidden in @(
    'ExtractDirectionalTradeItems(',
    'GetMember(station, "Storage")',
    'GetMember(station, "ActiveStorage")',
    'GetMember(station, "StationStorage")',
    'FindItemEntry(GetMember(station, "ItemsPrices")')) {
    if ($sourceText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "current retired semantic fallback returned: $forbidden" }
}
if ($runtimeText.IndexOf('Defensive fallback for future versions whose signature may change.',[StringComparison]::Ordinal) -ge 0 -or
    $runtimeText.IndexOf('new string[] { "GetItemSellPrice", "GetSellPrice", "GetPrice" }',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: heuristic trade-price fallback returned.'
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    foreach ($token in @('ui.station_production_produced_from','ui.station_production_used_to_produce','ui.station_production_note','loot.column.save_estimate','loot.note.container_context_chance','loot.note.container_modifier_unavailable','loot.note.container_save_unavailable')) {
        if ($locText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current localization token missing: $token" }
    }
}
if ($enLocalizationText.IndexOf("loot.note.container_context_chance`t≈ = chance to get the item at least once.",[StringComparison]::Ordinal) -lt 0 -or
    $ruLocalizationText.IndexOf("loot.note.container_context_chance`t≈ = шанс получить предмет хотя бы раз.",[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: save-aware container chance disclosure missing.'
}

# release UX recovery contracts. MCM/faction fixes remain; its
# auto-expanded Space Loot modifier rows are intentionally retired.
$configurationText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Configuration.cs') -Raw
$lootModifiersText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.LootModifiers.cs') -Raw
$weaponModeDamagePerApText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.WeaponModeDamagePerAP.cs') -Raw
if ($configurationText.IndexOf('AddMcmBool(add, list, configValueType, "EnableItemIntelligence"',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: duplicate visible Item Intelligence master switch returned to MCM.'
}
foreach ($token in @(
    'AddMcmBool(add, list, configValueType, "InspectorEnabled", InspectorEnabled, Ui("mcm.header.inspector"), Ui("ui.enable_item_intelligence")',
    'if (!EnableItemIntelligence)',
    'InspectorEnabled = false;',
    'EnableItemIntelligence = true;')) {
    if ($configurationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current preserved MCM consolidation token missing: $token" }
}
foreach ($token in @(
    'ResolveFactionAvailabilityForCurrentSave',
    '"IsEnabledFaction", new Type[] { typeof(Faction) }',
    'exactGate=Factions.IsEnabledFaction(Faction)',
    'if (availability == 0)',
    'ui.no_active_faction_reward_in_current_save')) {
    if ($factionsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current preserved faction current-save token missing: $token" }
}
foreach ($forbidden in @(
    '_lootModifierAutoManualForContext',
    '_lootModifierAutoManualLogged',
    'CURRENT character unavailable; exposing MANUAL projection controls for this context.',
    'manualPresentation')) {
    if ($lootModifiersText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "current regression: Space Loot auto-expansion returned: $forbidden" }
}
foreach ($token in @(
    'return BuildManualLootModifierSnapshot();',
    '_lootModifierUseManual ? Ui("ui.loot_modifiers_manual") : Ui("ui.loot_modifiers_current")',
    'if (!_lootModifierUseManual) return;')) {
    if ($lootModifiersText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Loot dropdown recovery token missing: $token" }
}
foreach ($token in @(
    'if (weapon.IsMelee)',
    'weapon.GetMeleeDamageFromCreature',
    'TryRoundAndScaleDamage(baseMin * modeMult, 1, casts, out meleeTotalMin)',
    'TryRoundAndScaleDamage(baseMax * modeMult, 1, casts, out meleeTotalMax)',
    'meleeFormula=Round(WeaponRecord.Damage*FireMode.DamageMult)*WeaponCastsCount')) {
    if ($weaponModeDamagePerApText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current melee Damage/AP token missing: $token" }
}
if ($weaponModeDamagePerApText.IndexOf('string ammoId = weapon.DefaultAmmoId ?? string.Empty;',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModeDamagePerApText.IndexOf('ammo.BulletCastsPerShot',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModeDamagePerApText.IndexOf('TryRoundAndScaleDamage(baseMin * perFragmentMult, fragments, casts, out totalMin)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current regression: existing ranged Damage/AP contract changed unexpectedly.'
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    if ($locText.IndexOf('ui.no_active_faction_reward_in_current_save',[StringComparison]::Ordinal) -lt 0) {
        throw 'current preserved current-save faction localization missing.'
    }
}

# Current-build chance/UI safety: unresolved faction-panel math stays dash, generic story scripts stay conditional.
foreach ($token in @('float percent = float.NaN;','Percentage intentionally remains NaN until current-build panel math is')) {
    if ($factionsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current faction reward fail-closed presentation missing: $token" }
}
$lootSpecialText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootSpecialSources.cs'))
foreach ($token in @('Ui("loot.special.story_source")','"StoryScript", "", false')) {
    if ($lootSpecialText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current special-source semantic cleanup missing: $token" }
}

foreach ($token in @('manualProjectionVerified = IsLootManualProjectionContractVerified();','if (_lootModifierUseManual || manualProjectionVerified)','!IsLootManualProjectionContractVerified()','GetStaticMember(typeof(Data), "Perks")','"FLootStorageItem"','"FLootCorpseItem"','"FImplantDropChance"','marauderTenths.Contains(12)')) {
    if (($lootModifiersText + $sourceText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current feature-owned manual Loot projection gate missing: $token" }
}

# Loot special-source presentation: pooled text must be overwritten and compact columns retained.
$specialRendererText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.BrowserRowRendererParts.cs'))
foreach ($token in @('SetBrowserTextIfChanged(left, NormalizeModUiText(ctx.LeftText));','rewardPool ? 184f : 206f','ConfigureLootColumn(kind, 198f, 118f','ConfigureLootColumn(condition, 320f, 294f','ConfigureLootColumn(result, 618f, 70f','ConfigureLootColumn(kind, 220f, 126f','ConfigureLootColumn(condition, 350f, 198f','ConfigureLootColumn(result, 554f, 134f')) {
    if ($specialRendererText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "special-source pooled-row/layout regression: $token" }
}
foreach ($contract in @(
    @{ Text=$ruLocalizationText; Token="loot.special.other_section`tОСОБЫЕ СПОСОБЫ ПОЛУЧЕНИЯ" }, @{ Text=$ruLocalizationText; Token="loot.special.reward_section`tНАГРАДЫ ФРАКЦИЙ" }, @{ Text=$ruLocalizationText; Token="loot.special.start_section`tСТАРТ НОВОЙ ИГРЫ" },
    @{ Text=$enLocalizationText; Token="loot.special.other_section`tSPECIAL ACQUISITION" }, @{ Text=$enLocalizationText; Token="loot.special.reward_section`tFACTION REWARDS" }, @{ Text=$enLocalizationText; Token="loot.special.start_section`tNEW GAME START" })) {
    if ($contract.Text.IndexOf($contract.Token,[StringComparison]::Ordinal) -lt 0) { throw "Loot section clarity contract missing: $($contract.Token)" }
}
foreach ($token in @('loot.special.header_faction','loot.special.header_where','loot.special.header_requirement','loot.special.result_in_pool','"reward_pool"','return "eligible";')) {
    if (($lootSpecialText + $ruLocalizationText + $enLocalizationText + $templateLocalizationText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "special-source contextual header missing: $token" }
}
foreach ($token in @('StartingLoadoutGroup','loot.special.fixed_loadout_count','loot.note.special_sources','loot.special.story_missions_count','loot.special.story_mission')) { if (($lootSpecialText + $ruLocalizationText + $enLocalizationText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "special/start clarity token missing: $token" } }
if ($lootSpecialText.IndexOf('IsRussian() ? "Высокий"',[StringComparison]::Ordinal) -ge 0 -or $lootSpecialText.IndexOf('IsRussian() ? "Нормальный"',[StringComparison]::Ordinal) -ge 0) { throw 'unproven High/Normal preset translation returned.' }
