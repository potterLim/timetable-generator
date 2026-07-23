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
    $volumeRoot = [System.IO.Path]::GetPathRoot($fullPath)
    $comparison = Get-PathComparison
    if ($fullPath.Equals($volumeRoot, $comparison)) {
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
                throw "게시 경로에는 symbolic link 또는 reparse point를 사용할 수 없습니다: $currentPath"
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
            throw "게시 원본에는 symbolic link 또는 reparse point를 포함할 수 없습니다: $($item.FullName)"
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

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet 명령이 종료 코드 $LASTEXITCODE 로 실패했습니다."
    }
}

function Get-DesktopProjectMetadata {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath
    )

    $metadataJson = & dotnet msbuild $ProjectPath `
        --nologo `
        -getProperty:Version `
        -getProperty:AssemblyName `
        -property:Configuration=Release

    if ($LASTEXITCODE -ne 0) {
        throw "게시 프로젝트 메타데이터를 읽을 수 없습니다."
    }

    $metadata = $metadataJson | ConvertFrom-Json
    $assemblyName = [string] $metadata.Properties.AssemblyName
    $version = [string] $metadata.Properties.Version
    if ([string]::IsNullOrWhiteSpace($assemblyName) -or [string]::IsNullOrWhiteSpace($version)) {
        throw "게시 프로젝트의 AssemblyName 또는 Version이 비어 있습니다."
    }

    return [pscustomobject]@{
        AssemblyName = $assemblyName
        Version = $version
    }
}

function Resolve-DistributionOutputRoot {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [string] $RequestedOutputRoot
    )

    $resolvedRepositoryRoot = Get-NormalizedFullPath -Path $RepositoryRoot

    if ([string]::IsNullOrWhiteSpace($RequestedOutputRoot)) {
        $candidate = Join-Path $resolvedRepositoryRoot "artifacts/publish"
    }
    elseif ([System.IO.Path]::IsPathRooted($RequestedOutputRoot)) {
        $candidate = $RequestedOutputRoot
    }
    else {
        $candidate = Join-Path $resolvedRepositoryRoot $RequestedOutputRoot
    }

    $resolvedOutputRoot = Get-NormalizedFullPath -Path $candidate
    $volumeRoot = Get-NormalizedFullPath -Path ([System.IO.Path]::GetPathRoot($resolvedOutputRoot))
    $comparison = Get-PathComparison

    if ($resolvedOutputRoot.Equals($volumeRoot, $comparison)) {
        throw "파일 시스템 루트는 게시 출력 위치로 사용할 수 없습니다: $resolvedOutputRoot"
    }

    if ($resolvedOutputRoot.Equals($resolvedRepositoryRoot, $comparison)) {
        throw "저장소 루트는 게시 출력 위치로 사용할 수 없습니다: $resolvedOutputRoot"
    }

    $repositoryArtifactsRoot = Get-NormalizedFullPath -Path (Join-Path $resolvedRepositoryRoot "artifacts")
    if ((Test-PathIsSameOrDescendant -Path $resolvedOutputRoot -ParentPath $resolvedRepositoryRoot) -and
        -not (Test-PathIsSameOrDescendant -Path $resolvedOutputRoot -ParentPath $repositoryArtifactsRoot)) {
        throw "저장소 내부의 게시 출력 위치는 artifacts 아래에 있어야 합니다: $resolvedOutputRoot"
    }

    Assert-PathHasNoReparsePoint -Path $resolvedOutputRoot

    return $resolvedOutputRoot
}

function Initialize-DistributionOutputRoot {
    param(
        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string[]] $ReplaceableEntryNames
    )

    Assert-PathHasNoReparsePoint -Path $OutputRoot
    $null = New-Item -ItemType Directory -Path $OutputRoot -Force
    Assert-PathHasNoReparsePoint -Path $OutputRoot

    $comparison = Get-PathComparison
    foreach ($entry in @(Get-ChildItem -LiteralPath $OutputRoot -Force)) {
        $isReplaceable = $false
        foreach ($replaceableEntryName in $ReplaceableEntryNames) {
            if ($entry.Name.Equals($replaceableEntryName, $comparison)) {
                $isReplaceable = $true
                break
            }
        }

        if (-not $isReplaceable) {
            throw "게시 출력 위치에 이번 실행이 소유하지 않는 항목이 있습니다. 별도의 빈 출력 위치를 사용하거나 항목을 직접 확인하세요: $($entry.FullName)"
        }

        if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "게시 출력 항목에는 symbolic link 또는 reparse point를 사용할 수 없습니다: $($entry.FullName)"
        }
    }
}

