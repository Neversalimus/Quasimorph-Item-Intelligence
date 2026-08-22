# ============================================================================
# CODE HYGIENE / ZOMBIE-GATE CONTRACTS
# Current dead-code and single-reference invariants.
# ============================================================================

# Advisory only: a private static method whose identifier occurs exactly once in Source
# is suspicious dead code. Reflection/Harmony conventions can make this legitimate, so
# this check deliberately reports candidates instead of blocking a build.
$singleReferencePrivateStatic = @()
$privateStaticMethodPattern = '(?m)^\s*private\s+static\s+(?:(?:readonly|unsafe|async)\s+)*[^;=\r\n]+?\s+([A-Za-z_]\w*)\s*\('
foreach ($candidateFile in $sourceFiles) {
    $candidateText = Get-Content -LiteralPath $candidateFile.FullName -Raw
    foreach ($match in [regex]::Matches($candidateText, $privateStaticMethodPattern)) {
        $methodName = $match.Groups[1].Value
        $mentionCount = ([regex]::Matches($sourceText, ('\b' + [regex]::Escape($methodName) + '\b'))).Count
        if ($mentionCount -eq 1) {
            $singleReferencePrivateStatic += ($candidateFile.Name + '::' + $methodName)
        }
    }
}
if ($singleReferencePrivateStatic.Count -gt 0) {
    Write-Host ('Static audit advisory: private static single-reference candidates=' + $singleReferencePrivateStatic.Count) -ForegroundColor DarkYellow
    $singleReferencePrivateStatic | Select-Object -First 12 | ForEach-Object { Write-Host ('  review: ' + $_) -ForegroundColor DarkYellow }
}

# Dead Code / Zombie Gate Cleanup.
# These private implementations had no live caller. Some formed larger transitive islands
# (legacy tooltip injection, abandoned Starmap unwind experiments, and the generic container
# count probe). They are now forbidden from silently returning through stale merge/build history.
$retiredDeadCodeTokens = @(
    'AddWrappedBrowserValue','AppendQuick','ApplyBackgroundStyle','ApplyTooltipRow',
    'BuildAmmoIndex','BuildItemTooltipPostfix','BuildQuickRows','BuildQuickSignature',
    'ContainerHasItem','EnsureMagnumProgressionResolved','ExtractMatchedContainerCount',
    'FindActiveArsenalScreen','FindActiveBlockingModalBeforeStarmap','FindActiveDecisionOverlayByStructure',
    'FindActiveTechnologyTreeOverlayBeforeStarmap','FindBestTooltipPropertyTemplate',
    'FindDecisionOverlayRootForLabels','FindItemIdFromObject','FindItemIdInArgs',
    'FindNearestCommonUiAncestor','FindNestedObject','FindPropertiesTooltip','GetAmmoRelationCount',
    'GetCachedContainerCountMethods','GetItemRecord','GetRecipeAvailabilityLabel','GetRuntimeStations',
    'HasNonMagnumUse','HasRandomWeight','InjectQuickRowsPooled','InvokeStringSetter',
    'IsContainerLikeItem','IsDecisionNoLabel','IsDecisionYesLabel','IsRandomDropMember',
    'IsSafeVanillaStarmapHostReady','IsTechnologyTreeHeaderText','IsVanillaStarmapInvocationReady',
    'ItemTooltipBuildPostfix','LogIconSchemaOnce','LooksLikeBlockingModalName',
    'LooksLikeDecisionOverlayRoot','MarkTooltipLayout','MatchesDecisionLabelToken','NormalizeDecisionLabel',
    'RefreshInspectorForHoveredItem','RemoveInjectedRows','ResolveRuntimeObjectByTypeName','ResolveSpriteDeep',
    'RestoreTooltipPostfix','StringSetsOverlap','TryFindContainerItemDeep','TryGetActiveTooltipScreenRect',
    'TryGetContainerItemCountDeep','TryGetContainerItemCountFast','TryOpenPendingStarmap',
    'TryStarmapExperimentEmergencyRecovery','TryVanillaBackForStarmap',
    'ContainerCountMethodsByType','ContainerCountInvokeArgs','ContainerDeepSearchVisited',
    '_loggedTooltipTemplateFailure','MaxQuickRows','DisplayRow')
