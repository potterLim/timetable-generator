function Assert-NoDebugSymbols {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $pdbFiles = @(Get-ChildItem -LiteralPath $Path -Filter "*.pdb" -File -Recurse)
    if ($pdbFiles.Count -ne 0) {
        throw "Release 산출물에 PDB가 포함되어 있습니다: $($pdbFiles[0].FullName)"
    }
}
