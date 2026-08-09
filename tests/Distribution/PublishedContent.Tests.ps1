#requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
. (Join-Path $repositoryRoot "scripts/Distribution/Common.ps1")
. (Join-Path $repositoryRoot "scripts/Distribution/BinaryValidation.ps1")
. (Join-Path $repositoryRoot "scripts/Distribution/MacOSEventKitBridgeValidation.ps1")
. (Join-Path $repositoryRoot "scripts/Distribution/MacOSPropertyList.ps1")
. (Join-Path $repositoryRoot "tests/Distribution/TestAssertions.ps1")

function Assert-PathExists {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "예상한 파일을 찾을 수 없습니다: $Path"
    }
}

function Assert-PathDoesNotExist {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    if (Test-Path -LiteralPath $Path) {
        throw "제거되어야 하는 파일이 남아 있습니다: $Path"
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

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("TimetableGenerator-PublishedContentTests-" + [System.Guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $testRoot
try {
    Invoke-TestCase -Name "publish removes only XML documentation" -Action {
        $publishPath = Join-Path $testRoot "publish"
        $nestedPath = Join-Path $publishPath "nested"
        $null = New-Item -ItemType Directory -Path $nestedPath

        $productDocumentationPath = Join-Path $publishPath "TimetableGenerator.xml"
        $dependencyDocumentationPath = Join-Path $nestedPath "Dependency.xml"
        $applicationDataPath = Join-Path $publishPath "ApplicationData.xml"
        $noticePath = Join-Path $publishPath "THIRD-PARTY-NOTICES.txt"
        [System.IO.File]::WriteAllBytes(
            (Join-Path $publishPath "TimetableGenerator.dll"),
            [byte[]] @(1))
        [System.IO.File]::WriteAllBytes(
            (Join-Path $nestedPath "Dependency.dll"),
            [byte[]] @(1))
        [System.IO.File]::WriteAllBytes(
            (Join-Path $publishPath "ApplicationData.dll"),
            [byte[]] @(1))
        [System.IO.File]::WriteAllText(
            $productDocumentationPath,
            "<?xml version=`"1.0`"?><doc><assembly><name>TimetableGenerator</name></assembly></doc>")
        [System.IO.File]::WriteAllText(
            $dependencyDocumentationPath,
            "<doc><assembly><name>Dependency</name></assembly></doc>")
        [System.IO.File]::WriteAllText(
            $applicationDataPath,
            "<?xml version=`"1.0`"?><applicationData />")
        [System.IO.File]::WriteAllText($noticePath, "Required third-party notice")

        Remove-PublishedXmlDocumentationFiles -Path $publishPath

        Assert-PathDoesNotExist -Path $productDocumentationPath
        Assert-PathDoesNotExist -Path $dependencyDocumentationPath
        Assert-PathExists -Path $applicationDataPath
        Assert-PathExists -Path $noticePath
    }

    Invoke-TestCase -Name "Unix executable mode preserves access bits" -Action {
        if ($IsWindows) {
            return
        }

        $executablePath = Join-Path $testRoot "TimetableGenerator"
        [System.IO.File]::WriteAllBytes($executablePath, [byte[]] @(1))
        $initialMode = [System.IO.UnixFileMode]::UserRead `
            -bor [System.IO.UnixFileMode]::UserWrite `
            -bor [System.IO.UnixFileMode]::GroupRead `
            -bor [System.IO.UnixFileMode]::OtherRead
        [System.IO.File]::SetUnixFileMode($executablePath, $initialMode)

        Set-UnixExecutableFileMode -Path $executablePath

        $expectedMode = $initialMode `
            -bor [System.IO.UnixFileMode]::UserExecute `
            -bor [System.IO.UnixFileMode]::GroupExecute `
            -bor [System.IO.UnixFileMode]::OtherExecute
        $actualMode = [System.IO.File]::GetUnixFileMode($executablePath)
        if ($actualMode -ne $expectedMode) {
            throw "실행 권한이 보존된 access mode에 추가되지 않았습니다: $actualMode"
        }
    }

    Invoke-TestCase -Name "macOS metadata requires EventKit full access without Apple Events" -Action {
        $infoPlistTemplatePath = Join-Path $repositoryRoot "src/TimetableGenerator.Desktop/Platforms/macOS/Info.plist.template"
        $infoPlistPath = Join-Path $testRoot "Info.plist"
        $infoPlist = [System.IO.File]::ReadAllText($infoPlistTemplatePath)
        $infoPlist = $infoPlist.Replace("__EXECUTABLE_NAME__", "TimetableGenerator")
        $infoPlist = $infoPlist.Replace("__BUNDLE_IDENTIFIER__", "io.github.potterlim.timetable")
        $infoPlist = $infoPlist.Replace("__VERSION__", "1.0.0")
        [System.IO.File]::WriteAllText($infoPlistPath, $infoPlist)

        Assert-MacOSInfoPlist `
            -Path $infoPlistPath `
            -ExecutableName "TimetableGenerator" `
            -BundleIdentifier "io.github.potterlim.timetable" `
            -ProductVersion "1.0.0"

        $entitlementsPath = Join-Path $repositoryRoot "src/TimetableGenerator.Desktop/Platforms/macOS/TimetableGenerator.entitlements"
        Assert-MacOSEntitlements -Path $entitlementsPath

        $legacyInfoPlistPath = Join-Path $testRoot "Legacy-Info.plist"
        $legacyInfoPlist = $infoPlist.Replace("  <key>NSPrincipalClass</key>", "  <key>NSAppleEventsUsageDescription</key>`n  <string>legacy</string>`n  <key>NSPrincipalClass</key>")
        [System.IO.File]::WriteAllText($legacyInfoPlistPath, $legacyInfoPlist)
        Assert-Throws -Action {
            Assert-MacOSInfoPlist `
                -Path $legacyInfoPlistPath `
                -ExecutableName "TimetableGenerator" `
                -BundleIdentifier "io.github.potterlim.timetable" `
                -ProductVersion "1.0.0"
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "사용하지 않는 Apple Events 권한 설명"

        $legacyEntitlementsPath = Join-Path $testRoot "Legacy.entitlements"
        $legacyEntitlements = [System.IO.File]::ReadAllText($entitlementsPath).Replace("</dict>", "  <key>com.apple.security.automation.apple-events</key>`n  <true/>`n</dict>")
        [System.IO.File]::WriteAllText($legacyEntitlementsPath, $legacyEntitlements)
        Assert-Throws -Action {
            Assert-MacOSEntitlements -Path $legacyEntitlementsPath
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "구성이 예상과 일치하지 않습니다"
    }

    Invoke-TestCase -Name "macOS EventKit bridge must expose and satisfy the versioned C ABI" -Action {
        Assert-MacOSEventKitBridgeExportSymbols -Symbols @("_tg_eventkit_abi_version", "_tg_eventkit_execute", "_tg_eventkit_execute_cancellable", "_tg_eventkit_free", "_tg_eventkit_schema_version")
        Assert-Throws -Action {
            Assert-MacOSEventKitBridgeExportSymbols -Symbols @("_tg_eventkit_execute", "_tg_eventkit_free")
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "필수 C ABI export가 없습니다"

        $eventKitBridgePath = Join-Path $testRoot "libTimetableGenerator.EventKitBridge.dylib"
        $eventKitBridgeSourcePath = Join-Path $repositoryRoot "tests/Distribution/Fixtures/EventKitBridgeStub.c"
        if ($IsMacOS) {
            $compilerOutput = @(& xcrun clang -dynamiclib -arch arm64 -std=c11 -Wall -Wextra -Werror $eventKitBridgeSourcePath -o $eventKitBridgePath 2>&1)
            if ($LASTEXITCODE -ne 0) {
                throw "테스트용 EventKit bridge를 빌드하지 못했습니다: $($compilerOutput -join "`n")"
            }
        }
        else {
            [System.IO.File]::WriteAllBytes($eventKitBridgePath, [byte[]] @(0xCF, 0xFA, 0xED, 0xFE, 0x0C, 0x00, 0x00, 0x01))
        }

        Assert-MacOSEventKitBridgeBinary -Path $eventKitBridgePath -RuntimeIdentifier "osx-arm64"
        if ($IsMacOS -and [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
            $invalidAbiPath = Join-Path $testRoot "libTimetableGenerator.EventKitBridge.InvalidAbi.dylib"
            $compilerOutput = @(& xcrun clang -dynamiclib -arch arm64 -std=c11 -Wall -Wextra -Werror -DTG_EVENT_KIT_TEST_ABI_VERSION=2 $eventKitBridgeSourcePath -o $invalidAbiPath 2>&1)
            if ($LASTEXITCODE -ne 0) {
                throw "잘못된 ABI 테스트용 EventKit bridge를 빌드하지 못했습니다: $($compilerOutput -join "`n")"
            }
            Assert-Throws -Action {
                Assert-MacOSEventKitBridgeBinary -Path $invalidAbiPath -RuntimeIdentifier "osx-arm64"
            } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "ABI, schema 또는 execute/free 검증에 실패했습니다"

            $invalidSchemaPath = Join-Path $testRoot "libTimetableGenerator.EventKitBridge.InvalidSchema.dylib"
            $compilerOutput = @(& xcrun clang -dynamiclib -arch arm64 -std=c11 -Wall -Wextra -Werror -DTG_EVENT_KIT_TEST_SCHEMA_VERSION=2 $eventKitBridgeSourcePath -o $invalidSchemaPath 2>&1)
            if ($LASTEXITCODE -ne 0) {
                throw "잘못된 schema 테스트용 EventKit bridge를 빌드하지 못했습니다: $($compilerOutput -join "`n")"
            }
            Assert-Throws -Action {
                Assert-MacOSEventKitBridgeBinary -Path $invalidSchemaPath -RuntimeIdentifier "osx-arm64"
            } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "ABI, schema 또는 execute/free 검증에 실패했습니다"
        }

        [System.IO.File]::WriteAllText($eventKitBridgePath, "not a Mach-O binary")
        Assert-Throws -Action {
            Assert-MacOSEventKitBridgeBinary -Path $eventKitBridgePath -RuntimeIdentifier "osx-arm64"
        } -ExceptionType ([System.Management.Automation.RuntimeException]) -ExpectedMessageFragment "Mach-O 형식이 아닙니다"
    }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
