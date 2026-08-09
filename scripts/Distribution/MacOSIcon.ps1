function Write-BigEndianUInt32 {
    param(
        [Parameter(Mandatory)]
        [System.IO.BinaryWriter] $Writer,

        [Parameter(Mandatory)]
        [uint32] $Value
    )

    $bytes = [System.BitConverter]::GetBytes($Value)
    if ([System.BitConverter]::IsLittleEndian) {
        [System.Array]::Reverse($bytes)
    }

    $Writer.Write($bytes)
}

function Read-BigEndianUInt32 {
    param(
        [Parameter(Mandatory)]
        [byte[]] $Buffer,

        [Parameter(Mandatory)]
        [int] $Offset
    )

    return ([uint32] $Buffer[$Offset] * 16777216) +
        ([uint32] $Buffer[$Offset + 1] * 65536) +
        ([uint32] $Buffer[$Offset + 2] * 256) +
        [uint32] $Buffer[$Offset + 3]
}

function Get-IcnsPngSpecifications {
    return @(
        [pscustomobject]@{ Type = "icp4"; Size = 16 },
        [pscustomobject]@{ Type = "icp5"; Size = 32 },
        [pscustomobject]@{ Type = "icp6"; Size = 64 },
        [pscustomobject]@{ Type = "ic07"; Size = 128 },
        [pscustomobject]@{ Type = "ic08"; Size = 256 },
        [pscustomobject]@{ Type = "ic09"; Size = 512 },
        [pscustomobject]@{ Type = "ic10"; Size = 1024 }
    )
}

