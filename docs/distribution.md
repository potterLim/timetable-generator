# 데스크톱 제품 배포

이 문서는 v1에서 실제 검증하고 공개 배포하는 Windows 11 x64와 macOS 14 이상 Apple Silicon 제품 산출물을 만드는 절차와, 현재 저장소에서 자동 검증할 수 있는 범위를 정의합니다. `osx-x64` 개별 게시는 호환성 개발을 위해 유지하지만 Intel Mac 실기기 검증 전에는 공식 Release 자산으로 취급하지 않습니다.

배포 식별자는 첫 공개 버전부터 다음 값을 유지합니다.

| 용도 | 값 |
| --- | --- |
| 사용자 표시 제품명 | `Timetable Generator` |
| Windows 실행 파일 | `TimetableGenerator.exe` |
| macOS 앱 번들 | `Timetable Generator.app` |
| macOS bundle identifier | `io.github.potterlim.timetable` |

`TimetableGenerator.Desktop` 프로젝트명과 네임스페이스는 소스·관리 어셈블리 구조로 유지하되, 사용자에게 보이는 실행 파일명과 제품 메타데이터에는 노출하지 않습니다.

## 재현 가능한 복원과 빌드 증거

`.NET SDK 10.0.301`은 `global.json`에서 roll-forward 없이 고정됩니다. NuGet 패키지 버전은 `Directory.Packages.props`에서 중앙 관리하고, 각 프로젝트의 `packages.lock.json`이 전이 의존성과 content hash를 고정합니다.

```powershell
dotnet restore TimetableGenerator.sln --locked-mode
```

Windows와 macOS 산출물을 실제로 만드는 각 빌드 호스트에서, 깨끗한 Release 커밋을 체크아웃한 직후 실제 빌드 환경을 증거 파일로 남깁니다. `-RequireClean`은 커밋하지 않은 변경이 있으면 실패합니다.

```powershell
pwsh ./scripts/write-release-build-info.ps1 `
  -Version 1.0.0 `
  -RequireClean
```

결과는 `artifacts/release-evidence/1.0.0/<host-rid>/build-info.txt`에 저장됩니다. Git commit·clean 상태·UTC 시각·OS 및 아키텍처·실제 `dotnet --version`·`dotnet --info` 출력을 기록하며, 사용자에게 제공하는 ZIP에는 포함하지 않습니다. 같은 버전과 호스트의 기존 증거는 자동으로 덮어쓰지 않으며 의도적으로 다시 기록할 때만 `-Force`를 사용합니다.

## unsigned 산출물 만들기

.NET 10 SDK와 PowerShell 7이 설치된 Windows 또는 macOS에서 저장소 루트 기준으로 실행합니다. Windows에서는 PowerShell의 `System.Drawing.Common`, macOS에서는 운영체제 기본 `sips`와 `iconutil`을 사용해 동일한 다중 해상도 `AppIcon.icns`를 생성합니다. 별도 이미지 변환 프로그램은 필요하지 않습니다.

```powershell
pwsh ./scripts/publish-desktop.ps1
```

특정 대상만 다시 만들 수도 있습니다.

```powershell
pwsh ./scripts/publish-desktop.ps1 -Runtime win-x64
pwsh ./scripts/publish-desktop.ps1 -Runtime osx-arm64

# 공식 Release에는 포함하지 않는 선택적 Intel 호환성 게시
pwsh ./scripts/publish-desktop.ps1 -Runtime osx-x64
```

앱 버전은 프로젝트의 `Version`을 사용하며 필요할 때 `-Version 1.0.1`처럼 명시할 수 있습니다. macOS bundle identifier 기본값은 GitHub 사용자 네임스페이스를 기준으로 한 `io.github.potterlim.timetable`입니다. 첫 공개 버전부터 모든 버전과 CPU 아키텍처에서 이 값을 유지하며, 이후 Apple Developer portal 등록이 필요한 capability를 추가할 때도 동일한 식별자를 사용합니다.

```powershell
pwsh ./scripts/publish-desktop.ps1 `
  -Runtime osx-arm64 `
  -BundleIdentifier "io.github.potterlim.timetable"
```

생성 위치는 다음과 같습니다.

| 대상 | 실행·검증용 디렉터리 | 전달용 archive |
| --- | --- | --- |
| Windows x64 | `artifacts/publish/win-x64` | `TimetableGenerator-<version>-win-x64-unsigned.zip` |
| macOS Apple Silicon | `artifacts/publish/osx-arm64/Timetable Generator.app` | `TimetableGenerator-<version>-osx-arm64-unsigned.zip` |

현재 명령에서 생성하고 검증한 archive의 SHA-256만 `artifacts/publish/checksums.sha256`에 기록됩니다. 인수 없는 전체 게시는 공식 대상 두 개만 만들고 두 archive의 checksum을 기록합니다. `-Runtime osx-x64`를 명시한 선택적 게시는 해당 Intel archive만 만들며 공식 v1 checksum 집계에는 포함하지 않습니다. macOS zip에는 Mach-O 실행 권한도 보존됩니다.

