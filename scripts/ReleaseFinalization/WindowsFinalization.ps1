function Assert-WindowsProductMetadata {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Version
    )

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    $expectedTextValues = @{
        CompanyName = $script:PRODUCT_COMPANY_NAME
        FileDescription = $script:PRODUCT_DISPLAY_NAME
        ProductName = $script:PRODUCT_DISPLAY_NAME
    }
    foreach ($propertyName in $expectedTextValues.Keys) {
        $actualValue = [string] $versionInfo.$propertyName
        $expectedValue = [string] $expectedTextValues[$propertyName]
        if ($actualValue -cne $expectedValue) {
            throw "Windows 제품 메타데이터가 예상과 일치하지 않습니다: $propertyName ($actualValue)"
        }
    }

    $productVersion = [string] $versionInfo.ProductVersion
    $expectedPattern = "^" + [System.Text.RegularExpressions.Regex]::Escape($Version) + "(?:\+.+)?$"
    if ([string]::IsNullOrWhiteSpace($productVersion) -or
        $productVersion -notmatch $expectedPattern) {
        throw "제품 버전이 요청한 Release 버전과 일치하지 않습니다: $Path ($productVersion)"
    }
}

function Assert-WindowsSignature {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
        $null -eq $signature.SignerCertificate) {
        throw "Windows 실행 파일의 Authenticode 서명이 유효하지 않습니다: $($signature.Status)"
    }

    if ($null -eq $signature.TimeStamperCertificate) {
        throw "Windows 실행 파일에 신뢰 가능한 timestamp가 없습니다: $Path"
    }
}

function Assert-WindowsExecutableIsUnsigned {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::NotSigned) {
        throw "공식 무서명 Windows 실행 파일은 서명이 없는 정상 상태여야 합니다: $($signature.Status)"
    }
}

function Invoke-WindowsFinalization {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $Version,

        [string] $SourcePath,

        [string] $OutputRoot,

        [ValidateSet("Signed", "Unsigned")]
        [string] $WindowsSignatureMode = "Signed",

        [switch] $AllowUnsigned
    )

    if ($AllowUnsigned -and $WindowsSignatureMode -eq "Unsigned") {
        throw "공식 무서명 Windows 정책과 unsigned smoke 정책은 함께 사용할 수 없습니다."
    }

    if ($IsWindows -eq $false) {
        throw "Windows Release 최종화는 Windows에서만 실행할 수 있습니다."
    }

    $source = if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        Resolve-PathFromRepository `
            -RepositoryRoot $RepositoryRoot `
            -Path "artifacts/publish/win-x64"
    }
    else {
        Resolve-PathFromRepository -RepositoryRoot $RepositoryRoot -Path $SourcePath
    }
    if ((Test-Path -LiteralPath $source -PathType Container) -eq $false) {
        throw "Windows 게시 디렉터리를 찾을 수 없습니다: $source"
    }
    Assert-TreeHasNoReparsePoint -Path $source

    $executablePath = Join-Path $source "$($script:PRODUCT_EXECUTABLE_BASE_NAME).exe"
    $managedAssemblyPath = Join-Path $source "$($script:PRODUCT_EXECUTABLE_BASE_NAME).dll"
    foreach ($requiredPath in @(
        $executablePath,
        $managedAssemblyPath,
        (Join-Path $source "$($script:PRODUCT_EXECUTABLE_BASE_NAME).deps.json"),
        (Join-Path $source "$($script:PRODUCT_EXECUTABLE_BASE_NAME).runtimeconfig.json"),
        (Join-Path $source "coreclr.dll"))) {
        Assert-NonEmptyFile -Path $requiredPath
    }

    Assert-WindowsProductMetadata -Path $executablePath -Version $Version
    Assert-WindowsX64PeBinary -Path $executablePath
    Assert-NoDebugSymbols -Path $source
    Assert-RequiredConfigurationFiles -Path $source
    Assert-RequiredNoticeFiles -Path (Join-Path $source "ThirdPartyNotices")
    if ($AllowUnsigned) {
        Write-Verbose "로컬 구조 검사용 unsigned smoke ZIP을 생성합니다."
    }
    elseif ($WindowsSignatureMode -eq "Unsigned") {
        Assert-WindowsExecutableIsUnsigned -Path $executablePath
    }
    else {
        Assert-WindowsSignature -Path $executablePath
    }

    $releaseRoot = Resolve-ReleaseOutputRoot `
        -RepositoryRoot $RepositoryRoot `
        -Version $Version `
        -RequestedOutputRoot $OutputRoot `
        -SourcePath $source `
        -AllowUnsigned:$AllowUnsigned
    $allowedFileNames = @(Get-AllowedReleaseOutputFileNames -Version $Version -AllowUnsigned:$AllowUnsigned)
    Assert-ReleaseOutputRootContents -OutputRoot $releaseRoot -AllowedFileNames $allowedFileNames
    $archiveFileName = Get-WindowsReleaseArchiveFileName `
        -Version $Version `
        -WindowsSignatureMode $WindowsSignatureMode `
        -AllowUnsigned:$AllowUnsigned
    $archivePath = Join-Path $releaseRoot $archiveFileName
    Remove-ExistingReleaseFile `
        -OutputRoot $releaseRoot `
        -Path $archivePath `
        -ExpectedFileName $archiveFileName
    $archiveRootName = "TimetableGenerator-$Version"
    try {
        New-DeterministicWindowsArchive `
            -SourcePath $source `
            -DestinationPath $archivePath `
            -ArchiveRootName $archiveRootName

        $prefix = "$archiveRootName/"
        $requiredEntries = @(
            "$prefix$($script:PRODUCT_EXECUTABLE_BASE_NAME).exe",
            "$prefix$($script:PRODUCT_EXECUTABLE_BASE_NAME).dll",
            "$prefix$($script:PRODUCT_EXECUTABLE_BASE_NAME).deps.json",
            "$prefix$($script:PRODUCT_EXECUTABLE_BASE_NAME).runtimeconfig.json",
            "${prefix}coreclr.dll"
        )
        foreach ($configurationFileName in $script:CONFIGURATION_FILE_NAMES) {
            $requiredEntries += "$prefix$configurationFileName"
        }
        foreach ($noticeFileName in $script:REQUIRED_NOTICE_FILE_NAMES) {
            $requiredEntries += "${prefix}ThirdPartyNotices/$noticeFileName"
        }

        Assert-ArchiveEntries `
            -ArchivePath $archivePath `
            -RequiredEntryNames $requiredEntries `
            -RequiredPrefix $prefix
    }
    catch {
        Remove-ExistingReleaseFile `
            -OutputRoot $releaseRoot `
            -Path $archivePath `
            -ExpectedFileName $archiveFileName
        throw
    }
    Write-Host "Windows Release ZIP을 생성했습니다: $archivePath"
}
