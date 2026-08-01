# 데스크톱 제품 배포

이 문서는 배포 담당자를 위한 운영 절차입니다.  
Timetable Generator 1.0.2가 지원하는 Windows 11 x64용 배포 파일과 Apple Silicon 기반 macOS 14 이상용 배포 파일의 생성·검증·공개 절차를 정의합니다.

배포 식별자는 첫 공개 버전부터 다음 값을 유지합니다.

| 용도 | 값 |
| --- | --- |
| 사용자 표시 제품명 | `Timetable Generator` |
| Windows 실행 파일 | `TimetableGenerator.exe` |
| macOS 앱 번들 | `Timetable Generator.app` |
| macOS 번들 식별자 | `io.github.potterlim.timetable` |

`TimetableGenerator.Desktop` 프로젝트명과 네임스페이스는 소스와 관리형 어셈블리에서만 사용합니다.  
사용자에게 보이는 실행 파일명과 제품 메타데이터에는 `Timetable Generator`를 사용합니다.

## 재현 가능한 복원과 빌드 증거

`.NET SDK 10.0.301`은 `global.json`에서 롤포워드 없이 고정합니다.  
NuGet 패키지 버전은 `Directory.Packages.props`에서 중앙 관리하고, 각 프로젝트의 `packages.lock.json`이 전이적 종속성과 콘텐츠 해시를 고정합니다.

```powershell
dotnet restore TimetableGenerator.sln --locked-mode
```

Windows와 macOS 배포 파일을 만드는 각 빌드 호스트에서 릴리스 대상 커밋을 체크아웃하고 작업 트리가 깨끗한지 확인한 직후 빌드 환경을 기록합니다.  
`-RequireClean`은 커밋하지 않은 변경이 있으면 실패합니다.

```powershell
pwsh ./scripts/write-release-build-info.ps1 `
  -Version 1.0.2 `
  -RequireClean
```

결과는 `artifacts/release-evidence/1.0.2/<host-rid>/build-info.txt`에 저장됩니다.
Git 커밋과 작업 트리 상태, UTC 시각, 운영체제와 아키텍처, 실제 `dotnet --version`·`dotnet --info` 출력을 기록하며 사용자용 ZIP에는 포함하지 않습니다.  
같은 버전과 호스트의 기록을 다시 만들 때만 `-Force`를 사용합니다.

## 서명 전 산출물 만들기

.NET 10 SDK와 PowerShell 7이 설치된 Windows 또는 macOS에서 저장소 루트를 기준으로 실행합니다.  
Windows에서는 PowerShell에서 `System.Drawing.Common`을 사용하고, macOS에서는 운영체제의 `sips`와 `iconutil`을 사용해 다중 해상도 `AppIcon.icns`를 생성하므로 별도 이미지 변환 프로그램은 필요하지 않습니다.

```powershell
pwsh ./scripts/publish-desktop.ps1
```

특정 대상만 게시할 수도 있습니다.

```powershell
pwsh ./scripts/publish-desktop.ps1 -Runtime win-x64
pwsh ./scripts/publish-desktop.ps1 -Runtime osx-arm64
```

앱 버전은 프로젝트의 `Version`을 사용하며 필요할 때 `-Version 1.0.2`처럼 명시할 수 있습니다.
macOS 번들 식별자는 모든 버전과 CPU 아키텍처에서 `io.github.potterlim.timetable`을 유지합니다.

```powershell
pwsh ./scripts/publish-desktop.ps1 `
  -Runtime osx-arm64 `
  -BundleIdentifier "io.github.potterlim.timetable"
```

생성 위치는 다음과 같습니다.

| 대상 | 실행·검증용 디렉터리 | 게시 전 ZIP |
| --- | --- | --- |
| Windows x64 | `artifacts/publish/win-x64` | `TimetableGenerator-<version>-win-x64-unsigned.zip` |
| macOS Apple Silicon | `artifacts/publish/osx-arm64/Timetable Generator.app` | `TimetableGenerator-<version>-osx-arm64-unsigned.zip` |

현재 명령에서 생성하고 검증한 ZIP의 SHA-256만 `artifacts/publish/checksums.sha256`에 기록합니다.  
인수 없이 실행하면 공식 대상 두 개를 만들고 두 ZIP의 체크섬을 기록합니다.  
macOS ZIP에는 Mach-O 실행 파일의 실행 권한도 보존됩니다.

하나의 게시 출력 디렉터리에는 같은 명령이 관리하는 결과만 둡니다.  
같은 명령으로 기존 결과를 교체할 수 있지만 다른 버전·RID 또는 수동으로 만든 파일이 있으면 아무것도 삭제하지 않고 중단합니다.  
이 경우 기존 파일을 확인한 뒤 별도의 빈 `-OutputRoot`를 사용하세요.  
출력 경로와 원본 경로에는 심볼릭 링크나 정션을 허용하지 않습니다.

