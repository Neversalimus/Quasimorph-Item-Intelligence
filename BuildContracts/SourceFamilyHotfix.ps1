# ============================================================================
# SOURCE FAMILY HOTFIX / A38 OWNERSHIP CONTRACT
# Current invariant only: the 1.0.3.578 source-family audit owns only
# hardcoded story acquisition literals and random-start General_* pool exposure.
# ============================================================================

$sourceFamilyFeatureGateText = [IO.File]::ReadAllText(
    (Join-Path $sourceDir 'ModMain.CompatibilityFeatureGates.cs'))

foreach ($token in @(
    'AuditedSourceFamilyAssemblySha103Hotfix',
    'IsCurrentSourceFamilyAssembly()',
    'IsAuditedSourceFamilyContractVerified()',
    'A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1')) {
    if ($sourceFamilyFeatureGateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current 1.0.3.578 source-family narrow hotfix gate missing: $token"
    }
}

$sourceFamilyGateBody = [regex]::Match(
    $sourceFamilyFeatureGateText,
    'private static bool IsCurrentSourceFamilyAssembly\(\).*?\n        \}',
    [Text.RegularExpressions.RegexOptions]::Singleline).Value
if ([string]::IsNullOrEmpty($sourceFamilyGateBody) -or
    $sourceFamilyGateBody.IndexOf('AuditedSourceFamilyAssemblySha103Hotfix',[StringComparison]::Ordinal) -lt 0) {
    throw 'current source-family ownership helper is missing or no longer owns the A38 fingerprint.'
}

$auditedFeatureBody = [regex]::Match(
    $sourceFamilyFeatureGateText,
    'private static bool IsAuditedFeatureAssembly\(\).*?\n        \}',
    [Text.RegularExpressions.RegexOptions]::Singleline).Value
if ($auditedFeatureBody.IndexOf(
        'A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1',
        [StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'source-family hotfix must not promote A38 into broad IsAuditedFeatureAssembly ownership.'
}

if ($sourceFamilyFeatureGateText.IndexOf(
        'IsCurrentSourceFamilyAssembly() && _compatLoot ? 1 : -1;',
        [StringComparison]::Ordinal) -lt 0) {
    throw 'source-family contract state is not bound to the narrow current-build ownership helper.'
}
