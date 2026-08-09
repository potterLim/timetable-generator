#requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
. (Join-Path $repositoryRoot "scripts/Distribution/Common.ps1")
. (Join-Path $repositoryRoot "scripts/Distribution/Archive.ps1")
. (Join-Path $repositoryRoot "scripts/Distribution/Distribution.ps1")
. (Join-Path $repositoryRoot "scripts/ReleaseFinalization/ArchiveValidation.ps1")
. (Join-Path $repositoryRoot "tests/Distribution/TestAssertions.ps1")

function Assert-Equal {
    param(
        [Parameter(Mandatory)]
        [object] $Expected,

        [Parameter(Mandatory)]
        [object] $Actual
    )

    if ($Expected -cne $Actual) {
        throw "값이 일치하지 않습니다. 예상: $Expected, 실제: $Actual"
    }
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

function New-TestSourceDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $nestedPath = Join-Path $Path "nested"
    $null = New-Item -ItemType Directory -Path $nestedPath
    [System.IO.File]::WriteAllText((Join-Path $Path "b.txt"), "bravo")
    [System.IO.File]::WriteAllText((Join-Path $nestedPath "a.txt"), "alpha")
}

$tempDirectoryPath = [System.IO.Path]::GetTempPath()
if (-not $IsWindows) {
    $tempDirectoryPath = & realpath $tempDirectoryPath
    if ($LASTEXITCODE -ne 0) {
        throw "운영체제 임시 경로를 확인할 수 없습니다."
    }
}

