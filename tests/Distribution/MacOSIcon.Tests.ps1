#requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
. (Join-Path $repositoryRoot "scripts/Distribution/MacOSIcon.ps1")

function Get-Crc32 {
    param(
        [Parameter(Mandatory)]
        [byte[]] $Bytes
    )

    [uint64] $crc = [uint32]::MaxValue
    foreach ($value in $Bytes) {
        $crc = $crc -bxor [uint64] $value
        for ($bitIndex = 0; $bitIndex -lt 8; $bitIndex++) {
            if (($crc -band 1) -ne 0) {
                $crc = ($crc -shr 1) -bxor [uint64] 3988292384
            }
            else {
                $crc = $crc -shr 1
            }
        }
    }

    return [uint32] ($crc -bxor [uint64] [uint32]::MaxValue)
}

function Write-PngChunk {
    param(
        [Parameter(Mandatory)]
        [System.IO.BinaryWriter] $Writer,

        [Parameter(Mandatory)]
        [ValidateLength(4, 4)]
        [string] $Type,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [byte[]] $Data
    )

    $typeBytes = [System.Text.Encoding]::ASCII.GetBytes($Type)
    Write-BigEndianUInt32 -Writer $Writer -Value ([uint32] $Data.Length)
    $Writer.Write($typeBytes)
    $Writer.Write($Data)

    $crcInput = [byte[]]::new($typeBytes.Length + $Data.Length)
    [System.Array]::Copy($typeBytes, 0, $crcInput, 0, $typeBytes.Length)
    [System.Array]::Copy($Data, 0, $crcInput, $typeBytes.Length, $Data.Length)
    Write-BigEndianUInt32 -Writer $Writer -Value (Get-Crc32 -Bytes $crcInput)
}

function New-TransparentPngBytes {
    param(
        [Parameter(Mandatory)]
        [int] $Size
    )

    $headerStream = [System.IO.MemoryStream]::new()
    $headerWriter = [System.IO.BinaryWriter]::new(
        $headerStream,
        [System.Text.Encoding]::UTF8,
        $true)
    Write-BigEndianUInt32 -Writer $headerWriter -Value ([uint32] $Size)
    Write-BigEndianUInt32 -Writer $headerWriter -Value ([uint32] $Size)
    $headerWriter.Write([byte[]] @(8, 6, 0, 0, 0))
    $headerWriter.Flush()
    $headerBytes = $headerStream.ToArray()
    $headerWriter.Dispose()
    $headerStream.Dispose()

    $rawPixels = [byte[]]::new((($Size * 4) + 1) * $Size)
    $compressedStream = [System.IO.MemoryStream]::new()
    $zlibStream = [System.IO.Compression.ZLibStream]::new(
        $compressedStream,
        [System.IO.Compression.CompressionLevel]::SmallestSize,
        $true)
    $zlibStream.Write($rawPixels, 0, $rawPixels.Length)
    $zlibStream.Dispose()
    $compressedBytes = $compressedStream.ToArray()
    $compressedStream.Dispose()

    $pngStream = [System.IO.MemoryStream]::new()
    $pngWriter = [System.IO.BinaryWriter]::new(
        $pngStream,
        [System.Text.Encoding]::UTF8,
        $true)
    $pngWriter.Write([byte[]] @(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
    Write-PngChunk -Writer $pngWriter -Type "IHDR" -Data $headerBytes
    Write-PngChunk -Writer $pngWriter -Type "IDAT" -Data $compressedBytes
    Write-PngChunk -Writer $pngWriter -Type "IEND" -Data ([byte[]]::new(0))
    $pngWriter.Flush()
    $pngBytes = $pngStream.ToArray()
    $pngWriter.Dispose()
    $pngStream.Dispose()

    return $pngBytes
}

function New-TestIcnsChunk {
    param(
        [Parameter(Mandatory)]
        [ValidateLength(4, 4)]
        [string] $Type,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [byte[]] $Data
    )

    return [pscustomobject]@{
        Type = $Type
        Data = $Data
    }
}

function New-LegacyArgbPayload {
    $signature = [System.Text.Encoding]::ASCII.GetBytes("ARGB")
    $payload = [byte[]]::new($signature.Length + 1)
    [System.Array]::Copy($signature, $payload, $signature.Length)
    $payload[$payload.Length - 1] = 1
    return $payload
}

function New-ValidMixedIcnsChunks {
    return @(
        (New-TestIcnsChunk -Type "ic04" -Data (New-LegacyArgbPayload)),
        (New-TestIcnsChunk -Type "ic05" -Data (New-LegacyArgbPayload)),
        (New-TestIcnsChunk -Type "icp6" -Data (New-TransparentPngBytes -Size 64)),
        (New-TestIcnsChunk -Type "ic07" -Data (New-TransparentPngBytes -Size 128)),
        (New-TestIcnsChunk -Type "ic08" -Data (New-TransparentPngBytes -Size 256)),
        (New-TestIcnsChunk -Type "ic09" -Data (New-TransparentPngBytes -Size 512)),
        (New-TestIcnsChunk -Type "ic10" -Data (New-TransparentPngBytes -Size 1024))
    )
}

function Write-TestIcnsFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [object[]] $Chunks
    )

    [uint32] $totalLength = 8
    foreach ($chunk in $Chunks) {
        $totalLength += 8 + $chunk.Data.Length
    }

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes("icns"))
        Write-BigEndianUInt32 -Writer $writer -Value $totalLength
        foreach ($chunk in $Chunks) {
            $writer.Write([System.Text.Encoding]::ASCII.GetBytes($chunk.Type))
            Write-BigEndianUInt32 `
                -Writer $writer `
                -Value ([uint32] (8 + $chunk.Data.Length))
            $writer.Write([byte[]] $chunk.Data)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)]
        [scriptblock] $Action
    )

    try {
        & $Action
    }
    catch {
        return
    }

    throw "예상한 예외가 발생하지 않았습니다."
}

