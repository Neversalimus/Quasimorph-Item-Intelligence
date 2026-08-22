# ============================================================================
# TRADE ARCHITECTURE / 1.0.3 PRESENTATION OWNERSHIP
# ============================================================================
$tradeFacadePath = Join-Path $sourceDir 'ModMain.Trade.cs'
$tradePresentationPath = Join-Path $sourceDir 'ModMain.TradePresentation.cs'
$tradeBatchPricing103Path = Join-Path $sourceDir 'ModMain.TradeBatchPricing103.cs'
$runtimeTradeOwnerPath = Join-Path $sourceDir 'ModMain.Runtime.cs'
$tradeRowRendererPath = Join-Path $sourceDir 'ModMain.BrowserRowRendererTrade.cs'
$configurationPath = Join-Path $sourceDir 'ModMain.Configuration.cs'
foreach ($requiredPath in @($tradeFacadePath,$tradePresentationPath,$tradeBatchPricing103Path,$runtimeTradeOwnerPath,$tradeRowRendererPath,$configurationPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { throw "Trade architecture source missing: $requiredPath" }
}
$tradeFacadeText = [IO.File]::ReadAllText($tradeFacadePath)
$tradePresentationText = [IO.File]::ReadAllText($tradePresentationPath)
$tradeBatchPricing103Text = [IO.File]::ReadAllText($tradeBatchPricing103Path)
$runtimeTradeOwnerText = [IO.File]::ReadAllText($runtimeTradeOwnerPath)
$tradeRowRendererText = [IO.File]::ReadAllText($tradeRowRendererPath)
$configurationText = [IO.File]::ReadAllText($configurationPath)
$tradePresentationLines = (Get-Content -LiteralPath $tradePresentationPath).Count
$tradeBatchPricing103Lines = (Get-Content -LiteralPath $tradeBatchPricing103Path).Count
$tradeRowRendererLines = (Get-Content -LiteralPath $tradeRowRendererPath).Count
if ($tradePresentationLines -gt 180) { throw "Trade presentation ownership regressed: $tradePresentationLines/180 lines." }
if ($tradeBatchPricing103Lines -gt 180) { throw "Trade 1.0.3 pricing ownership regressed: $tradeBatchPricing103Lines/180 lines." }
if ($tradeRowRendererLines -gt 120) { throw "Trade row-renderer ownership regressed: $tradeRowRendererLines/120 lines." }

foreach ($token in @('BuildBrowserTrade','AddTradeStationCard103','AddTradeStationTable103','FormatTradePriceRange','FormatTradeBuyBatchCard','FormatTradeSellBatchCard','FormatTradeTableBatch','TradeStationCard103')) {
    if ($tradePresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade presentation owner missing helper: $token" }
}
foreach ($token in @('private static void BuildBrowserTrade','private static void AddTradeStationCard103','private static string FormatTradePriceRange')) {
    if ($tradeFacadeText.IndexOf($token,[StringComparison]::Ordinal) -ge 0) { throw "Trade facade regression: presentation helper returned to ModMain.Trade.cs: $token" }
}
foreach ($token in @('GetTradeBatchSampleQuantity','TryGetExactStationBatchPrice103','lastUnitPrice','rawBeforeLast','GetItemSellTradePoints','GetBuyPrice')) {
    if ($tradeBatchPricing103Text.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade pricing owner missing contract token: $token" }
}
if ($runtimeTradeOwnerText.IndexOf('private static bool TryGetExactStationBatchPrice103',[StringComparison]::Ordinal) -ge 0) { throw 'Runtime ownership regression: 1.0.3 batch pricing returned to Runtime.cs.' }

$browserModelsText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.BrowserModels.cs'))
foreach ($navigationToken in @('public static BrowserLine TradeStation(', 'public static BrowserLine TradeStationCard103(', 'BrowserAction.OpenStarmap(spaceObjectId)')) {
    if ($browserModelsText.IndexOf($navigationToken,[StringComparison]::Ordinal) -lt 0) { throw "Trade station navigation contract missing: $navigationToken" }
}
foreach ($retired in @('TradeHeader6(', 'TradeStation6(')) {
    if ($browserModelsText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "Retired duplicate six-column Trade model returned: $retired" }
}
foreach ($token in @('private static bool UsePreviousTradeLayout = false;', '"UsePreviousTradeLayout"', 'Ui("mcm.trade_previous_layout")')) {
    if ($configurationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade layout preference contract missing: $token" }
}
foreach ($token in @('bool cards103 = current103 && !UsePreviousTradeLayout;', 'bool table103 = current103 && UsePreviousTradeLayout;', 'AddTradeStationTable103', 'ui.trade_previous_note')) {
    if ($tradePresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Optional previous Trade layout gate missing: $token" }
}
foreach ($token in @('bool sixColumns = !string.IsNullOrEmpty(line.Right);', 'ConfigureLootColumn(right, 553f, 135f, line.Right, 12f);')) {
    if ($tradeRowRendererText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Previous Trade table geometry contract missing: $token" }
}

# 1.0.3 uses one fixed-height pooled row with two explicit text lines and three wide regions.
# This keeps every station grouped while preserving the existing row action/hit target.
foreach ($geometryToken in @(
    'line.RowKind == BrowserRowKind.TradeStationCard',
    'SetBrowserRectPositionIfChanged(leftRt, showIcon ? 36f : 10f, 0f);',
    'SetBrowserRectSizeIfChanged(leftRt, showIcon ? 390f : 416f, leftRt.sizeDelta.y);',
    'ConfigureLootColumn(middle, 426f, 112f, line.ColumnReward, 11.75f);',
    'ConfigureLootColumn(right, 538f, 150f, line.Right, 11.5f);',
    'SetBrowserAutoSizingIfChanged(left, true);',
    'SetBrowserFontSizeMinIfChanged(left, 10.75f);'
)) {
    if ($tradeRowRendererText.IndexOf($geometryToken,[StringComparison]::Ordinal) -lt 0) { throw "Trade card/icon geometry contract missing: $geometryToken" }
}
$sharedRendererPartsText = [IO.File]::ReadAllText((Join-Path $sourceDir 'ModMain.BrowserRowRendererParts.cs'))
if ($sharedRendererPartsText.IndexOf('private static void RenderBrowserTradeRow',[StringComparison]::Ordinal) -ge 0) { throw 'Trade row renderer returned to shared BrowserRowRendererParts.cs.' }
if ($sharedRendererPartsText.IndexOf('BrowserRowKind.TradeStationCard',[StringComparison]::Ordinal) -lt 0) { throw 'Trade card dispatch missing from shared renderer orchestration.' }

foreach ($token in @('AddBrowserBarterRelations(sources, true)','AddBrowserBarterRelations(consumers, false)','Ui("ui.station_economy_recipe_output")','Ui("ui.station_economy_recipe_input")')) {
    if ($tradePresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade presentation clarity contract missing: $token" }
}
foreach ($token in @('private static void AddBrowserBarterRelations','unique.Sort(delegate(TradeRelation a, TradeRelation b)','new List<KeyValuePair<string, int>>(relation.RelatedItems)')) {
    if ($tradeFacadeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Trade relation-renderer clarity contract missing: $token" }
}
