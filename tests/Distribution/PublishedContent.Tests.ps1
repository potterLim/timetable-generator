#requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
. (Join-Path $repositoryRoot "scripts/Distribution/Common.ps1")

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

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    $null = $Arguments
}

$testRoot = Join-Path (
    [System.IO.Path]::GetTempPath()) (
    "TimetableGenerator-PublishedContentTests-" + [System.Guid]::NewGuid().ToString("N"))
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

        Invoke-SelfContainedPublish `
            -ProjectPath (Join-Path $testRoot "Desktop.csproj") `
            -RuntimeIdentifier "osx-arm64" `
            -DestinationPath $publishPath `
            -ProductVersion "1.0.0" `
            -NoRestore

        Assert-PathDoesNotExist -Path $productDocumentationPath
        Assert-PathDoesNotExist -Path $dependencyDocumentationPath
        Assert-PathExists -Path $applicationDataPath
        Assert-PathExists -Path $noticePath
    }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
