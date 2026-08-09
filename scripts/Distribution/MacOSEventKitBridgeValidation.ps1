function Assert-MacOSEventKitBridgeBinary {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [ValidateSet("osx-arm64")]
        [string] $RuntimeIdentifier
    )

    Assert-NonEmptyFile -Path $Path
    if (-not (Test-IsMachOFile -Path $Path)) {
        throw "EventKit 네이티브 모듈이 Mach-O 형식이 아닙니다: $Path"
    }

    Assert-MachOArchitecture -Path $Path -RuntimeIdentifier $RuntimeIdentifier
    if ($IsMacOS) {
        Assert-MacOSEventKitBridgeExports -Path $Path
        if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
            Assert-MacOSEventKitBridgeAbi -Path $Path
        }
    }
}

function Assert-MacOSEventKitBridgeExports {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $symbolOutput = @(& xcrun nm -gjU -- $Path 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "EventKit 네이티브 모듈의 export 목록을 읽을 수 없습니다: $Path"
    }

    Assert-MacOSEventKitBridgeExportSymbols -Symbols $symbolOutput
}

function Assert-MacOSEventKitBridgeExportSymbols {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]] $Symbols
    )

    $exportedSymbols = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($symbol in $Symbols) {
        $normalizedSymbol = $symbol.Trim()
        if ($normalizedSymbol.StartsWith("_", [System.StringComparison]::Ordinal)) {
            $normalizedSymbol = $normalizedSymbol.Substring(1)
        }
        $null = $exportedSymbols.Add($normalizedSymbol)
    }

    foreach ($requiredSymbol in @("tg_eventkit_abi_version", "tg_eventkit_execute", "tg_eventkit_execute_cancellable", "tg_eventkit_free", "tg_eventkit_schema_version")) {
        if (-not $exportedSymbols.Contains($requiredSymbol)) {
            throw "EventKit 네이티브 모듈에 필수 C ABI export가 없습니다: $requiredSymbol"
        }
    }
}

function Assert-MacOSEventKitBridgeAbi {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $probeSourcePath = Join-Path $PSScriptRoot "Native/EventKitBridgeAbiProbe.c"
    Assert-NonEmptyFile -Path $probeSourcePath

    $temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("TimetableGenerator-EventKitBridgeAbi-" + [System.Guid]::NewGuid().ToString("N"))
    $executablePath = Join-Path $temporaryRoot "EventKitBridgeAbiProbe"
    $null = New-Item -ItemType Directory -Path $temporaryRoot
    try {
        $compilerOutput = @(& xcrun clang -std=c11 -Wall -Wextra -Werror $probeSourcePath -o $executablePath 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "EventKit 네이티브 모듈 ABI 검사기를 빌드하지 못했습니다: $($compilerOutput -join "`n")"
        }

        $resolvedBridgePath = [System.IO.Path]::GetFullPath($Path)
        $probeOutput = @(& $executablePath $resolvedBridgePath 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "EventKit 네이티브 모듈의 ABI, schema 또는 execute/free 검증에 실패했습니다: $($probeOutput -join "`n")"
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }
}