릴리스 최종화에서도 원본 앱과 최종 ZIP 출력 위치는 서로 같거나 포함 관계일 수 없습니다.  
원본 앱 안에 ZIP을 만들거나 출력 폴더를 다시 원본에 포함하는 경로는 파일을 만들기 전에 거부합니다.

최종 배포 파일에는 다음 두 설정 파일이 필요합니다.

- `catalog-source.local.json`: `schemaVersion` 값이 `1`이고 `indexUri`가 사용자 정보나 프래그먼트가 없는 절대 HTTPS 주소인 과목 카탈로그 설정
- `google-calendar.local.json`: `schemaVersion`, `clientId`, `clientSecret`만 포함하는 Google 데스크톱 OAuth 설정 스키마 v2

두 파일은 `src/TimetableGenerator.Desktop`에 준비하며, Git에서 추적하지 않고 게시 전에 제품에 포함합니다.  
각 파일에는 위에서 설명한 속성만 허용됩니다.  
OAuth 설정에는 외부 사용자용 프로덕션 데스크톱 클라이언트의 ID와 보안 비밀만 넣으며, 액세스 토큰·갱신 토큰과 웹 애플리케이션 OAuth 클라이언트의 보안 비밀은 넣지 않습니다.  
실제 사용자는 내보낼 때 자신의 Google 계정으로 로그인하고 권한을 승인합니다.  
두 설정 파일 중 하나라도 없거나 비어 있거나 스키마 검증에 실패하면 최종화가 중단됩니다.  
Google OAuth 설정은 [Google Calendar 연동 설정](google-calendar-integration-setup.md)에 따라 준비합니다.

제품에는 Pretendard, Fluent UI System Icons, Avalonia·ANGLE, FluentIcons, SkiaSharp·HarfBuzzSharp, MicroCom, Tmds.DBus.Protocol과 자체 포함 .NET 런타임의 원문 라이선스와 제3자 고지를 함께 제공합니다.  
Windows에서는 `ThirdPartyNotices`, macOS에서는 `Contents/Resources/ThirdPartyNotices`에 배치하며 게시와 최종화 단계에서 전체 파일을 검증합니다.

## 자동 검증 범위

게시 스크립트는 다음 조건을 만족하지 않으면 실패합니다.

- `Release` 구성의 자체 포함·트리밍 비활성 상태로 게시에 성공함
- Windows apphost가 x64 PE이고 `coreclr.dll`을 포함함
- macOS apphost가 대상 CPU의 64비트 Mach-O이고 `libcoreclr.dylib`을 포함함
- `.app/Contents/MacOS`, `Resources`, `Info.plist`의 번들 구조가 완전함
- `AppIcon.icns`에 16px부터 1024px까지 ICNS가 지원하는 PNG 또는 ARGB 아이콘 표현이 포함됨
- 잠금 파일과 배포 구성에 정의된 제3자 라이선스·고지 파일이 운영체제별 위치에 포함됨
- 제품 ZIP에 PDB 디버그 심볼이 없음
- 제품 게시 디렉터리와 ZIP에 관리형 어셈블리용 XML 개발 문서가 없음
- macOS에서 실행할 때 `Info.plist`가 `plutil` 검사를 통과함

이 검증은 코드 서명, Apple 공증, 악성 코드 검사와 운영체제별 실제 실행을 대신하지 않습니다.  
게시 스크립트가 만든 macOS ZIP은 서명 전 검증용이므로 파일명에 `unsigned`를 표시합니다.

## 최종 릴리스 파일 만들기

`publish-desktop.ps1`이 만드는 ZIP은 게시 단계 검증용이므로 GitHub 릴리스에 올리지 않습니다.  
플랫폼별 최종 정책을 적용한 원본 디렉터리에서 최종 ZIP을 새로 만듭니다.

Windows 공식 배포 정책은 Authenticode 서명을 적용하지 않는 것입니다.  
최종화 스크립트는 실행 파일이 손상되거나 불완전하게 서명된 상태가 아니라 정확히 `NotSigned`인지 확인합니다.

```powershell
pwsh ./scripts/finalize-desktop-release.ps1 `
  -Stage Windows `
  -Version 1.0.2 `
  -WindowsSignatureMode Unsigned
