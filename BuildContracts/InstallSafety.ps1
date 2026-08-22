# ============================================================================
# INSTALL / WORKSHOP STAGING SAFETY
# Mode-specific installer invariants. TEST may touch only the known DEV item;
# RELEASE may build a local public staging payload but may never edit Steam's
# live subscribed Workshop content or publish automatically.
# ============================================================================

$installPath = Join-Path $root 'Install.ps1'
if (-not (Test-Path -LiteralPath $installPath -PathType Leaf)) { throw 'Install.ps1 is missing.' }
$installText = Read-Utf8Strict -Path $installPath

if ($Mode -eq 'TEST') {
    foreach ($token in @(
        '$DevWorkshopId = ''3781927679''',
        '$buildArguments = @{ Mode = ''TEST'';',
        'Find-LiveDevWorkshopFolder $resolvedGame $DevWorkshopId',
        '$tempLive = Join-Path $liveParent ($DevWorkshopId + ''.qii_install_tmp'')',
        '''mod_updateworkshopitem '' + $DevWorkshopId')) {
        if ($installText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "DEV install safety token missing: $token" }
    }
    if ($installText.IndexOf('$PublicWorkshopId',[StringComparison]::Ordinal) -ge 0 -or
        [regex]::Matches($installText,'3780078201').Count -ne 1) { throw 'TEST Install.ps1 must never target the public Workshop item.' }
} else {
    foreach ($token in @(
        '$PublicWorkshopId = ''3780078201''',
        '$stage = ''C:\QM_Workshop\ItemIntelligence''',
        '$buildArguments = @{ Mode = ''RELEASE''; WorkshopStage = $stage }',
        'No Steam upload is performed automatically.',
        'does not overwrite the live subscribed Workshop copy')) {
        if ($installText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "RELEASE staging safety token missing: $token" }
    }
    foreach ($forbidden in @('$DevWorkshopId','Find-LiveDevWorkshopFolder','workshop\content')) {
        if ($installText.IndexOf($forbidden,[StringComparison]::OrdinalIgnoreCase) -ge 0) { throw "RELEASE Install.ps1 must remain stage-only: $forbidden" }
    }
}
