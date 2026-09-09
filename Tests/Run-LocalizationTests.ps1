#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$trees = @{}
function Read-Members([string]$file, [string]$kind, [string[]]$names) {
    if (-not $trees.ContainsKey($file)) {
        $trees[$file] = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
            [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $file))))
    }
    foreach ($name in $names) {
        $nodes = @($trees[$file].GetRoot().DescendantNodes() | Where-Object {
            $_.GetType().Name -eq $kind -and $_.Identifier.ValueText -eq $name
        })
        if (-not $nodes.Count) { throw "Production member missing: $file / $name" }
        foreach ($node in $nodes) {
            if ($kind -eq 'VariableDeclaratorSyntax') { $node.Parent.Parent.ToFullString() }
            else { $node.ToFullString() }
        }
    }
}
$members = @(
    Read-Members 'ModMain.Localization.cs' 'VariableDeclaratorSyntax' @(
        'ExternalUiTranslations','EnglishUiFallback','MissingUiTranslationKeys','ResolvedUiTextCache',
        '_externalUiTranslationLanguage','_externalUiTranslationFile','_externalUiEnglishLoaded',
        '_cachedGameLanguageSignature','_cachedGameLanguageFrame','GameLanguageRefreshFrames',
        '_localizationManagerType','_localizationManagerTypeResolved')
    Read-Members 'ModMain.Localization.cs' 'MethodDeclarationSyntax' @(
        'NormalizeModLanguageToken','ExternalLanguageMatches','LanguageAliasMatches','ReadLanguageFromOwner',
        'ResolveLocalizationManagerType','ResolveLanguageSignatureUncached','GetLanguageSignature',
        'ProbeVanillaLanguage','LoadUiLanguageFile','ReadUiLanguageDeclaration','ReadUiForceDeclaration',
        'EnsureExternalUiTranslations','Ui','IsEnglishLanguageToken','IsEnglishLanguage')
    Read-Members 'ModMain.DataAccess.cs' 'VariableDeclaratorSyntax' @(
        'InstanceFlags','StaticFlags','InstanceMemberLookupCache','StaticMemberLookupCache')
    Read-Members 'ModMain.DataAccess.cs' 'MethodDeclarationSyntax' @(
        'GetStaticMember','GetMember','FindCachedMember','BuildMemberLookup','AddMemberLookupAlias',
        'NormalizeMemberLookupName','GetMemberValue','FirstNonNull')
    Read-Members 'ModMain.Runtime.cs' 'MethodDeclarationSyntax' @('ConvertToStableString','ContainsCyrillic','NormalizeGameText','IsRussian')
    Read-Members 'ModMain.Runtime.cs' 'ClassDeclarationSyntax' @('LiveMarketEntry')
    Read-Members 'ModMain.Configuration.cs' 'VariableDeclaratorSyntax' @('UiLanguagePreference')
    Read-Members 'ModMain.Configuration.cs' 'MethodDeclarationSyntax' @('TryReadMcmString','AddMcmStringDropdown')
    Read-Members 'ModMain.Hardening.cs' 'MethodDeclarationSyntax' @('ReadUtf8LinesStrict')
    Read-Members 'ModMain.LootContainerChanceMath.cs' 'MethodDeclarationSyntax' @('FormatContainerEstimateNumber')
    Read-Members 'ModMain.LootModifiers.cs' 'MethodDeclarationSyntax' @('FormatExpectedNumber')
    Read-Members 'ModMain.WeaponModeLocalization.cs' 'MethodDeclarationSyntax' @('IsWeaponModeLabelCompatibleWithCurrentLanguage')
    Read-Members 'ModMain.LocalizationFonts.cs' 'VariableDeclaratorSyntax' @('UiMixedFonts','_uiFontSignature','_uiResolvedFont','_uiFontRetryFrame')
    Read-Members 'ModMain.LocalizationFonts.cs' 'MethodDeclarationSyntax' @('FindGameLanguageFont','ResolveUiFont')
) -join "`n"
$whole = foreach ($file in @('LocalizationInternational','LocalizationTextLayout','LocalizationTiming')) {
    $text = [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/ModMain.' + $file + '.cs')))
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text)
    $tree.GetRoot().Members | ForEach-Object { $_.ToFullString() }
}
$cases = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'LocalizationCases.cs'))
$code = "using System; using System.IO; using System.Text; using System.Reflection; using System.Globalization; using System.Collections; using System.Collections.Generic; using TMPro; using UnityEngine; using HarmonyLib;`n" +
    ($whole -join "`n") + "`nnamespace ItemIntelligence { public static partial class ModMain {`n" + $members + "`n} }`n" + $cases
# Keep emitted assembly next to real language files so production path discovery and
# selection run unchanged. Every test run gets a fresh assembly identity and directory.
$identity = 'QiiLocalization_' + [guid]::NewGuid().ToString('N')
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) $identity
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $SourceRoot 'WORKSHOP_CONTENT/Localization') -Destination $fixtureRoot -Recurse
    $dll = Join-Path $fixtureRoot ($identity + '.dll')
    Add-Type -TypeDefinition $code -OutputAssembly $dll -WarningAction Stop
    $assembly = [Reflection.Assembly]::LoadFrom($dll)
    $type = $assembly.GetType($identity + '.ModMain')
    $count = $type::RunLocalizationCases($fixtureRoot)
    Write-Host "Production localization behavior: PASS ($count assertions)." -ForegroundColor Green
} finally {
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
}
