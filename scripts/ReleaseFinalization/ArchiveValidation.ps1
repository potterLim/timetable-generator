function Get-ArchiveExternalAttributes {
    $unixMode = [System.Convert]::ToUInt32("81A40000", 16)
    return [System.BitConverter]::ToInt32([System.BitConverter]::GetBytes($unixMode), 0)
}

function New-DeterministicWindowsArchive {
    param(
        [Parameter(Mandatory)]
        [string] $SourcePath,

        [Parameter(Mandatory)]
        [string] $DestinationPath,

        [Parameter(Mandatory)]
        [string] $ArchiveRootName
    )

    Add-Type -AssemblyName System.IO.Compression
    $sourceRoot = Get-NormalizedFullPath -Path $SourcePath
    $entrySourcePaths = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    foreach ($file in @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse)) {
        $relativePath = [System.IO.Path]::GetRelativePath($sourceRoot, $file.FullName).Replace("\", "/")
        $entryName = $ArchiveRootName.TrimEnd("/") + "/" + $relativePath
        if ($entryName.Contains("../", [System.StringComparison]::Ordinal) -or
            $entrySourcePaths.TryAdd($entryName, $file.FullName) -eq $false) {
            throw "Windows ZIP entry 경로가 안전하지 않거나 중복되었습니다: $entryName"
        }
    }

    if ($entrySourcePaths.Count -eq 0) {
        throw "Windows ZIP 원본 디렉터리가 비어 있습니다: $SourcePath"
    }

    [string[]] $entryNames = @($entrySourcePaths.Keys)
    [System.Array]::Sort($entryNames, [System.StringComparer]::Ordinal)
    $stream = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::CreateNew)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            foreach ($entryName in $entryNames) {
                $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [System.DateTimeOffset]::new(
                    2000,
                    1,
                    1,
                    0,
                    0,
                    0,
                    [System.TimeSpan]::Zero)
                $entry.ExternalAttributes = Get-ArchiveExternalAttributes

                $inputStream = [System.IO.File]::OpenRead($entrySourcePaths[$entryName])
                $outputStream = $entry.Open()
                try {
                    $inputStream.CopyTo($outputStream)
                }
                finally {
                    $outputStream.Dispose()
                    $inputStream.Dispose()
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

function Assert-ArchiveEntries {
    param(
        [Parameter(Mandatory)]
        [string] $ArchivePath,

        [Parameter(Mandatory)]
        [string[]] $RequiredEntryNames,

        [Parameter(Mandatory)]
        [string] $RequiredPrefix,

        [switch] $AllowMacOSMetadataEntries
    )

    Add-Type -AssemblyName System.IO.Compression
    $stream = [System.IO.File]::OpenRead($ArchivePath)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Read, $false)
        try {
            if ($archive.Entries.Count -eq 0) {
                throw "Release ZIP이 비어 있습니다: $ArchivePath"
            }

            $entryNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            foreach ($entry in $archive.Entries) {
                $entryName = $entry.FullName
                $hasExpectedPrefix = $entryName.StartsWith($RequiredPrefix, [System.StringComparison]::Ordinal)
                $isAllowedMacOSMetadataEntry = $false
                if ($AllowMacOSMetadataEntries) {
                    $rootName = $RequiredPrefix.TrimEnd("/")
                    $isAllowedMacOSMetadataEntry = $entryName.Equals("__MACOSX/", [System.StringComparison]::Ordinal) -or
                        $entryName.Equals("__MACOSX/._$rootName", [System.StringComparison]::Ordinal) -or
                        $entryName.StartsWith("__MACOSX/$RequiredPrefix", [System.StringComparison]::Ordinal)
                }

                if (($hasExpectedPrefix -eq $false -and $isAllowedMacOSMetadataEntry -eq $false) -or
                    $entryName.Contains("../", [System.StringComparison]::Ordinal) -or
                    $entryName.Contains("\", [System.StringComparison]::Ordinal) -or
                    $entryName.StartsWith("/", [System.StringComparison]::Ordinal)) {
                    throw "Release ZIP entry 경로가 예상한 루트 안에 있지 않습니다: $($entry.FullName)"
                }

                if ($entry.FullName.EndsWith(".pdb", [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Release ZIP에 PDB가 포함되어 있습니다: $($entry.FullName)"
                }

                if ($entryNames.Add($entry.FullName) -eq $false) {
                    throw "Release ZIP에 중복 entry가 있습니다: $($entry.FullName)"
                }
            }

            foreach ($requiredEntryName in $RequiredEntryNames) {
                if ($entryNames.Contains($requiredEntryName) -eq $false) {
                    throw "Release ZIP에 필수 entry가 없습니다: $requiredEntryName"
                }

                $requiredEntry = $archive.GetEntry($requiredEntryName)
                if ($null -eq $requiredEntry -or $requiredEntry.Length -eq 0) {
                    throw "Release ZIP의 필수 entry가 비어 있습니다: $requiredEntryName"
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
