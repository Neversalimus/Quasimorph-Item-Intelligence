#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) { Add-Type -Path (Join-Path $PSHOME $name) }
$identity = 'QiiItemDecay_' + [guid]::NewGuid().ToString('N')
$pieces = foreach ($name in @('ItemDecayIndex.cs','ModMain.ItemDecay.cs','ModMain.BrowserModels.cs')) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $SourceRoot ('Source/' + $name))))
    $errors = @($tree.GetDiagnostics() | Where-Object Severity -eq 'Error')
    if ($errors.Count) { throw ($errors -join "`n") }
    $tree.GetRoot().Members | ForEach-Object ToFullString
}
$code = "using System; using System.Collections.Generic; using System.Globalization; using MGSC;`n" + ($pieces -join "`n") +
    "`n" + [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ItemDecayCases.cs')) +
    "`n" + [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ItemDecayAdapterCases.cs'))
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity)).
    Replace('namespace MGSC', ('namespace ' + $identity + '.MGSC')).
    Replace('using MGSC;', ('using ' + $identity + '.MGSC;'))
Add-Type -TypeDefinition $code -WarningAction Stop
$owner = ($identity + '.ItemDecayCases') -as [type]
$count = $owner::Run()
Write-Host "Production item spoilage index: PASS ($count assertions; direct/reverse links, chains, disappearance and reset)." -ForegroundColor Green
$adapter = ($identity + '.ModMain') -as [type]
$count = $adapter::RunItemDecayAdapterCases()
Write-Host "Production item spoilage adapter: PASS ($count assertions; game-record fixtures and actual clickable browser rows)." -ForegroundColor Green
