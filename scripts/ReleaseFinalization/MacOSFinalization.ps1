function Invoke-MacOSFinalization {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [ValidateSet("osx-arm64")]
        [string] $Runtime,

        [Parameter(Mandatory)]
        [string] $BundleIdentifier,

        [string] $SourcePath,

        [string] $OutputRoot,

        [switch] $AllowUnsigned
    )

    if ($IsMacOS -eq $false) {
        throw "macOS Release 최종화는 macOS에서만 실행할 수 있습니다."
    }

    foreach ($commandName in @("codesign", "ditto", "plutil", "spctl", "xcrun")) {
        if ($null -eq (Get-Command $commandName -CommandType Application -ErrorAction SilentlyContinue)) {
            throw "macOS Release 최종화에 필요한 명령을 찾을 수 없습니다: $commandName"
        }
    }

    if ($script:PLACEHOLDER_BUNDLE_IDENTIFIERS -contains $BundleIdentifier) {
        throw "placeholder Bundle ID는 Release 최종화에 사용할 수 없습니다."
    }

    $applicationPath = if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        Resolve-PathFromRepository -RepositoryRoot $RepositoryRoot -Path "artifacts/publish/$Runtime/$($script:MACOS_APPLICATION_NAME)"
    }
    else {
        Resolve-PathFromRepository -RepositoryRoot $RepositoryRoot -Path $SourcePath
    }
    if ((Test-Path -LiteralPath $applicationPath -PathType Container) -eq $false) {
        throw "macOS 앱 번들을 찾을 수 없습니다: $applicationPath"
    }
    Assert-TreeHasNoReparsePoint -Path $applicationPath

    $bundleLayout = Get-MacOSReleaseBundleLayout -ApplicationPath $applicationPath
    Assert-MacOSReleaseBundle `
        -Layout $bundleLayout `
        -Version $Version `
        -Runtime $Runtime `
        -BundleIdentifier $BundleIdentifier `
        -AllowUnsigned:$AllowUnsigned

    New-MacOSReleaseArchive `
        -RepositoryRoot $RepositoryRoot `
        -Version $Version `
        -Runtime $Runtime `
        -ApplicationPath $applicationPath `
        -OutputRoot $OutputRoot `
        -AllowUnsigned:$AllowUnsigned
}
