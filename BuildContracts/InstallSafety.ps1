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

# Both installers prepare a package beside the source tree. The build script owns
# Workshop IDs and validates any explicit stage path; Install never deploys/uploads.
$installerModeToken = '$buildArguments = @{ Mode = ''' + $Mode + '''; WorkshopStage = $stage }'
foreach ($token in @(
    'Join-Path $root ''_stage\ItemIntelligence''',
    $installerModeToken,
    'No Steam upload is performed automatically.',
    'does not overwrite the live subscribed Workshop copy')) {
    if ($installText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) { throw "$Mode staging safety token missing: $token" }
}
foreach ($forbidden in @('$PublicWorkshopId','$DevWorkshopId','Find-LiveDevWorkshopFolder','workshop\content','.qii_install_tmp','mod_updateworkshopitem')) {
    if ($installText.IndexOf($forbidden,[StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Install.ps1 must remain stage-only and never touch live Workshop content: $forbidden"
    }
}
