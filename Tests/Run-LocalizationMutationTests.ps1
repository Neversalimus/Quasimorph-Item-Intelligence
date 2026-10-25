#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$identity = 'qii-l10n-mutations-' + [guid]::NewGuid().ToString('N')
$temp = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $identity))
$cases = @(
    @{ File='UiFontResolver.cs'; Before='_uiFontReady = ui != null && game != null;'; After='_uiFontReady = true;'; Error='late UI font recovers without changing language or restarting' },
    @{ File='UiFontResolver.cs'; Before='UnityEngine.Object.Destroy(font);'; After='GC.KeepAlive(font);'; Error='session releases every private font' },
    @{ File='UiLocalization.cs'; Before='ResolvedUiTextCache.Clear();'; After='/* intentionally retained for mutation test */'; Error='active value' },
    @{ File='GameLocalization.cs'; Before='frame - _cachedGameLanguageFrame < GameLanguageRefreshFrames'; After='true'; Error='exact game metadata Russian' },
    @{ File='RequiredDataReader.cs'; Before='if (reported.Contains(key)) return;'; After='/* duplicate warnings */'; Error='diagnostics deduplicate and omit values' },
    @{ File='ModMain.LootContainerChanceMath.cs'; Before='"~ " + minText + "–" + maxText'; After='"~ " + minText + "-" + maxText'; Error='estimate range uses a separator, not subtraction' }
)
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $SourceRoot 'Source') -Destination $temp -Recurse
    New-Item -ItemType Directory -Path (Join-Path $temp 'WORKSHOP_CONTENT') | Out-Null
    Copy-Item -LiteralPath (Join-Path $SourceRoot 'WORKSHOP_CONTENT/Localization') -Destination (Join-Path $temp 'WORKSHOP_CONTENT') -Recurse
    foreach ($case in $cases) {
        $path = Join-Path $temp ('Source/' + $case.File)
        $original = [IO.File]::ReadAllText($path)
        if (-not $original.Contains($case.Before)) { throw "Mutation target missing: $($case.File)" }
        [IO.File]::WriteAllText($path, $original.Replace($case.Before, $case.After))
        $caught = $false
        try { & (Join-Path $PSScriptRoot 'Run-LocalizationTests.ps1') -SourceRoot $temp }
        catch {
            if ($_.Exception.ToString().Contains($case.Error)) { $caught = $true } else { throw }
        } finally { [IO.File]::WriteAllText($path, $original) }
        if (-not $caught) { throw "Behavior tests missed the mutation: $($case.File)" }
        Write-Host "Rejected mutation: $($case.File)"
    }
    Write-Host "Localization mutation sensitivity: PASS ($($cases.Count)/$($cases.Count); source untouched)." -ForegroundColor Green
} finally {
    if ($temp -ne [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $identity)) -or
        (Split-Path -Leaf $temp) -notmatch '^qii-l10n-mutations-[0-9a-f]{32}$') { throw 'Unexpected cleanup path.' }
    Remove-Item -LiteralPath $temp -Recurse -Force
}
