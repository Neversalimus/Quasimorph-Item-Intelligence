# ============================================================================
# PERFORMANCE / LAZY UI / RENDERER CONTRACTS
# Current invariants only. Historical test/build provenance intentionally omitted.
# ============================================================================

# close ItemDropSystem.Randomize exactness for Baron and generic
# GenerateEquipment additional-item projections. This gate is intentionally source-owned:
# do not satisfy it by reintroducing the old PactRecord.BramfaturaId reconstruction.
$itemDropMathPath = Join-Path $sourceDir 'ModMain.ItemDropRandomizeMath.cs'
if (-not (Test-Path -LiteralPath $itemDropMathPath -PathType Leaf)) { throw 'current ItemDrop randomize math owner missing.' }
$itemDropMathText = [IO.File]::ReadAllText($itemDropMathPath)
foreach ($token in @('ExtractItemDropWeightMap','GetItemDropCategoryWeight','Contains(factionTag ?? string.Empty)','whitelistExists')) {
    if ($itemDropMathText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current ItemDrop exact math gate missing token: $token" }
}
foreach ($token in @('GenerateEquipment additional items use the same exact','GetItemDropCategoryWeight(','context.FactionId','inventoryWeight = baseWeight + categoryWeight')) {
    if ($runtimeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current generic additional-item exactness gate missing token: $token" }
}
foreach ($token in @('No PactRecord.BramfaturaId filter exists','ItemDropSystemRandomizeExact','itemDropWhitelist=','DefaultItemFactionTag')) {
    if ($baronUltimateDataText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Baron ItemDrop exactness gate missing token: $token" }
}

# habitat uses both exact vanilla raid-entry contracts.
foreach ($token in @('CollectBaronHabitatFromRuntimeStations','CollectBaronHabitatFromRuntimeMissions','Mission.StationId','missionMatches=','exactRaidSource=')) {
    if (($sourceText + $baronHabitatText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current Baron habitat exact-source contract missing: $token"
    }
}

# safe render reuse + habitat station indexing. These optimizations must
# remain projection-only and fail closed for dynamic/icon-bearing rows.
$rowCachePath = Join-Path $sourceDir 'ModMain.BrowserRowRenderCache.cs'
$habitatRuntimeIndexPath = Join-Path $sourceDir 'ModMain.BaronHabitatRuntimeIndex.cs'
foreach ($requiredOptOwner in @($rowCachePath,$habitatRuntimeIndexPath)) {
    if (-not (Test-Path -LiteralPath $requiredOptOwner -PathType Leaf)) { throw "current optimization owner missing: $requiredOptOwner" }
}
$rowCacheText = [IO.File]::ReadAllText($rowCachePath)
$habitatRuntimeIndexText = [IO.File]::ReadAllText($habitatRuntimeIndexPath)
foreach ($token in @('IsBrowserRowRenderReuseSafe','CanReuseBrowserRowRender','RestoreCachedBrowserRowBindings','CaptureBrowserRowRenderStamp')) {
    if ($rowCacheText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current render-reuse gate missing: $token" }
}
foreach ($token in @('BrowserNavigation.Tab != (int)BrowserTabId.Overview','line.LeftContentKind != BrowserLeftContentKind.Text','line.Action.IsNone','line.ShowRecipeChipContext','line.ContainerIconId')) {
    if ($rowCacheText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current fail-closed render-reuse token missing: $token" }
}
foreach ($token in @('BaronStationBodiesByBramfatura','BaronStationBodyById','EnsureBaronHabitatRuntimeStationIndex','ResetBaronHabitatRuntimeStationIndex')) {
    if ($habitatRuntimeIndexText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current habitat station-index gate missing: $token" }
}
if ($baronHabitatText.IndexOf('EnumerateData(values)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current exactness regression: mission habitat must remain live-scanned.'
}

# language-safe final display caches + no-op Unity/TMP dirty-write
# suppression. These are projection/cache optimizations only; exact gameplay semantics stay owned
# by the existing feature resolvers and Mission habitat remains live-scanned.
$uiDirtyPath = Join-Path $sourceDir 'ModMain.BrowserUiDirtySuppression.cs'
if (-not (Test-Path -LiteralPath $uiDirtyPath -PathType Leaf)) { throw 'current UI dirty-suppression owner missing.' }
$uiDirtyText = [IO.File]::ReadAllText($uiDirtyPath)
$localizationText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.Localization.cs'))
$hardeningText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.Hardening.cs'))
foreach ($token in @('SetBrowserActiveIfChanged','SetBrowserTextIfChanged','SetBrowserRectPositionIfChanged','SetBrowserRectSizeIfChanged','SetBrowserFontSizeIfChanged')) {
    if ($uiDirtyText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current UI dirty-suppression gate missing: $token" }
}
foreach ($token in @('LocalizedItemDisplayCache','LocalizedMagnumPerkDisplayCache','EnsureLocalizationCacheLanguage();','|item|','|magnum|')) {
    if ($localizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current localization hot-cache gate missing: $token" }
}
foreach ($token in @('ItemDisplayCache','MagnumDisplayCache')) {
    if ($hardeningText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current memory-hygiene cache gate missing: $token" }
}
foreach ($token in @("value.IndexOf('Ё') < 0","value.IndexOf('ё') < 0")) {
    if ($runtimeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current NormalizeGameText fast-path gate missing: $token" }
}
foreach ($token in @('BrowserRowKind.OverviewCombatHeader','BrowserRowKind.BaronLootHeader','BrowserRowKind.BaronLootRow','Tab == BrowserNavigation.Tab')) {
    if ($rowCacheText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current safe-reuse expansion gate missing: $token" }
}
foreach ($token in @('SetBrowserActiveIfChanged(column.gameObject, visible)','SetBrowserTextIfChanged(column, visible ? NormalizeModUiText(value) : string.Empty)','SetBrowserAutoSizingIfChanged(column, false)','SetBrowserWordWrappingIfChanged(column, false)','SetBrowserOverflowIfChanged(column, TextOverflowModes.Ellipsis)')) {
    if ($lootPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Loot column no-op gate missing: $token" }
}
if ($baronHabitatText.IndexOf('EnumerateData(values)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current exactness regression: Mission habitat live scan was removed.'
}

# finish no-op suppression across pooled row/search/catalog chrome and
# remove tiny per-redraw label-array allocations. No gameplay or live-data cache semantics change.
$browserPresentationText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.BrowserPresentation.cs'))
$browserCatalogPresentationText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.BrowserCatalogPresentation.cs'))
$overviewDashboardText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.OverviewDashboard.cs'))
foreach ($token in @('SetBrowserInteractableIfChanged','SetBrowserRaycastTargetIfChanged','SetBrowserGraphicColorIfChanged','SetBrowserImageSpriteIfChanged','SetBrowserImageEnabledIfChanged','SetBrowserAlignmentIfChanged','SetBrowserOutlineEnabledIfChanged','SetBrowserOutlineColorIfChanged','SetBrowserOutlineDistanceIfChanged')) {
    if ($uiDirtyText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current extended UI dirty-suppression gate missing: $token" }
}
foreach ($token in @('GetBrowserTabLabel','SetBrowserTextIfChanged(_browserSearchStatusText','SetBrowserImageSpriteIfChanged(icon, nextIcon)','SetBrowserGraphicColorIfChanged(BrowserTabBackgrounds[i]')) {
    if ($browserPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current browser chrome/search suppression gate missing: $token" }
}
foreach ($token in @('SetBrowserTextIfChanged(_browserCatalogHeaderText','SetBrowserImageSpriteIfChanged(BrowserCatalogRowIcons[i], nextIcon)','SetBrowserGraphicColorIfChanged(BrowserCatalogRowFavoriteBackgrounds[i]','switch (category)')) {
    if ($browserCatalogPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current catalog suppression/allocation gate missing: $token" }
}
if ($browserCatalogPresentationText.IndexOf('string[] labels = new string[]',[StringComparison]::Ordinal) -ge 0 -or
    $browserCatalogPresentationText.IndexOf('string[] fullLabels = new string[]',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: catalog category redraw label arrays returned.'
}
foreach ($token in @('SetBrowserTextIfChanged(_browserStatsText','string.Join("   •   ", parts)')) {
    if ($overviewDashboardText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Overview stats redraw gate missing: $token" }
}
foreach ($token in @('SetBrowserGraphicColorIfChanged','SetBrowserRectPositionIfChanged','SetBrowserImageSpriteIfChanged','SetBrowserFontSizeIfChanged','SetBrowserInteractableIfChanged')) {
    if ($browserRowRenderCombinedText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current pooled-row no-op gate missing: $token" }
}
foreach ($forbidden in @('.fontSize =','.fontStyle =','.anchoredPosition =','.sizeDelta =','.sprite =','.alignment =','.raycastTarget =','.interactable =')) {
    if ($browserRowRenderCombinedText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "current pooled-row direct dirty setter returned: $forbidden" }
}
if ($baronHabitatText.IndexOf('EnumerateData(values)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current exactness regression: Mission habitat live scan was removed.'
}

# renderer decomposition must be behavior-preserving. Keep the hot loop
# orchestration-only, keep the context allocation-free, and retain exact mutation-call coverage.
if ($browserRowRendererPartsText.IndexOf('private struct BrowserRowRenderContext',[StringComparison]::Ordinal) -lt 0 -or
    $browserRowRendererPartsText.IndexOf('private class BrowserRowRenderContext',[StringComparison]::Ordinal) -ge 0) {
    throw 'current renderer context must remain a value type; per-row heap allocation is forbidden.'
}
$rendererPartsLines = (Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserRowRendererParts.cs')).Count
if ($rendererPartsLines -gt 1050) { throw "current renderer-parts line budget exceeded: $rendererPartsLines/1050" }



# Loot accordion must short-circuit hidden sections before sorting/row materialization.
$lootNavigationText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootNavigation.cs'))
$lootRewardSourcesText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootRewardSources.cs'))
$lootEnemyPresentationText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootEnemyPresentation.cs'))
$lootGeneralSpawnText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootGeneralSpawn.cs'))
$lootSpecialSourcesText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootSpecialSources.cs'))
$lootBaronSpecialText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootBaronSpecial.cs'))
foreach ($token in @('GetLootSectionExpandedState','AddLootSectionHeaderAndShouldBuild','large hidden tables just to throw them away')) {
    if ($lootNavigationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Loot accordion lazy-build gate missing: $token" }
}
# Check the current lazy-render shapes by feature owner. Do not pin this contract to
# a single historical spelling: the invariant is that sorting / row creation happens only
# after AddLootSectionHeaderAndShouldBuild reports the section as expanded.
foreach ($token in @('bool buildContainers = AddLootSectionHeaderAndShouldBuild(','if (buildContainers)','bool buildAmputations = AddLootSectionHeaderAndShouldBuild(','if (buildAmputations)','bool buildMissionPools = AddLootSectionHeaderAndShouldBuild(','if (buildMissionPools)')) {
    if ($lootPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current primary Loot collapsed-section gate missing: $token" }
}
foreach ($token in @('bool buildRegular = AddLootSectionHeaderAndShouldBuild(','if (buildRegular)','bool buildBonus = AddLootSectionHeaderAndShouldBuild(','if (buildBonus)')) {
    if ($lootEnemyPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current enemy Loot collapsed-section gate missing: $token" }
}
foreach ($pair in @(
    @($lootGeneralSpawnText,'Ui("ui.other_containers"), LootGeneralSpawnAdditionalContainerBuffer.Count)) return;'),
    @($lootSpecialSourcesText,'if (!AddLootSectionHeaderAndShouldBuild(sectionLabel, visibleRowCount)) return;'),
    @($lootBaronSpecialText,'if (!AddLootSectionHeaderAndShouldBuild(Ui("loot.baron.section"), groups.Count)) return;')
)) {
    if ($pair[0].IndexOf($pair[1],[StringComparison]::Ordinal) -lt 0) { throw "current direct Loot collapsed-section short-circuit missing: $($pair[1])" }
}
foreach ($token in @('StationProductionRewardFactionsByItem','_stationProductionRewardFingerprint','_stationProductionRewardNextRefreshTime','now + 1.0f')) {
    if ($lootRewardSourcesText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current station-reward reverse-index throttle missing: $token" }
}
$factionsPerfText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.Factions.cs'))
if ($factionsPerfText.IndexOf('FactionRewardPoolSnapshot snapshot = GetFactionRewardPoolSnapshot(unlock.FactionId);',[StringComparison]::Ordinal) -ge 0) { throw 'Factions click regression: full reward pools returned to row rendering.' }
foreach ($token in @('TryResolveFactionSmallIcon(factionId, runtimeFaction);','int techLimit = faction == null ? -1 : GetFactionTechLevelLimit(faction);')) {
    if ($factionsPerfText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Factions warm-render performance contract missing: $token" }
}
if ($sourceText.IndexOf('Resources.FindObjectsOfTypeAll<Sprite>()',[StringComparison]::Ordinal) -ge 0 -or $sourceText.IndexOf('Resources.FindObjectsOfTypeAll(typeof(Sprite))',[StringComparison]::Ordinal) -ge 0) {
    throw 'current performance safety regression: global Resources Sprite scan returned.'
}
