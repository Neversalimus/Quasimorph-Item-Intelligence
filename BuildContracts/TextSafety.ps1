# User-facing text safety.
# Dense browser notes and MCM help must stay concise enough to wrap cleanly without
# becoming paragraph-sized UI. This is a presentation budget, not a localization
# grammar check; terminology and exactness remain owned by feature contracts.

$localizationPaths = @(
    (Join-Path $root 'WORKSHOP_CONTENT\Localization\en.lang'),
    (Join-Path $root 'WORKSHOP_CONTENT\Localization\ru.lang'),
    (Join-Path $root 'WORKSHOP_CONTENT\Localization\TranslationTemplate.lang')
)

$explanationLimit = 150
$tradeDenseLimit = 90

function Test-QiiExplanationKey([string]$key) {
    if ([string]::IsNullOrWhiteSpace($key)) { return $false }
    if ($key.StartsWith('loot.note.', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($key.EndsWith('_tip', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($key.StartsWith('ui.starmap_unavailable_', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($key -eq 'ui.secret_data_story_effect') { return $true }
    if ($key -eq 'ui.chip_unlock_chance_note') { return $true }
    if ($key -eq 'ui.no_explicit_loot_sources_found_check_trade_recip') { return $true }
    if ($key -eq 'ui.no_active_faction_reward_in_current_save') { return $true }
    return $false
}

foreach ($path in $localizationPaths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "user-facing text safety localization missing: $path"
    }

    $seen = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) { continue }
        $parts = $line -split "`t", 2
        if ($parts.Count -ne 2) { continue }
        $key = $parts[0]
        $value = $parts[1]
        if ($seen.ContainsKey($key)) {
            throw "duplicate localization key in text safety surface: $key ($path)"
        }
        $seen[$key] = $true

        if ((Test-QiiExplanationKey $key) -and $value.Length -gt $explanationLimit) {
            throw "user-facing explanation too long: $key = $($value.Length)/$explanationLimit chars ($path)"
        }

        if (($key -eq 'ui.trade_repricing_note' -or $key -eq 'ui.trade_previous_note') -and
            $value.Length -gt $tradeDenseLimit) {
            throw "dense Trade explanation too long: $key = $($value.Length)/$tradeDenseLimit chars ($path)"
        }
    }

    foreach ($requiredKey in @('ui.trade_repricing_note','ui.trade_previous_note','ui.next','ui.batch','ui.trade_payout','ui.trade_stock_short','ui.trade_all','ui.trade_pcs','ui.trade_total','mcm.trade_previous_layout','mcm.trade_previous_layout_tip')) {
        if (-not $seen.ContainsKey($requiredKey)) {
            throw "Trade explanation localization missing: $requiredKey ($path)"
        }
    }
}
