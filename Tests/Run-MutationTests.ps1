#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$temp = Join-Path ([IO.Path]::GetTempPath()) ('qii-mutation-tests-' + [guid]::NewGuid().ToString('N'))
$cases = @(
    @{ File = 'ModMain.LootProbabilityMath.cs'; Before = 'return (1.0 - fraction) * pFloor + fraction * pCeil;'; After = 'return (1.0 - fraction) * pFloor + fraction * pCeil + 0.25;'; Error = 'fractional bonus probability' },
    @{ File = 'ModMain.ProductionUnlockRuntime.cs'; Before = ' + "|" + relationId;'; After = ';'; Error = 'same-name unrelated items must stay separate' },
    @{ File = 'ModMain.LootAvailability.cs'; Before = 'return manualAvailable ?'; After = 'return !manualAvailable ?'; Error = 'blocked manual control must not be suggested' },
    @{ File = 'ModMain.SourceFamilyPolicy.cs'; Before = '"RandomStart_rewardEquipment"'; After = '"General_rewardEquipment"'; Error = 'random-start equipment pool matches game version' },
    @{ File = 'ModMain.CompatibilityFeatureGates.cs'; Before = 'return IsAuditedFeatureAssembly() ||'; After = 'return IsAuditedFeatureAssembly() || _compatStaticChecked ||'; Error = 'Loot modifier fingerprint scope' },
    @{ File = 'ModMain.LootAmputationWeights.cs'; Before = 'result[id] = previous + row.Item1;'; After = 'result[id] = row.Item1;'; Error = 'repeated amputation outcomes retain total weight' },
    @{ File = 'ModMain.LootIndexes.cs'; Before = 'GetMember(entry.Value, "AmputatedDrop")'; After = 'GetMember(entry.Value, "MissingAmputatedDrop")'; Error = 'tuple amputation index contains known outcomes' }
)
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $root 'Source') -Destination $temp -Recurse
    foreach ($case in $cases) {
        $path = Join-Path $temp ('Source/' + $case.File)
        $original = [IO.File]::ReadAllText($path)
        if (-not $original.Contains($case.Before)) { throw "Mutation target missing: $($case.File)" }
        [IO.File]::WriteAllText($path, $original.Replace($case.Before, $case.After))
        $caught = $false
        try { & (Join-Path $PSScriptRoot 'Run-BehaviorTests.ps1') -SourceRoot $temp }
        catch {
            if ($_.Exception.ToString().Contains($case.Error)) { $caught = $true } else { throw }
        } finally { [IO.File]::WriteAllText($path, $original) }
        if (-not $caught) { throw "Behavior tests missed the mutation in $($case.File)" }
        Write-Host ('Mutation rejected by production behavior tests: ' + $case.File)
    }
    Write-Host ("Mutation sensitivity: PASS ($($cases.Count)/$($cases.Count); original source untouched).") -ForegroundColor Green
} finally { Remove-Item -LiteralPath $temp -Recurse -Force }
