#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$identity = 'QiiReflection_' + [guid]::NewGuid().ToString('N')
$code = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ReflectionDataReader.cs')) + "`n" +
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ReflectionCases.cs'))
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ReflectionCases') -as [type]
$count = $type::Run()
Write-Host "Production reflection reader: PASS ($count assertions)." -ForegroundColor Green
