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
    # Current TEST installer is intentionally stage-only: it builds the DEV payload
    # under C:\QM_Workshop and prints the explicit developer-console upload command.
    # It must never locate or overwrite Steam's live Workshop content directly.
    foreach ($token in @(
        '$DevWorkshopId = ''3781927679''',
        '$stage = ''C:\QM_Workshop\ItemIntelligence_DEV''',
        '$buildArguments = @{ Mode = ''TEST''; WorkshopStage = $stage }',
        'No Steam upload is performed automatically.',
        '''mod_updateworkshopitem '' + $DevWorkshopId + '' '' + $stage + '' FALSE''')) {
        if ($installText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "DEV stage/install safety token missing: $token" }
    }
    foreach ($forbidden in @('$PublicWorkshopId','Find-LiveDevWorkshopFolder','workshop\content','.qii_install_tmp')) {
        if ($installText.IndexOf($forbidden,[StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "TEST Install.ps1 must remain DEV-stage-only and never touch live/public Workshop content: $forbidden"
        }
    }
    if ([regex]::Matches($installText,'3780078201').Count -ne 1) {
        throw 'TEST Install.ps1 may mention the public Workshop ID only once in the explicit protection message.'
    }
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
