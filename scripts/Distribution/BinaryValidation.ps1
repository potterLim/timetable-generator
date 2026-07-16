function Get-BinaryPrefix {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [int] $Length
    )

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $buffer = [byte[]]::new($Length)
        $bytesRead = $stream.Read($buffer, 0, $Length)
        if ($bytesRead -ne $Length) {
            throw "바이너리 헤더가 너무 짧습니다: $Path"
        }

        return $buffer
    }
    finally {
        $stream.Dispose()
    }
}

function Test-IsMachOFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $file = Get-Item -LiteralPath $Path
    if ($file.Length -lt 4) {
        return $false
    }

    $header = Get-BinaryPrefix -Path $Path -Length 4
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

function Assert-MacOSPublishedBinaryArchitectures {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [ValidateSet("osx-x64", "osx-arm64")]
        [string] $RuntimeIdentifier
    )

    $machOFiles = @(
        Get-ChildItem -LiteralPath $Path -File -Recurse |
            Where-Object { Test-IsMachOFile -Path $_.FullName }
    )
    if ($machOFiles.Count -eq 0) {
        throw "macOS bundle에서 Mach-O 바이너리를 찾을 수 없습니다: $Path"
    }

    foreach ($machOFile in $machOFiles) {
        Assert-MachOArchitecture `
            -Path $machOFile.FullName `
            -RuntimeIdentifier $RuntimeIdentifier
    }
}

function Assert-MachOArchitecture {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [ValidateSet("osx-x64", "osx-arm64")]
        [string] $RuntimeIdentifier
    )

    $expectedCpuType = if ($RuntimeIdentifier -eq "osx-x64") { 0x01000007 } else { 0x0100000C }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $signature = [System.Convert]::ToHexString($bytes, 0, 4)
    switch ($signature) {
        "CFFAEDFE" {
            $actualCpuType = Read-MachOUInt32 -Buffer $bytes -Offset 4 -ByteOrder "LittleEndian"
            if ($actualCpuType -ne $expectedCpuType) {
                throw "macOS 실행 파일 아키텍처가 $RuntimeIdentifier 와 일치하지 않습니다: $Path"
            }
        }
        "FEEDFACF" {
            $actualCpuType = Read-MachOUInt32 -Buffer $bytes -Offset 4 -ByteOrder "BigEndian"
            if ($actualCpuType -ne $expectedCpuType) {
                throw "macOS 실행 파일 아키텍처가 $RuntimeIdentifier 와 일치하지 않습니다: $Path"
            }
        }
        "CAFEBABE" {
            Assert-FatMachOContainsArchitecture `
                -Buffer $bytes `
                -ExpectedCpuType $expectedCpuType `
                -ByteOrder "BigEndian" `
                -FatHeaderKind "32Bit" `
                -Path $Path
        }
        "BEBAFECA" {
            Assert-FatMachOContainsArchitecture `
                -Buffer $bytes `
                -ExpectedCpuType $expectedCpuType `
                -ByteOrder "LittleEndian" `
                -FatHeaderKind "32Bit" `
                -Path $Path
        }
        "CAFEBABF" {
            Assert-FatMachOContainsArchitecture `
                -Buffer $bytes `
                -ExpectedCpuType $expectedCpuType `
                -ByteOrder "BigEndian" `
                -FatHeaderKind "64Bit" `
                -Path $Path
        }
        "BFBAFECA" {
            Assert-FatMachOContainsArchitecture `
                -Buffer $bytes `
                -ExpectedCpuType $expectedCpuType `
                -ByteOrder "LittleEndian" `
                -FatHeaderKind "64Bit" `
                -Path $Path
        }
        default {
            throw "macOS 실행 파일이 64비트 또는 universal Mach-O 형식이 아닙니다: $Path"
        }
    }
}

function Read-MachOUInt32 {
    param(
        [Parameter(Mandatory)]
        [byte[]] $Buffer,

        [Parameter(Mandatory)]
        [int] $Offset,

        [Parameter(Mandatory)]
        [ValidateSet("BigEndian", "LittleEndian")]
        [string] $ByteOrder
    )

    if ($Offset -lt 0 -or $Offset + 4 -gt $Buffer.Length) {
        throw "Mach-O uint32 범위가 파일을 벗어났습니다."
    }

    if ($ByteOrder -eq "LittleEndian") {
        return [System.BitConverter]::ToUInt32($Buffer, $Offset)
    }

    return ([uint32] $Buffer[$Offset] * 16777216) +
        ([uint32] $Buffer[$Offset + 1] * 65536) +
        ([uint32] $Buffer[$Offset + 2] * 256) +
        [uint32] $Buffer[$Offset + 3]
}