function Assert-DirectChildPath {
    param(
        [Parameter(Mandatory)]
        [string] $ParentPath,

        [Parameter(Mandatory)]
        [string] $ChildPath,

        [Parameter(Mandatory)]
        [string] $ExpectedLeafName
    )

    $resolvedParent = Get-NormalizedFullPath -Path $ParentPath
    $resolvedChild = Get-NormalizedFullPath -Path $ChildPath
    $childParent = [System.IO.DirectoryInfo]::new($resolvedChild).Parent
    $comparison = Get-PathComparison

    if ($null -eq $childParent -or -not $childParent.FullName.Equals($resolvedParent, $comparison)) {
        throw "정리 대상은 게시 출력 위치의 직접 하위 항목이어야 합니다: $resolvedChild"
    }

    $actualLeafName = [System.IO.Path]::GetFileName($resolvedChild)
    if (-not $actualLeafName.Equals($ExpectedLeafName, $comparison)) {
        throw "정리 대상 이름이 예상과 일치하지 않습니다: $resolvedChild"
    }
}

function Reset-DistributionDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ExpectedLeafName
    )

    Assert-DirectChildPath `
        -ParentPath $OutputRoot `
        -ChildPath $Path `
        -ExpectedLeafName $ExpectedLeafName

    if (Test-Path -LiteralPath $Path) {
        $existingItem = Get-Item -LiteralPath $Path -Force
        if (-not $existingItem.PSIsContainer) {
            throw "게시 디렉터리 위치에 파일이 있습니다: $Path"
        }

        if (($existingItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "게시 디렉터리에는 symbolic link 또는 reparse point를 사용할 수 없습니다: $Path"
        }

        Assert-TreeHasNoReparsePoint -Path $Path
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    $null = New-Item -ItemType Directory -Path $Path -Force
}

function Remove-ExistingDistributionArchive {
    param(
        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string] $ArchivePath,

        [Parameter(Mandatory)]
        [string] $ExpectedFileName
    )

    Assert-DirectChildPath `
        -ParentPath $OutputRoot `
        -ChildPath $ArchivePath `
        -ExpectedLeafName $ExpectedFileName

    if (-not (Test-Path -LiteralPath $ArchivePath)) {
        return
    }

    $existingItem = Get-Item -LiteralPath $ArchivePath -Force
    if ($existingItem.PSIsContainer) {
        throw "게시 archive 위치에 디렉터리가 있습니다: $ArchivePath"
    }

    if (($existingItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "게시 archive에는 symbolic link 또는 reparse point를 사용할 수 없습니다: $ArchivePath"
    }

    Remove-Item -LiteralPath $ArchivePath -Force
}

function Assert-NonEmptyFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $file = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $file -or $file.PSIsContainer -or $file.Length -eq 0) {
        throw "필수 게시 파일이 없거나 비어 있습니다: $Path"
    }
}