```

macOS에서는 앱 내부의 개별 파일부터 바깥쪽 번들 순서로 서명하고, Apple 공증이 끝나면 공증 티켓을 스테이플합니다.  
아래 **macOS 서명·공증 경계** 절차를 먼저 완료한 앱을 대상으로 최종화 명령을 실행합니다.  
스크립트는 `codesign --strict`, Gatekeeper와 공증 티켓을 검증하고, 서명 결과에 필요한 엔타이틀먼트 키가 포함되어 있는지 확인합니다.  
최종 ZIP은 macOS 메타데이터를 보존하는 `ditto`로 만듭니다.

```powershell
pwsh ./scripts/finalize-desktop-release.ps1 `
  -Stage MacOS `
  -Runtime osx-arm64 `
  -Version 1.0.2 `
  -BundleIdentifier "io.github.potterlim.timetable"
```

동일한 `artifacts/release/1.0.2`에 다음 두 ZIP을 모은 후 최종 체크섬을 생성합니다.

```text
TimetableGenerator-1.0.2-win-x64.zip
TimetableGenerator-1.0.2-osx-arm64.zip
```

```powershell
pwsh ./scripts/finalize-desktop-release.ps1 `
  -Stage Aggregate `
  -Version 1.0.2 `
  -WindowsSignatureMode Unsigned
```

`checksums.sha256`은 모든 플랫폼의 최종 ZIP 생성이 끝난 뒤 한 번만 만듭니다.  
로컬 구조 검증용 `-AllowUnsigned`는 `artifacts/release-smoke`와 `unsigned-smoke` 이름으로 분리하며 `Aggregate` 단계에서는 허용하지 않습니다.

## Windows 공개 전 검증

1. `Get-AuthenticodeSignature` 결과가 `NotSigned`인지 확인합니다. `HashMismatch`나 불완전한 서명은 공식 미서명 상태가 아닙니다.
2. 악성 코드 검사 후 실제 Windows 11 x64 기기에서 기존 앱 데이터 폴더를 안전하게 격리하고 첫 실행, 카탈로그 로딩, 자동 저장, PNG 저장과 Google Calendar 로그인·내보내기를 확인합니다.
3. 최종 ZIP의 SHA-256을 다시 계산해 배포 페이지에 함께 게시합니다.
4. GitHub 릴리스에서 ZIP을 새로 내려받아 체크섬과 압축 무결성을 확인한 뒤 Windows에서 실행합니다.

## macOS 서명·공증 경계

서명과 공증은 Developer ID Application 인증서와 이에 대응하는 개인 키, Xcode Command Line Tools가 설치된 macOS 빌드 기기에서 수행합니다. 공증 권한이 있는 Apple ID와 Team ID, 해당 Apple ID의 앱 암호도 필요합니다.  
서명 ID는 `security find-identity -p codesigning -v`에서 유효한 항목으로 확인되어야 합니다.  
자체 포함 .NET 앱의 JIT 권한과 Hardened Runtime에서 EventKit 읽기·쓰기에 필요한 Calendar 권한은 `src/TimetableGenerator.Desktop/Platforms/macOS/TimetableGenerator.entitlements`에 명시합니다. EventKit을 사용하는 네이티브 모듈은 앱 번들 안에 포함하고 외부 앱 자동화 권한 없이 동작하도록 구성하며, 불필요한 디버깅·샌드박스 예외는 포함하지 않습니다.

### Apple Calendar 내보내기 경계

데스크톱 앱은 앱 번들에 함께 서명된 네이티브 macOS 모듈과 EventKit을 통해 Apple Calendar에 접근합니다.  
캘린더 목록과 기존 앱 관리 일정을 확인하고, 현재 시간표 이름의 별도 캘린더를 새로 만들거나 앱이 관리하던 같은 이름의 캘린더를 사용자의 선택에 따라 대체합니다.

같은 이름으로 확인된 캘린더가 정확히 하나이고, 그 캘린더가 앱이 만든 쓰기 가능한 캘린더일 때만 대체할 수 있습니다.  
그 밖의 캘린더는 변경하지 않고 ` (2)`, ` (3)` 순서로 사용할 수 있는 첫 이름을 골라 새 캘린더를 만듭니다.  
대체 직전에도 캘린더 ID·이름·로컬 관리 정보·쓰기 가능 여부를 다시 확인합니다. 확인 결과가 모호하면 기존 캘린더를 변경하지 않습니다.

주간 수업은 학기 종료일까지 반복되는 일정으로 저장합니다. 앱이 만든 캘린더와 일정의 소유권 정보는 로컬 앱 데이터로 관리하며 일정의 제목·메모·URL처럼 사용자에게 보이는 필드에 내부 관리 문자열을 남기지 않습니다.  
앱 관리 캘린더를 대체할 때는 앱이 만든 일정만 변경하며 사용자가 직접 추가한 일정은 유지합니다.

