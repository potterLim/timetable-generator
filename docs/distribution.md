# 데스크톱 제품 배포

이 문서는 Windows x64와 macOS 14 이상 Intel·Apple Silicon 제품 산출물을 만드는 절차와, 현재 저장소에서 자동 검증할 수 있는 범위를 정의합니다.

## unsigned 산출물 만들기

.NET 10 SDK와 PowerShell 7이 설치된 Windows 또는 macOS에서 저장소 루트 기준으로 실행합니다. Windows에서는 PowerShell의 `System.Drawing.Common`, macOS에서는 운영체제 기본 `sips`와 `iconutil`을 사용해 동일한 다중 해상도 `AppIcon.icns`를 생성합니다. 별도 이미지 변환 프로그램은 필요하지 않습니다.

```powershell
pwsh ./scripts/publish-desktop.ps1
```

특정 대상만 다시 만들 수도 있습니다.

```powershell
pwsh ./scripts/publish-desktop.ps1 -Runtime win-x64
pwsh ./scripts/publish-desktop.ps1 -Runtime osx-x64
pwsh ./scripts/publish-desktop.ps1 -Runtime osx-arm64
```

앱 버전은 프로젝트의 `Version`을 사용하며 필요할 때 `-Version 1.0.1`처럼 명시할 수 있습니다. macOS bundle identifier 기본값인 `com.example.timetablegenerator`는 로컬 unsigned 검증 전용 placeholder입니다. 서명하거나 배포할 산출물은 Apple Developer 계정에 등록한 App ID를 반드시 명시해 새로 만듭니다.

```powershell
pwsh ./scripts/publish-desktop.ps1 `
  -Runtime osx-arm64 `
  -BundleIdentifier "<registered-reverse-dns-app-id>"
```

생성 위치는 다음과 같습니다.

| 대상 | 실행·검증용 디렉터리 | 전달용 archive |
| --- | --- | --- |
| Windows x64 | `artifacts/publish/win-x64` | `TimetableGenerator-<version>-win-x64.zip` |
| macOS Intel | `artifacts/publish/osx-x64/시간표.app` | `TimetableGenerator-<version>-osx-x64-unsigned.zip` |
| macOS Apple Silicon | `artifacts/publish/osx-arm64/시간표.app` | `TimetableGenerator-<version>-osx-arm64-unsigned.zip` |

현재 명령에서 생성하고 검증한 archive의 SHA-256만 `artifacts/publish/checksums.sha256`에 기록됩니다. 따라서 세 대상을 한 파일에서 확인하려면 인수 없이 전체 게시를 실행합니다. macOS zip에는 Mach-O 실행 권한도 보존됩니다.

`catalog-source.local.json`이 Desktop 프로젝트에 있으면 게시 산출물에도 포함됩니다. 이 파일은 Git에서 무시되지만 앱이 서버에 접속하려면 최종 사용자에게 보이는 설정입니다. 비밀 키를 넣지 말고, 공개 전 archive 안의 주소가 의도한 배포 환경을 가리키는지 확인합니다.

제품에 포함된 Pretendard 글꼴은 SIL Open Font License 1.1을 따릅니다. Windows 산출물에는 `ThirdPartyNotices/Pretendard-LICENSE.txt`, macOS 앱에는 `Contents/Resources/ThirdPartyNotices/Pretendard-LICENSE.txt`로 원문 라이선스를 함께 제공하며, 게시 스크립트는 해당 파일이 없거나 비어 있으면 실패합니다.

## 자동 검증 범위

게시 스크립트는 다음 조건을 만족하지 않으면 실패합니다.

- Release, self-contained, trim 비활성 게시 성공
- Windows apphost가 x64 PE이고 `coreclr.dll`을 포함함
- macOS apphost가 대상 CPU의 64비트 Mach-O이고 `libcoreclr.dylib`을 포함함
- `.app/Contents/MacOS`, `Resources`, `Info.plist`의 번들 구조가 완전함
- `AppIcon.icns`에 16px부터 1024px까지 필요한 PNG 해상도가 모두 포함됨
- Pretendard 원문 라이선스가 운영체제별 고지 위치에 포함됨
- 제품 archive에 PDB 디버그 심볼이 없음
- macOS에서 실행할 때 `Info.plist`가 `plutil` 검사를 통과함

이 검증은 코드 서명, Apple notarization, 악성 코드 검사, 운영체제별 실제 실행을 대신하지 않습니다. 현재 저장소에는 인증서나 Apple notarization 자격 증명이 없으므로 스크립트가 만든 macOS archive에는 의도적으로 `unsigned`가 표시됩니다.

## Windows 공개 전 검증

1. 조직의 코드 서명 인증서로 `win-x64`의 실행 파일을 서명합니다. 추후 MSI·MSIX 설치 패키지를 만들면 그 패키지도 별도로 서명합니다. ZIP 자체는 SHA-256으로 무결성을 확인합니다.
2. `Get-AuthenticodeSignature` 또는 `signtool verify /pa`로 서명을 검증합니다.
3. 악성 코드 검사 후 깨끗한 Windows 10·11 x64 기기에서 첫 실행, 카탈로그 로딩, 자동 저장, PNG 저장을 확인합니다.
4. 최종 archive의 SHA-256을 다시 계산해 배포 페이지에 함께 게시합니다.

## macOS 서명·notarization 경계

서명과 notarization은 Developer ID Application 인증서와 Xcode Command Line Tools가 설치된 macOS 빌드 기기에서 수행합니다. 일반 .NET self-contained 앱은 JIT 권한이 필요하므로 저장소의 `Platforms/macOS/TimetableGenerator.entitlements`를 사용합니다. 불필요한 디버깅·Apple Events·sandbox 예외는 포함하지 않았습니다.

다음 순서를 배포 담당자의 실제 identity와 keychain profile로 실행합니다. `codesign --deep`으로 서명하지 말고 내부 Mach-O부터 바깥 bundle 순서로 서명합니다.

```bash
APP="artifacts/publish/osx-arm64/시간표.app"
MAIN="$APP/Contents/MacOS/TimetableGenerator.Desktop"
ENTITLEMENTS="src/TimetableGenerator.Desktop/Platforms/macOS/TimetableGenerator.entitlements"
IDENTITY="Developer ID Application: YOUR NAME (TEAMID)"

find "$APP/Contents/MacOS" -type f -print0 | while IFS= read -r -d '' FILE; do
  if [ "$FILE" != "$MAIN" ] && file "$FILE" | grep -q "Mach-O"; then
    codesign --force --timestamp --options runtime --sign "$IDENTITY" "$FILE"
  fi
done

codesign --force --timestamp --options runtime \
  --entitlements "$ENTITLEMENTS" --sign "$IDENTITY" "$MAIN"
codesign --force --timestamp --options runtime \
  --entitlements "$ENTITLEMENTS" --sign "$IDENTITY" "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"

ditto -c -k --sequesterRsrc --keepParent "$APP" TimetableGenerator-notarization.zip
xcrun notarytool submit TimetableGenerator-notarization.zip \
  --wait --keychain-profile "YOUR_NOTARY_PROFILE"
xcrun stapler staple "$APP"
xcrun stapler validate "$APP"
spctl --assess --type execute --verbose=4 "$APP"
```

Intel archive도 같은 절차를 별도로 수행합니다. 마지막으로 실제 Intel Mac과 Apple Silicon Mac에서 내려받은 파일에 quarantine이 적용된 상태로 첫 실행, 카탈로그 로딩, 자동 저장, PNG 저장을 검증해야 공개 배포가 완료됩니다.
