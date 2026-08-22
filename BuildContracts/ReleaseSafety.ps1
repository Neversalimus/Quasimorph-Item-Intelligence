# ============================================================================
# RELEASE HYGIENE / READ-ONLY / LOCALIZATION / MANIFEST
# Current invariants only. Historical test/build provenance intentionally omitted.
# ============================================================================

# release hygiene gates. These are naming/packaging/support-surface checks only.
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.Test3RuntimeRefresh.cs') -PathType Leaf) {
    throw 'Release hygiene regression: test-era deferred refresh source filename returned.'
}
$deferredRefreshPath = Join-Path $sourceDir 'ModMain.DeferredBrowserRefresh.cs'
if (-not (Test-Path -LiteralPath $deferredRefreshPath -PathType Leaf)) {
    throw 'Release hygiene contract missing ModMain.DeferredBrowserRefresh.cs.'
}
foreach ($productionRefreshToken in @(
    'QueueBrowserRowsRefresh',
    'TickBrowserRowsRefresh',
    '_browserRowsRefreshPending',
    '_browserRowsRefreshDelayFrames'
)) {
    if ($sourceText.IndexOf($productionRefreshToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Release hygiene contract missing production deferred-refresh token: $productionRefreshToken"
    }
}
foreach ($staleRefreshToken in @(
    'QueueTest3RowsRefresh',
    'TickTest3RowsRefresh',
    '_test3RowsRefreshPending',
    '_test3RowsRefreshDelayFrames',
    'QII1739T3_MAGNUM_REFRESH'
)) {
    if ($sourceText.IndexOf($staleRefreshToken,[StringComparison]::Ordinal) -ge 0) {
        throw "Release hygiene regression: stale test-era deferred-refresh token remains: $staleRefreshToken"
    }
}
$hardeningText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Hardening.cs')
if ($hardeningText.IndexOf('else if (ModderMode && Input.GetKeyDown(KeyCode.F11))',[StringComparison]::Ordinal) -lt 0) {
    throw 'Release hygiene regression: F11 read-only self-test must remain Modder Mode-only.'
}
$lootContainerProfilesText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.LootContainerProfiles.cs')
if ($lootContainerProfilesText.IndexOf('Loot container profile audit:',[StringComparison]::Ordinal) -ge 0 -or
    $lootContainerProfilesText.IndexOf('Loot container profiles:',[StringComparison]::Ordinal) -lt 0) {
    throw 'Release hygiene regression: always-on Loot profile log must use neutral production wording.'
}


# exact Baron pact data retained, player-facing Baron UX simplified.
$baronSpecialText = [IO.File]::ReadAllText($lootBaronSpecialPath)
$baronUltimateDataText = [IO.File]::ReadAllText($lootBaronUltimateDataPath)
foreach ($token in @('LootBaronSpecialSourcesByItem','EnsureLootBaronSpecialIndex(string itemId)','AppendLootBaronSpecialLines','BaronLootHeader','BaronLootRow','loot.baron.column.any_pact')) {
    if ($baronSpecialText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Baron Loot current gate missing token: $token" }
}
foreach ($token in @('CanBaronUse','AdditItemClasses','AdditItemCount','GrantedItems','AiPresets','DropUltimateItemChance','ProbabilityAtLeastOnceUniformCount','BaronMobInventory+ItemDropSystemRandomizeExact+UltimateDeathRestore','rngInvoked=false','uniformPactPool=','ItemCategoriesWhitelist','DefaultItemFactionTag','GetItemDropCategoryWeight','ExtractItemDropWeightMap','itemDropWhitelist=','factionTag=','bramfatura=')) {
    if (($baronUltimateDataText + $sourceText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Baron Ultimate exact-selector data gate missing token: $token" }
}
foreach ($forbiddenToken in @('PactBramfaturaId','ResolveBaronPactBramfatura','CanonicalPactOwnership','_baronPactOwnershipComplete')) {
    if ($baronUltimateDataText.IndexOf($forbiddenToken,[StringComparison]::Ordinal) -ge 0) {
        throw "current regression: inferred same-Bramfatura pact selector returned: $forbiddenToken"
    }
}
foreach ($forbidden in @('DropManager.GenerateDrop','UnityEngine.Random.Range','CreateForInventory(','Monster.AddItem(','TryCreateUltimateItem(','TryConsumeUltimateByMonster(','TryApplyUltimateToMonsterFromInventory(')) {
    if ($baronUltimateDataText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "Baron Ultimate projection must remain read-only and cannot invoke vanilla mutation/RNG API: $forbidden" }
}
if ($sourceText.IndexOf('AppendLootBaronSpecialLines(itemId, ref any);',[StringComparison]::Ordinal) -lt 0) { throw 'Baron Loot presentation hook missing.' }
if ($sourceText.IndexOf('EnsureLootBaronSpecialIndex(itemId);',[StringComparison]::Ordinal) -lt 0) { throw 'Baron lazy item-gated index hook missing.' }
if ($sourceText.IndexOf('try { BuildLootBaronSpecialIndex(); }',[StringComparison]::Ordinal) -ge 0) { throw 'Baron current must not eagerly build the pact index for ordinary Loot.' }
$baronDataText = [IO.File]::ReadAllText($lootBaronDataPath)
foreach ($token in @('CollectQmorphosRecordsForBaronIndex','QmorphosRecord','Records','Values','ReferenceComparer.Instance')) {
    if ($baronDataText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Baron Qmorphos resolver current gate missing: $token" }
}

$overviewBaronSpecialText = [IO.File]::ReadAllText($overviewBaronSpecialPath)
$baronHabitatPresentationPath = Join-Path $sourceDir 'ModMain.BaronHabitatPresentation.cs'
if (-not (Test-Path -LiteralPath $baronHabitatPresentationPath -PathType Leaf)) { throw 'current Baron habitat presentation owner missing.' }
$baronHabitatPresentationText = [IO.File]::ReadAllText($baronHabitatPresentationPath)
$baronHabitatPresentationLines = (Get-Content -LiteralPath $baronHabitatPresentationPath).Count
if ($baronHabitatPresentationLines -gt 80) { throw "current Baron habitat presentation ownership regressed: $baronHabitatPresentationLines/80." }
$overviewBaronPresentationSurfaceText = $overviewBaronSpecialText + $baronHabitatPresentationText
# keeps the compact guaranteed-pact summary but moves habitat row construction to
# a dedicated presentation owner so multi-row planet/satellite layout stays out of the facade.
foreach ($token in @(
    'AppendOverviewBaronSpecial',
    'IsBaronPactItem',
    'loot.baron.section',
    'BrowserLine.Header',
    'AppendOverviewBaronHabitat',
    'ui.baron_habitat',
    'ui.baron_guaranteed',
    'ui.baron_one_pact',
    'ui.baron_this_pact',
    'ui.baron_depends_on_mission_tech')) {
    if ($overviewBaronPresentationSurfaceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "current Baron Overview current UX gate missing token: $token" }
}
foreach ($forbidden in @('BrowserTabId.Loot','BrowserTabActionPrefix','InternalAction(','loot.baron.column.qmorph','loot.baron.column.death_restore')) {
    if ($overviewBaronSpecialText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "current regression: Baron Overview must be complete/non-navigating and must not expose internal mechanics: $forbidden"
    }
}
foreach ($forbidden in @('loot.baron.column.qmorph','loot.baron.column.death_restore','loot.note.baron_dynamic_tech','loot.note.baron_ksiomara_exception','loot.note.baron_consumption_bound')) {
    if ($baronSpecialText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "current regression: technical Baron presentation returned to Loot: $forbidden"
    }
}
if ($overviewDashboardText.IndexOf('AppendOverviewBaronSpecial(itemId);',[StringComparison]::Ordinal) -lt 0) { throw 'Baron Overview current dashboard hook missing.' }

if ($browserTextLayoutText.IndexOf('GetPreferredValues(candidate, 4096f, 0f).x > fullWidthWrapLimit',[StringComparison]::Ordinal) -lt 0 -or
    $browserTextLayoutText.IndexOf('measure.enableWordWrapping = false;',[StringComparison]::Ordinal) -lt 0 -or
    $browserTextLayoutText.IndexOf('List<string> sentences = SplitBrowserNoteSentences(value);',[StringComparison]::Ordinal) -lt 0 -or
    $browserTextLayoutText.IndexOf('string sentenceCandidate = line.Length == 0',[StringComparison]::Ordinal) -lt 0 -or
    $browserTextLayoutText.IndexOf('float fullWidthWrapLimit = BrowserFullNoteWidth - 4f;',[StringComparison]::Ordinal) -lt 0) {
    throw 'current full-width text wrapping contract missing.'
}
if ($overviewDashboardText.IndexOf('FormatChipUnlockStatusSummary',[StringComparison]::Ordinal) -lt 0 -or
    $overviewDashboardText.IndexOf('BrowserLine.Header(',[StringComparison]::Ordinal) -lt 0) {
    throw 'current chip header summary contract missing.'
}
if ($browserModelsText.IndexOf('public static BrowserLine Header(string left, string right)',[StringComparison]::Ordinal) -lt 0) {
    throw 'current generic header row factory missing.'
}
foreach ($token in @('ui.chip_summary_unlocked','ui.chip_summary_locked','AppendChipUnlockStatusPart(summary, learnedCount','summary.Append(" • ")')) {
    if (($overviewDashboardText + $ruLocalizationText + $enLocalizationText + $templateLocalizationText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current human-readable chip summary contract missing: $token"
    }
}
if ($overviewDashboardText.IndexOf('"N " + learnedCount',[StringComparison]::Ordinal) -ge 0 -or
    $overviewDashboardText.IndexOf('" / n " + lockedCount',[StringComparison]::Ordinal) -ge 0) {
    throw 'current regression: raw N/n debug notation returned to the player-facing chip header.'
}


# Baron row kinds remain model-owned, while moved the shared full-width
# geometry from BrowserPresentation into BrowserRowLayout and row application into
# BrowserRowRenderer. Validate the current owners instead of the pre- file.
foreach ($token in @(
    'BrowserRowKind.BaronLootHeader',
    'BrowserRowKind.BaronLootRow',
    'ApplyBrowserBaronColumns(',
    'ConfigureLootColumn(itemChance, 430f, 140f',
    'ConfigureLootColumn(pactChance, 570f, 118f')) {
    if (($browserModelsText + $browserRowRenderCombinedText + $browserRowLayoutText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current Baron table ownership/geometry contract missing: $token"
    }
}

if ($ruLocalizationText.IndexOf("loot.baron.column.any_pact`tПАКТ",[StringComparison]::Ordinal) -lt 0 -or
    $enLocalizationText.IndexOf("loot.baron.column.any_pact`tPACT",[StringComparison]::Ordinal) -lt 0) {
    throw 'current Baron guaranteed-pact label contract missing.'
}
# audit disproved the old player-facing "own Bramfatura" selector wording.
# Keep the note compact, but require selector-neutral language that matches ItemDropSystem.Randomize.
if ($ruLocalizationText.IndexOf("loot.note.baron_ultimate_death`tБарон оставляет 1 пакт.",[StringComparison]::Ordinal) -lt 0 -or
    $ruLocalizationText.IndexOf('Tech миссии',[StringComparison]::Ordinal) -lt 0 -or $ruLocalizationText.IndexOf('доступной добычи',[StringComparison]::Ordinal) -lt 0 -or
    $enLocalizationText.IndexOf("loot.note.baron_ultimate_death`tA Baron drops 1 pact.",[StringComparison]::Ordinal) -lt 0 -or
    $enLocalizationText.IndexOf('mission Tech',[StringComparison]::Ordinal) -lt 0 -or $enLocalizationText.IndexOf('available loot',[StringComparison]::Ordinal) -lt 0) {
    throw 'current Baron player-facing exactness explanation contract missing.'
}
if ($ruLocalizationText.IndexOf("своей брамфатуры",[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
    $enLocalizationText.IndexOf("own Bramfatura",[StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'current regression: disproved same-Bramfatura wording returned to player-facing localization.'
}

function Get-LocalizationFormatMap([string]$text, [string]$name) {
    $map = @{}
    foreach ($line in ($text -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#') -or $line.TrimStart().StartsWith('@')) { continue }
        $tab = $line.IndexOf("`t")
        if ($tab -le 0) { throw "Malformed player-facing localization in ${name}: $line" }
        $key = $line.Substring(0,$tab).Trim()
        $value = $line.Substring($tab + 1)
        if ($value -match '[\u200B\u200C\u200D\uFEFF\uFFFD\u0000]') { throw "Invalid player-facing Unicode in ${name}: $key" }
        $literalBraces = $value.Replace('{{','').Replace('}}','')
        if ([regex]::Matches($literalBraces,'\{').Count -ne [regex]::Matches($literalBraces,'\}').Count) {
            throw "Unbalanced format braces in ${name}: $key"
        }
        $tokens = @([regex]::Matches($value,'(?<!\{)\{(\d+)(?:[^}]*)\}(?!\})') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        $map[$key] = $tokens -join ','
    }
    return $map
}
$formatMaps = @(
    (Get-LocalizationFormatMap $enLocalizationText 'en.lang'),
    (Get-LocalizationFormatMap $ruLocalizationText 'ru.lang'),
    (Get-LocalizationFormatMap $templateLocalizationText 'TranslationTemplate.lang'))
foreach ($key in $formatMaps[0].Keys) {
    if ($formatMaps[0][$key] -ne $formatMaps[1][$key] -or $formatMaps[0][$key] -ne $formatMaps[2][$key]) {
        throw "Localization format-placeholder mismatch: $key"
    }
}

$versionMatch = [regex]::Match($sourceText, 'public const string Version = "([^"]+)";')
if (-not $versionMatch.Success) { throw 'Source version contract missing.' }
$sourceVersion = $versionMatch.Groups[1].Value

if ($Mode -eq 'TEST') {
    if ($sourceVersion -notmatch '-test|-rc|-dev') { throw "TEST is blocked for stable source version: $sourceVersion" }
    if (-not $WorkshopStage) { $WorkshopStage = 'C:\QM_Workshop\ItemIntelligence_DEV' }
    $targetWorkshopId = $DevWorkshopId
} else {
    if ($sourceVersion -ne $ExpectedReleaseVersion) { throw "RELEASE source version mismatch: $sourceVersion != $ExpectedReleaseVersion" }
    if ($sourceVersion -match '-test|-rc|-dev') { throw "RELEASE is blocked for non-stable source version: $sourceVersion" }
    if (-not $WorkshopStage) { $WorkshopStage = 'C:\QM_Workshop\ItemIntelligence' }
    $targetWorkshopId = $PublicWorkshopId
}

foreach ($token in @(
    'public static partial class ModMain',
    'ReadOnlyKnowledgePolicy = true',
    'ModderModeExplicitSpawnException = true',
    'LastVerifiedGameVersion',
    'Build fingerprint:',
    'WriteDiagnosticsReportSafe',
    'diagnostics_session_state.txt',
    'diagnostics_session_end.txt',
    'ManualCtrlShiftF10',
    'RunReadOnlySelfTestSafe',
    'ManualCtrlShiftF11',
    'RunConservativeMemoryHygiene',
    'ReadUtf8LinesStrict',
    'RecordLocalizationDuplicateKey',
    'BrowserNavigation',
    'BrowserFavoriteItemIds',
    'BrowserRecentItemIds',
    'NavigateBrowserBack',
    'BrowserCatalogDataFilter',
    'EnforceInspectorModalInvariantSafe',
    'LogRuntimeBoundaryWarningOnce',
    'VerifyChipUnlockChanceContract',
    'SetCanonicalDatadiskUnlockPool',
    'UnlockPoolSizeByDatadisk',
    '_chipUnlockChanceContractVerified',
    'IsBrowserTabCompatibilityAvailable',
    'AddCompatibilityUnavailableLine',
    'CompatibilityVerdict',
    'VanillaObservedItemIcons',
    'TryResolveCanonicalItemSmallIcon',
    'TryResolveCompositeInventoryIcon',
    'ScoreVanillaInventorySprite',
    'CaptureVanillaItemSlotIcon',
    'Data.AnComDataRewards',
    'CreateBrowserPageScrollbar',
    'IsStarmapNavigationForbiddenByTravelState',
    'IsRaidPreparationStarmapFallback'
)) {
    if ($sourceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Hardening semantic guard missing: $token" }
}

# Ordinary-mode no-cheat guard. The one-item Modder Mode exception is separately
# allowlisted and fully constrained by ModderActions.ps1.
foreach ($forbidden in @(
    'SellItems(',
    'BuyItems(',
    'SetReputation(',
    'AddReputation(',
    'RemoveReputation(',
    'SetStoryVariable(',
    'SpawnItemToInventory(',
    'GiveItemToPlayer(',
    '.SetValue(',
    '.SetValueDirect('
)) {
    if ($sourceText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "Read-only architecture guard failed: forbidden mutation token $forbidden"
    }
}

# Architecture safety regression guard. These symbols belong to retired paths and
# must not silently return to production source.
foreach ($retired in @(
    'TrySetMemberValue',
    'DetailedIntelligence',
    'AppendDetailed(',
    'QII_Detail_'
)) {
    if ($sourceText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) {
        throw "Architecture safety guard failed: retired symbol returned: $retired"
    }
}

# Performance safety guard: the old global Sprite scan caused multi-second stalls.
# Narrow/targeted Image or SpriteRenderer discovery is allowed, but never enumerate
# every Sprite object in Resources again.
if ($sourceText.IndexOf('Resources.FindObjectsOfTypeAll<Sprite>()',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('Resources.FindObjectsOfTypeAll(typeof(Sprite))',[StringComparison]::Ordinal) -ge 0) {
    throw 'Performance safety guard failed: global Resources Sprite scan returned.'
}

$json = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
if ($json.UniqueModName -ne 'ItemIntelligence') { throw 'Manifest UniqueModName must be ItemIntelligence.' }
if ($json.Assemblies.Count -ne 1 -or $json.Assemblies[0] -ne 'ItemIntelligence.dll') { throw 'Manifest must contain only ItemIntelligence.dll.' }
if ($json.SteamTags -notcontains '1.0.2' -or $json.SteamTags -notcontains '1.0.3') { throw 'Manifest compatibility tags 1.0.2 and 1.0.3 are required by current audit evidence.' }
if (Test-Path -LiteralPath (Join-Path $content 'ItemIntelligenceAutoTests.dll')) { throw 'AutoTests DLL must never be staged with the main mod.' }

$locDir = Join-Path $content 'Localization'
$enPath = Join-Path $locDir 'en.lang'
$ruPath = Join-Path $locDir 'ru.lang'
$templatePath = Join-Path $locDir 'TranslationTemplate.lang'
foreach ($path in @($enPath,$ruPath,$templatePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Localization file missing: $path" }
}
$enKeys = Get-LangKeySet -Path $enPath
$ruKeys = Get-LangKeySet -Path $ruPath
$templateKeys = Get-LangKeySet -Path $templatePath
Assert-EqualKeySets -A $enKeys -B $ruKeys -AName 'en.lang' -BName 'ru.lang'
Assert-EqualKeySets -A $enKeys -B $templateKeys -AName 'en.lang' -BName 'TranslationTemplate.lang'
Assert-No-PlayerFacingDevText -Path $enPath
Assert-No-PlayerFacingDevText -Path $ruPath
Assert-No-PlayerFacingDevText -Path $templatePath
$enText = Read-Utf8Strict -Path $enPath
$ruText = Read-Utf8Strict -Path $ruPath
$templateText = Read-Utf8Strict -Path $templatePath
foreach ($englishLocalizationPath in @($enPath,$templatePath)) {
    foreach ($line in ((Read-Utf8Strict -Path $englishLocalizationPath) -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $trimmed = $line.TrimStart()
        if ($trimmed.StartsWith('#') -or $trimmed.StartsWith('@')) { continue }
        if ($line -match '[А-Яа-яЁё]') {
            throw "English localization contains Cyrillic player-facing text in ${englishLocalizationPath}: $line"
        }
    }
}
foreach ($literalUiMatch in [regex]::Matches($sourceText, '\b(?:Ui|UiFormat|HotkeyUi)\(\s*"([^"]+)"')) {
    $literalUiKey = $literalUiMatch.Groups[1].Value
    if ($enKeys -notcontains $literalUiKey) {
        throw "Source literal localization key is missing from en/ru/template: $literalUiKey"
    }
}
foreach ($barterLocalizationContract in @(
    @{ Text = $enText; Token = "ui.barter_receive_this_item`tBARTER — RECEIVE THIS ITEM" },
    @{ Text = $enText; Token = "ui.barter_give_this_item`tBARTER — GIVE THIS ITEM" },
    @{ Text = $ruText; Token = "ui.barter_receive_this_item`tБАРТЕР — ПОЛУЧИТЬ ЭТОТ ПРЕДМЕТ" },
    @{ Text = $ruText; Token = "ui.barter_give_this_item`tБАРТЕР — ОТДАТЬ ЭТОТ ПРЕДМЕТ" },
    @{ Text = $templateText; Token = "ui.barter_give_this_item`tBARTER — GIVE THIS ITEM" }
)) {
    if ($barterLocalizationContract.Text.IndexOf($barterLocalizationContract.Token,[StringComparison]::Ordinal) -lt 0) {
        throw "Trade localization clarity contract missing: $($barterLocalizationContract.Token)"
    }
}
foreach ($reverseDisassemblyLocalizationContract in @(
    @{ Text = $enText; Token = "ui.obtained_by_disassembling`tOBTAINED BY DISASSEMBLING" },
    @{ Text = $ruText; Token = "ui.obtained_by_disassembling`tМОЖНО ПОЛУЧИТЬ ПРИ РАЗБОРКЕ" },
    @{ Text = $templateText; Token = "ui.obtained_by_disassembling`tOBTAINED BY DISASSEMBLING" }
)) {
    if ($reverseDisassemblyLocalizationContract.Text.IndexOf($reverseDisassemblyLocalizationContract.Token,[StringComparison]::Ordinal) -lt 0) {
        throw "Reverse-disassembly localization contract missing: $($reverseDisassemblyLocalizationContract.Token)"
    }
}
foreach ($weaponModeLocalizationContract in @(
    @{ Text = $enText; Token = "ui.mode_tooltip_fire`tFire mode" },
    @{ Text = $enText; Token = "ui.mode_rate_of_fire`tRate of fire" },
    @{ Text = $enText; Token = "ui.mode_damage_modifier`tDamage mod." },
    @{ Text = $enText; Token = "ui.mode_ammo_consumption`tAmmo consumption" },
    @{ Text = $enText; Token = "ui.mode_accuracy`tAccuracy" },
    @{ Text = $enText; Token = "ui.mode_scatter`tScatter" },
    @{ Text = $ruText; Token = "ui.mode_tooltip_fire`tРежим огня" },
    @{ Text = $ruText; Token = "ui.mode_tooltip_attack`tРежим атаки" },
    @{ Text = $ruText; Token = "ui.mode_rate_of_fire`tСкорострельность" },
    @{ Text = $ruText; Token = "ui.mode_damage_modifier`tМод. урона" },
    @{ Text = $ruText; Token = "ui.mode_ammo_consumption`tРасход патронов" },
    @{ Text = $ruText; Token = "ui.mode_accuracy`tТочность" },
    @{ Text = $ruText; Token = "ui.mode_scatter`tРазброс" },
    @{ Text = $templateText; Token = "ui.mode_tooltip_fire`tFire mode" }
)) {
    if ($weaponModeLocalizationContract.Text.IndexOf($weaponModeLocalizationContract.Token,[StringComparison]::Ordinal) -lt 0) {
        throw "Weapon-mode localization contract missing: $($weaponModeLocalizationContract.Token)"
    }
}
$langCount = $enKeys.Count
