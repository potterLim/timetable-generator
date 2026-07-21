function Get-PathComparison {
    if ($IsWindows) {
        return [System.StringComparison]::OrdinalIgnoreCase
    }

    return [System.StringComparison]::Ordinal
}

function Get-NormalizedFullPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetPathRoot($fullPath)
    if ($fullPath.Equals($rootPath, (Get-PathComparison))) {
        return $fullPath
    }

    return $fullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

function Resolve-PathFromRepository {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return Get-NormalizedFullPath -Path $Path
    }

    return Get-NormalizedFullPath -Path (Join-Path $RepositoryRoot $Path)
}

function Resolve-ReleaseOutputRoot {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $Version,

        [string] $RequestedOutputRoot,

        [switch] $AllowUnsigned
    )

    if ([string]::IsNullOrWhiteSpace($RequestedOutputRoot)) {
        $relativePath = if ($AllowUnsigned) {
            "artifacts/release-smoke/$Version"
        }
        else {
            "artifacts/release/$Version"
        }

        $candidate = Join-Path $RepositoryRoot $relativePath
    }
    else {
        $candidate = Resolve-PathFromRepository `
            -RepositoryRoot $RepositoryRoot `
            -Path $RequestedOutputRoot
    }

    $outputRoot = Get-NormalizedFullPath -Path $candidate
    $repository = Get-NormalizedFullPath -Path $RepositoryRoot
    $volumeRoot = Get-NormalizedFullPath -Path ([System.IO.Path]::GetPathRoot($outputRoot))
    $comparison = Get-PathComparison
    if ($outputRoot.Equals($repository, $comparison) -or
        $outputRoot.Equals($volumeRoot, $comparison)) {
        throw "저장소 또는 파일 시스템 루트를 Release 출력 위치로 사용할 수 없습니다: $outputRoot"
    }

    $null = New-Item -ItemType Directory -Path $outputRoot -Force
    return $outputRoot
}

function Assert-DirectChildFilePath {
    param(
        [Parameter(Mandatory)]
        [string] $ParentPath,

        [Parameter(Mandatory)]
        [string] $ChildPath,

        [Parameter(Mandatory)]
        [string] $ExpectedFileName
    )

    $parent = Get-NormalizedFullPath -Path $ParentPath
    $child = Get-NormalizedFullPath -Path $ChildPath
    $childParent = [System.IO.DirectoryInfo]::new($child).Parent
    $comparison = Get-PathComparison
    if ($null -eq $childParent -or
        $childParent.FullName.Equals($parent, $comparison) -eq $false -or
        [System.IO.Path]::GetFileName($child).Equals($ExpectedFileName, $comparison) -eq $false) {
        throw "Release 출력 파일은 지정한 출력 디렉터리의 정확한 직접 하위 파일이어야 합니다: $child"
    }
}

function Remove-ExistingReleaseFile {
    param(
        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ExpectedFileName
    )

    Assert-DirectChildFilePath `
        -ParentPath $OutputRoot `
        -ChildPath $Path `
        -ExpectedFileName $ExpectedFileName

    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path -Force
        if ($item.PSIsContainer) {
            throw "Release 출력 파일 위치에 디렉터리가 있습니다: $Path"
        }

        Remove-Item -LiteralPath $Path -Force
    }
}