$testRoot = Join-Path $tempDirectoryPath ("TimetableGenerator-ReleaseArchiveContracts-" + [System.Guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $testRoot
try {
    Invoke-TestCase -Name "ZIP timestamp uses UTC and two-second precision" -Action {
        $timestamp = [System.DateTimeOffset]::Parse("2026-08-09T12:34:59.987+09:00", [System.Globalization.CultureInfo]::InvariantCulture)
        $normalized = Get-NormalizedZipEntryTimestamp -Timestamp $timestamp
        Assert-Equal -Expected "2026-08-09T03:34:58.0000000+00:00" -Actual $normalized.ToString("O")

        Assert-Throws `
            -Action { Get-NormalizedZipEntryTimestamp -Timestamp ([System.DateTimeOffset]::new(1979, 12, 31, 23, 59, 58, [System.TimeSpan]::Zero)) } `
            -ExceptionType ([System.Management.Automation.RuntimeException]) `
            -ExpectedMessageFragment "1980-01-01부터 2107-12-31까지"
    }

    Invoke-TestCase -Name "distribution archive is reproducible at the release commit timestamp" -Action {
        $sourcePath = Join-Path $testRoot "distribution-source"
        $firstOutputPath = Join-Path $testRoot "distribution-output-1"
        $secondOutputPath = Join-Path $testRoot "distribution-output-2"
        $null = New-Item -ItemType Directory -Path $sourcePath
        $null = New-Item -ItemType Directory -Path $firstOutputPath
        $null = New-Item -ItemType Directory -Path $secondOutputPath
        New-TestSourceDirectory -Path $sourcePath

        $archiveTimestamp = [System.DateTimeOffset]::Parse("2026-08-09T03:34:59Z", [System.Globalization.CultureInfo]::InvariantCulture)
        New-DistributionArchive `
            -SourcePath $sourcePath `
            -OutputRoot $firstOutputPath `
            -ArchiveFileName "first.zip" `
            -ArchiveRootName "Product" `
            -ArchivePlatform Windows `
            -ArchiveTimestamp $archiveTimestamp
        New-DistributionArchive `
            -SourcePath $sourcePath `
            -OutputRoot $secondOutputPath `
            -ArchiveFileName "second.zip" `
            -ArchiveRootName "Product" `
            -ArchivePlatform Windows `
            -ArchiveTimestamp $archiveTimestamp

        $firstHash = (Get-FileHash -LiteralPath (Join-Path $firstOutputPath "first.zip") -Algorithm SHA256).Hash
        $secondHash = (Get-FileHash -LiteralPath (Join-Path $secondOutputPath "second.zip") -Algorithm SHA256).Hash
        Assert-Equal -Expected $firstHash -Actual $secondHash
    }

    Invoke-TestCase -Name "Windows final archive is reproducible at the release commit timestamp" -Action {
        $sourcePath = Join-Path $testRoot "windows-source"
        $null = New-Item -ItemType Directory -Path $sourcePath
        New-TestSourceDirectory -Path $sourcePath

        $archiveTimestamp = [System.DateTimeOffset]::Parse("2026-08-09T03:34:59Z", [System.Globalization.CultureInfo]::InvariantCulture)
        $firstArchivePath = Join-Path $testRoot "windows-first.zip"
        $secondArchivePath = Join-Path $testRoot "windows-second.zip"
        New-DeterministicWindowsArchive `
            -SourcePath $sourcePath `
            -DestinationPath $firstArchivePath `
            -ArchiveRootName "Product" `
            -ArchiveTimestamp $archiveTimestamp
        New-DeterministicWindowsArchive `
            -SourcePath $sourcePath `
            -DestinationPath $secondArchivePath `
            -ArchiveRootName "Product" `
            -ArchiveTimestamp $archiveTimestamp

        $firstHash = (Get-FileHash -LiteralPath $firstArchivePath -Algorithm SHA256).Hash
        $secondHash = (Get-FileHash -LiteralPath $secondArchivePath -Algorithm SHA256).Hash
        Assert-Equal -Expected $firstHash -Actual $secondHash
        Assert-ArchiveEntries `
            -ArchivePath $firstArchivePath `
            -RequiredEntryNames @("Product/b.txt", "Product/nested/a.txt") `
            -RequiredPrefix "Product/" `
            -ExpectedEntryTimestamp $archiveTimestamp
    }

    Invoke-TestCase -Name "checksum file is UTF-8 without BOM and LF on every host" -Action {
        $firstFilePath = Join-Path $testRoot "alpha.zip"
        $secondFilePath = Join-Path $testRoot "beta.zip"
        [System.IO.File]::WriteAllText($firstFilePath, "alpha")
        [System.IO.File]::WriteAllText($secondFilePath, "beta")
        $checksumPath = Join-Path $testRoot "checksums.sha256"
        Write-Sha256ChecksumFile -Path $checksumPath -FilePaths @($secondFilePath, $firstFilePath)

        $bytes = [System.IO.File]::ReadAllBytes($checksumPath)
        if ([System.Array]::IndexOf($bytes, [byte] 0x0D) -ge 0) {
            throw "checksum 파일에 CR byte가 있습니다."
        }

        if ($bytes[$bytes.Length - 1] -ne 0x0A) {
            throw "checksum 파일이 LF로 끝나지 않습니다."
        }

        Assert-Sha256ChecksumFile -Path $checksumPath -FilePaths @($firstFilePath, $secondFilePath)
        if ($IsMacOS) {
            Push-Location $testRoot
            try {
                $shasumOutput = @(& shasum -a 256 -c "checksums.sha256" 2>&1)
                if ($LASTEXITCODE -ne 0) {
                    throw "macOS shasum이 checksum 파일을 검증하지 못했습니다: $($shasumOutput -join "`n")"
                }
            }
            finally {
                Pop-Location
            }
        }

        $lfContents = [System.IO.File]::ReadAllText($checksumPath)
        [System.IO.File]::WriteAllText($checksumPath, $lfContents.Replace("`n", "`r`n"), [System.Text.UTF8Encoding]::new($false))
        Assert-Throws `
            -Action { Assert-Sha256ChecksumFile -Path $checksumPath -FilePaths @($firstFilePath, $secondFilePath) } `
            -ExceptionType ([System.Management.Automation.RuntimeException]) `
            -ExpectedMessageFragment "LF 줄바꿈만 사용해야 합니다"
    }

    Invoke-TestCase -Name "publish rejects a foreign runtime before creating output" -Action {
        $foreignRuntime = "win-x64"
        $expectedMessage = "Windows에서만"
        if ($IsWindows) {
            $foreignRuntime = "osx-arm64"
            $expectedMessage = "macOS에서만"
        }

        $outputPath = Join-Path $testRoot "foreign-runtime-output"
        Assert-Throws `
            -Action {
                Publish-TimetableGeneratorDesktop `
                    -RepositoryRoot (Join-Path $testRoot "missing-repository") `
                    -Runtime $foreignRuntime `
                    -OutputRoot $outputPath
            } `
            -ExceptionType ([System.Management.Automation.RuntimeException]) `
            -ExpectedMessageFragment $expectedMessage
        if (Test-Path -LiteralPath $outputPath) {
            throw "지원하지 않는 호스트의 게시 preflight가 출력 경로를 만들었습니다: $outputPath"
        }
    }

    Invoke-TestCase -Name "publish runtime parameter is mandatory and does not support all" -Action {
        $runtimeParameter = (Get-Command Publish-TimetableGeneratorDesktop).Parameters["Runtime"]
        $parameterAttribute = @($runtimeParameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] })[0]
        if (-not $parameterAttribute.Mandatory) {
            throw "Publish-TimetableGeneratorDesktop의 Runtime 매개변수가 필수가 아닙니다."
        }

        $validateSetAttribute = @($runtimeParameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] })[0]
        if ($validateSetAttribute.ValidValues -ccontains "all") {
            throw "Publish-TimetableGeneratorDesktop의 Runtime에 all이 남아 있습니다."
        }
    }

    Invoke-TestCase -Name "Assert-Throws distinguishes exception type and message" -Action {
        Assert-Throws `
            -Action { throw [System.InvalidOperationException]::new("stable failure") } `
            -ExceptionType ([System.InvalidOperationException]) `
            -ExpectedMessageFragment "stable failure"

        $typeMismatchWasRejected = $false
        try {
            Assert-Throws `
                -Action { throw [System.InvalidOperationException]::new("stable failure") } `
                -ExceptionType ([System.IO.IOException]) `
                -ExpectedMessageFragment "stable failure"
        }
        catch {
            $typeMismatchWasRejected = $_.Exception.Message.Contains("예외 종류가 예상과 일치하지 않습니다", [System.StringComparison]::Ordinal)
        }

        if (-not $typeMismatchWasRejected) {
            throw "Assert-Throws가 잘못된 예외 종류를 거부하지 않았습니다."
        }
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
