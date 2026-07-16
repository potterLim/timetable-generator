function Get-ArchiveEntryExternalAttributes {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("Regular", "Executable")]
        [string] $EntryMode
    )

    $unixMode = switch ($EntryMode) {
        "Regular" { [System.Convert]::ToUInt32("81A40000", 16) }
        "Executable" { [System.Convert]::ToUInt32("81ED0000", 16) }
        default { throw "지원하지 않는 archive entry mode입니다: $EntryMode" }
    }

    return [System.BitConverter]::ToInt32([System.BitConverter]::GetBytes($unixMode), 0)
}

function Get-ZipCentralDirectoryEntries {
    param(
        [Parameter(Mandatory)]
        [byte[]] $Bytes,

        [Parameter(Mandatory)]
        [string] $ArchivePath
    )

    $minimumEndRecordLength = 22
    $maximumCommentLength = 65535
    if ($Bytes.Length -lt $minimumEndRecordLength) {
        throw "ZIP 파일이 유효한 end-of-central-directory record보다 짧습니다: $ArchivePath"
    }

    $searchStart = [System.Math]::Max(
        0,
        $Bytes.Length - $minimumEndRecordLength - $maximumCommentLength)
    $endRecordOffset = -1
    for ($index = $Bytes.Length - $minimumEndRecordLength; $index -ge $searchStart; $index--) {
        if ($Bytes[$index] -eq 0x50 -and
            $Bytes[$index + 1] -eq 0x4B -and
            $Bytes[$index + 2] -eq 0x05 -and
            $Bytes[$index + 3] -eq 0x06) {
            $endRecordOffset = $index
            break
        }
    }

    if ($endRecordOffset -lt 0) {
        throw "ZIP end-of-central-directory record를 찾을 수 없습니다: $ArchivePath"
    }

    $entryCount = [System.BitConverter]::ToUInt16($Bytes, $endRecordOffset + 10)
    $centralDirectoryOffset = [System.BitConverter]::ToUInt32($Bytes, $endRecordOffset + 16)
    if ($entryCount -eq [uint16]::MaxValue -or $centralDirectoryOffset -eq [uint32]::MaxValue) {
        throw "Zip64 archive는 현재 배포 패키지에서 지원하지 않습니다: $ArchivePath"
    }

    $entries = [System.Collections.Generic.List[object]]::new()
    $entryOffset = [int64] $centralDirectoryOffset
    for ($entryIndex = 0; $entryIndex -lt $entryCount; $entryIndex++) {
        if ($entryOffset + 46 -gt $Bytes.Length -or
            $Bytes[$entryOffset] -ne 0x50 -or
            $Bytes[$entryOffset + 1] -ne 0x4B -or
            $Bytes[$entryOffset + 2] -ne 0x01 -or
            $Bytes[$entryOffset + 3] -ne 0x02) {
            throw "ZIP central-directory entry가 유효하지 않습니다: $ArchivePath"
        }

        $fileNameLength = [System.BitConverter]::ToUInt16($Bytes, $entryOffset + 28)
        $extraFieldLength = [System.BitConverter]::ToUInt16($Bytes, $entryOffset + 30)
        $commentLength = [System.BitConverter]::ToUInt16($Bytes, $entryOffset + 32)
        $entries.Add([pscustomobject]@{
            Offset = [int64] $entryOffset
            HostSystem = [byte] $Bytes[$entryOffset + 5]
        })
        $entryOffset += 46 + $fileNameLength + $extraFieldLength + $commentLength
    }

    if ($entryOffset -ne $endRecordOffset) {
        throw "ZIP central directory 길이가 유효하지 않습니다: $ArchivePath"
    }

    return $entries.ToArray()
}

function Set-ZipHostSystemToUnix {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $entries = @(Get-ZipCentralDirectoryEntries -Bytes $bytes -ArchivePath $Path)
    foreach ($entry in $entries) {
        $bytes[$entry.Offset + 5] = 3
    }

    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

function Test-IsMachOArchiveEntry {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchiveEntry] $Entry
    )

    if ($Entry.Length -lt 4) {
        return $false
    }

    $stream = $Entry.Open()
    try {
        $header = [byte[]]::new(4)
        if ($stream.Read($header, 0, $header.Length) -ne $header.Length) {
            return $false
        }

        $signature = [System.Convert]::ToHexString($header)
        return $signature -in @(
            "CFFAEDFE",
            "CEFAEDFE",
            "FEEDFACF",
            "FEEDFACE",
            "CAFEBABE",
            "BEBAFECA",
            "CAFEBABF",
            "BFBAFECA"
        )
    }
    finally {
        $stream.Dispose()
    }
}

