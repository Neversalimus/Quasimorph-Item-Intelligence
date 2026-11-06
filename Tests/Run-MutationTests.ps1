#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$identity = 'qii-mutation-tests-' + [guid]::NewGuid().ToString('N')
$temp = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $identity))
$cases = @(
    @{ File = 'ModMain.LootProbabilityMath.cs'; Before = 'return (1.0 - fraction) * pFloor + fraction * pCeil;'; After = 'return (1.0 - fraction) * pFloor + fraction * pCeil + 0.25;'; Error = 'fractional bonus probability' },
    @{ File = 'ModMain.ProductionUnlockRuntime.cs'; Before = ' + "|" + relationId;'; After = ';'; Error = 'same-name unrelated items must stay separate' },
    @{ File = 'ModMain.LootAvailability.cs'; Before = 'return manualAvailable ?'; After = 'return !manualAvailable ?'; Error = 'blocked manual control must not be suggested' },
    @{ File = 'ModMain.SourceFamilyPolicy.cs'; Before = '"RandomStart_rewardEquipment"'; After = '"General_rewardEquipment"'; Error = 'random-start equipment pool matches game version' },
    @{ File = 'ModMain.CompatibilityFeatureGates.cs'; Before = 'return IsAuditedFeatureAssembly() ||'; After = 'return IsAuditedFeatureAssembly() || _compatStaticChecked ||'; Error = 'Loot modifier fingerprint scope' },
    @{ File = 'ModMain.LootAmputationWeights.cs'; Before = 'result[id] = previous + row.Item1;'; After = 'result[id] = row.Item1;'; Error = 'repeated amputation outcomes retain total weight' },
    @{ File = 'ModMain.LootIndexes.cs'; Before = 'GetMember(entry.Value, "AmputatedDrop")'; After = 'GetMember(entry.Value, "MissingAmputatedDrop")'; Error = 'tuple amputation index contains known outcomes' },
    @{ File = 'ItemDecayIndex.cs'; Test = 'Run-ItemDecayTests.ps1'; Before = 'linked.Add(relation);'; After = '/* omit reverse link */'; Error = 'both reverse sources retained' },
    @{ File = 'ModMain.ItemDecay.cs'; Test = 'Run-ItemDecayTests.ps1'; Before = '!IsExpirableItemRecord(itemRecord)'; After = 'false'; Error = 'only known expirable records with valid output and time indexed' },
    @{ File = 'ModMain.ItemDecay.cs'; Test = 'Run-ItemDecayTests.ps1'; Before = 'ItemDecay.Clear();'; After = '/* retain stale graph */'; Error = 'failed build clears partial graph' },
    @{ File = 'ModMain.DatadiskUnlockExpansion.cs'; Test = 'Run-ItemKnowledgeTests.ps1'; Before = 'foreach (string itemId in setItems) AddKnownDatadiskOutput(result, itemId);'; After = 'AddKnownDatadiskOutput(result, unlockId);'; Error = 'all set pieces link to the chip' },
    @{ File = 'ModMain.DatadiskRuntime.cs'; Test = 'Run-ItemKnowledgeTests.ps1'; Before = 'UnlockPoolSizeByDatadisk[datadiskItemId] = rawPool.Count;'; After = 'UnlockPoolSizeByDatadisk[datadiskItemId] = counts.Count;'; Error = 'original chip pool probability' },
    @{ File = 'ModMain.WeaponSearch.cs'; Test = 'Run-ItemKnowledgeTests.ps1'; Before = 'classId = weapon.WeaponClass.ToString();'; After = 'classId = "Pistol";'; Error = 'plain class search does not need an item-ID keyword' },
    @{ File = 'ModMain.WeaponSearch.cs'; Test = 'Run-ItemKnowledgeTests.ps1'; Before = 'perks.Add(LocalizeWeaponModePayloadTrait(traitId));'; After = '/* omit translated perk */'; Error = 'Chinese class and perk names are searchable' },
    @{ File = 'ModMain.WeaponSearch.cs'; Test = 'Run-ItemKnowledgeTests.ps1'; Before = 'BrowserWeaponPerkTerms.Clear();'; After = '/* retain old language */'; Error = 'language reset clears weapon search metadata' }
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
        $testName = if ($case.ContainsKey('Test')) { $case.Test } else { 'Run-BehaviorTests.ps1' }
        try { & (Join-Path $PSScriptRoot $testName) -SourceRoot $temp }
        catch {
            if ($_.Exception.ToString().Contains($case.Error)) { $caught = $true } else { throw }
        } finally { [IO.File]::WriteAllText($path, $original) }
        if (-not $caught) { throw "Behavior tests missed the mutation in $($case.File)" }
        Write-Host ('Mutation rejected by production behavior tests: ' + $case.File)
    }
    Write-Host ("Mutation sensitivity: PASS ($($cases.Count)/$($cases.Count); original source untouched).") -ForegroundColor Green
} finally {
    if ($temp -ne [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $identity)) -or
        (Split-Path -Leaf $temp) -notmatch '^qii-mutation-tests-[0-9a-f]{32}$') { throw 'Unexpected cleanup path.' }
    Remove-Item -LiteralPath $temp -Recurse -Force
}
