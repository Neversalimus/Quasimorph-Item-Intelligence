#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    Add-Type -Path (Join-Path $PSHOME $name)
}
$tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.Runtime.cs')))
$normalize = @($tree.GetRoot().DescendantNodes() | Where-Object {
    $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq 'NormalizeGameText'
})
if ($normalize.Count -ne 1) { throw 'Expected one production NormalizeGameText declaration.' }
$identity = 'QiiUiWrite_' + [guid]::NewGuid().ToString('N')
$code = [IO.File]::ReadAllText((Join-Path $SourceRoot 'Source/ModMain.BrowserUiDirtySuppression.cs')) +
    "`nnamespace ItemIntelligence { public static partial class ModMain {`n" + $normalize[0].ToFullString() + "`n} }`n" +
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UiWriteCases.cs'))
$code = $code.Replace('namespace ItemIntelligence', ('namespace ' + $identity))
$code = $code.Replace('UnityEngine', ($identity + '.UnityEngine')).Replace('TMPro', ($identity + '.TMPro'))
Add-Type -TypeDefinition $code -WarningAction Stop
$type = ($identity + '.ModMain') -as [type]
$count = $type::RunUiWriteCases()
Write-Host "Production UI write guards: PASS ($count assertions; counted setters, not Unity frame timing)." -ForegroundColor Green
