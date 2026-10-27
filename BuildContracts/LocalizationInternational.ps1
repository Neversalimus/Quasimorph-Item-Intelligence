# ============================================================================
# INTERNATIONAL LOCALIZATION
# Shipped-language parity, format integrity and language-aware presentation.
# ============================================================================

$internationalLocalizationDir = Join-Path $root 'WORKSHOP_CONTENT\Localization'
. (Join-Path $root 'BuildTools/LanguageCatalog.ps1')
$languages = @(Get-QiiLanguageCatalog $root)
$catalogType = ($languages[0].GetType().Namespace + '.LanguageCatalog') -as [type]
$internationalLanguageSpecs = @($languages | ForEach-Object { @{ Name=$_.Id; File=$_.FileName } }) +
    @(@{ Name='TPL'; File='TranslationTemplate.lang' })

function Get-QiiInternationalLangMap([string]$path,[string]$name) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "international localization missing: $path" }
    $text = Read-Utf8Strict -Path $path
    if ($text -match '(?im)^\s*@force\s*=\s*true\s*$') {
        throw "shipped localization must never force language selection: $name"
    }
    $declaration = [regex]::Match($text,'(?m)^@language=(.+)$').Groups[1].Value.Trim()
    if ($name -eq 'TPL') {
        if ($declaration -ne 'CHANGE_ME') { throw 'Translation template must not claim a real language.' }
    } else {
        if (-not $declaration) { throw "Language declaration missing: $name" }
        foreach ($alias in ($declaration -split ';')) {
            $resolved = $catalogType::Find($alias)
            if (-not $resolved -or $resolved.Id -ne $name) { throw "Language declaration disagrees with registry: $name / $alias" }
        }
    }

    $map = @{}
    foreach ($line in ($text -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $trimmed = $line.TrimStart()
        if ($trimmed.StartsWith('#') -or $trimmed.StartsWith('@')) { continue }
        $tab = $line.IndexOf("`t")
        if ($tab -le 0) { throw "malformed international localization row in ${name}: $line" }
        $key = $line.Substring(0,$tab).Trim()
        $value = $line.Substring($tab + 1)
        if ($map.ContainsKey($key)) { throw "duplicate international localization key in ${name}: $key" }
        if ($value -match '[\u200B\u200C\u200D\uFEFF\uFFFD\u0000]') { throw "invalid Unicode in ${name}: $key" }
        $map[$key] = $value
    }
    return $map
}

$internationalMaps = @{}
foreach ($spec in $internationalLanguageSpecs) {
    $path = Join-Path $internationalLocalizationDir $spec.File
    $internationalMaps[$spec.Name] = Get-QiiInternationalLangMap $path $spec.Name
    Assert-No-PlayerFacingDevText -Path $path
}

$internationalEnglish = $internationalMaps['EN']
if ($internationalEnglish.Count -ne 696) {
    throw "international localization key count drifted: $($internationalEnglish.Count), expected 696"
}
foreach ($name in @($internationalMaps.Keys | Where-Object { $_ -ne 'EN' })) {
    $map = $internationalMaps[$name]
    if ($map.Count -ne $internationalEnglish.Count) {
        throw "international localization count mismatch: ${name}=$($map.Count), EN=$($internationalEnglish.Count)"
    }
    foreach ($key in $internationalEnglish.Keys) {
        if (-not $map.ContainsKey($key)) { throw "${name} missing localization key: $key" }

        $enValue = [string]$internationalEnglish[$key]
        $value = [string]$map[$key]
        $enTokens = @([regex]::Matches($enValue,'(?<!\{)\{([^{}]+)\}(?!\})') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object)
        $tokens = @([regex]::Matches($value,'(?<!\{)\{([^{}]+)\}(?!\})') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object)
        if (($enTokens -join ',') -ne ($tokens -join ',')) {
            throw "international format-placeholder mismatch: $name / $key"
        }

        $enLeading = [regex]::Match($enValue,'^ *').Value.Length
        $leading = [regex]::Match($value,'^ *').Value.Length
        $enTrailing = [regex]::Match($enValue,' *$').Value.Length
        $trailing = [regex]::Match($value,' *$').Value.Length
        if ($enLeading -ne $leading -or $enTrailing -ne $trailing) {
            throw "international compositional whitespace mismatch: $name / $key"
        }
    }
}