function Read-MachOUInt64 {
    param(
        [Parameter(Mandatory)]
        [byte[]] $Buffer,

        [Parameter(Mandatory)]
        [int] $Offset,

        [Parameter(Mandatory)]
        [ValidateSet("BigEndian", "LittleEndian")]
        [string] $ByteOrder
    )

    if ($Offset -lt 0 -or $Offset + 8 -gt $Buffer.Length) {
        throw "Mach-O uint64 범위가 파일을 벗어났습니다."
    }

    if ($ByteOrder -eq "LittleEndian") {
        return [System.BitConverter]::ToUInt64($Buffer, $Offset)
    }

    [uint64] $value = 0
    for ($index = 0; $index -lt 8; $index++) {
        $value = ($value -shl 8) + $Buffer[$Offset + $index]
    }

    return $value
}

function Assert-FatMachOContainsArchitecture {
    param(
        [Parameter(Mandatory)]
        [byte[]] $Buffer,

        [Parameter(Mandatory)]
        [uint32] $ExpectedCpuType,

        [Parameter(Mandatory)]
        [ValidateSet("BigEndian", "LittleEndian")]
        [string] $ByteOrder,

        [Parameter(Mandatory)]
        [ValidateSet("32Bit", "64Bit")]
        [string] $FatHeaderKind,

        [Parameter(Mandatory)]
        [string] $Path
    )

    $architectureCount = Read-MachOUInt32 -Buffer $Buffer -Offset 4 -ByteOrder $ByteOrder
    if ($architectureCount -eq 0 -or $architectureCount -gt 32) {
        throw "universal Mach-O architecture 수가 유효하지 않습니다: $Path"
    }

    $architectureEntrySize = if ($FatHeaderKind -eq "32Bit") { 20 } else { 32 }
    $requiredHeaderLength = 8 + ([uint64] $architectureCount * $architectureEntrySize)
    if ($requiredHeaderLength -gt $Buffer.Length) {
        throw "universal Mach-O header가 잘렸습니다: $Path"
    }

    $foundExpectedArchitecture = $false
    $entryOffset = 8
    for ($index = 0; $index -lt $architectureCount; $index++) {
        $cpuType = Read-MachOUInt32 -Buffer $Buffer -Offset $entryOffset -ByteOrder $ByteOrder
        if ($FatHeaderKind -eq "32Bit") {
            [uint64] $sliceOffset = Read-MachOUInt32 `
                -Buffer $Buffer `
                -Offset ($entryOffset + 8) `
                -ByteOrder $ByteOrder
            [uint64] $sliceSize = Read-MachOUInt32 `
                -Buffer $Buffer `
                -Offset ($entryOffset + 12) `
                -ByteOrder $ByteOrder
        }
        else {
            [uint64] $sliceOffset = Read-MachOUInt64 `
                -Buffer $Buffer `
                -Offset ($entryOffset + 8) `
                -ByteOrder $ByteOrder
            [uint64] $sliceSize = Read-MachOUInt64 `
                -Buffer $Buffer `
                -Offset ($entryOffset + 16) `
                -ByteOrder $ByteOrder
        }

        if ($sliceSize -eq 0 -or
            $sliceSize -gt [uint64] $Buffer.Length -or
            $sliceOffset -gt ([uint64] $Buffer.Length - $sliceSize)) {
            throw "universal Mach-O slice 범위가 유효하지 않습니다: $Path"
        }

        if ($cpuType -eq $ExpectedCpuType) {
            $foundExpectedArchitecture = $true
        }

        $entryOffset += $architectureEntrySize
    }

    if (-not $foundExpectedArchitecture) {
        throw "universal Mach-O에 요청한 architecture가 없습니다: $Path"
    }
}

function Assert-WindowsX64PeBinary {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        try {
            if ($reader.ReadUInt16() -ne 0x5A4D) {
                throw "Windows 실행 파일에 DOS 헤더가 없습니다: $Path"
            }

            $stream.Position = 0x3C
            $peHeaderOffset = $reader.ReadUInt32()
            if ($peHeaderOffset -gt $stream.Length - 6) {
                throw "Windows 실행 파일의 PE header offset이 유효하지 않습니다: $Path"
            }

            $stream.Position = $peHeaderOffset
            if ($reader.ReadUInt32() -ne 0x00004550) {
                throw "Windows 실행 파일에 PE 헤더가 없습니다: $Path"
            }

            if ($reader.ReadUInt16() -ne 0x8664) {
                throw "Windows 실행 파일이 x64 아키텍처가 아닙니다: $Path"
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}
