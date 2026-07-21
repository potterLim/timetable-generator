function Assert-NonEmptyFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $item = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $item -or $item.PSIsContainer -or $item.Length -eq 0) {
        throw "필수 Release 파일이 없거나 비어 있습니다: $Path"
    }
}
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