function Get-RequiredThirdPartyNoticeFileNames {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath
    )

    $projectDirectory = [System.IO.Path]::GetDirectoryName(
        [System.IO.Path]::GetFullPath($ProjectPath))
    $noticeSourcePath = Join-Path $projectDirectory "ThirdPartyNotices"
    if (-not (Test-Path -LiteralPath $noticeSourcePath -PathType Container)) {
        throw "third-party notice 원본 디렉토리를 찾을 수 없습니다: $noticeSourcePath"
    }

    $fileNames = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($fileName in @(
        "FluentUiSystemIcons-LICENSE.txt",
        "Pretendard-LICENSE.txt")) {
        $null = $fileNames.Add($fileName)
    }

    foreach ($noticeFile in @(Get-ChildItem -LiteralPath $noticeSourcePath -Filter "*.txt" -File)) {
        if (-not $fileNames.Add($noticeFile.Name)) {
            throw "third-party notice 파일 이름이 중복됩니다: $($noticeFile.Name)"
        }
    }

    if (-not $fileNames.Contains("THIRD-PARTY-NOTICES.txt")) {
        throw "third-party notice 인덱스를 찾을 수 없습니다: $noticeSourcePath"
    }

    [string[]] $result = @($fileNames)
    [System.Array]::Sort($result, [System.StringComparer]::Ordinal)
    return $result
}

function Assert-RequiredThirdPartyNoticeFiles {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $PublishedNoticePath
    )

    foreach ($fileName in @(Get-RequiredThirdPartyNoticeFileNames -ProjectPath $ProjectPath)) {
        Assert-NonEmptyFile -Path (Join-Path $PublishedNoticePath $fileName)
    }
}

function Remove-DebugSymbols {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $debugSymbols = @(Get-ChildItem -LiteralPath $Path -Filter "*.pdb" -File -Recurse)
    foreach ($debugSymbol in $debugSymbols) {
        Remove-Item -LiteralPath $debugSymbol.FullName -Force
    }
}

function Test-IsXmlDocumentationFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $directoryPath = [System.IO.Path]::GetDirectoryName($Path)
    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $assemblyPath = Join-Path $directoryPath "$assemblyName.dll"
    $executablePath = Join-Path $directoryPath "$assemblyName.exe"
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf) -and
        -not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        return $false
    }

    $readerSettings = [System.Xml.XmlReaderSettings]::new()
    $readerSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $readerSettings.XmlResolver = $null

    $stream = $null
    $reader = $null
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        $reader = [System.Xml.XmlReader]::Create($stream, $readerSettings)
        $nodeType = $reader.MoveToContent()
        return $nodeType -eq [System.Xml.XmlNodeType]::Element -and
            $reader.LocalName.Equals("doc", [System.StringComparison]::Ordinal)
    }
    catch [System.Xml.XmlException] {
        return $false
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }

        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Remove-PublishedXmlDocumentationFiles {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $xmlFiles = @(Get-ChildItem -LiteralPath $Path -Filter "*.xml" -File -Recurse)
    foreach ($xmlFile in $xmlFiles) {
        if (Test-IsXmlDocumentationFile -Path $xmlFile.FullName) {
            Remove-Item -LiteralPath $xmlFile.FullName -Force
        }
    }
}

function Set-UnixExecutableFileMode {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $currentMode = [System.IO.File]::GetUnixFileMode($Path)
    $executeMode = [System.IO.UnixFileMode]::UserExecute `
        -bor [System.IO.UnixFileMode]::GroupExecute `
        -bor [System.IO.UnixFileMode]::OtherExecute
    [System.IO.File]::SetUnixFileMode($Path, $currentMode -bor $executeMode)
}

function Invoke-SelfContainedPublish {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $RuntimeIdentifier,

        [Parameter(Mandatory)]
        [string] $DestinationPath,

        [Parameter(Mandatory)]
        [string] $ProductVersion,

        [switch] $NoRestore
    )

    if (-not $NoRestore) {
        Invoke-DotNetCommand -Arguments @(
            "restore",
            $ProjectPath,
            "--locked-mode",
            "--nologo"
        )
    }

    $arguments = @(
        "publish",
        $ProjectPath,
        "--configuration", "Release",
        "--runtime", $RuntimeIdentifier,
        "--self-contained", "true",
        "--output", $DestinationPath,
        "--no-restore",
        "--nologo",
        "/m:1",
        "/nodeReuse:false",
        "-p:Version=$ProductVersion",
        "-p:ContinuousIntegrationBuild=true",
        "-p:DebugSymbols=false",
        "-p:DebugType=None",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false"
    )

    Invoke-DotNetCommand -Arguments $arguments
    Remove-PublishedXmlDocumentationFiles -Path $DestinationPath
}