function Write-IcnsFile {
    param(
        [Parameter(Mandatory)]
        [string] $DestinationPath,

        [Parameter(Mandatory)]
        [object[]] $Chunks
    )

    [uint64] $totalLength = 8
    $chunkTypes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($chunk in $Chunks) {
        if ($chunk.Type -isnot [string] -or $chunk.Type.Length -ne 4) {
            throw "ICNS chunk type은 ASCII 문자 4개여야 합니다."
        }

        if (-not $chunkTypes.Add($chunk.Type)) {
            throw "ICNS chunk type이 중복되었습니다: $($chunk.Type)"
        }

        [byte[]] $data = $chunk.Data
        $totalLength += 8 + $data.Length
    }

    if ($totalLength -gt [uint32]::MaxValue) {
        throw "ICNS 파일 크기가 지원 범위를 초과합니다."
    }

    $stream = [System.IO.MemoryStream]::new()
    try {
        $writer = [System.IO.BinaryWriter]::new($stream, [System.Text.Encoding]::UTF8, $true)
        try {
            $writer.Write([System.Text.Encoding]::ASCII.GetBytes("icns"))
            Write-BigEndianUInt32 -Writer $writer -Value ([uint32] $totalLength)
            foreach ($chunk in $Chunks) {
                [byte[]] $data = $chunk.Data
                $writer.Write([System.Text.Encoding]::ASCII.GetBytes($chunk.Type))
                Write-BigEndianUInt32 -Writer $writer -Value ([uint32] (8 + $data.Length))
                $writer.Write($data)
            }

            $writer.Flush()
            $icnsBytes = $stream.ToArray()
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    [System.IO.File]::WriteAllBytes($DestinationPath, $icnsBytes)
}

function New-ResizedPngBytes {
    param(
        [Parameter(Mandatory)]
        $SourceImage,

        [Parameter(Mandatory)]
        [int] $Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

            $imageAttributes = [System.Drawing.Imaging.ImageAttributes]::new()
            try {
                $imageAttributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
                $destination = [System.Drawing.Rectangle]::new(0, 0, $Size, $Size)
                $graphics.DrawImage(
                    $SourceImage,
                    $destination,
                    0,
                    0,
                    $SourceImage.Width,
                    $SourceImage.Height,
                    [System.Drawing.GraphicsUnit]::Pixel,
                    $imageAttributes)
            }
            finally {
                $imageAttributes.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }

        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return $stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

function New-IcnsOnWindows {
    param(
        [Parameter(Mandatory)]
        [string] $SourcePath,

        [Parameter(Mandatory)]
        [string] $DestinationPath
    )

    Add-Type -AssemblyName System.Drawing.Common
    $sourceImage = [System.Drawing.Image]::FromFile($SourcePath)
    try {
        if ($sourceImage.Width -ne $sourceImage.Height -or $sourceImage.Width -lt 1024) {
            throw "macOS 원본 아이콘은 1024px 이상의 정사각형이어야 합니다: $SourcePath"
        }

        $chunks = foreach ($specification in Get-IcnsPngSpecifications) {
            [byte[]] $pngBytes = New-ResizedPngBytes -SourceImage $sourceImage -Size $specification.Size
            [pscustomobject]@{
                Type = $specification.Type
                Data = $pngBytes
            }
        }

        Write-IcnsFile -DestinationPath $DestinationPath -Chunks $chunks
    }
    finally {
        $sourceImage.Dispose()
    }
}

function New-IcnsOnMacOS {
    param(
        [Parameter(Mandatory)]
        [string] $SourcePath,

        [Parameter(Mandatory)]
        [string] $DestinationPath
    )

    if ($null -eq (Get-Command "sips" -CommandType Application -ErrorAction SilentlyContinue)) {
        throw "macOS ICNS 생성에 필요한 시스템 도구를 찾을 수 없습니다: sips"
    }

    $temporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) ("TimetableGenerator-" + [System.Guid]::NewGuid().ToString("N"))
    $null = New-Item -ItemType Directory -Path $temporaryPath
    try {
        $chunks = foreach ($specification in Get-IcnsPngSpecifications) {
            $pngPath = Join-Path $temporaryPath ("$($specification.Type).png")
            & sips -z $specification.Size $specification.Size $SourcePath --out $pngPath | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "sips가 $($specification.Size)px macOS 아이콘을 생성하지 못했습니다."
            }

            [pscustomobject]@{
                Type = $specification.Type
                Data = [System.IO.File]::ReadAllBytes($pngPath)
            }
        }

        Write-IcnsFile -DestinationPath $DestinationPath -Chunks $chunks
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Recurse -Force
        }
    }
}

function Assert-IcnsFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 8 -or
        [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne "icns" -or
        (Read-BigEndianUInt32 -Buffer $bytes -Offset 4) -ne $bytes.Length) {
        throw "macOS 아이콘이 올바른 ICNS 컨테이너가 아닙니다: $Path"
    }

    $knownChunkSizes = @{
        ic04 = 16
        ic05 = 32
        icp4 = 16
        icp5 = 32
        icp6 = 64
        ic07 = 128
        ic08 = 256
        ic09 = 512
        ic10 = 1024
        ic11 = 32
        ic12 = 64
        ic13 = 256
        ic14 = 512
    }
    $legacyArgbChunkTypes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $null = $legacyArgbChunkTypes.Add("ic04")
    $null = $legacyArgbChunkTypes.Add("ic05")
    $requiredSizes = [System.Collections.Generic.HashSet[uint32]]::new([uint32[]] @(16, 32, 64, 128, 256, 512, 1024))
    $foundSizes = [System.Collections.Generic.HashSet[uint32]]::new()
    $pngSignature = [byte[]] @(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
    $argbSignature = [System.Text.Encoding]::ASCII.GetBytes("ARGB")
    $offset = 8
    while ($offset -lt $bytes.Length) {
        if ($offset + 8 -gt $bytes.Length) {
            throw "ICNS chunk header가 잘렸습니다: $Path"
        }

        $type = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, 4)
        $chunkLength = Read-BigEndianUInt32 -Buffer $bytes -Offset ($offset + 4)
        if ($chunkLength -lt 8 -or $offset + $chunkLength -gt $bytes.Length) {
            throw "ICNS chunk 길이가 유효하지 않습니다: $type"
        }

        $pngOffset = $offset + 8
        $hasPngPayload = $chunkLength -ge 32
        if ($hasPngPayload) {
            for ($index = 0; $index -lt $pngSignature.Length; $index++) {
                if ($bytes[$pngOffset + $index] -ne $pngSignature[$index]) {
                    $hasPngPayload = $false
                    break
                }
            }
        }

        $hasLegacyArgbPayload = $legacyArgbChunkTypes.Contains($type) -and
            $chunkLength -gt (8 + $argbSignature.Length)
        if ($hasLegacyArgbPayload) {
            $argbOffset = $offset + 8
            for ($index = 0; $index -lt $argbSignature.Length; $index++) {
                if ($bytes[$argbOffset + $index] -ne $argbSignature[$index]) {
                    $hasLegacyArgbPayload = $false
                    break
                }
            }
        }

        if ($hasPngPayload) {
            $width = Read-BigEndianUInt32 -Buffer $bytes -Offset ($pngOffset + 16)
            $height = Read-BigEndianUInt32 -Buffer $bytes -Offset ($pngOffset + 20)
            if ($width -ne $height) {
                throw "ICNS chunk의 PNG가 정사각형이 아닙니다: $type"
            }

            if ($knownChunkSizes.ContainsKey($type) -and $width -ne $knownChunkSizes[$type]) {
                throw "ICNS chunk 해상도가 type과 일치하지 않습니다: $type"
            }

            if ($requiredSizes.Contains($width)) {
                $null = $foundSizes.Add($width)
            }
        }
        elseif ($hasLegacyArgbPayload) {
            $null = $foundSizes.Add([uint32] $knownChunkSizes[$type])
        }
        elseif ($legacyArgbChunkTypes.Contains($type)) {
            throw "legacy ICNS ARGB chunk payload가 유효하지 않습니다: $type"
        }
        elseif ($knownChunkSizes.ContainsKey($type)) {
            throw "필수 ICNS chunk가 PNG payload를 포함하지 않습니다: $type"
        }

        $offset += $chunkLength
    }

    if ($offset -ne $bytes.Length -or $foundSizes.Count -ne $requiredSizes.Count) {
        throw "ICNS 파일에 필요한 모든 해상도가 포함되지 않았습니다: $Path"
    }
}

function New-MacOSAppIcon {
    param(
        [Parameter(Mandatory)]
        [string] $SourcePath,

        [Parameter(Mandatory)]
        [string] $DestinationPath
    )

    if ($IsWindows) {
        New-IcnsOnWindows -SourcePath $SourcePath -DestinationPath $DestinationPath
    }
    elseif ($IsMacOS) {
        New-IcnsOnMacOS -SourcePath $SourcePath -DestinationPath $DestinationPath
    }
    else {
        throw "macOS ICNS 생성은 Windows 또는 macOS 게시 호스트를 지원합니다."
    }

    Assert-IcnsFile -Path $DestinationPath
}