foreach ($retiredToken in $retiredDeadCodeTokens) {
    # Retired entries are C# identifiers. Match identifier boundaries instead of substrings:
    # e.g. retired FindNestedObject must not reject live FindNestedObjectByTypeName.
    $retiredIdentifierPattern = '(?<![A-Za-z0-9_])' + [Regex]::Escape($retiredToken) + '(?![A-Za-z0-9_])'
    if ([Regex]::IsMatch($sourceText, $retiredIdentifierPattern)) {
        throw "current dead-code hygiene regression: retired symbol returned: $retiredToken"
    }
}
if ($singleReferencePrivateStatic.Count -ne 0) {
    throw "current dead-code closure incomplete: private static single-reference candidates=$($singleReferencePrivateStatic.Count)."
}
# Release-polish dead-state and user-facing dedup gates.
foreach ($retired in @('QuickTooltipPool','QuickTooltipPools','_quickTooltipLastPruneFrame','_slowTooltipWarnings','_pendingStarmapBackAttempts','_starmapExperimentRecoveryLastFrame')) {
    if ($sourceText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "current dead-state regression returned: $retired" }
}
$lootSpecialText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.LootSpecialSources.cs') -Raw
foreach ($token in @('collapsePlayerEquivalentStoryRoutes','string.Equals(kind, "StoryScript", StringComparison.Ordinal)','(collapsePlayerEquivalentStoryRoutes ||','storyPrizeCount','AddLootSectionHeaderAndShouldBuild(sectionLabel, visibleRowCount)')) {
    if ($lootSpecialText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current player-facing special-source upstream dedup contract missing: $token" }
}
foreach ($retiredPresentationDedup in @('LootSpecialPresentationRow','HashSet<string> rowKeys','if (!rowKeys.Add(key)) continue;','AddLootSectionHeaderAndShouldBuild(sectionLabel, rows.Count)')) {
    if ($lootSpecialText.IndexOf($retiredPresentationDedup,[StringComparison]::Ordinal) -ge 0) { throw "current special-source lazy-render regression returned: $retiredPresentationDedup" }
}


# General-placement rows are compatibility information, not a per-container probability table.
$generalPlacementText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.LootGeneralSpawn.cs'))
foreach ($token in @('Ui("loot.column.container")','Ui("loot.general_spawn.placement")','"eligible"','StringSplitOptions.RemoveEmptyEntries')) {
    if ($generalPlacementText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "general container-placement presentation regression: $token" }
}
if ($generalPlacementText.IndexOf('Ui("ui.chance")',[StringComparison]::Ordinal) -ge 0) { throw 'general container-placement rows must not imply a named-container item chance.' }

# Cross-cutting release hardening must not disappear when feature owners are split.
foreach ($token in @(
    'public static partial class ModMain','ReadOnlyKnowledgePolicy = true','ModderModeExplicitSpawnException = true',
    'LastVerifiedGameVersion','Build fingerprint:','WriteDiagnosticsReportSafe','diagnostics_session_state.txt','diagnostics_session_end.txt',
    'ManualCtrlShiftF10','RunReadOnlySelfTestSafe','ManualCtrlShiftF11','RunConservativeMemoryHygiene','ReadUtf8LinesStrict','RecordLocalizationDuplicateKey',
    'BrowserNavigation','BrowserFavoriteItemIds','BrowserRecentItemIds','NavigateBrowserBack','BrowserCatalogDataFilter','EnforceInspectorModalInvariantSafe',
    'LogRuntimeBoundaryWarningOnce','VerifyChipUnlockChanceContract','SetCanonicalDatadiskUnlockPool','UnlockPoolSizeByDatadisk','_chipUnlockChanceContractVerified',
    'IsBrowserTabCompatibilityAvailable','AddCompatibilityUnavailableLine','CompatibilityVerdict','VanillaObservedItemIcons','TryResolveCanonicalItemSmallIcon',
    'TryResolveCompositeInventoryIcon','ScoreVanillaInventorySprite','CaptureVanillaItemSlotIcon','Data.AnComDataRewards','CreateBrowserPageScrollbar',
    'IsStarmapNavigationForbiddenByTravelState','IsRaidPreparationStarmapFallback')) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current hardening semantic guard missing: $token" }
}
foreach ($retired in @('TrySetMemberValue','DetailedIntelligence','AppendDetailed(','QII_Detail_')) {
    if ($sourceText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "current architecture safety regression: retired symbol returned: $retired" }
}
