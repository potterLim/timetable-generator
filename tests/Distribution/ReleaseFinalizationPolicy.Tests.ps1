#requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$modulePath = Join-Path $repositoryRoot "scripts/ReleaseFinalization/TimetableGenerator.ReleaseFinalization.psm1"
$commonPath = Join-Path $repositoryRoot "scripts/Distribution/Common.ps1"
$pathUtilitiesPath = Join-Path $repositoryRoot "scripts/ReleaseFinalization/PathUtilities.ps1"
$macOSReleaseValidationPath = Join-Path $repositoryRoot "scripts/ReleaseFinalization/MacOSReleaseValidation.ps1"
$nativeCommandPath = Join-Path $repositoryRoot "scripts/ReleaseFinalization/NativeCommand.ps1"
$macOSPropertyListPath = Join-Path $repositoryRoot "scripts/Distribution/MacOSPropertyList.ps1"
$testAssertionsPath = Join-Path $repositoryRoot "tests/Distribution/TestAssertions.ps1"

Import-Module -Name $modulePath -Force
. $commonPath
. $pathUtilitiesPath
. $macOSPropertyListPath
. $nativeCommandPath
. $macOSReleaseValidationPath
. $testAssertionsPath

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

$tempDirectoryPath = [System.IO.Path]::GetTempPath()
if (-not $IsWindows) {
    $tempDirectoryPath = & realpath $tempDirectoryPath
    if ($LASTEXITCODE -ne 0) {
        throw "운영체제 임시 경로를 확인할 수 없습니다."
    }
}

