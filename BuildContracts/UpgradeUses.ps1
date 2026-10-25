# Upgrade-use metadata is separate from finite ship-research requirements.
$upgradeReaderPath = Join-Path $sourceDir 'ModMain.GenomeUpgradeUseReader.cs'
$upgradeOwnerPath = Join-Path $sourceDir 'ModMain.GenomeUpgradeUses.cs'
$upgradePresentationPath = Join-Path $sourceDir 'ModMain.UpgradeUsesPresentation.cs'
foreach ($entry in @(@($upgradeReaderPath,160),@($upgradeOwnerPath,120),@($upgradePresentationPath,80))) {
    if (-not (Test-Path -LiteralPath $entry[0] -PathType Leaf)) { throw "Upgrade-use owner missing: $($entry[0])" }
    if (@(Get-Content -LiteralPath $entry[0]).Count -gt $entry[1]) { throw "Upgrade-use module budget exceeded: $($entry[0])" }
}
$upgradeReaderText = Read-Utf8Strict -Path $upgradeReaderPath
$upgradeOwnerText = Read-Utf8Strict -Path $upgradeOwnerPath
$upgradePresentationText = Read-Utf8Strict -Path $upgradePresentationPath
foreach ($token in @('MagnumProjectPrices','MagnumProjectPrice','ItemsGrades','"Mercenary"',
    'typeof(Dictionary<string, int>)','entry.Value > 0','knownItems.Contains(entry.Key)',
    'ambiguous duplicate genome price rows','MaximumPriceRows','MaximumMaterialRows')) {
    if (-not $upgradeReaderText.Contains($token)) { throw "Upgrade-use reader contract missing: $token" }
}
foreach ($forbidden in @('AddMagnumUseUnique','GetPrices(','new MagnumProject(','.SetValue(',
    'ExtractItemQuantities','ExtractKnownItemQuantitiesDeep','GetSafeMagnumRequired','GetVisibleMagnumRequired',
    'CreateForInventory','UnityEngine.Random','HarmonyPatch')) {
    if (($upgradeReaderText + $upgradeOwnerText + $upgradePresentationText).Contains($forbidden)) {
        throw "Upgrade-use metadata crossed a research/gameplay boundary: $forbidden"
    }
}
foreach ($token in @('SupportsGenomeUpgradeUseSchema','ResetGenomeUpgradeUseCache',
    'D72CD91F98DE4D6CCE8EE420AC1A906D89E68BA0CB164694D266B56F46339EBE',
    'GenomeUpgradeMaterialIds.Clear();','shipResearchTotal=unchanged')) {
    if (-not $upgradeOwnerText.Contains($token)) { throw "Upgrade-use lifecycle/gate contract missing: $token" }
}
foreach ($token in @('BuildBrowserMagnumResearch(itemId);','RefreshGenomeUpgradeUses(true);',
    'if (!ShowMagnumUses) return;','ui.upgrade_uses','ui.upgrade_genomes','ui.upgrade_uses_note')) {
    if (-not $upgradePresentationText.Contains($token)) { throw "Upgrade-use UI contract missing: $token" }
}
if (-not $browserPresentationText.Contains('private static void BuildBrowserMagnumResearch(string itemId)') -or
    $browserPresentationText.Contains('private static void BuildBrowserMagnum(string itemId)')) {
    throw 'Upgrade-use wrapper must have exactly one separate research builder.'
}
$upgradeMagnumText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Magnum.cs')
if (-not $upgradeMagnumText.Contains('ResetGenomeUpgradeUseCache();') -or
    -not $informationText.Contains('if (HasGenomeUpgradeUse(itemId)) return true;')) {
    throw 'Upgrade-use reset/discoverability integration missing.'
}
foreach ($lang in @('en','ru','de','pl','es','pt-BR','fr','zh-Hans','TranslationTemplate')) {
    $langText = Read-Utf8Strict -Path (Join-Path $content ("Localization/$lang.lang"))
    foreach ($key in @('ui.upgrade_uses','ui.upgrade_genomes','ui.upgrade_uses_note')) {
        if (-not $langText.Contains($key + "`t")) { throw "Upgrade-use translation missing: $lang / $key" }
    }
}
& (Join-Path $root 'Tests/Run-UpgradeUseTests.ps1') -SourceRoot $root