지원하는 macOS 버전에서는 `NSCalendarsFullAccessUsageDescription`을 선언하고 EventKit의 전체 캘린더 접근 권한을 요청합니다. 앱 내부 파일을 먼저 서명한 뒤 바깥쪽 앱 번들을 서명하고, 최종 산출물에서 중첩 코드와 Hardened Runtime 유효성을 함께 검증합니다.  
공개 전에 새 사용자 프로필에서 최초 권한 요청, 허용, 거부, 시스템 설정에서 권한 철회와 재시도를 실기기로 검증합니다.

공증 제출 전에 다음 명령으로 `notarytool` 자격 증명을 키체인 프로필에 저장합니다.  
비밀번호 옵션을 생략하면 Apple ID의 앱 암호를 안전한 방식으로 입력하도록 요청하며, 저장하기 전에 자격 증명을 검증합니다.

```bash
xcrun notarytool store-credentials "YOUR_NOTARY_PROFILE" \
  --apple-id "YOUR_APPLE_ID" \
  --team-id "YOUR_TEAM_ID"
```

다음 순서를 배포 담당자의 실제 서명 ID와 `notarytool` 키체인 프로필로 실행합니다.  
`codesign --deep`으로 서명하지 말고 `Contents/MacOS`의 개별 파일부터 바깥쪽 앱 번들 순서로 서명합니다.  
자체 포함 .NET 앱의 관리형 어셈블리와 구성 파일도 일반 코드 서명으로 봉인해야 번들의 심층 검증과 공증이 일관되게 통과합니다.

```bash
APP="artifacts/publish/osx-arm64/Timetable Generator.app"
MAIN="$APP/Contents/MacOS/TimetableGenerator"
ENTITLEMENTS="src/TimetableGenerator.Desktop/Platforms/macOS/TimetableGenerator.entitlements"
IDENTITY="Developer ID Application: YOUR NAME (TEAMID)"

find "$APP/Contents/MacOS" -type f -print0 | while IFS= read -r -d '' FILE; do
  if [ "$FILE" != "$MAIN" ]; then
    codesign --force --timestamp --options runtime --sign "$IDENTITY" "$FILE" || exit 1
  fi
done

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

마지막으로 실제 Apple Silicon Mac에서 새로 내려받은 파일에 격리 속성이 적용된 상태로 첫 실행, 카탈로그 로딩, 자동 저장, PNG 저장, Google Calendar 로그인과 Apple Calendar 권한 요청·내보내기를 확인합니다.

## GitHub 릴리스 최종 확인

다음 조건을 모두 만족한 커밋만 공개 버전으로 확정합니다.

1. `main` 작업 트리가 깨끗하고 원격 브랜치와 일치하며 GitHub Actions의 품질 검사가 모두 성공해야 합니다.
2. Windows와 macOS 실기기에서 첫 실행, 카탈로그 로딩, 자동 저장, 시간표 구성과 PNG 저장을 확인해야 합니다. Windows에서는 Google Calendar를, macOS에서는 Google Calendar와 Apple Calendar 내보내기를 각각 확인합니다.
3. 두 빌드 호스트에서 같은 커밋을 체크아웃하고 `write-release-build-info.ps1 -Version <version> -RequireClean`으로 빌드 환경을 기록해야 합니다.
4. 게시 전 `catalog-source.local.json`과 제품 설정 스키마 v2의 `google-calendar.local.json`이 준비되어 있어야 합니다. 실제 값은 출력하거나 Git에 추가하지 않습니다.
5. Windows 공식 미서명 정책과 macOS 서명·공증·스테이플을 적용한 뒤 플랫폼별 최종화와 `Aggregate` 단계를 모두 통과해야 합니다.
6. 최종 커밋에 `v<version>` 태그를 만들고 태그가 가리키는 커밋을 변경하지 않습니다.
7. 저장소 루트의 `RELEASE-NOTES.md`를 GitHub 릴리스 본문으로 사용하고 공식 Windows 미서명 ZIP, Apple Silicon macOS ZIP과 `checksums.sha256`만 첨부합니다. 게시 단계의 `unsigned` ZIP, `unsigned-smoke` ZIP, PDB, QA 로그, 빌드 증거와 로컬 설정 원본은 첨부하지 않습니다.
8. GitHub 릴리스에서 두 ZIP을 새로 내려받아 체크섬을 다시 확인하고 Windows 실행과 macOS Gatekeeper 첫 실행을 마지막으로 점검합니다.

최종 자산은 다음 세 파일입니다.

```text
TimetableGenerator-<version>-win-x64.zip
TimetableGenerator-<version>-osx-arm64.zip
checksums.sha256
```

태그와 GitHub 릴리스를 만들기 전까지는 모든 산출물을 릴리스 후보로 취급합니다.
