#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$trees = @{}
function Read-PriceMethod([string]$file, [string]$name) {
    if (-not $trees.ContainsKey($file)) {
        $trees[$file] = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
            [IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/ModMain.' + $file + '.cs'))))
    }
    $found = @($trees[$file].GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq $name
    })
    if ($found.Count -ne 1) { throw "Production price method missing/ambiguous: $name" }
    $text = $found[0].ToFullString()
    if ($text.Contains('TypeByName') -or $text.Contains('GetMethods(')) { throw "Repeated API discovery returned to price adapter: $name" }
    $text
}
$members = @(
    Read-PriceMethod 'Runtime' 'GetContainerItemCount'
    Read-PriceMethod 'Runtime' 'TryGetExactStationPrice'
    Read-PriceMethod 'Runtime' 'TryGetExactStationPanelPrice103'
    Read-PriceMethod 'TradeBatchPricing103' 'TryGetExactStationBatchPrice103'
    Read-PriceMethod 'TradeBatchPricing103' 'GetTradeBatchSampleQuantity'
) -join "`n"
$api = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.TradePriceContracts.cs'))
$cases = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'TradePricingCases.cs'))
$code = $api + "`nnamespace ItemIntelligence { public static partial class ModMain {`n" + $members + "`n} }`n" + $cases
$imports = [regex]::Matches($code, '(?m)^using [^;]+;\r?$') | ForEach-Object Value | Select-Object -Unique
$code = "#pragma warning disable 0649`n" + ($imports -join "`n") + "`n" + [regex]::Replace($code, '(?m)^using [^;]+;\r?$', '')
$identity = 'QiiTradePricing_' + [guid]::NewGuid().ToString('N')
$code = $code.Replace('ItemIntelligence', $identity).Replace('UnityEngine', ($identity + '.UnityFixture')).Replace('MGSC', ($identity + '.GameFixture'))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunTradePricingCases()
Write-Host "Production Trade price adapters: PASS ($count assertions; exact API calls, rounding, batch totals, live state and fail-closed cases; vanilla API fixtures)." -ForegroundColor Green
