#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$verboseLoggingFixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VerboseLoggingFixture.cs'))
$source = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.McmFonts.cs'))
$cases = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'McmFontCases.cs'))
$identity = 'QiiMcmFonts_' + [guid]::NewGuid().ToString('N')
# Execute the complete production scope and callbacks. The hierarchy, event dispatch,
# font assets and optional Harmony transport are fixtures, not a Unity renderer.
$code = ("using System.Reflection;`n" + $source + "`n" + $cases).Replace(
    'namespace ItemIntelligence', ('namespace ' + $identity))
$code += "`n" + $verboseLoggingFixture.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunMcmFontCases()
Write-Host "Production MCM font ownership/lifecycle: PASS ($count assertions; Unity/MCM fixtures)." -ForegroundColor Green
