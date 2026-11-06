& (Join-Path $root 'Tests/Run-ItemKnowledgeTests.ps1') -SourceRoot $root
# Integration seams are checked separately from the behavior of the actual parser/index.
foreach ($wire in @(
    @{File='ModMain.Ammo.cs'; Method='ResetAmmoKnowledgeIndexState'; Call='ResetDatadiskUnlockExpansion'},
    @{File='ModMain.BrowserAdvancedSearch.cs'; Method='IndexBrowserAdvancedSearchMetadata'; Call='IndexBrowserWeaponSearchMetadata'},
    @{File='ModMain.BrowserCatalog.cs'; Method='RefreshBrowserCatalog'; Call='BrowserCatalogItemMatchesWeaponClass'},
    @{File='ModMain.BrowserCatalogPresentation.cs'; Method='CreateBrowserCatalogUi'; Call='CreateBrowserWeaponTypeUi'},
    @{File='ModMain.BrowserCatalogPresentation.cs'; Method='UpdateBrowserCatalogControls'; Call='UpdateBrowserWeaponTypeControl'})) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $sourceDir $wire.File)))
    $method = @($tree.GetRoot().DescendantNodes() | Where-Object { $_.GetType().Name -eq 'MethodDeclarationSyntax' -and $_.Identifier.ValueText -eq $wire.Method })
    if ($method.Count -ne 1 -or -not @($method[0].DescendantNodes() | Where-Object {
        $_.GetType().Name -eq 'InvocationExpressionSyntax' -and $_.Expression.ToString() -eq $wire.Call
    }).Count) { throw "Item knowledge integration missing: $($wire.Method)/$($wire.Call)" }
}
