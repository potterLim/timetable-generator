function Assert-Throws {
    param(
        [Parameter(Mandatory)]
        [scriptblock] $Action,

        [Parameter(Mandatory)]
        [type] $ExceptionType,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $ExpectedMessageFragment
    )

    try {
        & $Action
    }
    catch {
        $actualException = $_.Exception
        if (-not $ExceptionType.IsAssignableFrom($actualException.GetType())) {
            throw "예외 종류가 예상과 일치하지 않습니다. 예상: $($ExceptionType.FullName), 실제: $($actualException.GetType().FullName)"
        }

        if (-not $actualException.Message.Contains($ExpectedMessageFragment, [System.StringComparison]::Ordinal)) {
            throw "예외 메시지가 예상과 일치하지 않습니다. 예상 문구: $ExpectedMessageFragment, 실제: $($actualException.Message)"
        }

        return
    }

    throw "예상한 예외가 발생하지 않았습니다: $($ExceptionType.FullName)"
}
