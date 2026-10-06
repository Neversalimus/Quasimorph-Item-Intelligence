# ============================================================================
# SOURCE FAMILY / VERSIONED OWNERSHIP CONTRACT
# Independent source-family audits own hardcoded story acquisition and
# random-start pool exposure. 1.0.4 requires its own RandomStart_* selection.
# ============================================================================

$sourceFamilyFeatureGateText = [IO.File]::ReadAllText(
    (Join-Path $sourceDir 'ModMain.CompatibilityFeatureGates.cs'))

foreach ($token in @(
    'AuditedSourceFamilyAssemblySha103Hotfix',
    'AuditedSourceFamilyAssemblySha104',
    'AuditedSourceFamilyAssemblySha104582',
    'AuditedSourceFamilyAssemblySha104590',
    'IsCurrentSourceFamilyAssembly()',
    'IsAuditedSourceFamilyContractVerified()',
    'A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1',
    'D72CD91F98DE4D6CCE8EE420AC1A906D89E68BA0CB164694D266B56F46339EBE')) {
    if ($sourceFamilyFeatureGateText.IndexOf($token,[StringComparison]::Ordinal) -lt 0) {
        throw "current 1.0.3.578 source-family narrow hotfix gate missing: $token"
    }
}

$sourceFamilyGateBody = [regex]::Match(
    $sourceFamilyFeatureGateText,
    'private static bool IsCurrentSourceFamilyAssembly\(\).*?\n        \}',
    [Text.RegularExpressions.RegexOptions]::Singleline).Value
if ([string]::IsNullOrEmpty($sourceFamilyGateBody) -or
    $sourceFamilyGateBody.IndexOf('AuditedSourceFamilyAssemblySha103Hotfix',[StringComparison]::Ordinal) -lt 0 -or
    $sourceFamilyGateBody.IndexOf('AuditedSourceFamilyAssemblySha104',[StringComparison]::Ordinal) -lt 0 -or
    $sourceFamilyGateBody.IndexOf('AuditedSourceFamilyAssemblySha104582',[StringComparison]::Ordinal) -lt 0 -or
    $sourceFamilyGateBody.IndexOf('AuditedSourceFamilyAssemblySha104590',[StringComparison]::Ordinal) -lt 0) {
    throw 'current source-family helper must own the independently audited fingerprints.'
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

