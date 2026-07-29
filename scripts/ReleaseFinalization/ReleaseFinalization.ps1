function Invoke-TimetableGeneratorReleaseFinalization {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet("Windows", "MacOS", "Aggregate")]
        [string] $Stage,

        [Parameter(Mandatory)]
        [ValidatePattern("^\d+\.\d+\.\d+$")]
        [string] $Version,

        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [ValidateSet("osx-x64", "osx-arm64")]
        [string] $Runtime,

        [string] $BundleIdentifier = "io.github.potterlim.timetable",

        [ValidateSet("Signed", "Unsigned")]
        [string] $WindowsSignatureMode = "Signed",

        [string] $SourcePath,

        [string] $OutputRoot,

        [switch] $AllowUnsigned
    )

    if ($AllowUnsigned -and $WindowsSignatureMode -eq "Unsigned") {
        throw "-AllowUnsigned와 -WindowsSignatureMode Unsigned는 함께 사용할 수 없습니다."
    }

    $repository = Get-NormalizedFullPath -Path $RepositoryRoot
    switch ($Stage) {
        "Windows" {
            Invoke-WindowsFinalization `
                -RepositoryRoot $repository `
                -Version $Version `
                -SourcePath $SourcePath `
                -OutputRoot $OutputRoot `
                -WindowsSignatureMode $WindowsSignatureMode `
                -AllowUnsigned:$AllowUnsigned
        }
        "MacOS" {
            if ([string]::IsNullOrWhiteSpace($Runtime)) {
                throw "MacOS 단계에는 -Runtime osx-x64 또는 osx-arm64가 필요합니다."
            }

            if ([string]::IsNullOrWhiteSpace($BundleIdentifier)) {
                throw "MacOS 단계에는 최종 제품 -BundleIdentifier가 필요합니다."
            }

            Invoke-MacOSFinalization `
                -RepositoryRoot $repository `
                -Version $Version `
                -Runtime $Runtime `
                -BundleIdentifier $BundleIdentifier `
                -SourcePath $SourcePath `
                -OutputRoot $OutputRoot `
                -AllowUnsigned:$AllowUnsigned
        }
        "Aggregate" {
            Invoke-AggregateFinalization `
                -RepositoryRoot $repository `
                -Version $Version `
                -SourcePath $SourcePath `
                -OutputRoot $OutputRoot `
                -WindowsSignatureMode $WindowsSignatureMode `
                -AllowUnsigned:$AllowUnsigned
        }
        default {
            throw "지원하지 않는 Release 최종화 단계입니다: $Stage"
        }
    }
}