$testRoot = Join-Path $tempDirectoryPath ("TimetableGenerator-ReleasePolicyTests-" + [System.Guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $testRoot
try {
    Invoke-TestCase -Name "official Windows archive keeps a product-facing name" -Action {
        Assert-Equal `
            -Expected "TimetableGenerator-1.0.0-win-x64.zip" `
            -Actual (Get-WindowsReleaseArchiveFileName -Version "1.0.0")
        Assert-Equal `
            -Expected "TimetableGenerator-1.0.0-win-x64.zip" `
            -Actual (Get-WindowsReleaseArchiveFileName `
                -Version "1.0.0" `
                -WindowsSignatureMode Unsigned)
        Assert-Equal `
            -Expected "TimetableGenerator-1.0.0-win-x64-unsigned-smoke.zip" `
            -Actual (Get-WindowsReleaseArchiveFileName -Version "1.0.0" -AllowUnsigned)
    }

    Invoke-TestCase -Name "Aggregate contract selects exactly one Windows policy" -Action {
        $signedNames = @(Get-ExpectedReleaseArchiveFileNames -Version "1.0.0")
        $unsignedNames = @(Get-ExpectedReleaseArchiveFileNames `
            -Version "1.0.0" `
            -WindowsSignatureMode Unsigned)

        Assert-Equal -Expected 2 -Actual $signedNames.Count
        Assert-Equal -Expected 2 -Actual $unsignedNames.Count
        Assert-Equal -Expected "TimetableGenerator-1.0.0-osx-arm64.zip" -Actual $signedNames[0]
        Assert-Equal -Expected "TimetableGenerator-1.0.0-win-x64.zip" -Actual $signedNames[1]
        Assert-Equal -Expected "TimetableGenerator-1.0.0-osx-arm64.zip" -Actual $unsignedNames[0]
        Assert-Equal `
            -Expected "TimetableGenerator-1.0.0-win-x64.zip" `
            -Actual $unsignedNames[1]
    }

    Invoke-TestCase -Name "legacy unsigned suffix is not an official output" -Action {
        $officialNames = @(Get-AllowedReleaseOutputFileNames -Version "1.0.0")
        if ($officialNames -ccontains "TimetableGenerator-1.0.0-win-x64-unsigned.zip") {
            throw "기존 무서명 suffix가 공식 Release 출력에 남아 있습니다."
        }
    }

    Invoke-TestCase -Name "official unsigned and smoke policies cannot be combined" -Action {
        Assert-Throws -Action {
            Invoke-TimetableGeneratorReleaseFinalization `
                -Stage Aggregate `
                -Version "1.0.0" `
                -RepositoryRoot $repositoryRoot `
                -WindowsSignatureMode Unsigned `
                -AllowUnsigned
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "함께 사용할 수 없습니다"
    }

    Invoke-TestCase -Name "official unsigned policy uses the release output tree" -Action {
        $testRepository = Join-Path $testRoot "repository"
        $null = New-Item -ItemType Directory -Path $testRepository
        $officialRoot = Resolve-ReleaseOutputRoot -RepositoryRoot $testRepository -Version "1.0.0"
        $smokeRoot = Resolve-ReleaseOutputRoot -RepositoryRoot $testRepository -Version "1.0.0" -AllowUnsigned

        Assert-Equal `
            -Expected ([System.IO.Path]::GetFullPath((Join-Path $testRepository "artifacts/release/1.0.0"))) `
            -Actual $officialRoot
        Assert-Equal `
            -Expected ([System.IO.Path]::GetFullPath((Join-Path $testRepository "artifacts/release-smoke/1.0.0"))) `
            -Actual $smokeRoot
    }

    Invoke-TestCase -Name "smoke policy remains isolated and rejected by Aggregate" -Action {
        Assert-Throws -Action {
            Invoke-TimetableGeneratorReleaseFinalization `
                -Stage Aggregate `
                -Version "1.0.0" `
                -RepositoryRoot $repositoryRoot `
                -AllowUnsigned
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "unsigned smoke archive를 허용하지 않습니다"

    }

    Invoke-TestCase -Name "macOS signed components require Developer ID runtime and one TeamIdentifier" -Action {
        $validSigningDetails = @"
Executable=/tmp/example
Identifier=io.github.potterlim.timetable
CodeDirectory v=20500 size=123 flags=0x10000(runtime) hashes=1+7 location=embedded
Authority=Developer ID Application: Timetable Generator (ABCDE12345)
TeamIdentifier=ABCDE12345
Runtime Version=26.0.0
"@
        $teamIdentifier = Get-MacOSDeveloperIDTeamIdentifierFromSigningDetails -SigningDetails $validSigningDetails -ArtifactDescription "테스트 앱"
        Assert-Equal -Expected "ABCDE12345" -Actual $teamIdentifier
        Assert-MacOSMatchingTeamIdentifiers -ApplicationTeamIdentifier $teamIdentifier -MainExecutableTeamIdentifier $teamIdentifier -EventKitBridgeTeamIdentifier $teamIdentifier

        Assert-Throws -Action {
            $invalidDetails = $validSigningDetails.Replace("flags=0x10000(runtime)", "flags=0x0(none)")
            Get-MacOSDeveloperIDTeamIdentifierFromSigningDetails -SigningDetails $invalidDetails -ArtifactDescription "테스트 앱"
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "hardened runtime"
        Assert-Throws -Action {
            $invalidDetails = $validSigningDetails.Replace("Authority=Developer ID Application:", "Authority=Apple Development:")
            Get-MacOSDeveloperIDTeamIdentifierFromSigningDetails -SigningDetails $invalidDetails -ArtifactDescription "테스트 앱"
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "Developer ID Application"
        Assert-Throws -Action {
            $invalidDetails = $validSigningDetails.Replace("TeamIdentifier=ABCDE12345", "TeamIdentifier=not set")
            Get-MacOSDeveloperIDTeamIdentifierFromSigningDetails -SigningDetails $invalidDetails -ArtifactDescription "테스트 앱"
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "TeamIdentifier"
        Assert-Throws -Action {
            Assert-MacOSMatchingTeamIdentifiers -ApplicationTeamIdentifier "ABCDE12345" -MainExecutableTeamIdentifier "ABCDE12345" -EventKitBridgeTeamIdentifier "ZZZZZ99999"
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "정확히 일치하지 않습니다"
    }

    Invoke-TestCase -Name "signed entitlement plist requires exact true keys" -Action {
        $validPath = Join-Path $testRoot "valid-signed-entitlements.plist"
        [System.IO.File]::Copy(
            (Join-Path $repositoryRoot "src/TimetableGenerator.Desktop/Platforms/macOS/TimetableGenerator.entitlements"),
            $validPath)
        Assert-MacOSSignedEntitlementsFile -Path $validPath

        $falsePath = Join-Path $testRoot "false-signed-entitlements.plist"
        $falseContents = [System.IO.File]::ReadAllText($validPath).Replace("<true/>", "<false/>")
        [System.IO.File]::WriteAllText($falsePath, $falseContents)
        Assert-Throws -Action {
            Assert-MacOSSignedEntitlementsFile -Path $falsePath
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "=true가 없습니다"

        $unexpectedPath = Join-Path $testRoot "unexpected-signed-entitlements.plist"
        $unexpectedContents = [System.IO.File]::ReadAllText($validPath).Replace("</dict>", "  <key>com.apple.security.get-task-allow</key>`n  <true/>`n</dict>")
        [System.IO.File]::WriteAllText($unexpectedPath, $unexpectedContents)
        Assert-Throws -Action {
            Assert-MacOSSignedEntitlementsFile -Path $unexpectedPath
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "구성이 예상과 일치하지 않습니다"

        $missingPath = Join-Path $testRoot "missing-signed-entitlements.plist"
        Assert-Throws -Action {
            Assert-MacOSSignedEntitlementsFile -Path $missingPath
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "필수 산출물 파일이 없거나 비어 있습니다"
    }

    Invoke-TestCase -Name "codesign XML extraction rejects a missing entitlement blob" -Action {
        if (-not $IsMacOS) {
            return
        }

        $signedExecutablePath = Join-Path $testRoot "signed-test-executable"
        Copy-Item -LiteralPath "/bin/ls" -Destination $signedExecutablePath
        $entitlementsPath = Join-Path $repositoryRoot "src/TimetableGenerator.Desktop/Platforms/macOS/TimetableGenerator.entitlements"
        Invoke-NativeCommand `
            -Command "codesign" `
            -Arguments @("--force", "--sign", "-", "--options", "runtime", "--entitlements", $entitlementsPath, "--", $signedExecutablePath) `
            -FailureMessage "테스트 실행 파일을 임시 서명할 수 없습니다."
        Assert-MacOSSignedEntitlements -MainExecutablePath $signedExecutablePath

        Assert-Throws `
            -Action { Assert-MacOSSignedEntitlements -MainExecutablePath "/bin/ls" } `
            -ExceptionType ([System.Management.Automation.RuntimeException]) `
            -ExpectedMessageFragment "유효한 entitlement XML을 생성하지 않았습니다"
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
