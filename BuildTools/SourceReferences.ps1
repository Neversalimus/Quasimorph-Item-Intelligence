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
