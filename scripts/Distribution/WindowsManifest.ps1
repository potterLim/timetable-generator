function Assert-WindowsApplicationManifest {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
    }
    finally {
        $reader.Dispose()
    }

    $namespaces = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
    $namespaces.AddNamespace("asm", "urn:schemas-microsoft-com:asm.v1")
    $namespaces.AddNamespace("asm3", "urn:schemas-microsoft-com:asm.v3")
    $namespaces.AddNamespace("compat", "urn:schemas-microsoft-com:compatibility.v1")
    $namespaces.AddNamespace("windows", "http://schemas.microsoft.com/SMI/2016/WindowsSettings")

    $identity = $document.SelectSingleNode("/asm:assembly/asm:assemblyIdentity", $namespaces)
    if ($null -eq $identity -or
        $identity.GetAttribute("name") -ne "io.github.potterlim.timetable" -or
        $identity.GetAttribute("version") -ne "1.0.0.0") {
        throw "Windows manifest assembly identity가 유효하지 않습니다: $Path"
    }

    $supportedOperatingSystems = @($document.SelectNodes("/asm:assembly/compat:compatibility/compat:application/compat:supportedOS", $namespaces))
    $windowsTenAndElevenIdentifier = "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"
    if ($supportedOperatingSystems.Count -ne 1 -or
        -not $supportedOperatingSystems[0].GetAttribute("Id").Equals($windowsTenAndElevenIdentifier, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Windows manifest는 공식 Windows 10/11 supportedOS ID 하나만 포함해야 합니다: $Path"
    }

    $executionLevel = $document.SelectSingleNode("/asm:assembly/asm3:trustInfo/asm3:security/asm3:requestedPrivileges/asm3:requestedExecutionLevel", $namespaces)
    if ($null -eq $executionLevel -or
        $executionLevel.GetAttribute("level") -ne "asInvoker" -or
        $executionLevel.GetAttribute("uiAccess") -ne "false") {
        throw "Windows manifest의 실행 권한은 asInvoker, uiAccess=false여야 합니다: $Path"
    }

    $dpiAwareness = $document.SelectSingleNode("/asm:assembly/asm3:application/asm3:windowsSettings/windows:dpiAwareness", $namespaces)
    $longPathAwareness = $document.SelectSingleNode("/asm:assembly/asm3:application/asm3:windowsSettings/windows:longPathAware", $namespaces)
    if ($null -eq $dpiAwareness -or $dpiAwareness.InnerText -ne "PerMonitorV2") {
        throw "Windows manifest의 DPI awareness는 PerMonitorV2여야 합니다: $Path"
    }

    if ($null -eq $longPathAwareness -or $longPathAwareness.InnerText -ne "true") {
        throw "Windows manifest의 long-path awareness가 활성화되지 않았습니다: $Path"
    }
}
