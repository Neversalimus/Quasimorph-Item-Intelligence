# Responsive presentation: fixed-capacity pools, uniform zoom and persisted preferences.
$viewportText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserViewport.cs')
$viewportMathText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserViewportMath.cs')
$viewportChromeText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserViewportChrome.cs')
$viewportStateText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.BrowserState.cs')
$viewportConfigText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Configuration.cs')
$viewportIconText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.InterfaceIcons.cs')
foreach ($token in @('BrowserRowCapacity = 32','BrowserCatalogRowCapacity = 8',
    'new GameObject[BrowserRowCapacity]','new TMP_Text[BrowserRowCapacity]')) {
    if (-not $viewportStateText.Contains($token)) { throw "Browser pool capacity contract missing: $token" }
}
foreach ($token in @('Vector3.one * _browserViewport.Scale','InvalidateBrowserRowRenderCache();',
    'BrowserVisibleRows = _browserViewport.Rows','BrowserCatalogVisibleRows = _browserViewport.CatalogRows',
    'CreateBrowserViewportControls','SaveConfig()','CalculateBrowserViewport','SetBrowserRowTextSize')) {
    if (-not $viewportText.Contains($token)) { throw "Browser viewport integration missing: $token" }
}
foreach ($token in @('BrowserExpanded','BrowserWindowZoom','BrowserExpandedZoom')) {
    if (-not $viewportConfigText.Contains('"' + $token + '="') -or
        -not $viewportConfigText.Contains('string.Equals(key, "' + $token + '"')) {
        throw "Browser view preference is not read and saved: $token"
    }
    # In-browser settings must not be overwritten by stale MCM snapshots.
    if ($viewportConfigText -match ('AddMcm\w+\([^\r\n]+"' + $token + '"')) {
        throw "Browser view preference gained a competing MCM snapshot: $token"
    }
}
foreach ($token in @('ApplyBrowserViewport();','CreateBrowserViewportControls();')) {
    if (-not $browserPresentationText.Contains($token)) { throw "Browser shell viewport hook missing: $token" }
}
if (-not $browserUiText.Contains('TickBrowserViewport();')) { throw 'Browser resolution-change hook missing.' }
if (-not $viewportIconText.Contains('LayoutBrowserViewportChrome();')) { throw 'Icon visibility must reapply responsive chrome.' }
if (-not $browserRowRendererText.Contains('i >= BrowserVisibleRows || lineIndex >= total') -or
    -not $browserCatalogPresentationText.Contains('i >= BrowserCatalogVisibleRows || index >= total')) {
    throw 'Hidden pooled rows must be cleared when zoom reduces the visible window.'
}
if (-not $browserRowLayoutText.Contains('SetBrowserRowTextSize') -or
    -not $browserTextLayoutText.Contains('BrowserColumnCoordinate(BrowserFullNoteWidth, _browserViewport.Width)')) {
    throw 'Rows and full-note wrapping must share the viewport width.'
}
if (($viewportText + $viewportMathText + $viewportChromeText) -match 'UnityEngine\.Random|MGSC\.Random|CreateItem\(|SpawnMob\(') {
    throw 'Browser view controls must not touch game RNG or spawn records.'
}
$viewportWeaponText = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.WeaponModePresentation.cs')
if (-not $viewportWeaponText.Contains('CalculateBrowserWeaponTooltipPosition(')) {
    throw 'Weapon hover card must respect the viewport and screen bounds.'
}
& (Join-Path $root 'Tests/Run-ViewportTests.ps1') -SourceRoot $root
