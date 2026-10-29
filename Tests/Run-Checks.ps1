#requires -Version 7.0
[CmdletBinding()]
param([switch]$IncludeMutationTests)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$checkRoot = Split-Path -Parent $PSScriptRoot
$checkEngine = Join-Path $PSHOME 'pwsh.exe'
$checkRuntime = [IO.File]::ReadAllText((Join-Path $checkRoot 'Source/ModMain.Runtime.cs'))
$checkVersion = [regex]::Match($checkRuntime, 'public const string Version = "([^"]+)";')
if (-not $checkVersion.Success) { throw 'Cannot determine source version for checks.' }
$checkMode = if ($checkVersion.Groups[1].Value -match '-(?:test|rc|dev)') { 'TEST' } else { 'RELEASE' }

function Invoke-QiiCheck {
    param([string]$CheckScript, [string[]]$CheckArguments = @())
    # Isolate Add-Type assemblies, fixture state and native exit codes per suite.
    & $checkEngine -NoProfile -File $CheckScript @CheckArguments
    if ($LASTEXITCODE -ne 0) { throw "Check failed: ${CheckScript} (exit $LASTEXITCODE)." }
}

Invoke-QiiCheck (Join-Path $checkRoot 'BUILD_AND_STAGE.ps1') @('-Mode', $checkMode, '-ContractsOnly')
if ($IncludeMutationTests) {
    foreach ($checkName in @('Run-MutationTests.ps1', 'Run-LocalizationMutationTests.ps1', 'Run-CacheMutationTests.ps1')) {
        Invoke-QiiCheck (Join-Path $PSScriptRoot $checkName)
    }
}
Write-Host 'All requested checks passed; no game installation or staging required.' -ForegroundColor Green
