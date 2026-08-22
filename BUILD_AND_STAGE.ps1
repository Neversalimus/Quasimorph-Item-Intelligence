param(
    [ValidateSet('TEST','RELEASE')]
    [string]$Mode = 'TEST',
    [string]$GameRoot = '',
    [string]$WorkshopStage = '',
    [switch]$ContractsOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$DevWorkshopId = '3781927679'
$PublicWorkshopId = '3780078201'
$ExpectedReleaseVersion = '1.7.41.3'

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

function Assert-WorkshopContentFilenameHygiene {
    param([string]$Root)
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Workshop content root missing: $Root" }
    foreach ($file in (Get-ChildItem -LiteralPath $Root -File -Recurse)) {
        $relative = $file.FullName.Substring($Root.Length).TrimStart('\','/')
        if ($relative -match '(?i)(^|[\\/._ -])(test[0-9]*|debug|audit|dev|development)(?=$|[\\/._ -])') {
            throw "Workshop content hygiene failed: development artifact filename is forbidden: $relative"
        }
    }
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $root 'Source'
$content = Join-Path $root 'WORKSHOP_CONTENT'
$manifest = Join-Path $content 'modmanifest.json'
foreach ($required in @($sourceDir,$manifest)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required path missing: $required" }
}

Assert-No-PowerShellVariableColonHazards -Path $MyInvocation.MyCommand.Path
Assert-WorkshopContentFilenameHygiene -Root $content

$contractsScript = Join-Path $root 'BUILD_CONTRACTS.ps1'
if (-not (Test-Path -LiteralPath $contractsScript -PathType Leaf)) { throw 'BUILD_CONTRACTS.ps1 not found.' }
Assert-No-PowerShellVariableColonHazards -Path $contractsScript
$buildOrchestratorLines = (Get-Content -LiteralPath $MyInvocation.MyCommand.Path).Count
if ($buildOrchestratorLines -gt 350) { throw "BUILD_AND_STAGE.ps1 orchestration budget exceeded: $buildOrchestratorLines/350. Static contracts belong in BuildContracts/." }
$contractCoordinatorLines = (Get-Content -LiteralPath $contractsScript).Count
if ($contractCoordinatorLines -gt 80) { throw "BUILD_CONTRACTS.ps1 coordinator budget exceeded: $contractCoordinatorLines/80. Split contract ownership instead of growing the coordinator." }
. $contractsScript
if ($ContractsOnly) {
    Write-Host 'Static build contracts: PASS' -ForegroundColor Green
    exit 0
}

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
Assert-WorkshopContentFilenameHygiene -Root $WorkshopStage
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
    ('WeaponModeCriticalDamagePerAPLines=' + $weaponModeCriticalDamagePerApLines),
    ('FactionTechnologyNavigationLines=' + $factionTechnologyNavigationLines),
    ('FactionTechnologyPanelResolverLines=' + $factionTechnologyPanelResolverLines),
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
    ('LootContainerSaveEstimateLines=' + $lootContainerSaveEstimateLines),
    ('LootPresentationLines=' + $lootPresentationLines),
    ('LootBaronSpecialLines=' + $lootBaronSpecialLines),
    ('LootBaronUltimateDataLines=' + $lootBaronUltimateDataLines),
    ('LootBaronDataLines=' + $lootBaronDataLines),
    ('OverviewBaronSpecialLines=' + $overviewBaronSpecialLines),
    ('LootModifiersLines=' + $lootModifiersLines),
    ('LootEnemyPresentationLines=' + $lootEnemyPresentationLines),
    ('LootModifierRuntimeLines=' + $lootModifierRuntimeLines),
    ('ModderSpawnRuntimeLines=' + $modderSpawnRuntimeLines),
    ('ModderSpawnPanelLines=' + $modderSpawnPanelLines),
    ('BrowserLinkPresentationLines=' + $browserLinkPresentationLines),
    ('LocalizationParity=' + $langCount + '/' + $langCount + '/' + $langCount),
    ('OrdinaryReadOnlyArchitecture=OK'),
    ('ModderModeSingleItemSpawnException=OK'),
    ('MathSafetyProperties=OK'),
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
