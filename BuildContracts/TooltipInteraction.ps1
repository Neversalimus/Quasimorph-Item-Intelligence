# ============================================================================
# NATIVE ITEM TOOLTIP MORE / ALT DETAILS
# Keep QII modal input closed except for the exact vanilla tooltip-more action.
# ============================================================================

$tooltipMorePath = Join-Path $sourceDir 'ModMain.BrowserNativeTooltipMore.cs'
if (-not (Test-Path -LiteralPath $tooltipMorePath -PathType Leaf)) { throw 'Native tooltip-more source module missing.' }
$tooltipMoreText = Get-Content -LiteralPath $tooltipMorePath -Raw
$compatibilityText = Get-Content -LiteralPath (Join-Path $sourceDir 'ModMain.Compatibility.cs') -Raw

foreach ($token in @(
    'BrowserTooltipMoreActionId = "UI_TooltipMore"',
    'BrowserTooltipMoreLayout = "UI"',
    '_browserNativeTooltipMoreActive',
    '_browserNativeTooltipMoreHandler',
    '_browserNativeTooltipMoreItemId',
    'SetBrowserNativeTooltipMoreTarget',
    'ClearBrowserNativeTooltipMoreTarget',
    'ReleaseBrowserNativeTooltipMoreTarget',
    'ReleaseBrowserNativeTooltipMoreTarget(object handler)',
    'ShouldAllowBrowserTooltipMoreAction',
    '!raw',
    'StringComparison.Ordinal'
)) {
    if ($tooltipMoreText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Native tooltip-more contract token missing: $token" }
}


if ($tooltipMoreText.IndexOf('ReleaseBrowserNativeTooltipMoreTarget(ItemTooltipHandler',[StringComparison]::Ordinal) -ge 0) {
    throw 'Native tooltip-more compile-safety regression: release helper must stay game-type agnostic (object handler).'
}

if ($tooltipMoreText.IndexOf('Input.GetKey(KeyCode.LeftAlt)',[StringComparison]::Ordinal) -ge 0 -or
    $tooltipMoreText.IndexOf('Input.GetKey(KeyCode.RightAlt)',[StringComparison]::Ordinal) -ge 0) {
    throw 'Native tooltip-more regression: raw ALT polling bypasses the vanilla remappable UI_TooltipMore action.'
}
if ($runtimeText.IndexOf('InputControllerModalActionPrefix(object[] __args',[StringComparison]::Ordinal) -ge 0 -or
    $runtimeText.IndexOf('string __0, string __1, bool __2, ref bool __result',[StringComparison]::Ordinal) -lt 0) {
    throw 'Input hot-path regression: tooltip-more exception must use typed Harmony arguments, never object[] __args.'
}
foreach ($token in @(
    'parameters.Length != 3',
    'parameters[0].ParameterType != typeof(string)',
    'parameters[1].ParameterType != typeof(string)',
    'parameters[2].ParameterType != typeof(bool)',
    'ShouldAllowBrowserTooltipMoreAction(__0, __1, __2)',
    'SetBrowserNativeTooltipMoreTarget(__instance, itemId)',
    'ClearBrowserNativeTooltipMoreTarget(__instance)',
    'ReleaseBrowserNativeTooltipMoreTarget(GetComponent<ItemTooltipHandler>())'
)) {
    if ($runtimeText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Native tooltip-more runtime wiring missing: $token" }
}
foreach ($token in @(
    '_compatTooltipMore = true',
    '"BuildItemAdditionalTooltip", 0, null',
    '"RestoreItemTooltip", 0, null',
    '"get_IsShowingItemTooltip", 0, null',
    '"TooltipMore"'
)) {
    if ($compatibilityText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Native tooltip-more compatibility gate missing: $token" }
}
foreach ($forbidden in @(
    'PatchNamedMethods(harmony, "MGSC.TooltipFactory", "BuildItemAdditionalTooltip"',
    'PatchNamedMethods(harmony, "MGSC.TooltipFactory", "RestoreItemTooltip"'
)) {
    if ($runtimeText.IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) { throw "Native tooltip-more safety regression: vanilla builder patch returned: $forbidden" }
}
foreach ($token in @('ClearBrowserNativeTooltipMoreTarget(null);')) {
    if ($browserUiText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "Native tooltip-more browser cleanup missing: $token" }
}