function Invoke-TestCase {
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [Parameter(Mandatory)]
        [scriptblock] $Action
    )

    try {
        & $Action
        Write-Host "PASS $Name"
    }
    catch {
        throw "FAIL $Name`: $($_.Exception.Message)"
    }
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("TimetableGenerator-MacOSIconTests-" + [System.Guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $testRoot
try {
    Invoke-TestCase -Name "mixed legacy ARGB and PNG chunks" -Action {
        $path = Join-Path $testRoot "valid-mixed.icns"
        Write-TestIcnsFile -Path $path -Chunks (New-ValidMixedIcnsChunks)
        Assert-IcnsFile -Path $path
    }

    Invoke-TestCase -Name "malformed legacy ARGB marker" -Action {
        $path = Join-Path $testRoot "malformed-argb.icns"
        $chunks = @(New-ValidMixedIcnsChunks)
        $chunks[0] = New-TestIcnsChunk `
            -Type "ic04" `
            -Data ([System.Text.Encoding]::ASCII.GetBytes("XRGB1"))
        Write-TestIcnsFile -Path $path -Chunks $chunks
        Assert-Throws -Action { Assert-IcnsFile -Path $path }
    }

    Invoke-TestCase -Name "empty legacy ARGB body" -Action {
        $path = Join-Path $testRoot "empty-argb.icns"
        $chunks = @(New-ValidMixedIcnsChunks)
        $chunks[0] = New-TestIcnsChunk `
            -Type "ic04" `
            -Data ([System.Text.Encoding]::ASCII.GetBytes("ARGB"))
        Write-TestIcnsFile -Path $path -Chunks $chunks
        Assert-Throws -Action { Assert-IcnsFile -Path $path }
    }

    Invoke-TestCase -Name "missing required 16px representation" -Action {
        $path = Join-Path $testRoot "missing-16px.icns"
        $chunks = @(
            New-ValidMixedIcnsChunks |
                Where-Object { $_.Type -ne "ic04" }
        )
        Write-TestIcnsFile -Path $path -Chunks $chunks
        Assert-Throws -Action { Assert-IcnsFile -Path $path }
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
