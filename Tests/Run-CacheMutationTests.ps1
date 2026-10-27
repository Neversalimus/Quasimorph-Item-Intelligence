#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$identity = 'qii-cache-mutations-' + [guid]::NewGuid().ToString('N')
$temp = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $identity))
$cases = @(
    @{ File='ReflectionDataReader.cs'; Test='Run-ReflectionTests.ps1';
       Before='private readonly Dictionary<Type, Dictionary<string, MemberInfo>> InstanceMemberLookupCache =';
       After='private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> InstanceMemberLookupCache =';
       Error='reader owns its lookup cache' },
    @{ File='ReflectionDataReader.cs'; Test='Run-ReflectionTests.ps1';
       Before='return snapshot;'; After='return new List<MemberInfo>(snapshot);';
       Error='readable metadata is an immutable snapshot' },
    @{ File='ModMain.BrowserUiDirtySuppression.cs'; Test='Run-UiWriteTests.ps1';
       Before='target != null && target.fontSize != value'; After='target != null';
       Error='fontSize: unchanged initial value' },
    @{ File='ModMain.BrowserUiDirtySuppression.cs'; Test='Run-UiWriteTests.ps1';
       Before='if (!string.Equals(target.text, next, StringComparison.Ordinal)) target.text = next;';
       After='target.text = next;'; Error='text: unchanged initial value' },
    @{ File='ModMain.BrowserRowRenderCache.cs'; Test='Run-ReadabilityTests.ps1';
       Before='if (!line.Action.IsNone) return false;'; After='/* intentionally allow interactive rows */';
       Error='dynamic row fails closed even with a matching stamp' },
    @{ File='ModMain.BrowserRowRenderCache.cs'; Test='Run-ReadabilityTests.ps1';
       Before='string.Equals(Language, language ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&';
       After='true &&'; Error='language switch rejects stale row text' },
    @{ File='ShipCargoMonitor.cs'; Test='Run-ShipCargoTests.ps1';
       Before='ItemId = null;'; After='/* intentionally retain previous selection */';
       Error='reset releases selected item and count' },
    @{ File='ShipCargoMonitor.cs'; Test='Run-ShipCargoTests.ps1';
       Before='string.Equals(ItemId, itemId, StringComparison.Ordinal)'; After='true';
       Error='another item cannot poll or delay the selected item' },
    @{ File='ShipCargoMonitor.cs'; Test='Run-ShipCargoTests.ps1';
       Before='if (next == Count) return false;'; After='/* intentionally redraw unchanged stock */';
       Error='unchanged stock does not request rendering' },
    @{ File='BrowserNavigationSessionState.cs'; Test='Run-BrowserNavigationTests.ps1';
       Before='ScrollOffset = scrollOffsets[Tab];'; After='ScrollOffset = 0;';
       Error='switching tabs restores each saved offset' },
    @{ File='BrowserNavigationSessionState.cs'; Test='Run-BrowserNavigationTests.ps1';
       Before='history.RemoveAt(0);'; After='history.RemoveAt(history.Count - 1);';
       Error='history restores bounded newest-first snapshots' },
    @{ File='BrowserNavigationSessionState.cs'; Test='Run-BrowserNavigationTests.ps1';
       Before='Array.Clear(scrollOffsets, 0, scrollOffsets.Length);'; After='/* intentionally retain tab offsets */';
       Error='new inspection clears per-tab offsets' },
    @{ File='BrowserNavigationSessionState.cs'; Test='Run-BrowserNavigationTests.ps1';
       Before='if (!isKnownItem(entry.ItemId)) continue;';
       After='if (isKnownItem(entry.ItemId) && entry.ItemId == null) continue;';
       Error='back skips items missing from the current game data' },
    @{ File='BrowserNavigationSessionState.cs'; Test='Run-BrowserNavigationTests.ps1';
       Before='SetScrollOffset(0);'; After='history.Clear(); SetScrollOffset(0);';
       Error='new item resets its current tab without discarding back history' }
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
        try { & (Join-Path $PSScriptRoot $case.Test) -SourceRoot $temp }
        catch {
            if ($_.Exception.ToString().Contains($case.Error)) { $caught = $true } else { throw }
        } finally { [IO.File]::WriteAllText($path, $original) }
        if (-not $caught) { throw "Behavior tests missed the mutation: $($case.File)" }
        Write-Host "Rejected mutation: $($case.File) / $($case.Error)"
    }
    Write-Host "Cache and UI mutation sensitivity: PASS ($($cases.Count)/$($cases.Count); source untouched)." -ForegroundColor Green
} finally {
    if ($temp -ne [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $identity)) -or
        (Split-Path -Leaf $temp) -notmatch '^qii-cache-mutations-[0-9a-f]{32}$') { throw 'Unexpected cleanup path.' }
    Remove-Item -LiteralPath $temp -Recurse -Force
}
