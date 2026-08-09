function Get-PathComparison {
    if ($IsWindows) {
        return [System.StringComparison]::OrdinalIgnoreCase
    }

    return [System.StringComparison]::Ordinal
}

function Get-NormalizedZipEntryTimestamp {
    param(
        [Parameter(Mandatory)]
        [System.DateTimeOffset] $Timestamp
    )

    $utcTimestamp = $Timestamp.ToUniversalTime()
    $minimumTimestamp = [System.DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [System.TimeSpan]::Zero)
    $maximumTimestamp = [System.DateTimeOffset]::new(2107, 12, 31, 23, 59, 58, [System.TimeSpan]::Zero)
    if ($utcTimestamp -lt $minimumTimestamp -or $utcTimestamp -gt $maximumTimestamp) {
        throw "ZIP entry timestamp는 1980-01-01부터 2107-12-31까지여야 합니다: $utcTimestamp"
    }

    $normalizedSecond = $utcTimestamp.Second - ($utcTimestamp.Second % 2)
    return [System.DateTimeOffset]::new(
        $utcTimestamp.Year,
        $utcTimestamp.Month,
        $utcTimestamp.Day,
        $utcTimestamp.Hour,
        $utcTimestamp.Minute,
        $normalizedSecond,
        [System.TimeSpan]::Zero)
}

function Get-RepositoryCommitArchiveTimestamp {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot
    )

    $resolvedRepositoryRoot = Get-NormalizedFullPath -Path $RepositoryRoot
    $timestampOutput = @(& git -C $resolvedRepositoryRoot show -s --format=%ct HEAD 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "릴리스 커밋 시각을 읽을 수 없습니다: $($timestampOutput -join "`n")"
    }

    $timestampText = ($timestampOutput -join "`n").Trim()
    [long] $unixTimeSeconds = 0
    if (-not [long]::TryParse(
        $timestampText,
        [System.Globalization.NumberStyles]::None,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref] $unixTimeSeconds)) {
        throw "릴리스 커밋 시각이 유효한 Unix timestamp가 아닙니다: $timestampText"
    }

    $commitTimestamp = [System.DateTimeOffset]::FromUnixTimeSeconds($unixTimeSeconds)
    return Get-NormalizedZipEntryTimestamp -Timestamp $commitTimestamp
}

function Test-ZipEntryTimestampEquals {
    param(
        [Parameter(Mandatory)]
        [System.DateTimeOffset] $Actual,

        [Parameter(Mandatory)]
        [System.DateTimeOffset] $Expected
    )

    $normalizedExpected = Get-NormalizedZipEntryTimestamp -Timestamp $Expected
    return $Actual.Year -eq $normalizedExpected.Year -and
        $Actual.Month -eq $normalizedExpected.Month -and
        $Actual.Day -eq $normalizedExpected.Day -and
        $Actual.Hour -eq $normalizedExpected.Hour -and
        $Actual.Minute -eq $normalizedExpected.Minute -and
        $Actual.Second -eq $normalizedExpected.Second
}