하나의 게시 출력 디렉터리는 한 번의 게시 명령만 소유합니다. 같은 명령을 다시 실행해 기존 결과를 교체할 수 있지만, 다른 버전·RID 또는 수동으로 만든 파일이 있으면 스크립트는 아무것도 자동 삭제하지 않고 중단합니다. 이 경우 기존 파일을 직접 확인한 뒤 별도의 빈 `-OutputRoot`를 사용하세요. 출력·원본 경로에 symbolic link나 junction도 허용하지 않습니다.

Release 최종화에서도 서명된 원본과 최종 ZIP 출력 위치는 서로 같거나 포함 관계일 수 없습니다. 원본 앱 안에 ZIP을 만들거나 출력 폴더를 다시 원본에 포함하는 잘못된 경로는 파일을 만들기 전에 거부합니다.

`catalog-source.local.json`이 Desktop 프로젝트에 있으면 게시 산출물에도 포함됩니다. 이 파일은 Git에서 무시되지만 앱이 서버에 접속하려면 최종 사용자에게 보이는 설정입니다. `google-calendar.local.json`은 정확히 `schemaVersion`, `clientId`, `clientSecret` 세 속성을 가진 제품 설정 스키마 v2여야 하며, 외부 사용자용 프로덕션 **Desktop OAuth** 클라이언트 ID와 보안 비밀을 넣습니다. 현재 이 Desktop 클라이언트는 승인 코드를 토큰으로 교환할 때 두 값을 모두 요구합니다. 액세스 토큰·새로 고침 토큰과 웹 애플리케이션 OAuth 클라이언트의 보안 비밀은 절대 넣지 않습니다. 실제 사용자는 Google Calendar 내보내기 때 자신의 계정으로 직접 로그인하고 권한을 승인합니다. 두 설정 파일 중 하나라도 없거나 비어 있거나 스키마 검증에 실패하면 최종화가 중단됩니다.

Desktop 앱과 함께 배포되는 클라이언트 보안 비밀은 사용자가 추출할 수 있으므로 서버 비밀 같은 기밀 보안 경계가 아닙니다. 네이티브 앱 흐름의 승인 코드 보호는 요청마다 생성하는 PKCE(S256)가 담당합니다. `google-calendar.local.json` 원본은 자격 증명 교체와 저장소 공개 범위를 분리하기 위해 Git에 추적하지 않고, `scripts/set-google-calendar-local-configuration.ps1`로 v2 설정을 준비한 뒤 게시 전 미추적 sidecar로 주입해 최종 Release에만 포함합니다. 보안 비밀은 명령줄 인수나 빌드 로그에 기록하지 않습니다. 앱은 개발 환경 이전을 위해 v1 설정도 읽지만 Release 최종화는 v2만 허용합니다.

제품에 포함된 Pretendard, Fluent UI System Icons, Avalonia·ANGLE, FluentIcons, SkiaSharp·HarfBuzzSharp, MicroCom, Tmds.DBus.Protocol, self-contained .NET runtime의 원문 라이선스와 third-party notice를 함께 제공합니다. Windows는 `ThirdPartyNotices`, macOS는 `Contents/Resources/ThirdPartyNotices`에 배치하며, 게시·최종화 두 단계가 전체 파일 세트를 검증합니다.

## 자동 검증 범위

게시 스크립트는 다음 조건을 만족하지 않으면 실패합니다.

- Release, self-contained, trim 비활성 게시 성공
- Windows apphost가 x64 PE이고 `coreclr.dll`을 포함함
- macOS apphost가 대상 CPU의 64비트 Mach-O이고 `libcoreclr.dylib`을 포함함
- `.app/Contents/MacOS`, `Resources`, `Info.plist`의 번들 구조가 완전함
- `AppIcon.icns`에 16px부터 1024px까지 필요한 PNG 해상도가 모두 포함됨
- 현재 잠금 파일과 배포 구성에 맞춰 저장소가 필수로 정의한 third-party 라이선스·notice 파일 세트가 운영체제별 고지 위치에 포함됨
- 제품 archive에 PDB 디버그 심볼이 없음
- macOS에서 실행할 때 `Info.plist`가 `plutil` 검사를 통과함

이 검증은 코드 서명, Apple notarization, 악성 코드 검사, 운영체제별 실제 실행을 대신하지 않습니다. 현재 저장소에는 인증서나 Apple notarization 자격 증명을 포함하지 않으므로 게시 스크립트가 만든 macOS archive에는 의도적으로 `unsigned`가 표시됩니다.

## 서명 후 최종 Release 자산 만들기

`publish-desktop.ps1`의 ZIP은 서명 전 후보이므로 GitHub Release에 올리지 않습니다. Windows Authenticode 서명 또는 macOS notarization·stapling이 앱 바이트를 변경하므로, 모든 플랫폼은 해당 절차가 끝난 다음 최종 ZIP을 새로 만듭니다.

