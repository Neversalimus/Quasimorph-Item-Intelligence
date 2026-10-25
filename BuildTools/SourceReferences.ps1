# Count textual identifier mentions once. This preserves the existing heuristic,
# including comments and strings; it is not a call graph or reachability analysis.
function Get-QiiIdentifierCounts([string]$Text) {
    $counts = [Collections.Generic.Dictionary[string,int]]::new([StringComparer]::Ordinal)
    foreach ($token in [regex]::Matches($Text, '\b[A-Za-z_]\w*\b')) {
        $name = $token.Value
        $count = 0
        if ($counts.TryGetValue($name, [ref]$count)) { $counts[$name] = $count + 1 }
        else { $counts.Add($name, 1) }
    }
    return ,$counts
}

# Retired-code gates inspect declarations, not comments, string values or substrings.
function Get-QiiDeclaredIdentifiers([string]$Text) {
    foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
        Add-Type -Path (Join-Path $PSHOME $name)
    }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($Text)
    foreach ($node in $tree.GetRoot().DescendantNodes()) {
        if ($node.GetType().Name -in @('MethodDeclarationSyntax','VariableDeclaratorSyntax','PropertyDeclarationSyntax',
                'ClassDeclarationSyntax','StructDeclarationSyntax','EnumDeclarationSyntax','DelegateDeclarationSyntax')) {
            $null = $names.Add($node.Identifier.ValueText)
        }
    }
    return ,$names
}