function Assert-Sha256ChecksumFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string[]] $FilePaths
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "SHA-256 checksum 파일을 찾을 수 없습니다: $Path"
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0) {
        throw "SHA-256 checksum 파일이 비어 있습니다: $Path"
    }

    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw "SHA-256 checksum 파일에는 UTF-8 BOM을 사용할 수 없습니다: $Path"
    }

    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        $contents = $strictUtf8.GetString($bytes)
    }
    catch [System.Text.DecoderFallbackException] {
        throw "SHA-256 checksum 파일이 유효한 UTF-8이 아닙니다: $Path"
    }

    if ($contents.Contains("`r", [System.StringComparison]::Ordinal)) {
        throw "SHA-256 checksum 파일은 LF 줄바꿈만 사용해야 합니다: $Path"
    }

    if (-not $contents.EndsWith("`n", [System.StringComparison]::Ordinal)) {
        throw "SHA-256 checksum 파일은 LF로 끝나야 합니다: $Path"
    }

    $body = $contents.Substring(0, $contents.Length - 1)
    if ([string]::IsNullOrEmpty($body) -or $body.EndsWith("`n", [System.StringComparison]::Ordinal)) {
        throw "SHA-256 checksum 파일은 마지막에 정확히 하나의 LF만 사용해야 합니다: $Path"
    }

    $expectedFilesByName = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    foreach ($filePath in $FilePaths) {
        Assert-NonEmptyFile -Path $filePath
        $fileName = [System.IO.Path]::GetFileName($filePath)
        if ([System.IO.Path]::GetFileName($fileName) -cne $fileName -or $expectedFilesByName.TryAdd($fileName, $filePath) -eq $false) {
            throw "SHA-256 checksum 대상 파일 이름이 안전하지 않거나 중복되었습니다: $fileName"
        }
    }

    [string[]] $expectedFileNames = @($expectedFilesByName.Keys)
    [System.Array]::Sort($expectedFileNames, [System.StringComparer]::Ordinal)
    $lines = @($body.Split("`n"))
    if ($lines.Count -ne $expectedFileNames.Count) {
        throw "SHA-256 checksum 항목 수가 예상과 일치하지 않습니다: $Path"
    }

    for ($index = 0; $index -lt $expectedFileNames.Count; $index++) {
        $match = [System.Text.RegularExpressions.Regex]::Match($lines[$index], "^(?<hash>[0-9a-f]{64})  (?<fileName>[^/\\]+)$")
        if (-not $match.Success) {
            throw "SHA-256 checksum 항목 형식이 유효하지 않습니다: $($lines[$index])"
        }

        $expectedFileName = $expectedFileNames[$index]
        $actualFileName = $match.Groups["fileName"].Value
        if ($actualFileName -cne $expectedFileName) {
            throw "SHA-256 checksum 파일 이름 또는 정렬이 예상과 일치하지 않습니다: $actualFileName"
        }

        $actualHash = $match.Groups["hash"].Value
        $expectedHash = (Get-FileHash -LiteralPath $expectedFilesByName[$expectedFileName] -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -cne $expectedHash) {
            throw "SHA-256 checksum 값이 실제 파일과 일치하지 않습니다: $actualFileName"
        }
    }
}

function Write-Sha256ChecksumFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string[]] $FilePaths
    )

    $filesByName = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    foreach ($filePath in $FilePaths) {
        Assert-NonEmptyFile -Path $filePath
        $fileName = [System.IO.Path]::GetFileName($filePath)
        if ([System.IO.Path]::GetFileName($fileName) -cne $fileName -or $filesByName.TryAdd($fileName, $filePath) -eq $false) {
            throw "SHA-256 checksum 대상 파일 이름이 안전하지 않거나 중복되었습니다: $fileName"
        }
    }

    [string[]] $fileNames = @($filesByName.Keys)
    [System.Array]::Sort($fileNames, [System.StringComparer]::Ordinal)
    $lines = foreach ($fileName in $fileNames) {
        $hash = (Get-FileHash -LiteralPath $filesByName[$fileName] -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $fileName"
    }

    $contents = ($lines -join "`n") + "`n"
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $contents, $utf8WithoutBom)
    Assert-Sha256ChecksumFile -Path $Path -FilePaths $FilePaths
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

    return $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
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
                throw "산출물 경로에는 symbolic link 또는 reparse point를 사용할 수 없습니다: $currentPath"
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
            throw "산출물 원본에는 symbolic link 또는 reparse point를 포함할 수 없습니다: $($item.FullName)"
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

    $relativePath = [System.IO.Path]::GetRelativePath((Get-NormalizedFullPath -Path $ParentPath), (Get-NormalizedFullPath -Path $Path))
    if ([System.IO.Path]::IsPathRooted($relativePath)) {
        return $false
    }

    return $relativePath -ne ".." -and -not $relativePath.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)", (Get-PathComparison))
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
        throw "필수 산출물 파일이 없거나 비어 있습니다: $Path"
    }
}

function Get-RequiredThirdPartyNoticeFileNames {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath
    )

    $projectDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($ProjectPath))
    $noticeSourcePath = Join-Path $projectDirectory "ThirdPartyNotices"
    if (-not (Test-Path -LiteralPath $noticeSourcePath -PathType Container)) {
        throw "third-party notice 원본 디렉토리를 찾을 수 없습니다: $noticeSourcePath"
    }

    $fileNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
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
