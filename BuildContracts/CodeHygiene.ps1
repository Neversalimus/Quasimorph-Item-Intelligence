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

# Player.log hygiene: ordinary informational logging is intentionally centralized.
$loggingPath = Join-Path $sourceDir 'ModMain.Logging.cs'
if (-not (Test-Path -LiteralPath $loggingPath -PathType Leaf)) { throw 'Logging owner missing: ModMain.Logging.cs' }
$loggingText = Get-Content -LiteralPath $loggingPath -Raw
foreach ($token in @('private static void VerboseLog(string message)','if (!VerboseLogging)','Debug.Log(message);')) {
    if ($loggingText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Verbose logging contract missing: $token" }
}
$configLoggingText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Configuration.cs') -Raw
foreach ($token in @('private static bool VerboseLogging = false;','"VerboseLogging=" + VerboseLogging','"VerboseLogging", VerboseLogging','ApplyMcmBool(currentConfig, "VerboseLogging", ref VerboseLogging)')) {
    if ($configLoggingText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Verbose logging configuration contract missing: $token" }
}
$ordinaryInfoLogCount = 0
foreach ($candidateFile in $sourceFiles) {
    if ($candidateFile.Name -eq 'ModMain.Logging.cs') { continue }
    $candidateText = Get-Content -LiteralPath $candidateFile.FullName -Raw
    $ordinaryInfoLogCount += [regex]::Matches($candidateText,'(?<![A-Za-z0-9_])(?:UnityEngine\.)?Debug\.Log\(').Count
}
if ($ordinaryInfoLogCount -ne 5) {
    throw "Player.log hygiene regression: ordinary non-verbose Debug.Log sites=$ordinaryInfoLogCount, expected 5. Use VerboseLog for diagnostics."
}

# Isolated Add-Type suites compile selected production fragments without the full logging owner.
# Keep their shared silent-by-default VerboseLog fixture wired in so logging refactors cannot break the
# test harness before the real game compilation/staging step. Suites that explicitly test verbose
# diagnostics may opt into the fixture sink.
$verboseFixturePath = Join-Path $root 'Tests/VerboseLoggingFixture.cs'
if (-not (Test-Path -LiteralPath $verboseFixturePath -PathType Leaf)) { throw 'Verbose logging test fixture missing.' }
$verboseFixtureText = Get-Content -LiteralPath $verboseFixturePath -Raw
foreach ($token in @('public static partial class ModMain','private static void VerboseLog(string message)','private static bool _testVerboseLoggingEnabled = false;','private static System.Action<string> _testVerboseLogSink = null;')) {
    if ($verboseFixtureText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Verbose logging test fixture malformed: $token" }
}
$tradeRuntimeCasesText = Get-Content -LiteralPath (Join-Path $root 'Tests/TradeRuntimeCases.cs') -Raw
foreach ($token in @('Normal logging suppresses TradePerf summaries','Verbose five-second window emits a single scan summary','_testVerboseLoggingEnabled = true;')) {
    if ($tradeRuntimeCasesText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade verbose-logging runtime contract missing: $token" }
}
$dynamicTestScripts = @(Get-ChildItem -LiteralPath (Join-Path $root 'Tests') -File -Filter 'Run-*.ps1' | Where-Object {
    (Get-Content -LiteralPath $_.FullName -Raw).IndexOf('Add-Type -TypeDefinition $code',[StringComparison]::Ordinal) -ge 0
})
foreach ($dynamicTestScript in $dynamicTestScripts) {
    $dynamicTestText = Get-Content -LiteralPath $dynamicTestScript.FullName -Raw
    foreach ($token in @('VerboseLoggingFixture.cs','verboseLoggingFixture.Replace')) {
        if ($dynamicTestText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
            throw "Verbose logging isolated-test guard missing in $($dynamicTestScript.Name): $token"
        }
    }
}
