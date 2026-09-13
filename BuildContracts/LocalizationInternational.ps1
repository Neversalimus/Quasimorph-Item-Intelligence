# ============================================================================
# INTERNATIONAL LOCALIZATION
# Shipped-language parity, format integrity and language-aware presentation.
# ============================================================================

$internationalLocalizationDir = Join-Path $root 'WORKSHOP_CONTENT\Localization'
$internationalLanguageSpecs = @(
    @{ Name='EN'; File='en.lang'; Declaration='EnglishUS;English;english;en;Английский' },
    @{ Name='RU'; File='ru.lang'; Declaration='Russian;russian;ru;Русский;рус' },
    @{ Name='DE'; File='de.lang'; Declaration='German;german;Deutsch;de;de-DE' },
    @{ Name='PL'; File='pl.lang'; Declaration='Polish;polish;Polski;pl;pl-PL' },
    @{ Name='ZH'; File='zh-Hans.lang'; Declaration='ChineseSimp;ChineseSimplified;SimplifiedChinese;schinese;zhcn;zh-CN;zh-Hans;简体中文;Chinese (Simplified);Simplified Chinese' },
    @{ Name='ES'; File='es.lang'; Declaration='Spanish;Español;Espanol;es;es-ES' },
    @{ Name='PTBR'; File='pt-BR.lang'; Declaration='BrazilianPortugal;BrazilianPortuguese;PortugueseBrazil;Português (Brasil);Portugues (Brasil);pt-BR' },
    @{ Name='FR'; File='fr.lang'; Declaration='French;Français;Francais;fr;fr-FR' },
    @{ Name='TPL'; File='TranslationTemplate.lang'; Declaration='CHANGE_ME' }
)

function Get-QiiInternationalLangMap([string]$path,[string]$name,[string]$expectedDeclaration) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "international localization missing: $path" }
    $text = Read-Utf8Strict -Path $path
    if ($text -match '(?im)^\s*@force\s*=\s*true\s*$') {
        throw "shipped localization must never force language selection: $name"
    }
    $declaration = [regex]::Match($text,'(?m)^@language=(.+)$').Groups[1].Value.Trim()
    if ($declaration -ne $expectedDeclaration) {
        throw "international localization declaration mismatch: $name = $declaration"
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
    $internationalMaps[$spec.Name] = Get-QiiInternationalLangMap $path $spec.Name $spec.Declaration
    Assert-No-PlayerFacingDevText -Path $path
}

$internationalEnglish = $internationalMaps['EN']
if ($internationalEnglish.Count -ne 660) {
    throw "international localization key count drifted: $($internationalEnglish.Count), expected 660"
}
foreach ($name in @('RU','DE','PL','ZH','ES','PTBR','FR','TPL')) {
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
foreach ($name in @('DE','PL','ZH','ES','PTBR','FR')) {
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

foreach ($key in @(
    'ui.loot_storage_short','ui.loot_corpse_short','ui.loot_implant_short','ui.loot_storage_corpse_short',
    'ui.unit_day_short','ui.unit_hour_short','ui.unit_minute_short')) {
    foreach ($name in @('EN','RU','DE','PL','ZH','ES','PTBR','FR','TPL')) {
        if (-not $internationalMaps[$name].ContainsKey($key)) {
            throw "international compact UI key missing: $name / $key"
        }
    }
}

$internationalSourcePath = Join-Path $sourceDir 'ModMain.LocalizationInternational.cs'
if (-not (Test-Path -LiteralPath $internationalSourcePath -PathType Leaf)) {
    throw 'international localization presentation owner missing.'
}
$internationalSource = Read-Utf8Strict -Path $internationalSourcePath
if ((Get-Content -LiteralPath $internationalSourcePath).Count -gt 230) {
    throw 'international localization presentation owner exceeded 230 lines.'
}
foreach ($token in @(
    'GetUiLanguageOptions()','SetUiLanguagePreference(','GetUiLanguageSignature()','IsUiRussianLanguage()',
    'IsGermanLanguage()','IsPolishLanguage()','IsSimplifiedChineseLanguage()','IsCjkLanguage()',
    'GetUiNumberCulture()','GetUiPercentSuffix()','GetLanguageAwareWrapFallback(',
    'FormatCompactUiDurationFallback(','ContainsHanScript(','ContainsKanaScript(','ContainsHangulScript(')) {
    if ($internationalSource.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "international localization presentation contract missing: $token"
    }
}

$lootModifierInternational = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.LootModifiers.cs')
$scavengerTimingInternational = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.ScavengerMissionTiming.cs')
$tradeTimingInternational = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.TradeMissionStatus.cs')
foreach ($forbidden in @('IsRussian() ? "Т" : "B"','IsRussian() ? "К/Т" : "C/B"','? days.ToString() + "д "','? totalHours.ToString() + "ч"')) {
    if (($lootModifierInternational + $scavengerTimingInternational + $tradeTimingInternational).IndexOf($forbidden,[StringComparison]::Ordinal) -ge 0) {
        throw "RU/EN-only compact presentation branch returned: $forbidden"
    }
}


foreach ($selectorKey in @('mcm.header.language','mcm.language','mcm.language_tip')) {
    foreach ($name in @('EN','RU','DE','PL','ZH','ES','PTBR','FR','TPL')) {
        if (-not $internationalMaps[$name].ContainsKey($selectorKey)) {
            throw "language-selector localization key missing: $name / $selectorKey"
        }
    }
}

$configurationInternational = Read-Utf8Strict -Path (Join-Path $sourceDir 'ModMain.Configuration.cs')
foreach ($token in @(
    'UiLanguagePreference = "Auto (Game)"',
    '"UiLanguage=" + UiLanguagePreference',
    '"UiLanguage", UiLanguagePreference',
    'GetUiLanguageOptions()',
    'SetUiLanguagePreference(savedUiLanguage, "MCM")')) {
    if ($configurationInternational.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "language-selector configuration contract missing: $token"
    }
}

& (Join-Path $root 'Tests/Run-LocalizationTests.ps1') -SourceRoot $root
& (Join-Path $root 'Tests/Run-McmFontTests.ps1') -SourceRoot $root