$deText = (($internationalMaps['DE'].Values) -join "`n")
$plText = (($internationalMaps['PL'].Values) -join "`n")
$zhText = (($internationalMaps['ZH'].Values) -join "`n")
if ([regex]::Matches($deText,'[ÄÖÜäöüß]').Count -lt 100) { throw 'German translation script-density sanity failed.' }
if ([regex]::Matches($plText,'[ĄĆĘŁŃÓŚŹŻąćęłńóśźż]').Count -lt 150) { throw 'Polish translation script-density sanity failed.' }
if ([regex]::Matches($zhText,'[\u3400-\u4DBF\u4E00-\u9FFF]').Count -lt 900) { throw 'Simplified Chinese translation script-density sanity failed.' }
foreach ($name in @($internationalMaps.Keys | Where-Object { $_ -notin @('EN','RU','TPL') })) {
    $joined = (($internationalMaps[$name].Values) -join "`n")
    if ($joined -match '[А-Яа-яЁё]') { throw "unexpected Cyrillic residue in shipped $name translation." }
}

$allowedChineseLatinOnly = @(
    'catalog.data.magnum','catalog.sort.id','label.magnum','stats.magnum',
    'tab.magnum','ui.item_intelligence','ui.magnum','tab.magnum.short'
)
foreach ($key in $internationalMaps['ZH'].Keys) {
    $value = [string]$internationalMaps['ZH'][$key]
    $hasLatin = $value -match '[A-Za-z]'
    $hasHan = $value -match '[\u3400-\u4DBF\u4E00-\u9FFF]'
    if ($hasLatin -and -not $hasHan -and $allowedChineseLatinOnly -notcontains $key) {
        throw "unexpected untranslated Latin-only Simplified Chinese value: $key = $value"
    }
}

# Brand names, identifiers and compact symbols are intentionally shared. Every
# other Japanese/Korean value must contain the corresponding writing system.
foreach ($name in @('JA','KO')) {
    foreach ($key in $internationalMaps[$name].Keys) {
        if ($allowedChineseLatinOnly -contains $key -or ($name -eq 'KO' -and $key -eq 'ui.loot_pp')) { continue }
        $script = if ($name -eq 'JA') { '[\u3040-\u30FF\u3400-\u9FFF]' } else { '[\uAC00-\uD7AF]' }
        if ([string]$internationalMaps[$name][$key] -notmatch $script) {
            throw "untranslated $name value: $key"
        }
    }
}

foreach ($key in @(
    'ui.loot_storage_short','ui.loot_corpse_short','ui.loot_implant_short','ui.loot_storage_corpse_short',
    'ui.unit_day_short','ui.unit_hour_short','ui.unit_minute_short')) {
    foreach ($name in $internationalMaps.Keys) {
        if (-not $internationalMaps[$name].ContainsKey($key)) {
            throw "international compact UI key missing: $name / $key"
        }
    }
}

# Language selection, aliases, cultures, file precedence and cache/font lifetimes
# are executed by the production-component suite below, independent of source spelling.
$lootModifierInternational = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.LootModifiers.cs')
$scavengerTimingInternational = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.ScavengerMissionTiming.cs')
$tradeTimingInternational = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.TradeMissionStatus.cs')
foreach ($forbidden in @('IsRussian() ? "Т" : "B"','IsRussian() ? "К/Т" : "C/B"','? days.ToString() + "д "','? totalHours.ToString() + "ч"')) {
    if (($lootModifierInternational + $scavengerTimingInternational + $tradeTimingInternational).IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "RU/EN-only compact presentation branch returned: $forbidden"
    }
}


foreach ($selectorKey in @('mcm.header.language','mcm.language','mcm.language_tip')) {
    foreach ($name in $internationalMaps.Keys) {
        if (-not $internationalMaps[$name].ContainsKey($selectorKey)) {
            throw "language-selector localization key missing: $name / $selectorKey"
        }
    }
}

& (Join-Path $root 'Tests/Run-LocalizationTests.ps1') -SourceRoot $root
& (Join-Path $root 'Tests/Run-McmFontTests.ps1') -SourceRoot $root
