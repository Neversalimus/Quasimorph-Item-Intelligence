#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$tradeTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.Trade.cs')))
$travel = @($tradeTree.GetRoot().DescendantNodes() | Where-Object {
    $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq 'GetTradeTravelTimeSafe' -and
    $_.ParameterList.Parameters.Count -eq 2
})
if ($travel.Count -ne 1) { throw 'Exact production travel calculation method missing.' }
$context = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.TradeTravelContext.cs'))
$perf = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.TradePerformance.cs'))
$cases = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'TradeRuntimeCases.cs'))
$identity = 'QiiTradeRuntime_' + [guid]::NewGuid().ToString('N')
$code = '#pragma warning disable 0649' + "`n" + $context + "`n" + $perf +
    "`nnamespace ItemIntelligence { public static partial class ModMain {`n" + $travel[0].ToFullString() + "`n} }`n" + $cases
# Move imports to the compilation-unit start before joining complete source files.
$imports = [regex]::Matches($code, '(?m)^using [^;]+;\r?$') | ForEach-Object Value | Select-Object -Unique
$code = ($imports -join "`n") + "`n" + [regex]::Replace($code, '(?m)^using [^;]+;\r?$', '')
$code = $code.Replace('ItemIntelligence', $identity).Replace('UnityEngine', ($identity + '.UnityFixture')).Replace('MGSC', ($identity + '.GameFixture'))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunTradeRuntimeCases()
Write-Host "Production Trade runtime: PASS ($count assertions; live UI transitions, call counts and bounded diagnostics; not Unity FPS)." -ForegroundColor Green
