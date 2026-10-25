# ============================================================================
# INSTALL / WORKSHOP STAGING SAFETY
# Mode-specific installer invariants. TEST prepares a local package only;
# RELEASE may build a local public staging payload but may never edit Steam's
# live subscribed Workshop content or publish automatically.
# ============================================================================

$installPath = Join-Path $root 'Install.ps1'
if (-not (Test-Path -LiteralPath $installPath -PathType Leaf)) { throw 'Install.ps1 is missing.' }
$installText = Read-Utf8Strict -Path $installPath
& (Join-Path $root 'Tests/Run-StagePathTests.ps1') -SourceRoot $root

if ($Mode -eq 'TEST') {
    # The test installer defaults to a package beside the source tree. Installation
    # into a live game and any Workshop publication are separate, explicit actions.
    foreach ($token in @(
        'Join-Path $root ''_stage\ItemIntelligence''',
        '$buildArguments = @{ Mode = ''TEST''; WorkshopStage = $stage }',
        'No Steam upload is performed automatically.',
        'does not overwrite the live subscribed Workshop copy')) {
        if ($installText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "TEST staging safety token missing: $token" }
    }
    foreach ($forbidden in @('$PublicWorkshopId','$DevWorkshopId','Find-LiveDevWorkshopFolder','workshop\content','.qii_install_tmp','mod_updateworkshopitem')) {
        if ($installText.IndexOf($forbidden,[StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "TEST Install.ps1 must remain stage-only and never touch live Workshop content: $forbidden"
        }
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
