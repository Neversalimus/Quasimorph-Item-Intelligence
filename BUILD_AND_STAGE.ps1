param(
    [ValidateSet('TEST','RELEASE')]
    [string]$Mode = 'TEST',
    [string]$GameRoot = '',
    [string]$WorkshopStage = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$DevWorkshopId = '3781927679'
$PublicWorkshopId = '3780078201'
$ExpectedReleaseVersion = '1.7.39'

function Resolve-GameRoot {
    param([string]$ExplicitRoot)
    if ($ExplicitRoot) {
        $candidate = [IO.Path]::GetFullPath($ExplicitRoot)
        if (Test-Path -LiteralPath (Join-Path $candidate 'Quasimorph_Data\Managed\Assembly-CSharp.dll')) { return $candidate }
        throw "Quasimorph not found at -GameRoot: $candidate"
    }
    $steamRoots = New-Object System.Collections.Generic.List[string]
    if (${env:ProgramFiles(x86)}) { $steamRoots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam')) }
    if ($env:ProgramFiles) { $steamRoots.Add((Join-Path $env:ProgramFiles 'Steam')) }
    foreach ($regPath in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam')) {
        try {
            $props = Get-ItemProperty -LiteralPath $regPath -ErrorAction Stop
            foreach ($name in @('SteamPath','InstallPath')) {
                if ($props.PSObject.Properties.Name -contains $name) {
                    $value = [string]$props.$name
                    if ($value) { $steamRoots.Add(($value -replace '/', '\')) }
                }
            }
        } catch { }
    }
    $candidates = New-Object System.Collections.Generic.List[string]
    foreach ($steamRoot in ($steamRoots | Select-Object -Unique)) {
        if (-not $steamRoot) { continue }
        $candidates.Add((Join-Path $steamRoot 'steamapps\common\Quasimorph'))
        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            try {
                $raw = Get-Content -LiteralPath $vdf -Raw
                foreach ($m in [regex]::Matches($raw, '"path"\s*"([^"]+)"')) {
                    $library = $m.Groups[1].Value -replace '\\\\', '\'
                    if ($library) { $candidates.Add((Join-Path $library 'steamapps\common\Quasimorph')) }
                }
            } catch { }
        }
    }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if ($candidate -and (Test-Path -LiteralPath (Join-Path $candidate 'Quasimorph_Data\Managed\Assembly-CSharp.dll'))) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }
    throw 'Quasimorph was not found automatically. Re-run with -GameRoot "X:\...\Quasimorph".'
}

function Resolve-Csc {
    foreach ($p in @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )) { if (Test-Path -LiteralPath $p) { return $p } }
    throw '.NET Framework C# compiler csc.exe was not found.'
}

function Read-Utf8Strict {
    param([string]$Path)
    $utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false,$true
    return [IO.File]::ReadAllText($Path, $utf8)
}

function Get-LangKeySet {
    param([string]$Path)
    $text = Read-Utf8Strict -Path $Path
    $keys = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::Ordinal)
    $duplicates = New-Object System.Collections.Generic.List[string]
    foreach ($line in ($text -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $trimmed = $line.TrimStart()
        if ($trimmed.StartsWith('#') -or $trimmed.StartsWith('@')) { continue }
        $tab = $line.IndexOf("`t")
        if ($tab -le 0) { throw "Malformed localization line in ${Path}: $line" }
        $key = $line.Substring(0,$tab).Trim()
        if (-not $keys.Add($key)) { $duplicates.Add($key) }
    }
    if ($duplicates.Count -gt 0) { throw "Duplicate localization key(s) in ${Path}: $($duplicates -join ', ')" }
    return @($keys)
}

function Assert-EqualKeySets {
    param($A, $B, [string]$AName, [string]$BName)
    foreach ($key in $A) { if (-not $B.Contains($key)) { throw "Localization parity: ${BName} missing key $key from ${AName}" } }
    foreach ($key in $B) { if (-not $A.Contains($key)) { throw "Localization parity: ${AName} missing key $key from ${BName}" } }
}

function Assert-No-PlayerFacingDevText {
    param([string]$Path)
    $text = Read-Utf8Strict -Path $Path
    foreach ($line in ($text -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $trimmed = $line.TrimStart()
        if ($trimmed.StartsWith('#') -or $trimmed.StartsWith('@')) { continue }
        $tab = $line.IndexOf("`t")
        if ($tab -le 0) { continue }
        $value = $line.Substring($tab + 1)
        if ($value -match '(?i)\btest[0-9]*\b|\baudit\b|\bdebug\b|\bdev(?:elopment)?\b|тест[0-9]*|аудит|отлад|текущей версии игры|patched further|добивать патчами|internal build|runtime audit|diagnostic only|\bv[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:-[a-z0-9.-]+)?\b') {
            throw "Player-facing development text found in ${Path}: $line"
        }
    }
}

function Assert-No-PowerShellVariableColonHazards {
    param([string]$Path)
    $raw = Get-Content -LiteralPath $Path -Raw
    $allowedScopes = @('env','global','script','local','private','using','variable','function')
    foreach ($m in [regex]::Matches($raw, '(?<!\{)\$([A-Za-z_][A-Za-z0-9_]*):(?!:)')) {
        $name = $m.Groups[1].Value
        if ($allowedScopes -notcontains $name) {
            throw ('PowerShell parser hazard detected in ' + $Path + ': $' + $name + ': . Use ${' + $name + '}: inside interpolated strings.')
        }
    }
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $root 'Source'
$content = Join-Path $root 'WORKSHOP_CONTENT'
$manifest = Join-Path $content 'modmanifest.json'
$regressionMatrix = Join-Path $root 'RELEASE_REGRESSION_V17383.md'
$architectureGuard = Join-Path $root 'READ_ONLY_ARCHITECTURE.md'

foreach ($required in @($sourceDir,$manifest,$regressionMatrix,$architectureGuard)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required path missing: $required" }
}

Assert-No-PowerShellVariableColonHazards -Path $MyInvocation.MyCommand.Path

$sourceFiles = @(Get-ChildItem -LiteralPath $sourceDir -Filter '*.cs' -File -Recurse | Sort-Object FullName)
if ($sourceFiles.Count -lt 30) { throw "Source decomposition contract failed: expected at least 30 C# files, found $($sourceFiles.Count)." }
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ItemIntelligence.cs')) { throw 'Monolithic ItemIntelligence.cs must not return in the hardening branch.' }
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.AmmoDisassembly.cs')) { throw 'Architecture regression: Ammo and Disassembly must remain separate feature files.' }
foreach ($moduleFile in @(
    'ModMain.Ammo.cs','ModMain.WeaponModes.cs','ModMain.WeaponModePresentation.cs','ModMain.WeaponModeLocalization.cs','ModMain.WeaponModeScatter.cs','ModMain.WeaponModeDamagePerAP.cs','ModMain.Disassembly.cs','ModMain.BrowserModels.cs','ModMain.BrowserState.cs',
    'ModMain.BrowserCatalog.cs','ModMain.BrowserPresentation.cs','ModMain.BrowserTextLayout.cs','ModMain.BrowserCatalogPresentation.cs','ModMain.BrowserLazyUi.cs',
    'ModMain.InterfaceIcons.cs',
    'ModMain.LootIndexes.cs','ModMain.LootContainerProfiles.cs','ModMain.LootContainerIcons.cs',
    'ModMain.LootPresentation.cs','ModMain.LootEnemyPresentation.cs','ModMain.LootGeneralSpawn.cs','ModMain.LootModifiers.cs','ModMain.LootModifierRuntime.cs',
    'ModMain.Configuration.cs','ModMain.CoreIndexes.cs','ModMain.DataAccess.cs','ModMain.FeatureLifecycle.cs',
    'ModMain.Icons.cs','ModMain.Information.cs','ModMain.Magnum.cs','ModMain.RuntimeServices.cs','ModMain.TradeMissionStatus.cs','ModMain.TradeFreshness.cs',
    'ModMain.StarmapUiResolution.cs','ModMain.BrowserAdvancedSearch.cs','ModMain.ModderMode.cs')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir $moduleFile) -PathType Leaf)) {
        throw "Architecture contract missing source module: $moduleFile"
    }
}
$runtimePath = Join-Path $sourceDir 'ModMain.Runtime.cs'
if (-not (Test-Path -LiteralPath $runtimePath -PathType Leaf)) { throw 'ModMain.Runtime.cs missing.' }
$runtimeLines = (Get-Content -LiteralPath $runtimePath).Count
if ($runtimeLines -gt 6650) { throw "Source decomposition regressed: ModMain.Runtime.cs has $runtimeLines lines; v1.7.36-test18 requires <= 6650." }
$browserUiPath = Join-Path $sourceDir 'ModMain.BrowserUI.cs'
$browserUiLines = (Get-Content -LiteralPath $browserUiPath).Count
$browserPresentationPath = Join-Path $sourceDir 'ModMain.BrowserPresentation.cs'
$browserPresentationLines = (Get-Content -LiteralPath $browserPresentationPath).Count
$browserCatalogPath = Join-Path $sourceDir 'ModMain.BrowserCatalog.cs'
$browserCatalogLines = (Get-Content -LiteralPath $browserCatalogPath).Count
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
$lootPresentationPath = Join-Path $sourceDir 'ModMain.LootPresentation.cs'
$lootPresentationLines = (Get-Content -LiteralPath $lootPresentationPath).Count
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
if ($browserUiLines -gt 1650) { throw "Browser controller ownership regressed: ModMain.BrowserUI.cs has $browserUiLines lines; v1.7.36-test18 requires <= 1650." }
$browserUiText = [IO.File]::ReadAllText($browserUiPath)
foreach ($token in @('HasObservedInspectorItemUi','directMissionHotkeyBootstrap','!directMissionHotkeyBootstrap')) {
    if ($browserUiText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Direct-mission first-hotkey regression guard missing: $token" }
}
if ($browserPresentationLines -gt 2350) { throw "Browser presentation ownership regressed: ModMain.BrowserPresentation.cs has $browserPresentationLines lines; v1.7.36-test18 requires <= 2350." }
$browserLazyUiPath = Join-Path $sourceDir 'ModMain.BrowserLazyUi.cs'
$browserLazyUiText = [IO.File]::ReadAllText($browserLazyUiPath)
$browserLazyUiLines = (Get-Content -LiteralPath $browserLazyUiPath).Count
if ($browserLazyUiLines -gt 180) { throw "Browser lazy-UI ownership regressed: ModMain.BrowserLazyUi.cs has $browserLazyUiLines lines; v1.7.37-test10 requires <= 180." }
if ($browserCatalogLines -gt 550) { throw "Browser catalog controller ownership regressed: ModMain.BrowserCatalog.cs has $browserCatalogLines lines; v1.7.36-test18 requires <= 550." }
if ($browserCatalogPresentationLines -gt 575) { throw "Browser catalog presentation ownership regressed: ModMain.BrowserCatalogPresentation.cs has $browserCatalogPresentationLines lines; v1.7.36-test18 requires <= 575." }
if ($interfaceIconsLines -gt 875) { throw "Interface-icon presentation ownership regressed: ModMain.InterfaceIcons.cs has $interfaceIconsLines lines; v1.7.36-test18 requires <= 875." }
if ($lootLines -gt 550) { throw "Loot facade/model ownership regressed: ModMain.Loot.cs has $lootLines lines; v1.7.36-test18 requires <= 550." }
if ($lootIndexesLines -gt 1350) { throw "Loot index ownership regressed: ModMain.LootIndexes.cs has $lootIndexesLines lines; v1.7.36-test18 requires <= 1350." }
if ($lootContainerProfilesLines -gt 300) { throw "Loot container-profile ownership regressed: ModMain.LootContainerProfiles.cs has $lootContainerProfilesLines lines; v1.7.36-test18 requires <= 300." }
if ($lootContainerIconsLines -gt 1400) { throw "Loot container-icon ownership regressed: ModMain.LootContainerIcons.cs has $lootContainerIconsLines lines; v1.7.36-test18 requires <= 1400." }
if ($lootPresentationLines -gt 950) { throw "Loot presentation ownership regressed: ModMain.LootPresentation.cs has $lootPresentationLines lines; v1.7.36-test18 requires <= 950." }
if ($lootModifiersLines -gt 320) { throw "Loot modifier UI ownership regressed: ModMain.LootModifiers.cs has $lootModifiersLines lines; v1.7.38-test8 requires <= 320." }
if ($lootEnemyPresentationLines -gt 220) { throw "Loot enemy presentation ownership regressed: ModMain.LootEnemyPresentation.cs has $lootEnemyPresentationLines lines; v1.7.38-test8 requires <= 220." }
if ($lootModifierRuntimeLines -gt 240) { throw "Loot modifier runtime ownership regressed: ModMain.LootModifierRuntime.cs has $lootModifierRuntimeLines lines; v1.7.38-test8 requires <= 240." }
if ($tradeLines -gt 900) { throw "Trade ownership regressed: ModMain.Trade.cs has $tradeLines lines; v1.7.36-test18 requires <= 900." }
if ($tradeFreshnessLines -gt 100) { throw "TradeFreshness line budget exceeded: $tradeFreshnessLines/100" }
if ($disassemblyLines -gt 460) { throw "Disassembly ownership regressed: ModMain.Disassembly.cs has $disassemblyLines lines; v1.7.36-test18 requires <= 460 after legacy scanner removal." }
if ($weaponModesLines -gt 160) { throw "Weapon-mode data ownership regressed: ModMain.WeaponModes.cs has $weaponModesLines lines; v1.7.37-test2 requires <= 160." }
if ($weaponModePresentationLines -gt 300) { throw "Weapon-mode presentation ownership regressed: ModMain.WeaponModePresentation.cs has $weaponModePresentationLines lines; v1.7.37-test8 requires <= 300." }
if ($weaponModeLocalizationLines -gt 180) { throw "Weapon-mode localization ownership regressed: ModMain.WeaponModeLocalization.cs has $weaponModeLocalizationLines lines; v1.7.37-test8 requires <= 180." }
if ($weaponModeScatterLines -gt 190) { throw "Weapon-mode scatter ownership regressed: ModMain.WeaponModeScatter.cs has $weaponModeScatterLines lines; v1.7.37-test8 requires <= 190." }
if ($weaponModeDamagePerApLines -gt 260) { throw "Weapon-mode Damage/AP ownership regressed: ModMain.WeaponModeDamagePerAP.cs has $weaponModeDamagePerApLines lines; v1.7.39 requires <= 260." }

$runtimeText = Get-Content -LiteralPath $runtimePath -Raw
$coreIndexesPath = Join-Path $sourceDir 'ModMain.CoreIndexes.cs'
$coreIndexesText = Get-Content -LiteralPath $coreIndexesPath -Raw
$informationPath = Join-Path $sourceDir 'ModMain.Information.cs'
$informationText = Get-Content -LiteralPath $informationPath -Raw
$coreIndexesLines = (Get-Content -LiteralPath $coreIndexesPath).Count
$informationLines = (Get-Content -LiteralPath $informationPath).Count
if ($coreIndexesLines -gt 650) { throw "Core-index ownership regressed: ModMain.CoreIndexes.cs has $coreIndexesLines lines; v1.7.36-test18 requires <= 650." }
if ($informationLines -gt 220) { throw "Information policy regressed: ModMain.Information.cs has $informationLines lines; v1.7.36-test18 requires <= 220." }
$stateOwnershipContracts = @{
    'ModMain.Starmap.cs' = @('_pendingStarmapTargetId','StarmapSourceViewVisualStates')
    'ModMain.Trade.cs' = @('_marketItemId','MarketStations','MarketFactionRelations','BarterSources','BarterConsumers','_stationsState','_stationSystem','_tradeSystem','_worldPricesSystem','_itemsPrices','_marketEmptyRetryCooldown','_stationSchemaLogged')
    'ModMain.Factions.cs' = @('RuntimeFactionsById','FactionTechUnlocksByItem','_secretDataSelectedFactionId','_secretDataContractLogged','_factionTradeSchemaLogged','_factionsState','_difficultyState')
    'ModMain.Magnum.cs' = @('_magnumProgression','_magnumLightLookupAttempted','_runtimeMagnumIndexBuilt','MagnumUses')
    'ModMain.RuntimeServices.cs' = @('_customResources','_runtimeResolveOwnerTypes','_runtimeFallbackResolveActive','_stateServicesResolved')
    'ModMain.Ammo.cs' = @('WeaponsByItem','WeaponModeRecordsById','AmmoWarmupItems','WeaponModeItemIdByKey')
    'ModMain.WeaponModeScatter.cs' = @('WeaponModeWeaponRecordsByItem','_weaponModeScatterFormulaLogged','_weaponModeCreatures','WeaponModeScatterLoggedKeys')
    'ModMain.WeaponModes.cs' = @('WeaponModeStatsByRawId','WeaponModeStatsByKey')
    'ModMain.Disassembly.cs' = @('DisassemblyOutputsByItem','DisassemblySourcesByOutputItem','DisassemblyWarmupItems')
    'ModMain.LootIndexes.cs' = @('LootContainerSourcesByItem','_lootEnemyContextIndexReady','_lootWarmupActive','_lootContainerDropCollection')
    'ModMain.LootContainerProfiles.cs' = @('_lootContainerProfileCount','_lootContainerMappedProfileCount','_lootContainerFallbackProfileCount','_lootContainerIndexedProfileCount','_lootContainerItemLinkCount','LootMultiProfilePhysicalContainerIds','LootFallbackContainerProfileIds')
    'ModMain.LootContainerIcons.cs' = @('LootContainerIconsById','_lootContainerRendererCatalog','LootContainerRecordsById')
    'ModMain.LootPresentation.cs' = @('_lootProgressRoot','_lootProgressLastVisible','LootDisplayNameCache')
    'ModMain.LootEnemyPresentation.cs' = @('LootEnemyRegularPresentationBuffer','LootEnemyCorpseBonusPresentationBuffer')
    'ModMain.LootGeneralSpawn.cs' = @('LootGeneralSpawnContainersByItem','_lootGeneralSpawnPairCount','LootGeneralSpawnManualContainerBuffer','LootGeneralSpawnAdditionalContainerBuffer')
    'ModMain.LootModifiers.cs' = @('_lootModifierUseManual','_lootManualMarauderLevel','_lootManualOrganization','_lootManualFieldMedic','LootActiveContainerPresentationBuffer')
    'ModMain.LootModifierRuntime.cs' = @('_lootModifierTypesResolved','_lootPerkSumMethod','_lootImplantBaseProgression')
    'ModMain.BrowserState.cs' = @('_browserTab','_browserSearchInput','_browserCatalogOpen','_browserCatalogScope','BrowserFavoriteItemIds','BrowserRecentItemIds','_browserBackButton','BrowserPageByTab','BrowserLines','_lastHoveredItemId','_itemPointerScope','_inspectorRoot')
    'ModMain.Configuration.cs' = @('_configLoaded','_mcmRegistered','EnableItemIntelligence','InspectorKeyCode','ShowInterfaceIcons')
    'ModMain.InterfaceIcons.cs' = @('BrowserInterfaceIconSprites','BrowserInterfaceIconBindings','_browserInterfaceSearchIcon')
    'ModMain.DataAccess.cs' = @('InstanceFlags','StaticFlags','ReadableMemberCache','InstanceMemberLookupCache')
    'ModMain.Icons.cs' = @('ItemSmallIcons','ItemSmallIconMisses','VanillaObservedItemIcons')
    'ModMain.Compatibility.cs' = @('_compatStaticChecked','_compatCore','CompatibilityReasons','RuntimeBoundaryWarningLogs')
    'ModMain.Localization.cs' = @('LocalizationCache','ExternalUiTranslations','ResolvedUiTextCache')
    'ModMain.CoreIndexes.cs' = @('_indexesBuilt','PriceByItem','UsedInRecipes','CraftedFromRecipes','RecipesById','KnownItemIds','BarterItemIds','ItemRecordsById','SpaceObjectRecordsById','ItemDataSourceNames')
    'ModMain.ItemMetadata.cs' = @('CanonicalItemMetadataRecordsById','ExactItemTechLevelsById','UnresolvedItemMetadataIds')
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

$runtimeDecompositionContracts = @{
    'ModMain.Configuration.cs' = @('EnsureConfigLoaded','SaveConfig','TryRegisterMcm','OnMcmConfigSaved')
    'ModMain.DataAccess.cs' = @('GetStaticMember','GetMember','FindCachedMember','EnumerateData','ExtractKnownItemQuantitiesDeep','ExtractItemQuantities','GetReadableMembers')
    'ModMain.Icons.cs' = @('TryResolveItemSmallIcon','TryResolveCanonicalItemSmallIcon','TryResolveCompositeInventoryIcon','CaptureVanillaItemSlotIcon','ResolveSpriteDeep')
    'ModMain.StarmapUiResolution.cs' = @('FindActiveDecisionOverlayByStructure','FindActiveUnityObject','FindActiveArsenalScreen','IsUiObjectActuallyUsable')
    'ModMain.CoreIndexes.cs' = @('BuildIndexesSafe','RunIndexStage','EnsureRuntimeIndexesReady','BuildSpaceObjectIndex','ClearIndexes','BuildItemCoverageIndex','IndexItemRecords','BuildMagnumIndex','BuildMagnumPriceRecordLookup','BuildMagnumProjectPriceIndex','BuildGenericMagnumCostIndex','AddMagnumUseUnique','BuildRecipeIndex','BuildBarterIndex')
    'ModMain.Information.cs' = @('HasInspectorData','HasVisibleMagnumUses','GetVisibleMagnumRequired','GetMagnumSnapshot','UpdateBrowserStats','FormatVisibleTradeCounts')
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
$weaponModesText = Get-Content -LiteralPath $weaponModesPath -Raw
$weaponModePresentationText = Get-Content -LiteralPath $weaponModePresentationPath -Raw
$weaponModeLocalizationText = Get-Content -LiteralPath $weaponModeLocalizationPath -Raw
$weaponModeScatterText = Get-Content -LiteralPath $weaponModeScatterPath -Raw
$browserCatalogText = Get-Content -LiteralPath $browserCatalogPath -Raw
$browserCatalogPresentationText = Get-Content -LiteralPath $browserCatalogPresentationPath -Raw
$interfaceIconsText = Get-Content -LiteralPath $interfaceIconsPath -Raw
$browserAllText = $browserUiText + $browserPresentationText + $browserCatalogText + $browserCatalogPresentationText

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
if ($browserPresentationText -match 'BrowserLine\.WeaponMode\(mode\.Label') {
    throw 'Weapon-mode localization regression: cached warmup-time mode.Label must never be rendered directly.'
}
if ($browserPresentationText -notmatch 'ResolveWeaponModeDisplayLabel\(mode\)') {
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
    throw 'Weapon-mode inline stat rows returned; v1.7.37-test8 requires hover-only stats.'
}
if ($weaponModePresentationText -match 'ui\.mode_shot_delay') {
    throw 'Weapon-mode hover tooltip must not expose non-vanilla shot-delay presentation.'
}
if ($weaponModePresentationText -notmatch 'TryCalculateVanillaFiremodeScatter\(modeKey, stats, out scatter\)' -or
    $weaponModePresentationText -notmatch 'ui\.mode_scatter') {
    throw 'Weapon-mode scatter regression: test8 must present the exact audited vanilla scatter path from the hover tooltip.'
}
if ($weaponModeScatterText -notmatch 'FireMode\.ScatterAngle\+WeaponRecord\.BonusScatterAngle' -or
    $weaponModeScatterText -notmatch 'GetScatterAngleMult\(record\)' -or
    $weaponModeScatterText -notmatch 'GetSimpleRecord<WeaponRecord>') {
    throw 'Weapon-mode scatter regression: exact vanilla IL formula/resolver contract is incomplete.'
}
if ($weaponModeScatterText -match 'BuildRelevantItemGraph|GetReadableMembers|GetProperties\(|GetFields\(') {
    throw 'Weapon-mode scatter performance regression: test8 forbids catalog/graph/reflection scans.'
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
    'CreateBrowserText','CreateBrowserPageScrollbar','RenderBrowser','RenderBrowserRowsOnly',
    'UpdateBrowserChromeLocalization','UpdateBrowserTabs','BuildBrowserOverview','BuildBrowserMagnum',
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
$lootPresentationText = Get-Content -LiteralPath $lootPresentationPath -Raw
$lootAllText = $lootText + $lootIndexesText + $lootContainerProfilesText + $lootContainerIconsText + $lootPresentationText
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
foreach ($iconType in @('LootContainerRendererSnapshot','LootContainerIconCandidate')) {
    if ($lootText -match ('\bclass\s+' + [regex]::Escape($iconType) + '\b')) {
        throw "Architecture regression: container-icon type $iconType returned to ModMain.Loot.cs."
    }
    if ($lootContainerIconsText -notmatch ('\bclass\s+' + [regex]::Escape($iconType) + '\b')) {
        throw "Architecture container-icon type contract missing $iconType."
    }
}

$sourceText = ($sourceFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
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
if ($sourceText -match '_browserTab\s*(?:==|!=)\s*[0-6]\b') { throw 'Architecture regression: raw browser tab numbers must not return; use BrowserTabId.' }
if ($sourceText -match 'rawMany\.Count\s*>\s*currentRaw\.Count') { throw 'Chip probability regression: longest UnlockIds pool heuristic must not return.' }
if ($sourceText -match '(?i)longest candidate once per chip') { throw 'Chip probability regression: legacy longest-pool comment/path returned.' }
$silentCatchCount = ([regex]::Matches($sourceText, 'catch\s*\{\s*\}')).Count
if ($silentCatchCount -gt 130) { throw "Silent catch budget regressed: $silentCatchCount > 130. Classify critical boundaries instead of adding opaque catches." }
foreach ($architectureToken in @('enum BrowserTabId','enum BrowserRowKind','BrowserRowKind.ChipNote','BrowserTabId.Loot')) {
    if ($sourceText -notmatch ([regex]::Escape($architectureToken))) { throw "Architecture contract token missing: $architectureToken" }
}

$ammoText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Ammo.cs') -Raw
$tradeText = Get-Content -LiteralPath $tradePath -Raw
$configurationText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Configuration.cs') -Raw
foreach ($tradeClarityToken in @(
    'AddBrowserBarterRelations(sources, true)',
    'AddBrowserBarterRelations(consumers, false)',
    'Ui("ui.station_economy_recipe_output")',
    'Ui("ui.station_economy_recipe_input")',
    'unique.Sort(delegate(TradeRelation a, TradeRelation b)',
    'new List<KeyValuePair<string, int>>(relation.RelatedItems)'
)) {
    if ($tradeText.IndexOf($tradeClarityToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Trade clarity contract missing: $tradeClarityToken"
    }
}
if ($runtimeText -match '\bclass\s+TradeRelation\b' -or
    $tradeText -notmatch '\bclass\s+TradeRelation\b') {
    throw 'Trade ownership regression: TradeRelation must remain owned by ModMain.Trade.cs.'
}
if ($runtimeText.IndexOf('GetUniqueRelationCount(',[StringComparison]::Ordinal) -ge 0 -or
    $informationText.IndexOf('GetUniqueRelationCount(',[StringComparison]::Ordinal) -lt 0) {
    throw 'Information ownership regression: visible Trade relation counts must remain in ModMain.Information.cs.'
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
    $coreIndexesText.IndexOf('new TradeRelation(id, input.Value, outputs)',[StringComparison]::Ordinal) -lt 0 -or
    $coreIndexesText.IndexOf('new TradeRelation(id, output.Value, inputs)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Trade localization contract failed: Barter index must store stable related item ids, never localized labels.'
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
    'if (!ShowFutureMagnumUses) continue;',
    'FormatVisibleTradeCounts(sources, consumers)'
)) {
    if ($sourceText.IndexOf($informationToken,[StringComparison]::Ordinal) -lt 0) {
        throw "MCM Information visibility contract token missing: $informationToken"
    }
}
if ($tradeText.IndexOf('if (!ShowSources && !ShowTradeInformation) return;',[StringComparison]::Ordinal) -lt 0 -or
    $ammoText.IndexOf('if (!ShowAmmoRelations) return;',[StringComparison]::Ordinal) -lt 0 -or
    $lootPresentationText.IndexOf('if (!ShowSources) return;',[StringComparison]::Ordinal) -lt 0) {
    throw 'MCM Information detail-renderer hard gate is incomplete.'
}
if ($tradeText.IndexOf('if (!IsStarmapExperimentSpaceContext(out spaceContextReason)) return "—";',[StringComparison]::Ordinal) -lt 0) {
    throw 'Trade travel regression: Dungeon must fail closed before the vanilla space-only calculation.'
}
if ($sourceText.IndexOf('ItemSlot hover had no resolvable item id',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('(record != null || item != null) && _itemHoverResolveWarnings < 4',[StringComparison]::Ordinal) -lt 0) {
    throw 'Hover diagnostic regression: empty/unbound vanilla controls must not emit false compatibility warnings.'
}
if ($informationText.IndexOf('overview.trade.sources_prefix',[StringComparison]::Ordinal) -ge 0 -or
    $informationText.IndexOf('overview.trade.consumers_separator',[StringComparison]::Ordinal) -ge 0) {
    throw 'Information UI regression: the shared Trade summary must stay compact (sources/consumers numeric pair).'
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
    'LootContainerProfiles','LootContainerMappedProfiles','LootContainerFallbackProfiles','LootContainerFallbackProfileIds',
    'LootContainerIndexedProfiles','LootContainerEmptyProfiles','LootContainerDescriptorLinks','LootContainerItemLinks',
    'LootMultiProfilePhysicalContainers'
)) {
    if ($hardeningText.IndexOf($lootProfileDiagnostic,[StringComparison]::Ordinal) -lt 0) {
        throw "Loot container-profile diagnostic contract missing: $lootProfileDiagnostic"
    }
}

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
# v1.7.37-test10 release-polish lazy UI gates.
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


# v1.7.38-test5: runtime audit writers removed after acceptance; no automatic Downloads output.

# v1.7.38-test5 general-spawn Loot coverage gates.
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

# v1.7.38-test5 BuildFix2 release-polish gates.
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.LootFullAudit.cs')) { throw 'Release polish regression: LootFullAudit runtime writer must stay removed.' }
if (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.LootContextAudit.cs')) { throw 'Release polish regression: LootContextAudit runtime writer must stay removed.' }
$lootFacadeText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Loot.cs') -Raw
foreach ($forbiddenAuditToken in @('TryWriteFullLootAuditReports','ResetLootFullAuditState','QII_LOOT_CONTEXT_AUDIT_TEST4')) {
    if ($lootFacadeText.IndexOf($forbiddenAuditToken,[StringComparison]::Ordinal) -ge 0) { throw "Release polish regression: automatic Loot audit token returned: $forbiddenAuditToken" }
}
foreach ($obsoleteUiToken in @('loot.note.container_identity')) {
    if ($lootPresentationText.IndexOf($obsoleteUiToken,[StringComparison]::Ordinal) -ge 0) { throw "Release polish regression: obsolete Loot note returned: $obsoleteUiToken" }
}

# v1.7.38-test6/test7 Loot modifier simulation gates.
$lootModifiersText = Get-Content -LiteralPath $lootModifiersPath -Raw
$lootModifierRuntimeText = Get-Content -LiteralPath $lootModifierRuntimePath -Raw
$lootModifierAllText = $lootModifiersText + "`n" + $lootModifierRuntimeText
foreach ($token in @(
    'LootModifierActionPrefix',
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
    'FormatLootContainerEffectiveChance(source, lootModifiers.StorageExpected)',
    'AppendLootEnemySections(rawEnemies, lootModifiers, ru, ref any)'
)) {
    if ($lootPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Loot modifier presentation token missing: $token" }
}
$browserUiText = Get-Content -LiteralPath $browserUiPath -Raw
foreach ($token in @('LootModifierActionPrefix','HandleLootModifierAction(line.ActionSpaceObjectId)')) {
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

# v1.7.38-test7 corpse-bonus split + render/runtime performance gates.
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

# v1.7.38-test8 performance hardening gates.
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
foreach ($token in @('TryGetContainerItemCountFast','TryGetContainerItemCountDeep','ContainerDeepSearchVisited','GetCachedContainerCountMethods','ContainerCountInvokeArgs','ItemIdNestedMemberNames')) {
    if ($dataAccessText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Data-access performance contract token missing: $token" }
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


# v1.7.38.1 production runtime cleanup gates.
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
if ($browserPresentationText.IndexOf('left.raycastTarget = line != null && line.LeftMode == 1 && IsKnownItemId(line.Left);',[StringComparison]::Ordinal) -lt 0) {
    throw 'Contextual item-navigation regression: item-name hit target is missing.'
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
if ($compatibilityText.IndexOf('IsHealthyVerifiedCompatibilityState',[StringComparison]::Ordinal) -lt 0) {
    throw 'Healthy compatibility report I/O cleanup contract missing.'
}
$cleanupLocDir = Join-Path (Join-Path $root 'WORKSHOP_CONTENT') 'Localization'
foreach ($langPath in @(
    (Join-Path $cleanupLocDir 'en.lang'),
    (Join-Path $cleanupLocDir 'ru.lang'),
    (Join-Path $cleanupLocDir 'TranslationTemplate.lang'))) {
    if ((Get-Content -LiteralPath $langPath -Raw).IndexOf('v1.7.38-test1',[StringComparison]::Ordinal) -ge 0) { throw "Release localization still contains test marker: $langPath" }
}

# v1.7.38 stable release-polish gates.
# Current Quasimorph 1.0.2.573s.9f33900 was runtime-audited against this exact Assembly-CSharp hash.
if ($sourceText.IndexOf('EE9214048DE649AA5C7E913F0CAFBCA44B8A1E164520D74DE72AEA11006C2729',[StringComparison]::Ordinal) -lt 0) {
    throw 'Release regression: current verified Assembly-CSharp SHA256 is missing.'
}
if ($lootGeneralSpawnText.IndexOf('_lootGeneralSpawnPairCount = CountLootGeneralSpawnPairs();',[StringComparison]::Ordinal) -lt 0 -or
    $lootGeneralSpawnText.IndexOf('private static int CountLootGeneralSpawnPairs()',[StringComparison]::Ordinal) -lt 0) {
    throw 'Release regression: general-spawn diagnostics must recount the final authoritative dictionary.'
}
if ($lootModifiersText.IndexOf('if (ModderMode)',[StringComparison]::Ordinal) -lt 0 -or
    $lootModifiersText.IndexOf('[ItemIntelligence][LootModifiers][Perf]',[StringComparison]::Ordinal) -lt 0) {
    throw 'Release regression: Loot modifier performance diagnostics must remain ModderMode-gated.'
}

# v1.7.38-test1 Modder Mode / advanced-search semantic gates.
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

# v1.7.39-test4 shared TechLevel resolver gates.
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


# v1.7.39-test1 exact nominal weapon damage/AP gates.
$weaponModeDamagePerApPath = Join-Path $sourceDir 'ModMain.WeaponModeDamagePerAP.cs'
$weaponModeDamagePerApText = Get-Content -LiteralPath $weaponModeDamagePerApPath -Raw
foreach ($token in @(
    'TryCalculateWeaponModeDamagePerAp',
    'weapon.DefaultAmmoId',
    'ammo.DamageMult',
    'ammo.BulletCastsPerShot',
    'stats.WeaponCastsCount',
    'Mathf.RoundToInt(baseMin * perFragmentMult)',
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


# v1.7.39-test4 user-facing polish gates.
$lootPresentationText = Get-Content -LiteralPath $lootPresentationPath -Raw
if ($browserPresentationText.IndexOf('BrowserLine.FullNote(Ui("ui.no_related_magnum_research_was_found"))',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test4 regression: Magnum empty-state must use the full-width note row.'
}
if ($lootPresentationText.IndexOf('AddWrappedBrowserNote(key, 110, 120);',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test4 regression: Loot helper-note wrap width is not the widened full-row contract.'
}
$ruLocalizationText = Get-Content -LiteralPath (Join-Path $content 'Localization\ru.lang') -Raw
$enLocalizationText = Get-Content -LiteralPath (Join-Path $content 'Localization\en.lang') -Raw
foreach ($forbiddenVisibleToken in @('AdditItemClasses','RollExpectedCount')) {
    if ($ruLocalizationText.IndexOf($forbiddenVisibleToken,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $enLocalizationText.IndexOf($forbiddenVisibleToken,[StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Test4 user-facing text regression: internal token remains visible: $forbiddenVisibleToken"
    }
}
foreach ($retainedToken in @('ui.item_id','ui.modder_search_syntax','ui.faction_technology','ui.barter_give_this_item','ui.barter_receive_this_item')) {
    if (($ruLocalizationText + $enLocalizationText).IndexOf($retainedToken,[StringComparison]::Ordinal) -lt 0) {
        throw "Test4 scope regression: intentionally retained UI contract missing: $retainedToken"
    }
}

# v1.7.39-test6 exact direct-disassembly chance gates.
$disassemblyChancePath = Join-Path $sourceDir 'ModMain.DisassemblyChance.cs'
if (-not (Test-Path -LiteralPath $disassemblyChancePath)) { throw 'Test6 exact disassembly chance owner is missing.' }
$disassemblyChanceText = Get-Content -LiteralPath $disassemblyChancePath -Raw
foreach ($token in @('DeathGiftId','GetDirectDisassemblyChancePercent','IsRandomDirectDisassemblyItem','return 100f')) {
    if ($disassemblyChanceText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test6 exact disassembly chance token missing: $token" }
}
if ($disassemblyText.IndexOf('GetDirectDisassemblyChancePercent(itemId)',[StringComparison]::Ordinal) -lt 0 -or
    $disassemblyText.IndexOf('IsRandomDirectDisassemblyItem(itemId)',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test6 exact disassembly chance is not connected to canonical forward presentation data.'
}
if ($sourceText.IndexOf('LogTest5DisassemblyExactAudit();',[StringComparison]::Ordinal) -ge 0 -or
    (Test-Path -LiteralPath (Join-Path $sourceDir 'ModMain.Test5DisassemblyExactAudit.cs'))) {
    throw 'Test6 regression: accepted test5 runtime probe must be removed.'
}


# v1.7.39-test7 player-facing Loot/Trade clarity gates.
$tradeText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Trade.cs') -Raw
if ($enLocalizationText.IndexOf("ui.context`tLOCATION TYPE",[StringComparison]::Ordinal) -lt 0 -or
    $ruLocalizationText.IndexOf("ui.context`tТИП ЛОКАЦИИ",[StringComparison]::Ordinal) -lt 0) {
    throw 'Test7 regression: Loot context header must be player-facing LOCATION TYPE / ТИП ЛОКАЦИИ.'
}
foreach ($token in @('ui.current_stock_may_change_during_travel','BrowserLine.FullSection')) {
    if ($tradeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test7 trade clarity token missing: $token" }
}
if ($browserPresentationText.IndexOf('left.enableAutoSizing = true;',[StringComparison]::Ordinal) -lt 0 -or
    $browserPresentationText.IndexOf('left.fontSizeMin = 12.5f;',[StringComparison]::Ordinal) -lt 0 -or
    $browserPresentationText.IndexOf('left.fontSizeMax = 17f;',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test7 regression: full-width localized trade section no longer has fit-safe TMP auto-sizing.'
}


# v1.7.39-test8 safe player-facing polish gates.
$specialVisualPath = Join-Path $sourceDir 'ModMain.LootContainerSpecialVisuals.cs'
if (-not (Test-Path -LiteralPath $specialVisualPath)) { throw 'Test8 special visual owner is missing.' }
$specialVisualText = Get-Content -LiteralPath $specialVisualPath -Raw
foreach ($token in @('AztecAltar','exact-altar-renderer-not-loaded','exactSemantic=true')) {
    if ($specialVisualText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test8 AztecAltar safe visual token missing: $token" }
}
foreach ($token in @("ui.qty_rolls`tКОЛ./БРОСКИ", "ui.rolls_2`t броск.", "ui.per_roll`tЗА БРОСОК")) {
    if ($ruLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test8 RU Loot terminology token missing: $token" }
}
if ($ruLocalizationText.IndexOf('РОЛЛ',[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
    $ruLocalizationText.IndexOf(' ролл.',[StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'Test8 regression: player-facing Russian Loot terminology still contains roll transliteration.'
}
if ($browserPresentationText.IndexOf('bool actionable = !string.IsNullOrEmpty(line.ActionSpaceObjectId);',[StringComparison]::Ordinal) -lt 0 -or
    $browserPresentationText.IndexOf('rowButton.interactable = actionable',[StringComparison]::Ordinal) -lt 0 -or
    $browserPresentationText.IndexOf('rowOutline.enabled = actionable;',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test8 regression: clickable browser rows no longer share the same interactable/outline affordance.'
}


# v1.7.39-test10 Loot clarity gates retained.
$tradeMissionPath = Join-Path $sourceDir 'ModMain.TradeMissionStatus.cs'
if (-not (Test-Path -LiteralPath $tradeMissionPath)) { throw 'Trade mission status owner is missing.' }
$tradeMissionText = Get-Content -LiteralPath $tradeMissionPath -Raw
if ($sourceText.IndexOf('LootMissionRow(string source, string type, string tech, bool eligible)',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('eligible ? "eligible" : "ineligible"',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test10 regression: redundant mission-pool eligible/status payload still exists.'
}
foreach ($token in @(
    "loot.note.container_chance`tCHANCE — get ≥1 item across all shown rolls. Marauder is already included. TECH — minimum source Tech.",
    "loot.note.mission_pools`tThe item is in the normal mission-loot pool for the listed sources. It may appear in a container or on the floor.")) {
    if ($enLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test10 EN Loot clarity token missing: $token" }
}
foreach ($token in @(
    "loot.note.container_chance`tШАНС — получить ≥1 предмет за все указанные броски. Бонус Мародёра уже учтён. TECH — минимальный Tech источника.",
    "loot.note.mission_pools`tПредмет входит в обычный пул добычи миссий для указанных источников. Может появиться в контейнере или на полу.")) {
    if ($ruLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test10 RU Loot clarity token missing: $token" }
}

# v1.7.39-test11 exact Trade mission countdown gates.
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
    if ($tradeMissionText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test11 exact mission token missing: $token" }
}
foreach ($forbidden in @('TradeMissionStationIds','IsTradeMissionTerminal','ResolveTradeMissionStationId','terminalByTest9','LogTradeMissionExactAuditOnce')) {
    if ($tradeMissionText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "Test11 stale heuristic/audit token remains: $forbidden" }
}
foreach ($token in @("ui.mission`tMISSION", "ui.yes`tYES")) {
    if ($enLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test11 EN localization token missing: $token" }
}
foreach ($token in @("ui.mission`tМИССИЯ", "ui.yes`tДА")) {
    if ($ruLocalizationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test11 RU localization token missing: $token" }
}
if ($tradeText.IndexOf('GetTradeMissionDisplay(entry)',[StringComparison]::Ordinal) -lt 0 -or
    $tradeText.IndexOf('entry.MissionArrivalState',[StringComparison]::Ordinal) -lt 0 -or
    $browserPresentationText.IndexOf('mission remaining | vanilla travel',[StringComparison]::Ordinal) -lt 0 -or
    $browserPresentationText.IndexOf('line.MetaState == 3',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test11 regression: exact mission countdown is not connected to pooled Trade rows.'
}
$tradeMissionLines = (Get-Content -LiteralPath $tradeMissionPath).Count
if ($tradeMissionLines -gt 260) { throw "Test11 TradeMissionStatus line budget exceeded: $tradeMissionLines/260" }
if ($sourceText.IndexOf('SellItems(',[StringComparison]::Ordinal) -ge 0 -or
    $sourceText.IndexOf('BuyItems(',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test11 read-only regression: mission countdown must not invoke trade mutation APIs.'
}

# v1.7.39-test12 Trade freshness / cleanup gates.
$tradeFreshnessText = Get-Content -LiteralPath $tradeFreshnessPath -Raw
$templateLocalizationText = Get-Content -LiteralPath (Join-Path $content 'Localization\TranslationTemplate.lang') -Raw
if ($tradeText.IndexOf('BuildLiveStationMarket',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test12 regression: obsolete BuildLiveStationMarket dead implementation returned.'
}
foreach ($token in @(
    'StartMarketScan(string itemId, bool forceRefresh = false)',
    '!forceRefresh && string.Equals(_marketItemId, itemId',
    'TickTradeMissionCountdownUiRefresh();',
    'MarkTradeMissionCountdownUiRendered();')) {
    if ($tradeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test12 Trade freshness token missing: $token" }
}
if ($browserUiText.IndexOf('StartMarketScan(_inspectorItemId, true);',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test12 regression: explicit Trade-tab entry/re-click no longer forces a fresh market snapshot.'
}
foreach ($token in @(
    'TradeMissionUiCheckFrames = 300',
    'TradeMissionUiRefreshMinutes = 5d',
    'RefreshTradeMissionStatusSnapshot();',
    'TradeMissionsByStationId.ContainsKey(entry.StationId)',
    'missionChanged || timeChanged')) {
    if ($tradeFreshnessText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test12 mission live-refresh token missing: $token" }
}
if ($tradeFreshnessText.IndexOf('BuildLiveMarketEntry(',[StringComparison]::Ordinal) -ge 0 -or
    $tradeFreshnessText.IndexOf('GetRuntimeStations',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test12 regression: cheap mission countdown refresh must not rescan the station market.'
}
foreach ($token in @('_tradeMissionsTypeChecked','_tradeSpaceTimeTypeChecked')) {
    if ($tradeMissionText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test12 type-lookup cache token missing: $token" }
}
if ($lootPresentationText.IndexOf('Ui("ui.tech")',[StringComparison]::Ordinal) -lt 0 -or
    $lootPresentationText.IndexOf('"TECH"',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test12 regression: Mission Pools TECH header must use localized ui.tech, never a raw literal.'
}
foreach ($token in @('const int maxShown = 64;','int remaining = totalValid - shown;','Ui("ui.more_rows_format")')) {
    if ($lootPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test12 Mission Pools truncation token missing: $token" }
}
if ($enLocalizationText.IndexOf("ui.more_rows_format`t+ {0} more ({1})",[StringComparison]::Ordinal) -lt 0 -or
    $templateLocalizationText.IndexOf("ui.more_rows_format`t+ {0} more ({1})",[StringComparison]::Ordinal) -lt 0) {
    throw 'Test12 EN/template Mission Pools overflow localization missing.'
}
if ($ruLocalizationText.IndexOf("ui.more_rows_format`t+ ещё {0} ({1})",[StringComparison]::Ordinal) -lt 0) {
    throw 'Test12 RU Mission Pools overflow localization missing.'
}


# v1.7.39-test13 Loot code-hygiene gates.
if ($sourceText.IndexOf('"TECH"',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test13 regression: raw "TECH" source literal returned; use Ui("ui.tech").'
}
if ($lootIndexesText.IndexOf('IndexLootMobClass',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test13 regression: retired synchronous IndexLootMobClass dead path returned.'
}
foreach ($token in @(
    'cached != null)',
    'Do not negative-cache a temporarily unavailable vanilla table.',
    'LootGeneralSpawnContainersByItem[itemId] = result;')) {
    if ($lootGeneralSpawnText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test13 negative-cache token missing: $token" }
}
if ($lootGeneralSpawnText.IndexOf('cached != null && cached.Count > 0',[StringComparison]::Ordinal) -ge 0 -or
    $lootGeneralSpawnText.IndexOf('if (result.Count == 0) return null;',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test13 regression: empty general-spawn results are no longer cached.'
}

# v1.7.39-test14 Loot unknown-semantics gates. Unknown roll count must never
# silently become a proven zero or a bonus-only total probability.
$lootFacadeText = Get-Content -LiteralPath $lootPath -Raw
foreach ($token in @('public readonly bool RollRangeResolved;','bool rollRangeResolved')) {
    if ($lootFacadeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test14 Loot model token missing: $token" }
}
foreach ($token in @(
    'private static bool ResolveLootContainerRollRange(',
    'if (range == null) return false;',
    'if (!minResolved || !maxResolved) return false;',
    'dropId, dropId, 0, 0, false')) {
    if ($lootContainerProfilesText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test14 roll-range resolver token missing: $token" }
}
foreach ($token in @(
    '!source.RollRangeResolved || source.MaxRolls > 0',
    'return "? +" + FormatExpectedNumber(storageExpected, IsRussian());',
    'return "?";')) {
    if ($lootModifiersText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test14 unknown-roll presentation token missing: $token" }
}
foreach ($token in @(
    'if (!source.RollRangeResolved) return "—";',
    'itemId, rawContainers, containerItemTech, ref any',
    'AddWrappedLootNote("loot.note.unknown_container_rolls")')) {
    if ($lootPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test14 unknown-chance/dedup token missing: $token" }
}
if ($lootPresentationText.IndexOf('itemId, containers, containerItemTech, ref any',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test14 regression: Other Containers dedup must use raw profile sources, not filtered presentation rows.'
}
if ($lootIndexesText.IndexOf('descriptor.RollRangeResolved',[StringComparison]::Ordinal) -lt 0 -or
    $lootIndexesText.IndexOf('!existing.RollRangeResolved && source.RollRangeResolved',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test14 regression: roll-range resolution state is not preserved/preferred by the weighted reverse index.'
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    if ($locText.IndexOf('loot.note.unknown_container_rolls',[StringComparison]::Ordinal) -lt 0) {
        throw 'Test14 unknown-roll explanatory localization missing.'
    }
}

# v1.7.39-test16 exact SELL TO STATIONS contract. The player-facing relation must
# mirror vanilla TradeSystem.IsValidItem, never the retired price/list heuristics.
foreach ($token in @(
    'TryEvaluateVanillaSellToStationGate',
    'tradeType, "IsValidItem", new Type[] { typeof(Faction), typeof(Station), typeof(string) }',
    'exactGate=TradeSystem.IsValidItem(Faction,Station,string)',
    'SELL TO STATIONS relations are omitted rather than guessed.',
    'bool sells = stock > 0;',
    'TryGetExactStationPrice(station, itemId, true, out price)')) {
    if ($tradeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test15 exact sell-contract token missing: $token" }
}
foreach ($forbidden in @(
    'GetMember(station, "AdditionalConsumableItems")',
    'GetMember(station, "ConsumeItemsRating")',
    'GetMember(station, "ItemsRequirement")',
    'if (!buys && priceRecord != null) buys = true;')) {
    if ($tradeText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "Test15 retired sell heuristic returned: $forbidden" }
}
if ($tradeText.IndexOf('SellItems(',[StringComparison]::Ordinal) -ge 0 -or
    $tradeText.IndexOf('BuyItems(',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test15 read-only regression: exact relation resolver must not invoke trade mutation APIs.'
}

# v1.7.39-test16 semantic truthfulness gates. Unknown values must stay unknown,
# and station-economy recipe relations must not masquerade as direct player barter.
$factionsText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Factions.cs') -Raw
$disassemblyText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Disassembly.cs') -Raw
$browserPresentationText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.BrowserPresentation.cs') -Raw
foreach ($token in @(
    'FormatFactionRewardPercent(view)',
    'if (view == null || view.State != 0) return "—";',
    'if (view.RewardPercent < 1f) return "<1%";',
    '// 2 = unresolved. Keep it distinct from a proven neutral relation (0).')) {
    if ($factionsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test16 faction truthfulness token missing: $token" }
}
if ($factionsText.IndexOf('view.RewardPercent.ToString(CultureInfo.InvariantCulture) + "%"',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test16 regression: faction locked/unknown chance can again render as numeric percent.'
}
foreach ($token in @(
    'if (storageExpected < 0.0)',
    'return FormatLootPercent((float)(baseHit * 100.0));',
    'loot.note.current_container_modifier_unknown')) {
    if ($lootPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test17 Loot base-fallback truthfulness token missing: $token" }
}
if ($browserPresentationText.IndexOf('if (ShowMagnumUses) { EnsureBrowserFactionColumnsUi(); BuildBrowserMagnum(itemId); }',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test17 regression: Magnum must materialize the shared quantity/state columns before its first render.'
}
foreach ($token in @('100%" + Ui("ui.roll")','chance + Ui("ui.roll")')) {
    if ($disassemblyText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test16 disassembly per-roll token missing: $token" }
}
foreach ($token in @(
    'Ui("ui.station_economy_recipe_output")',
    'Ui("ui.station_economy_recipe_input")',
    'Ui("ui.economy_output")',
    'Ui("ui.economy_input")',
    'object storage = GetMember(station, "InternalStorage");')) {
    if ($tradeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test16 trade/economy truthfulness token missing: $token" }
}
foreach ($forbidden in @(
    'ExtractDirectionalTradeItems(',
    'GetMember(station, "Storage")',
    'GetMember(station, "ActiveStorage")',
    'GetMember(station, "StationStorage")',
    'FindItemEntry(GetMember(station, "ItemsPrices")')) {
    if ($sourceText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "Test16 retired semantic fallback returned: $forbidden" }
}
if ($runtimeText.IndexOf('Defensive fallback for future versions whose signature may change.',[StringComparison]::Ordinal) -ge 0 -or
    $runtimeText.IndexOf('new string[] { "GetItemSellPrice", "GetSellPrice", "GetPrice" }',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test16 regression: heuristic trade-price fallback returned.'
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    foreach ($token in @('ui.station_economy_recipe_output','ui.station_economy_recipe_input','loot.note.current_container_modifier_unknown')) {
        if ($locText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test17 localization token missing: $token" }
    }
}
if ($enLocalizationText.IndexOf('CHANCE shows the exact base probability without that bonus.',[StringComparison]::Ordinal) -lt 0 -or
    $ruLocalizationText.IndexOf('ШАНС показывает точную базовую вероятность без этого бонуса.',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test17 regression: unresolved CURRENT modifier must be disclosed as a base-chance fallback.'
}

# v1.7.39 release UX recovery contracts. MCM/faction fixes remain; its
# auto-expanded Space Loot modifier rows are intentionally retired.
$configurationText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Configuration.cs') -Raw
$lootModifiersText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.LootModifiers.cs') -Raw
$weaponModeDamagePerApText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.WeaponModeDamagePerAP.cs') -Raw
if ($configurationText.IndexOf('AddMcmBool(add, list, configValueType, "EnableItemIntelligence"',[StringComparison]::Ordinal) -ge 0) {
    throw 'Test19 regression: duplicate visible Item Intelligence master switch returned to MCM.'
}
foreach ($token in @(
    'AddMcmBool(add, list, configValueType, "InspectorEnabled", InspectorEnabled, Ui("mcm.header.inspector"), Ui("ui.enable_item_intelligence")',
    'if (!EnableItemIntelligence)',
    'InspectorEnabled = false;',
    'EnableItemIntelligence = true;')) {
    if ($configurationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test19 preserved MCM consolidation token missing: $token" }
}
foreach ($token in @(
    'ResolveFactionAvailabilityForCurrentSave',
    '"IsEnabledFaction", new Type[] { typeof(Faction) }',
    'exactGate=Factions.IsEnabledFaction(Faction)',
    'if (availability == 0)',
    'ui.no_active_faction_reward_in_current_save')) {
    if ($factionsText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test19 preserved faction current-save token missing: $token" }
}
foreach ($forbidden in @(
    '_lootModifierAutoManualForContext',
    '_lootModifierAutoManualLogged',
    'CURRENT character unavailable; exposing MANUAL projection controls for this context.',
    'manualPresentation')) {
    if ($lootModifiersText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "Test19 regression: Space Loot auto-expansion returned: $forbidden" }
}
foreach ($token in @(
    'return BuildManualLootModifierSnapshot();',
    '_lootModifierUseManual ? Ui("ui.loot_modifiers_manual") : Ui("ui.loot_modifiers_current")',
    'if (!_lootModifierUseManual) return;')) {
    if ($lootModifiersText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test19 Loot dropdown recovery token missing: $token" }
}
foreach ($token in @(
    'if (weapon.IsMelee)',
    'weapon.GetMeleeDamageFromCreature',
    'Mathf.RoundToInt(baseMin * modeMult) * casts',
    'Mathf.RoundToInt(baseMax * modeMult) * casts',
    'meleeFormula=Round(WeaponRecord.Damage*FireMode.DamageMult)*WeaponCastsCount')) {
    if ($weaponModeDamagePerApText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Test19 melee Damage/AP token missing: $token" }
}
if ($weaponModeDamagePerApText.IndexOf('string ammoId = weapon.DefaultAmmoId ?? string.Empty;',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModeDamagePerApText.IndexOf('ammo.BulletCastsPerShot',[StringComparison]::Ordinal) -lt 0 -or
    $weaponModeDamagePerApText.IndexOf('int totalMin = minPerFragment * fragments * casts;',[StringComparison]::Ordinal) -lt 0) {
    throw 'Test19 regression: existing ranged Damage/AP contract changed unexpectedly.'
}
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    if ($locText.IndexOf('ui.no_active_faction_reward_in_current_save',[StringComparison]::Ordinal) -lt 0) {
        throw 'Test19 preserved current-save faction localization missing.'
    }
}

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

$versionMatch = [regex]::Match($sourceText, 'public const string Version = "([^"]+)";')
if (-not $versionMatch.Success) { throw 'Source version contract missing.' }
$sourceVersion = $versionMatch.Groups[1].Value

if ($Mode -eq 'TEST') {
    if ($sourceVersion -notmatch '-test|-rc|-dev') { throw "TEST is blocked for stable source version: $sourceVersion" }
    if (-not $WorkshopStage) { $WorkshopStage = 'C:\QM_Workshop\ItemIntelligence_ModderSearchTest' }
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
    'BrowserPageByTab',
    'BrowserFavoriteItemIds',
    'BrowserRecentItemIds',
    'NavigateBrowserBack',
    'BrowserCatalogDataFilter',
    'EnforceInspectorModalInvariantSafe',
    'LogRuntimeBoundaryWarningOnce',
    'VerifyChipUnlockChanceContract',
    'ObserveDatadiskUnlockPool',
    'CanonicalUnlockPoolDatadisks',
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

# Architectural no-cheat guard. These are deliberately narrow high-risk mutation APIs.
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
if ($json.SteamTags -notcontains '1.0.2') { throw 'Manifest compatibility tag 1.0.2 is required by the current runtime evidence.' }
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
foreach ($literalUiMatch in [regex]::Matches($sourceText, '\b(?:Ui|HotkeyUi)\(\s*"([^"]+)"')) {
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

$game = Resolve-GameRoot -ExplicitRoot $GameRoot
$managed = Join-Path $game 'Quasimorph_Data\Managed'
$csc = Resolve-Csc
Write-Host ('Mode: ' + $Mode) -ForegroundColor DarkGray
Write-Host ('C# compiler: ' + $csc) -ForegroundColor DarkGray
Write-Host ('Game: ' + $game) -ForegroundColor DarkGray
Write-Host ('Source files: ' + $sourceFiles.Count + '; localization keys: ' + $langCount) -ForegroundColor DarkGray
$gameAssemblyPath = Join-Path $managed 'Assembly-CSharp.dll'
$gameAssemblyHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $gameAssemblyPath).Hash
Write-Host ('Game Assembly-CSharp SHA256: ' + $gameAssemblyHash) -ForegroundColor DarkGray

$referenceNames = @(
    'Assembly-CSharp.dll','0Harmony.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.UI.dll',
    'Unity.TextMeshPro.dll','UnityEngine.UIModule.dll','UnityEngine.TextRenderingModule.dll',
    'UnityEngine.InputLegacyModule.dll','UnityEngine.IMGUIModule.dll','netstandard.dll','System.Runtime.dll'
)
foreach ($must in @('Assembly-CSharp.dll','0Harmony.dll','UnityEngine.CoreModule.dll','UnityEngine.UI.dll','Unity.TextMeshPro.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $managed $must) -PathType Leaf)) { throw "Required dependency missing: $must" }
}
$refs = New-Object System.Collections.Generic.List[string]
foreach ($name in $referenceNames) {
    $p = Join-Path $managed $name
    if (Test-Path -LiteralPath $p -PathType Leaf) { $refs.Add($p) }
}

$build = Join-Path $root '_build'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$dll = Join-Path $build 'ItemIntelligence.dll'
Remove-Item -LiteralPath $dll -Force -ErrorAction SilentlyContinue
$args = New-Object System.Collections.Generic.List[string]
$args.Add('/nologo'); $args.Add('/target:library'); $args.Add('/optimize+'); $args.Add('/debug-'); $args.Add('/platform:anycpu'); $args.Add('/out:' + $dll)
foreach ($r in $refs) { $args.Add('/reference:' + $r) }
foreach ($src in $sourceFiles) { $args.Add($src.FullName) }

Write-Host ('Building Item Intelligence ' + $sourceVersion + '...') -ForegroundColor Cyan
& $csc @args
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "C# build failed with exit code $LASTEXITCODE" }
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash
Copy-Item -LiteralPath $dll -Destination (Join-Path $content 'ItemIntelligence.dll') -Force
Write-Host ('Build OK: ' + $dll) -ForegroundColor Green
Write-Host ('SHA256: ' + $hash) -ForegroundColor Green

$stageParent = Split-Path -Parent $WorkshopStage
New-Item -ItemType Directory -Force -Path $stageParent | Out-Null
$tmpStage = $WorkshopStage + '.tmp'
if (Test-Path -LiteralPath $tmpStage) { Remove-Item -LiteralPath $tmpStage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $tmpStage | Out-Null
Copy-Item -Path (Join-Path $content '*') -Destination $tmpStage -Recurse -Force

if (Test-Path -LiteralPath $WorkshopStage) {
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupDir = Join-Path $env:USERPROFILE 'Downloads\Quasimorph_Mod_Backups'
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    $backupZip = Join-Path $backupDir ('ItemIntelligence_' + $Mode + '_Stage_' + $stamp + '.zip')
    Compress-Archive -Path (Join-Path $WorkshopStage '*') -DestinationPath $backupZip -Force
    Write-Host ('Previous stage backup: ' + $backupZip) -ForegroundColor DarkGray
    Remove-Item -LiteralPath $WorkshopStage -Recurse -Force
}
Move-Item -LiteralPath $tmpStage -Destination $WorkshopStage

$stageDll = Join-Path $WorkshopStage 'ItemIntelligence.dll'
$stageManifest = Join-Path $WorkshopStage 'modmanifest.json'
if (-not (Test-Path -LiteralPath $stageDll -PathType Leaf)) { throw 'Stage validation failed: ItemIntelligence.dll missing.' }
if (-not (Test-Path -LiteralPath $stageManifest -PathType Leaf)) { throw 'Stage validation failed: modmanifest.json missing.' }
if (Test-Path -LiteralPath (Join-Path $WorkshopStage 'ItemIntelligenceAutoTests.dll')) { throw 'Stage validation failed: AutoTests leaked into stage.' }
$stageHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $stageDll).Hash
if ($stageHash -ne $hash) { throw 'Stage validation failed: DLL hash mismatch.' }

@(
    ('Item Intelligence ' + $sourceVersion + ' ' + $Mode + ' validation'),
    ('Date=' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')),
    ('GameRoot=' + $game),
    ('SourceFiles=' + $sourceFiles.Count),
    ('RuntimeLines=' + $runtimeLines),
    ('CoreIndexesLines=' + $coreIndexesLines),
    ('InformationLines=' + $informationLines),
    ('DisassemblyLines=' + $disassemblyLines),
    ('WeaponModesLines=' + $weaponModesLines),
    ('WeaponModePresentationLines=' + $weaponModePresentationLines),
    ('WeaponModeScatterLines=' + $weaponModeScatterLines),
    ('WeaponModeLocalizationLines=' + $weaponModeLocalizationLines),
    ('BrowserUiLines=' + $browserUiLines),
    ('BrowserPresentationLines=' + $browserPresentationLines),
    ('BrowserLazyUiLines=' + $browserLazyUiLines),
    ('BrowserCatalogLines=' + $browserCatalogLines),
    ('BrowserCatalogPresentationLines=' + $browserCatalogPresentationLines),
    ('InterfaceIconsLines=' + $interfaceIconsLines),
    ('LootFacadeLines=' + $lootLines),
    ('LootIndexesLines=' + $lootIndexesLines),
    ('LootContainerProfilesLines=' + $lootContainerProfilesLines),
    ('LootContainerIconsLines=' + $lootContainerIconsLines),
    ('LootPresentationLines=' + $lootPresentationLines),
    ('LootModifiersLines=' + $lootModifiersLines),
    ('LootEnemyPresentationLines=' + $lootEnemyPresentationLines),
    ('LootModifierRuntimeLines=' + $lootModifierRuntimeLines),
    ('LocalizationParity=' + $langCount + '/' + $langCount + '/' + $langCount),
    ('ReadOnlyArchitecture=OK'),
    ('PowerShellParserHazardScan=OK'),
    ('SemanticGuards=OK'),
    ('PrivateStaticSingleReferenceAdvisory=' + $singleReferencePrivateStatic.Count),
    ('DLL_SHA256=' + $hash),
    ('WorkshopStage=' + $WorkshopStage),
    ('WorkshopTarget=' + $targetWorkshopId),
    ('StageHash=OK')
) | Set-Content -LiteralPath (Join-Path $root 'LAST_BUILD_VALIDATION.txt') -Encoding UTF8

Write-Host ''
Write-Host ('Workshop stage ready: ' + $WorkshopStage) -ForegroundColor Green
Get-ChildItem -LiteralPath $WorkshopStage | ForEach-Object { Write-Host ('  ' + $_.Name) }
Write-Host ''
if ($Mode -eq 'TEST') {
    Write-Host 'DEV/TEST channel only. Public Workshop item is protected by the source-version gate.' -ForegroundColor Yellow
} else {
    Write-Host 'Stable release mode.' -ForegroundColor Yellow
}
Write-Host 'Run this in the Quasimorph developer console:' -ForegroundColor Yellow
Write-Host ('mod_updateworkshopitem ' + $targetWorkshopId + ' ' + $WorkshopStage + ' FALSE') -ForegroundColor Cyan
