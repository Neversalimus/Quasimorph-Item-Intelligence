# Spoilage owns a small optional core index. No live item is constructed or changed.
& (Join-Path $root 'Tests/Run-ItemDecayTests.ps1') -SourceRoot $root

$decayWiring = @(
    @{ File='ModMain.CoreIndexes.cs'; Method='BuildIndexesSafe'; Call='BuildItemDecayIndex' },
    @{ File='ModMain.CoreIndexes.cs'; Method='ClearIndexes'; Call='ResetItemDecayIndex' },
    @{ File='ModMain.OverviewDashboard.cs'; Method='BuildBrowserOverview'; Call='AppendItemDecayRelations' },
    @{ File='ModMain.BrowserPresentation.cs'; Method='BuildBrowserRecipes'; Call='AppendItemDecayRelations' },
    @{ File='ModMain.OverviewPolicy.cs'; Method='EvaluateOverviewSignals'; Call='GetItemDecayRelationCount' }
)
foreach ($wire in $decayWiring) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $sourceDir $wire.File)))
    $method = @($tree.GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq $wire.Method
    })
    if ($method.Count -ne 1) { throw "Spoilage integration method missing: $($wire.Method)" }
    $calls = @($method[0].DescendantNodes() | Where-Object {
        $_.GetType().Name -eq 'InvocationExpressionSyntax' -and $_.Expression.ToString() -eq $wire.Call
    })
    if ($calls.Count -ne 1) { throw "Spoilage integration call missing or duplicated: $($wire.Method)/$($wire.Call)" }
}
