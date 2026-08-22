# ============================================================================
# MODDER ACTIONS / INTENTIONAL LINKS / ICON AUDIT
# The sole save-affecting exception is explicit, bounded to one item and absent
# from ordinary mode. Related-item links use visual action chrome, not punctuation.
# ============================================================================

$spawnRuntimePath = Join-Path $sourceDir 'ModMain.ModderSpawnRuntime.cs'
$spawnCargo103Path = Join-Path $sourceDir 'ModMain.ModderCargoSpawn103.cs'
$spawnPanelPath = Join-Path $sourceDir 'ModMain.ModderSpawnPanel.cs'
$linkPresentationPath = Join-Path $sourceDir 'ModMain.BrowserLinkPresentation.cs'
foreach ($path in @($spawnRuntimePath,$spawnCargo103Path,$spawnPanelPath,$linkPresentationPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Modder/action owner missing: $path" }
}
$spawnRuntimeText = Read-Utf8Strict -Path $spawnRuntimePath
$spawnCargo103Text = Read-Utf8Strict -Path $spawnCargo103Path
$spawnMutationText = $spawnRuntimeText + "`n" + $spawnCargo103Text
$spawnPanelText = Read-Utf8Strict -Path $spawnPanelPath
$linkPresentationText = Read-Utf8Strict -Path $linkPresentationPath
$modderSpawnRuntimeLines = (Get-Content -LiteralPath $spawnRuntimePath).Count
$modderCargo103Lines = (Get-Content -LiteralPath $spawnCargo103Path).Count
$modderSpawnPanelLines = (Get-Content -LiteralPath $spawnPanelPath).Count
$browserLinkPresentationLines = (Get-Content -LiteralPath $linkPresentationPath).Count
$rowRendererText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserRowRendererParts.cs')
$browserPresentationCurrentText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserPresentation.cs')
$browserStateCurrentText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserState.cs')
$interfaceIconsCurrentText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.InterfaceIcons.cs')
$runtimeCurrentText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Runtime.cs')
$hardeningCurrentText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Hardening.cs')

$ownerBudgets = @{
    $spawnRuntimePath = 280
    $spawnCargo103Path = 120
    $spawnPanelPath = 260
    $linkPresentationPath = 100
}
foreach ($path in $ownerBudgets.Keys) {
    $lines = (Get-Content -LiteralPath $path).Count
    if ($lines -gt $ownerBudgets[$path]) { throw "Modder/action owner budget exceeded: $path = $lines/$($ownerBudgets[$path])" }
}

# Exact save/context boundary: one inspected known item, one click, one vanilla call.
foreach ($token in @(
    '[Hook(ModHookType.DungeonStarted)]','[Hook(ModHookType.DungeonFinished)]',
    'IsKnownItemId(_inspectorItemId)','_modderSpawnLastFrame == Time.frameCount',
    'TryResolveModderCloneInventory','TryResolveModderCargoState',
    'CreateForInventory(itemId, false, false)','TryAddItemToAnyStorage',
    '"CellPosition"','"StoragePriority"','Enum.Parse(p[2].ParameterType, "Backpack")',
    'add.Invoke(inventory, args)','object command;','MethodInfo execute;',
    'DevConsole console = UI.Get<DevConsole>();','object daemon = GetMember(console, "Daemon");',
    'IDictionary commands = GetMember(daemon, "_commands") as IDictionary;',
    'commands.Contains("item")','command = commands["item"];','GetMember(command, "IsAvailable")',
    'new Type[] { typeof(List<string>) }',
    'execute.Invoke(command, new object[] { new List<string> { itemId, "1" } })')) {
    if ($spawnRuntimeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Modder spawn boundary missing: $token" }
}
if ([regex]::Matches($spawnRuntimeText,'!ModderMode').Count -lt 6) {
    throw 'Modder spawn boundary must recheck MCM state at the UI, context and both mutation boundaries.'
}
foreach ($token in @(
    'IsCurrent103FeatureAssembly() && TrySpawnModderItemToCargoViaSystem103(cargo, itemId)',
    'AccessTools.TypeByName("MGSC.MagnumCargoSystem")','AccessTools.TypeByName("MGSC.SpaceTime")',
    'string.Equals(method.Name, "AddCargo", StringComparison.Ordinal)',
    'factory.CreateForInventory(itemId, false, false)',
    'addCargo.Invoke(null, new object[] { cargo, spaceTime, item, null, false, true })')) {
    if ($spawnMutationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Modder 1.0.3 cargo contract missing: $token" }
}
if ([regex]::Matches($spawnRuntimeText,[regex]::Escape('execute.Invoke(command')).Count -ne 1 -or
    [regex]::Matches($spawnRuntimeText,[regex]::Escape('add.Invoke(inventory')).Count -ne 1 -or
    [regex]::Matches($spawnCargo103Text,[regex]::Escape('addCargo.Invoke(null')).Count -ne 1) {
    throw 'Modder spawn must invoke each context-specific mutation boundary exactly once.'
}
if ($spawnRuntimeText.IndexOf('new List<string> { itemId, "1" }',[StringComparison]::Ordinal) -lt 0 -or
    $spawnRuntimeText -match 'new List<string>\s*\{\s*itemId\s*,\s*"(?!1")') {
    throw 'Modder spawn quantity escaped the single-item contract.'
}
foreach ($forbidden in @(
    'ConsoleDaemon.CommandInterface',
    'console.Daemon._commands',
    'Activator.CreateInstance(execute.DeclaringType)',
    'AccessTools.TypeByName("MGSC.SpawnItemCommand")')) {
    if ($spawnRuntimeText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "Detached/static cargo command path returned: $forbidden"
    }
}

# Drawer must be a separate left-side, click-only Modder Mode surface with a save warning.
foreach ($token in @(
    'if (!ModderMode || _inspectorRoot == null)','DestroyModderSpawnPanel();',
    'if (_modderSpawnPanelRoot != null || _inspectorRoot == null || !ModderMode) return;',
    'panelRt.pivot = new Vector2(1f, 1f);','panelRt.anchoredPosition = new Vector2(0f, -86f);',
    '_modderSpawnButton.onClick.AddListener(HandleModderSpawnButton);',
    'TrySpawnCurrentModderItem(out statusKey)','ui.modder_spawn_save_warning')) {
    if ($spawnPanelText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Modder drawer contract missing: $token" }
}
if (($spawnPanelText + $spawnRuntimeText).IndexOf('Input.GetKey',[StringComparison]::Ordinal) -ge 0) {
    throw 'Save-affecting Modder action must remain an explicit click, never a hotkey.'
}
foreach ($token in @('ResetModderSpawnRuntime(false);','ModderModeExplicitSpawnException = true','ReadOnlyKnowledgePolicy = true')) {
    if ($runtimeCurrentText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Modder lifecycle/safety token missing: $token" }
}
foreach ($token in @('ModderModeExplicitSpawnException=','OrdinaryReadOnlyPolicy','ModderSpawnExceptionNarrow')) {
    if ($hardeningCurrentText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Modder diagnostics token missing: $token" }
}

# Link and icon audit: only navigation and the two spawn targets receive new glyphs.
foreach ($token in @('TryRenderBrowserItemLink','BrowserActionKind.OpenItem','ui.open_item_link','BrowserInterfaceIconKind.OpenItem')) {
    if ($linkPresentationText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Intentional item-link presentation missing: $token" }
}
foreach ($token in @('BrowserRowActionIcons','new GameObject("ActionGlyph")','ResetBrowserLinkPresentation(ctx.Slot)')) {
    if (($browserStateCurrentText + $browserPresentationCurrentText + $rowRendererText).IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Pooled link-glyph contract missing: $token" }
}
foreach ($kind in @('Cargo','Clone','OpenItem')) {
    if ($interfaceIconsCurrentText.IndexOf('BrowserInterfaceIconKind.' + $kind,[StringComparison]::Ordinal) -lt 0 -or
        $interfaceIconsCurrentText.IndexOf($kind + ',',[StringComparison]::Ordinal) -lt 0) {
        throw "Justified interface icon missing: $kind"
    }
}
foreach ($retired in @('"   >>"','">  "','" > "')) {
    if ($rowRendererText.IndexOf($retired,[StringComparison]::Ordinal) -ge 0) { throw "Debug-looking link punctuation returned: $retired" }
}
$catalogPresentationText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserCatalogPresentation.cs')
if ($catalogPresentationText.IndexOf('+ "  >"',[StringComparison]::Ordinal) -ge 0) {
    throw 'ASCII debug-looking catalog chevron returned.'
}

$spawnLocalizationKeys = @(
    'ui.open_item_link','ui.modder_spawn_title','ui.modder_spawn_save_warning',
    'ui.modder_spawn_clone','ui.modder_spawn_cargo','ui.modder_spawn_failed',
    'ui.modder_spawn_invalid_item','ui.modder_spawn_wait','ui.modder_spawn_clone_unavailable',
    'ui.modder_spawn_contract_unavailable','ui.modder_spawn_clone_success',
    'ui.modder_spawn_inventory_full','ui.modder_spawn_cargo_unavailable','ui.modder_spawn_cargo_success')
foreach ($locText in @($enLocalizationText,$ruLocalizationText,$templateLocalizationText)) {
    foreach ($key in $spawnLocalizationKeys) {
        if ($locText.IndexOf($key + "`t",[StringComparison]::Ordinal) -lt 0) { throw "Modder/action localization missing: $key" }
    }
}
if ($enLocalizationText.IndexOf("ui.modder_spawn_save_warning`tCreates a real item in this save.",[StringComparison]::Ordinal) -lt 0 -or
    $ruLocalizationText.IndexOf("ui.modder_spawn_save_warning`tСоздаёт реальный предмет в этом сохранении.",[StringComparison]::Ordinal) -lt 0) {
    throw 'MCM Modder Mode description must disclose the save-affecting action.'
}

# Ordinary Item Intelligence remains read-only; the explicit one-item Modder Mode
# exception above must not grow into direct economy/reputation/story mutation APIs.
foreach ($forbidden in @(
    'SellItems(','BuyItems(','SetReputation(','AddReputation(','RemoveReputation(',
    'SetStoryVariable(','SpawnItemToInventory(','GiveItemToPlayer(','.SetValue(','.SetValueDirect(')) {
    if ($sourceText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "ordinary-mode mutation API returned: $forbidden" }
}
