# ============================================================================
# STATION PRODUCTION / VANILLA BARTERRECEIPT OWNERSHIP
# ============================================================================
$stationProductionPath = Join-Path $sourceDir 'ModMain.StationProduction.cs'
$coreIndexesPath = Join-Path $sourceDir 'ModMain.CoreIndexes.cs'
$browserPresentationPath = Join-Path $sourceDir 'ModMain.BrowserPresentation.cs'
$informationPath = Join-Path $sourceDir 'ModMain.Information.cs'
$catalogPath = Join-Path $sourceDir 'ModMain.BrowserCatalog.cs'
$tradePresentationPath = Join-Path $sourceDir 'ModMain.TradePresentation.cs'
foreach ($requiredPath in @($stationProductionPath,$coreIndexesPath,$browserPresentationPath,$informationPath,$catalogPath,$tradePresentationPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { throw "Station production source missing: $requiredPath" }
}
$stationProductionText = [IO.File]::ReadAllText($stationProductionPath)
$coreIndexesText = [IO.File]::ReadAllText($coreIndexesPath)
$browserPresentationText = [IO.File]::ReadAllText($browserPresentationPath)
$informationText = [IO.File]::ReadAllText($informationPath)
$catalogText = [IO.File]::ReadAllText($catalogPath)
$tradePresentationText = [IO.File]::ReadAllText($tradePresentationPath)
$stationProductionLines = (Get-Content -LiteralPath $stationProductionPath).Count
if ($stationProductionLines -gt 140) { throw "Station production ownership regressed: $stationProductionLines/140 lines." }

foreach ($token in @(
    'StationProductionByInputItem','StationProductionByOutputItem','class StationProductionRelation',
    'ResetStationProductionIndexState','AddBrowserStationProductionRelations',
    'unique.Sort(delegate(StationProductionRelation a, StationProductionRelation b)',
    'new List<KeyValuePair<string, int>>(relation.RelatedItems)')) {
    if ($stationProductionText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Station production owner token missing: $token" }
}
foreach ($token in @(
    'RunCompatibilityIndexStage(', '"StationProduction"', '"Recipes"', 'BuildStationProductionIndex();',
    'GetStaticMember(typeof(Data), "BarterReceipts")', 'GetMember(record, "InputItems")', 'GetMember(record, "OutputItems")',
    'new StationProductionRelation(id, input.Value, outputs)', 'new StationProductionRelation(id, output.Value, inputs)')) {
    if ($coreIndexesText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Station production index contract missing: $token" }
}
foreach ($token in @(
    'StationProductionByOutputItem.TryGetValue(', 'StationProductionByInputItem.TryGetValue(',
    'Ui("ui.station_production_note")', 'Ui("ui.station_production_produced_from")',
    'Ui("ui.station_production_used_to_produce")', 'AddBrowserStationProductionRelations(stationProduced, true)',
    'AddBrowserStationProductionRelations(stationUsed, false)')) {
    if ($browserPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Recipes station-production presentation contract missing: $token" }
}
$disassemblyPresentationIndex = $browserPresentationText.IndexOf('if (hasDisassembly)',[StringComparison]::Ordinal)
$disassemblySourcesPresentationIndex = $browserPresentationText.IndexOf('if (hasDisassemblySources)',[StringComparison]::Ordinal)
$stationProductionPresentationIndex = $browserPresentationText.IndexOf('if (hasStationProduced || hasStationUsed)',[StringComparison]::Ordinal)
if ($disassemblyPresentationIndex -lt 0 -or $disassemblySourcesPresentationIndex -lt 0 -or $stationProductionPresentationIndex -lt 0 -or
    $stationProductionPresentationIndex -le $disassemblyPresentationIndex -or
    $stationProductionPresentationIndex -le $disassemblySourcesPresentationIndex) {
    throw 'Recipes presentation order regression: canonical disassembly must precede Station Production, and Station Production must remain last.'
}
foreach ($token in @('StationProductionByOutputItem, relationId','StationProductionByInputItem, relationId')) {
    if ($informationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0 -or $catalogText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "Station production Recipes/catalog visibility contract missing: $token"
    }
}
foreach ($token in @('case BrowserCatalogDataFilter.Sources: return _compatRecipes && ShowRecipes;', 'case BrowserCatalogDataFilter.Consumers: return _compatRecipes && ShowRecipes;')) {
    if ($catalogText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Station production catalog gate missing: $token" }
}
foreach ($retired in @('AddBrowserBarterRelations','ui.station_economy_recipe_output','ui.station_economy_recipe_input')) {
    if ($sourceText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "Retired station-economy UI returned: $retired" }
}
$stationProductionLocalizationDir = Join-Path $content 'Localization'
$stationProductionLocalizationTexts = @(
    [IO.File]::ReadAllText((Join-Path $stationProductionLocalizationDir 'en.lang')),
    [IO.File]::ReadAllText((Join-Path $stationProductionLocalizationDir 'ru.lang')),
    [IO.File]::ReadAllText((Join-Path $stationProductionLocalizationDir 'TranslationTemplate.lang'))
)
foreach ($locText in $stationProductionLocalizationTexts) {
    foreach ($token in @('ui.station_production_produced_from','ui.station_production_used_to_produce','ui.station_production_note','ui.station_production_output','ui.station_production_input','ui.station_production_recipe')) {
        if ($locText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Station production localization key missing: $token" }
    }
    foreach ($retired in @('ui.station_economy_recipe_output','ui.station_economy_recipe_input','ui.economy_recipe')) {
        if ($locText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "Retired station-economy localization key returned: $retired" }
    }
}
