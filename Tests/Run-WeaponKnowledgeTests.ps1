#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$SourceRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$GameTableInventory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$verboseLoggingFixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VerboseLoggingFixture.cs'))
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$pieces = foreach ($name in @('ModMain.WeaponModeDamageType.cs','ModMain.WeaponModePayloadEffects.cs',
    'ModMain.GrenadeAmmoRelations.cs','ModMain.ArmWeaponLinks.cs')) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $name))))
    $errors = @($tree.GetDiagnostics() | Where-Object Severity -eq 'Error')
    if ($errors.Count) { throw ($errors -join "`n") }
    $tree.GetRoot().Members | ForEach-Object ToFullString
}
$runtime = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.Runtime.cs')))
$members = foreach ($name in @('WeaponInfo','WeaponModeDescriptor','AddToList')) {
    $found = @($runtime.GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -in @('ClassDeclarationSyntax','MethodDeclarationSyntax') -and
        $_.Identifier.ValueText -eq $name
    })
    if ($found.Count -ne 1) { throw "Expected one production member: $name" }
    $found[0].ToFullString()
}
$identity = 'QiiWeaponKnowledge_' + [guid]::NewGuid().ToString('N')
$fixtures = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'WeaponKnowledgeCases.cs'))
$code = "using System; using System.Collections; using System.Collections.Generic; using System.Linq; using MGSC;`n" +
    ($pieces -join "`n") + "`nnamespace ItemIntelligence { public static partial class ModMain {`n" +
    ($members -join "`n") + "`n} }`n" + $fixtures
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
$code = $code.Replace('namespace MGSC', ('namespace ' + $identity + '.MGSC'))
$code = $code.Replace('using MGSC;', ('using ' + $identity + '.MGSC;'))
$code += "`n" + $verboseLoggingFixture.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunWeaponKnowledgeCases()
Write-Host "Production weapon knowledge: PASS ($count assertions; damage, effects, launcher reverse links and installed limbs)." -ForegroundColor Green

if ($GameTableInventory) {
    $tables = Get-Content -LiteralPath $GameTableInventory -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    $type::ResetFixture()
    foreach ($row in $tables.items.ammo.Values) {
        $type::AddAmmo($row.Id, [string]$row.DmgType, [string]$row.Traits, [string]$row.StatusEffectId)
    }
    foreach ($section in @('meleeweapons','rangeweapons')) {
        foreach ($row in $tables.items[$section].Values) {
            $type::AddWeapon($row.Id, $section -eq 'meleeweapons', [string]$row.Firemodes,
                [string]$row.OverrideAmmo, [string]$row.DefaultAmmoId, [string]$row['DefaultGrenadeId'],
                [string]$row['AllowedGrenadeIds'], $row.WeaponClass -eq 'GrenadeLauncher')
        }
    }
    foreach ($row in $tables.items.grenades.Values) { $type::AddGrenade($row.Id, [string]$row.Explosion) }
    foreach ($row in $tables.properties.explosions.Values) {
        $type::AddExplosion($row.Id, [string]$row['Parameters.DamageType'], $row['Parameters.GainDmgToCreature'] -eq 'TRUE')
    }
    foreach ($row in $tables.wounds.woundslots.Values) { $type::AddSlot($row.Id, [string]$row.BareHandWeapon) }
    foreach ($row in $tables.items.augmentations.Values) { $type::AddAugmentation($row.Id, [string]$row.WoundSlotIds) }
    $report = $type::RunAuditedTableCases()
    Write-Host ("Installed game table audit: PASS ($report).") -ForegroundColor Green
}
