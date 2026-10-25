#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
# Numeric research bodies must remain byte-identical to the validated source.
# The decorator is tested below; these checks also protect the real research math.
$researchBaselines = @(
    @('Source/ModMain.BrowserPresentation.cs','BuildBrowserMagnumResearch','8169C5324BE6B31B372E3DC00FFB7730C8E37467C44A100BEB150418D6F7DDEF'),
    @('Source/ModMain.Information.cs','GetVisibleMagnumRequired','6C9EA44544F3B37D81323E7BAEB85240E22379D6C61018A8705F142470BF6CAF'),
    @('Source/ModMain.Information.cs','GetMagnumSnapshot','637F271154EF9F394D5A77D048E0B962D2D7B985A809A971DFBE432F061AB763')
)
foreach ($baseline in $researchBaselines) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot $baseline[0])))
    $methods = @($tree.GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq $baseline[1]
    })
    if ($methods.Count -ne 1) { throw "Research owner missing or duplicated: $($baseline[1])" }
    $body = $methods[0].Body.ToString().Replace("`r`n", "`n")
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($body))).Replace('-','') }
    finally { $hasher.Dispose() }
    if ($hash -cne $baseline[2]) { throw "Numeric research body changed: $($baseline[1])" }
}
$pieces = foreach ($name in @('ModMain.GenomeUpgradeUseReader.cs','ModMain.GenomeUpgradeUses.cs','ModMain.UpgradeUsesPresentation.cs')) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $name))))
    $errors = @($tree.GetDiagnostics() | Where-Object Severity -eq 'Error')
    if ($errors.Count) { throw ($errors -join [Environment]::NewLine) }
    $tree.GetRoot().Members | ForEach-Object ToFullString
}
$cases = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UpgradeUseCases.cs'))
$verboseLoggingFixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VerboseLoggingFixture.cs'))
$identity = 'QiiUpgradeUses_' + [guid]::NewGuid().ToString('N')
$code = "using System; using System.Collections; using System.Collections.Generic; using System.Reflection;`nusing $identity.Fixtures;`n" +
    ($pieces -join "`n") + "`n" + $cases
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
$code = $code.Replace('UpgradeUseFixtures', ($identity + '.Fixtures'))
$code += "`n" + $verboseLoggingFixture.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunUpgradeUseCases()
Write-Host "Production genome upgrade-use reader/decorator: PASS ($count assertions; game schema fixtures, not Unity runtime)." -ForegroundColor Green