function Assert-DistributionArchive {
    param(
        [Parameter(Mandatory)]
        [string] $ArchivePath,

        [Parameter(Mandatory)]
        [string] $ArchiveRootName,

        [Parameter(Mandatory)]
        [ValidateSet("Windows", "MacOS")]
        [string] $ArchivePlatform
    )

    $bytes = [System.IO.File]::ReadAllBytes($ArchivePath)
    $centralEntries = @(Get-ZipCentralDirectoryEntries -Bytes $bytes -ArchivePath $ArchivePath)
    foreach ($centralEntry in $centralEntries) {
        if ($centralEntry.HostSystem -ne 3) {
            throw "ZIP entry의 host system이 Unix로 기록되지 않았습니다: $ArchivePath"
        }
    }

    $stream = [System.IO.File]::OpenRead($ArchivePath)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
        try {
            if ($archive.Entries.Count -eq 0) {
                throw "게시 archive가 비어 있습니다: $ArchivePath"
            }

            $requiredPrefix = $ArchiveRootName.TrimEnd("/") + "/"
            $previousEntryName = [string]::Empty
            foreach ($entry in $archive.Entries) {
                if (-not $entry.FullName.StartsWith($requiredPrefix, [System.StringComparison]::Ordinal) -or
                    $entry.FullName.Contains("../", [System.StringComparison]::Ordinal)) {
                    throw "게시 archive entry 경로가 유효하지 않습니다: $($entry.FullName)"
                }

                if (-not [string]::IsNullOrEmpty($previousEntryName) -and
                    [System.StringComparer]::Ordinal.Compare($previousEntryName, $entry.FullName) -ge 0) {
                    throw "게시 archive entry 정렬이 결정적이지 않습니다: $ArchivePath"
                }

                $previousEntryName = $entry.FullName
                if ($entry.LastWriteTime.Year -ne 2000 -or
                    $entry.LastWriteTime.Month -ne 1 -or
                    $entry.LastWriteTime.Day -ne 1 -or
                    $entry.LastWriteTime.Hour -ne 0 -or
                    $entry.LastWriteTime.Minute -ne 0 -or
                    $entry.LastWriteTime.Second -ne 0) {
                    throw "게시 archive entry timestamp가 결정적이지 않습니다: $($entry.FullName)"
                }

                $attributes = [System.BitConverter]::ToUInt32(
                    [System.BitConverter]::GetBytes($entry.ExternalAttributes),
                    0)
                $actualMode = ($attributes -shr 16) -band 0xFFFF
                $entryMode = if ($ArchivePlatform -eq "MacOS" -and (Test-IsMachOArchiveEntry -Entry $entry)) {
                    "Executable"
                }
                else {
                    "Regular"
                }
                $expectedAttributes = Get-ArchiveEntryExternalAttributes -EntryMode $entryMode
                $expectedAttributesAsUInt = [System.BitConverter]::ToUInt32(
                    [System.BitConverter]::GetBytes($expectedAttributes),
                    0)
                $expectedMode = ($expectedAttributesAsUInt -shr 16) -band 0xFFFF
                if ($actualMode -ne $expectedMode) {
                    throw "게시 archive entry 권한이 올바르지 않습니다: $($entry.FullName)"
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function New-DistributionArchive {
    param(
        [Parameter(Mandatory)]
        [string] $SourcePath,

        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string] $ArchiveFileName,

        [Parameter(Mandatory)]
        [string] $ArchiveRootName,

        [Parameter(Mandatory)]
        [ValidateSet("Windows", "MacOS")]
        [string] $ArchivePlatform
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        throw "게시 archive 원본 디렉터리를 찾을 수 없습니다: $SourcePath"
    }

    if ([System.IO.Path]::GetFileName($ArchiveFileName) -ne $ArchiveFileName) {
        throw "게시 archive 이름에는 디렉터리 구분자를 사용할 수 없습니다: $ArchiveFileName"
    }

    $archivePath = Join-Path $OutputRoot $ArchiveFileName
    Remove-ExistingDistributionArchive `
        -OutputRoot $OutputRoot `
        -ArchivePath $archivePath `
        -ExpectedFileName $ArchiveFileName

    $sourceRoot = [System.IO.Path]::GetFullPath($SourcePath).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $entrySourcePaths = [System.Collections.Generic.Dictionary[string, string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($file in @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse)) {
        $relativePath = [System.IO.Path]::GetRelativePath($sourceRoot, $file.FullName).Replace("\", "/")
        $entryName = $ArchiveRootName.TrimEnd("/") + "/" + $relativePath
        if ($entrySourcePaths.ContainsKey($entryName)) {
            throw "게시 archive에 중복 entry 이름이 있습니다: $entryName"
        }

        $entrySourcePaths.Add($entryName, $file.FullName)
    }

    [string[]] $entryNames = @($entrySourcePaths.Keys)
    [System.Array]::Sort($entryNames, [System.StringComparer]::Ordinal)

    $archiveStream = [System.IO.File]::Open($archivePath, [System.IO.FileMode]::CreateNew)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $archiveStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            foreach ($entryName in $entryNames) {
                $filePath = $entrySourcePaths[$entryName]
                $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [System.DateTimeOffset]::new(
                    2000,
                    1,
                    1,
                    0,
                    0,
                    0,
                    [System.TimeSpan]::Zero)
                $entryMode = if ($ArchivePlatform -eq "MacOS" -and (Test-IsMachOFile -Path $filePath)) {
                    "Executable"
                }
                else {
                    "Regular"
                }
                $entry.ExternalAttributes = Get-ArchiveEntryExternalAttributes -EntryMode $entryMode

                $sourceStream = [System.IO.File]::OpenRead($filePath)
                $entryStream = $entry.Open()
                try {
                    $sourceStream.CopyTo($entryStream)
                }
                finally {
                    $entryStream.Dispose()
                    $sourceStream.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $archiveStream.Dispose()
    }

    Set-ZipHostSystemToUnix -Path $archivePath
    Assert-DistributionArchive `
        -ArchivePath $archivePath `
        -ArchiveRootName $ArchiveRootName `
        -ArchivePlatform $ArchivePlatform
}

function Write-DistributionChecksums {
    param(
        [Parameter(Mandatory)]
        [string] $OutputRoot,

        [Parameter(Mandatory)]
        [string[]] $ArchivePaths
    )

    [System.Array]::Sort($ArchivePaths, [System.StringComparer]::Ordinal)
    $checksumLines = foreach ($archivePath in $ArchivePaths) {
        Assert-NonEmptyFile -Path $archivePath
        $archiveName = [System.IO.Path]::GetFileName($archivePath)
        $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $archiveName"
    }

    $checksumPath = Join-Path $OutputRoot "checksums.sha256"
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllLines($checksumPath, $checksumLines, $utf8WithoutBom)
}