Windows에서 서명된 게시 디렉토리를 최종화합니다. 유효한 Authenticode 서명과 timestamp가 없으면 실패합니다.

```powershell
pwsh ./scripts/finalize-desktop-release.ps1 `
  -Stage Windows `
  -Version 1.0.0
```

macOS에서는 각 아키텍처의 앱을 안쪽 Mach-O부터 밖쪽 bundle 순서로 서명하고, notarization 성공 후 ticket을 staple한 다음 실행합니다. 스크립트는 `codesign --strict`, Gatekeeper, stapler ticket, 필수 entitlement를 검증하고 macOS 메타데이터를 보존하는 `ditto`로 ZIP을 만듭니다.

```powershell
pwsh ./scripts/finalize-desktop-release.ps1 `
  -Stage MacOS `
  -Runtime osx-arm64 `
  -Version 1.0.0 `
  -BundleIdentifier "io.github.potterlim.timetable"
```

동일한 `artifacts/release/1.0.0`에 다음 두 ZIP을 모은 후 최종 checksum을 생성합니다.

```text
TimetableGenerator-1.0.0-win-x64.zip
TimetableGenerator-1.0.0-osx-arm64.zip
```

```powershell
pwsh ./scripts/finalize-desktop-release.ps1 `
  -Stage Aggregate `
  -Version 1.0.0
```

`checksums.sha256`은 서명·notarization·stapling·최종 ZIP 생성이 모두 끝난 바이트에서 한 번만 생성합니다. 로컬 구조 검증용 `-AllowUnsigned`는 `artifacts/release-smoke`의 `unsigned-smoke` 이름으로 격리되며 Aggregate가 절대 받지 않습니다.

## Windows 공개 전 검증

1. 조직의 코드 서명 인증서로 `win-x64`의 실행 파일을 서명합니다. 추후 MSI·MSIX 설치 패키지를 만들면 그 패키지도 별도로 서명합니다. ZIP 자체는 SHA-256으로 무결성을 확인합니다.
2. `Get-AuthenticodeSignature` 또는 `signtool verify /pa`로 서명을 검증합니다.
3. 악성 코드 검사 후 실제 Windows 11 x64 기기에서 기존 앱 데이터 폴더를 안전하게 격리하고 첫 실행, 카탈로그 로딩, 자동 저장, PNG 저장을 확인합니다.
4. 최종 archive의 SHA-256을 다시 계산해 배포 페이지에 함께 게시합니다.

## macOS 서명·notarization 경계

서명과 notarization은 Developer ID Application 인증서와 Xcode Command Line Tools가 설치된 macOS 빌드 기기에서 수행합니다. 일반 .NET self-contained 앱의 JIT 권한과 Apple Calendar 자동화에 필요한 Apple Events 권한은 저장소의 `Platforms/macOS/TimetableGenerator.entitlements`에 명시되어 있습니다. 불필요한 디버깅·sandbox 예외는 포함하지 않습니다.

### Apple Calendar 내보내기 경계

현재 데스크톱 프로젝트는 일반 `net10.0` Avalonia 앱이므로 EventKit 바인딩 대신 macOS의 Calendar 스크립팅 인터페이스를 사용합니다. 앱은 `/usr/bin/osascript`의 JavaScript for Automation으로 캘린더 목록을 확인하고, 사용자가 선택한 대상에 현재 시간표를 직접 내보냅니다.

같은 이름의 캘린더가 있으면 앱이 만든 단일 쓰기 가능 캘린더만 대체할 수 있습니다. 그 밖의 캘린더는 변경하지 않고 ` (2)`, ` (3)` 순서의 첫 사용 가능 이름으로 새 캘린더를 만듭니다. 대체 직전에도 캘린더 ID·이름·소유 표식·쓰기 가능 여부를 다시 확인합니다.

서명된 hardened runtime 앱에서 이 동작을 허용하려면 `NSAppleEventsUsageDescription`과 `com.apple.security.automation.apple-events=true`가 모두 필요합니다. 실제 공개 전에 새 사용자 프로필에서 최초 권한 요청, 허용, 거부, 시스템 설정에서 권한 철회, 재시도 동작을 실기기로 검증합니다.

다음 순서를 배포 담당자의 실제 identity와 keychain profile로 실행합니다. `codesign --deep`으로 서명하지 말고 내부 Mach-O부터 바깥 bundle 순서로 서명합니다.

```bash
APP="artifacts/publish/osx-arm64/Timetable Generator.app"
MAIN="$APP/Contents/MacOS/TimetableGenerator"
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

마지막으로 실제 Apple Silicon Mac에서 내려받은 파일에 quarantine이 적용된 상태로 첫 실행, 카탈로그 로딩, 자동 저장, PNG 저장을 검증해야 공개 배포가 완료됩니다. `osx-x64`를 향후 공식 지원하려면 별도의 Intel Mac에서 같은 서명·공증·다운로드 검증을 끝낸 뒤 해당 버전의 Release 범위에 추가합니다.
