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

function Assert-PathHasNoReparsePoint {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $currentPath = Get-NormalizedFullPath -Path $Path
    while (-not [string]::IsNullOrWhiteSpace($currentPath)) {
        if (Test-Path -LiteralPath $currentPath) {
            $item = Get-Item -LiteralPath $currentPath -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Release 경로에는 symbolic link 또는 reparse point를 사용할 수 없습니다: $currentPath"
            }
        }

        $parent = [System.IO.DirectoryInfo]::new($currentPath).Parent
        if ($null -eq $parent) {
            break
        }

        $currentPath = $parent.FullName
    }
}

function Assert-TreeHasNoReparsePoint {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    Assert-PathHasNoReparsePoint -Path $Path
    foreach ($item in @(Get-ChildItem -LiteralPath $Path -Force -Recurse)) {
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Release 원본에는 symbolic link 또는 reparse point를 포함할 수 없습니다: $($item.FullName)"
        }
    }
}

function Test-PathIsSameOrDescendant {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ParentPath
    )

    $relativePath = [System.IO.Path]::GetRelativePath(
        (Get-NormalizedFullPath -Path $ParentPath),
        (Get-NormalizedFullPath -Path $Path))
    if ([System.IO.Path]::IsPathRooted($relativePath)) {
        return $false
    }

    return $relativePath -ne ".." -and
        -not $relativePath.StartsWith(
            "..$([System.IO.Path]::DirectorySeparatorChar)",
            (Get-PathComparison))
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

        [string] $SourcePath,

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

    $repositoryArtifactsRoot = Get-NormalizedFullPath -Path (Join-Path $repository "artifacts")
    if ((Test-PathIsSameOrDescendant -Path $outputRoot -ParentPath $repository) -and
        -not (Test-PathIsSameOrDescendant -Path $outputRoot -ParentPath $repositoryArtifactsRoot)) {
        throw "저장소 내부의 Release 출력 위치는 artifacts 아래에 있어야 합니다: $outputRoot"
    }

    if (-not [string]::IsNullOrWhiteSpace($SourcePath)) {
        $source = Get-NormalizedFullPath -Path $SourcePath
        if ((Test-PathIsSameOrDescendant -Path $outputRoot -ParentPath $source) -or
            (Test-PathIsSameOrDescendant -Path $source -ParentPath $outputRoot)) {
            throw "Release 원본과 출력 위치는 서로 같거나 포함 관계일 수 없습니다: $source, $outputRoot"
        }
    }

    Assert-PathHasNoReparsePoint -Path $outputRoot
    $null = New-Item -ItemType Directory -Path $outputRoot -Force
    Assert-PathHasNoReparsePoint -Path $outputRoot
    return $outputRoot
}

function Assert-ReleaseOutputRootContents {
    param(
        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string[]] $AllowedFileNames
    )

    Assert-PathHasNoReparsePoint -Path $OutputRoot
    $comparison = Get-PathComparison
    foreach ($entry in @(Get-ChildItem -LiteralPath $OutputRoot -Force)) {
        if ($entry.PSIsContainer) {
            throw "Release 출력 위치에는 최종 파일만 둘 수 있습니다: $($entry.FullName)"
        }

        $isAllowed = $false
        foreach ($allowedFileName in $AllowedFileNames) {
            if ($entry.Name.Equals($allowedFileName, $comparison)) {
                $isAllowed = $true
                break
            }
        }

        if (-not $isAllowed) {
            throw "Release 출력 위치에 예상하지 않은 파일이 있습니다. 파일을 자동 삭제하지 않았으므로 직접 확인하세요: $($entry.FullName)"
        }

        if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Release 출력 파일에는 symbolic link 또는 reparse point를 사용할 수 없습니다: $($entry.FullName)"
        }
    }
}

function Get-AllowedReleaseOutputFileNames {
    param(
        [Parameter(Mandatory)]
        [string] $Version,

        [switch] $AllowUnsigned
    )

    if ($AllowUnsigned) {
        return @(
            "TimetableGenerator-$Version-osx-arm64-unsigned-smoke.zip",
            "TimetableGenerator-$Version-osx-x64-unsigned-smoke.zip",
            "TimetableGenerator-$Version-win-x64-unsigned-smoke.zip"
        )
    }

    return @(
        "TimetableGenerator-$Version-osx-arm64.zip",
        "TimetableGenerator-$Version-osx-x64.zip",
        "TimetableGenerator-$Version-win-x64.zip",
        "checksums.sha256"
    )
}

function Get-WindowsReleaseArchiveFileName {
    param(
        [Parameter(Mandatory)]
        [string] $Version,

        [ValidateSet("Signed", "Unsigned")]
        [string] $WindowsSignatureMode = "Signed",

        [switch] $AllowUnsigned
    )

    if ($AllowUnsigned -and $WindowsSignatureMode -eq "Unsigned") {
        throw "공식 무서명 Windows 정책과 unsigned smoke 정책은 함께 사용할 수 없습니다."
    }

    if ($AllowUnsigned) {
        return "TimetableGenerator-$Version-win-x64-unsigned-smoke.zip"
    }

    return "TimetableGenerator-$Version-win-x64.zip"
}

function Get-ExpectedReleaseArchiveFileNames {
    param(
        [Parameter(Mandatory)]
        [string] $Version,

        [ValidateSet("Signed", "Unsigned")]
        [string] $WindowsSignatureMode = "Signed"
    )

    return @(
        "TimetableGenerator-$Version-osx-arm64.zip",
        (Get-WindowsReleaseArchiveFileName `
            -Version $Version `
            -WindowsSignatureMode $WindowsSignatureMode)
    )
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

        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Release 출력 파일에는 symbolic link 또는 reparse point를 사용할 수 없습니다: $Path"
        }

        Remove-Item -LiteralPath $Path -Force
    }
}
