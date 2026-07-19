# macOS 출시 전 전수 검증 프롬프트

이 문서는 시간표 생성기를 macOS 실제 기기로 옮긴 뒤 Codex에 전달할 최종 QA 요청 전문입니다. 저장소 루트를 Codex 작업 공간으로 연 다음, 아래 프롬프트 전체를 새 작업에 그대로 붙여 넣습니다.

검증 결과와 스크린샷은 `artifacts/qa/` 아래에 보관하며 Git에는 커밋하지 않습니다. 실제 서비스 주소, OAuth 정보, 사용자 계획, 서명 자격 증명은 프롬프트나 결과 보고서에 기록하지 않습니다.

````text
이 저장소를 macOS 실제 기기에서 "출시 직전 제품 QA" 수준으로 전수 검증해줘. 단순히 빌드와 단위 테스트만 실행하지 말고, 실제 `.app`을 실행해 모든 주요 화면·입력·상태·오류·복구·내보내기·macOS 고유 동작을 직접 조작하고 눈으로 확인해야 한다.

이 프로젝트는 Swift/Xcode 앱이 아니라 .NET 10 + Avalonia 12.1 기반의 크로스 플랫폼 데스크톱 앱이다. Swift 프로젝트라고 가정하거나 Xcode 프로젝트를 새로 만들지 마라. Xcode 도구는 codesign, notarytool, Instruments, Accessibility Inspector 등 macOS 플랫폼 검증에 필요한 경우에만 사용한다.

사용 가능한 경우 다음 지침을 활용하라.

- build-macos-apps:build-run-debug
- build-macos-apps:test-triage
- build-macos-apps:packaging-notarization
- build-macos-apps:signing-entitlements
- build-macos-apps:window-management
- product-design:audit
- computer-use:computer-use

검증 원칙

1. 확인하지 않은 항목을 PASS로 처리하지 마라.
2. 모든 검증 항목은 반드시 `PASS`, `FAIL`, `BLOCKED`, `NOT APPLICABLE` 중 하나로 판정하라.
3. `BLOCKED`에는 정확히 무엇이 부족한지, 어떻게 해야 검증할 수 있는지 적어라.
4. 자동 테스트 결과만으로 실제 macOS UI, VoiceOver, Retina, Keychain, Gatekeeper, Apple Calendar를 검증했다고 판단하지 마라.
5. 화면 캡처만 보고 렌더링 문제라고 단정하지 마라. 실제 화면, 다른 배율에서의 재현, 반복 캡처, PNG 내보내기 결과를 비교해 실제 앱 문제인지 캡처 과정의 문제인지 구분하라.
6. 한두 개의 문제를 발견했다고 중단하지 말고, 아래 체크리스트 전체에 판정이 생길 때까지 계속 진행하라.
7. 이번 작업은 검증과 원인 분석이 목적이다. 추적 중인 소스·문서·설정 파일을 수정하거나 커밋하지 마라. 실제 결함은 최소 수정 방향과 관련 파일까지 제안하되, 수정은 별도 요청을 받은 뒤 진행한다.
8. `git reset`, `git checkout --`, `git clean` 등 기존 작업을 손상시킬 수 있는 명령을 사용하지 마라.
9. 실제 서비스 주소, OAuth Client ID, 토큰, Keychain 값, 서명 인증서 정보, 사용자 계획 내용 등 민감한 값은 출력·스크린샷·보고서에서 마스킹하라.
10. 실제 닷홈 게시 파일을 변경하거나 Google/Apple의 실제 사용자 캘린더를 임의로 변경하지 마라. 외부 계정 검증은 전용 QA 계정·QA 캘린더와 사용자의 명시적 확인이 있을 때만 수행하라.
11. 카탈로그 손상, 저장 권한 실패, 데이터 복구 시험은 실제 사용자 데이터를 직접 훼손하지 말고 반드시 백업과 격리된 복사본 또는 테스트용 데이터 루트에서 수행하라.
12. 마지막에는 "대체로 정상" 같은 표현 대신, 출시 가능 여부와 남은 위험을 증거에 따라 명확히 판정하라.

현재 기준 정보

- 현재 전송본이 동일하다면 기준 커밋은 `de8a93a`다. 실제 커밋을 기록하되 다르다고 임의로 되돌리지 마라.
- 브랜치는 `main`을 유지한다.
- 기술 스택: .NET 10 / net10.0 / Avalonia 12.1
- 제품 버전: 1.0.0
- 최소 지원 macOS: 14.0
- Apple Silicon RID: `osx-arm64`
- Intel RID: `osx-x64`
- 앱 이름: `시간표`
- 앱 번들:
  - `artifacts/publish/osx-arm64/시간표.app`
  - `artifacts/publish/osx-x64/시간표.app`
- 실행 파일:
  - `시간표.app/Contents/MacOS/TimetableGenerator.Desktop`
- 현재 배포 형식은 `.app`이 들어 있는 ZIP이며 DMG, PKG, App Store 패키지는 없다.
- 게시 결과는 기본적으로 unsigned 상태다.
- 현재 기본 bundle identifier인 `com.example.timetablegenerator`는 로컬 검증용 placeholder이며 실제 공개 배포용으로 인정할 수 없다.
- 로컬 설정 파일은 Git에서 제외되어 있다.
  - `src/TimetableGenerator.Desktop/catalog-source.local.json`
  - `src/TimetableGenerator.Desktop/google-calendar.local.json`
- 사용자 계획과 카탈로그 캐시는 .NET의 `Environment.SpecialFolder.LocalApplicationData` 아래 `TimetableGenerator`에 저장된다. macOS의 실제 해석 경로를 코드와 실행 결과로 확인하고, 경로를 추측만 하지 마라.
- 현재 Windows 기준 자동 테스트 기준선은 614개, 실패 0, 건너뜀 0이었다. macOS에서 테스트 수가 달라진다면 이유를 분석하라.

1단계: 작업 환경과 안전 상태 기록

먼저 다음을 확인하고 보고서에 기록하라.

```bash
git rev-parse --show-toplevel
git status --short
git branch --show-current
git rev-parse HEAD
git log -15 --oneline

sw_vers
uname -m
system_profiler SPHardwareDataType SPSoftwareDataType SPDisplaysDataType
xcode-select -p
dotnet --info
dotnet --list-sdks
pwsh --version
```

다음도 기록하라.

- Mac 모델
- Apple Silicon 또는 Intel
- macOS 정확한 버전
- 주 디스플레이와 외부 디스플레이 정보
- 각 디스플레이의 해상도와 배율
- 현재 시스템 언어·지역·시간대
- Rosetta 사용 가능 여부
- Developer ID 인증서 및 notarization 자격 증명 유무. 구체적인 인증서 이름과 계정 정보는 마스킹한다.

`artifacts/qa/macos-validation-YYYYMMDD-HHMM/` 디렉터리에 로그, 스크린샷, 내보낸 테스트 파일과 최종 보고서를 모아라. 이 디렉터리와 결과물을 Git에 stage하거나 commit하지 마라.

실제 사용자 데이터가 존재한다면 먼저 읽기 전용으로 위치와 상태를 확인하고 안전하게 백업하라. 손상·권한 시험은 백업한 원본이 아니라 격리된 복사본에서만 수행한다.

2단계: 저장소와 코딩 품질 정적 검증

다음을 검사하라.

- `.editorconfig`
- `Directory.Build.props`
- `global.json`
- `README.md`
- `design-qa.md`
- `instruction.md`
- `docs/`
- `scripts/publish-desktop.ps1`
- `src/TimetableGenerator.Desktop/Platforms/macOS/`
- 프로젝트 및 테스트 프로젝트 전체

확인할 내용:

- nullable, analyzer, warnings-as-errors, format 규칙이 실제 빌드에 적용되는지
- 기본 자료형 남용을 피하고 도메인 강타입을 사용하고 있는지
- 파일 분리가 논리적 책임 단위인지
- 플랫폼 분기가 문자열·임의 bool 남용 없이 명확한지
- Windows 전용 코드가 macOS에서 호출되지 않는지
- macOS 전용 API 실패가 전체 앱 크래시로 이어지지 않는지
- 문서의 UI 용어와 실제 UI가 일치하는지
- 과거 표현인 `PNG로 저장`, 불필요한 `추천 시간표` 제목, 오래된 화면 설명 등이 남아 있지 않은지
- 문서가 현재의 `내보내기`, 계획명, 개인 일정, Apple/Google Calendar 동작을 정확히 설명하는지
- 추적 파일에 실제 서비스 주소, OAuth 정보, 토큰, 인증서, 사용자 데이터가 들어 있지 않은지

코딩표준 원문이 저장소에 없다면 `.editorconfig`, analyzer 설정, 기존 코드 관례를 기준으로 점검하고 "원문 부재"를 보고서에 명시하라. 없는 표준을 임의로 만들어 적용하지 마라.

3단계: 깨끗한 복원·포맷·빌드·테스트

Windows에서 생성된 `bin`, `obj`, `artifacts`를 결과 근거로 사용하지 말고 macOS에서 새로 복원·빌드하라. 기존 파일을 파괴적으로 지우지는 말고 필요하면 별도 임시 worktree 또는 격리 출력 경로를 사용하라.

다음을 순서대로 실행한다.

```bash
dotnet restore TimetableGenerator.sln
dotnet format TimetableGenerator.sln --verify-no-changes --no-restore
dotnet build TimetableGenerator.sln --configuration Release --no-restore
dotnet test TimetableGenerator.sln --configuration Release --no-restore
```

확인할 내용:

- restore 오류 없음
- format 차이 없음
- Release 빌드 경고·오류 없음
- 테스트 실패 0
- 의도하지 않은 skip 0
- 테스트 프로세스 충돌·hang 없음
- 실행마다 테스트 결과가 달라지는 flaky test 없음
- 현재 기준선 614개와 차이가 있으면 정확한 이유 설명
- macOS에서만 발생하는 경로 구분자, 대소문자, Unicode 파일명, 로캘, 시간대 오류 없음

추천 알고리즘 관련 테스트는 특히 다음을 확인하라.

- oracle 기반 비교
- 무작위·경계 입력
- 선호/가능/제외 우선순위
- 대안 과목 그룹
- 개인 일정 충돌
- 시작 시간이 이전 일정의 종료와 정확히 같은 경우
- 시간 미정 분반
- 최대 24개 제한
- 빠른 연속 변경 시 이전 계산 취소
- 결과 결정성
- 현재 보고 있던 offering ID 집합 복원
- 515개 과목 카탈로그에서 계산 성능과 UI 응답성

4단계: macOS 게시 산출물 생성

현재 Mac의 네이티브 RID를 결정해 먼저 게시한다.

```bash
case "$(uname -m)" in
  arm64)  RID="osx-arm64" ;;
  x86_64) RID="osx-x64" ;;
  *) echo "지원하지 않는 Mac 아키텍처" >&2; exit 1 ;;
esac

pwsh ./scripts/publish-desktop.ps1 -Runtime "$RID"
```

전체 플랫폼 산출물과 완전한 checksum 목록도 검증할 수 있으면 다음을 별도로 실행하라.

```bash
pwsh ./scripts/publish-desktop.ps1
```

주의:

- `-Runtime`을 각각 따로 실행하면 `checksums.sha256`은 마지막 실행 대상만 포함할 수 있다.
- 전체 대상 checksum이 필요하면 인수 없는 전체 게시를 사용하라.
- Mac에서 만든 AppIcon.icns와 Windows 교차 게시에서 만든 ICNS의 생성 경로가 다르므로 서로의 ZIP SHA-256이 같다고 요구하지 마라.
- 같은 게시 실행에서 생성된 파일과 `checksums.sha256`의 일치만 요구한다.

앱 번들에서 다음을 검증하라.

```bash
APP="$PWD/artifacts/publish/$RID/시간표.app"
MAIN="$APP/Contents/MacOS/TimetableGenerator.Desktop"
INFO="$APP/Contents/Info.plist"

test -d "$APP/Contents/MacOS"
test -d "$APP/Contents/Resources"
test -x "$MAIN"
test -s "$APP/Contents/MacOS/libcoreclr.dylib"
test -s "$INFO"
test -s "$APP/Contents/Resources/AppIcon.icns"
test -s "$APP/Contents/Resources/ThirdPartyNotices/Pretendard-LICENSE.txt"
test -s "$APP/Contents/Resources/ThirdPartyNotices/FluentUiSystemIcons-LICENSE.txt"

plutil -lint "$INFO"
plutil -p "$INFO"
file "$MAIN"
lipo -info "$MAIN"
otool -L "$MAIN"
```

다음을 확인하라.

- `CFBundleDisplayName = 시간표`
- `CFBundleExecutable = TimetableGenerator.Desktop`
- `CFBundleIconFile = AppIcon.icns`
- `CFBundlePackageType = APPL`
- `CFBundleSupportedPlatforms = MacOSX`
- `LSMinimumSystemVersion = 14.0`
- Education category
- `NSPrincipalClass = NSApplication`
- Retina 지원
- 자동 그래픽 전환 지원
- self-contained runtime 포함
- trim되지 않음
- PDB 미포함
- Pretendard와 Fluent UI System Icons 라이선스 포함
- 모든 Mach-O가 요청 RID와 동일한 아키텍처
- 실행 권한 유지
- 잘못된 Windows DLL 호출이나 누락된 native library 없음
- 로컬 설정 파일이 존재할 때만 번들에 복사되고 내용이 손상되지 않았는지
- OAuth token, client secret, 사용자 계획은 번들에 포함되지 않았는지

ZIP checksum과 압축 해제 후 실행 권한도 검증한다.

```bash
(
  cd artifacts/publish
  shasum -a 256 -c checksums.sha256
)

ARCHIVE="$PWD/artifacts/publish/TimetableGenerator-1.0.0-$RID-unsigned.zip"
QA_UNPACK="$(mktemp -d)"
ditto -x -k "$ARCHIVE" "$QA_UNPACK"

test -d "$QA_UNPACK/시간표.app"
test -x "$QA_UNPACK/시간표.app/Contents/MacOS/TimetableGenerator.Desktop"
plutil -lint "$QA_UNPACK/시간표.app/Contents/Info.plist"
file "$QA_UNPACK/시간표.app/Contents/MacOS/TimetableGenerator.Desktop"
```

게시 디렉터리의 앱과 ZIP에서 새로 풀어낸 앱을 각각 검증하라.

5단계: 실제 앱 실행과 macOS 앱 수명주기

제품 동작 검증은 raw executable이 아니라 `.app` 번들로 실행한다.

```bash
/usr/bin/open -n "$QA_UNPACK/시간표.app"
```

raw executable 직접 실행은 bundle launch 실패 원인을 진단할 때만 사용한다.

실행 중 다음 로그를 수집하라.

```bash
/usr/bin/log show \
  --last 10m \
  --style compact \
  --predicate 'process == "TimetableGenerator.Desktop"'
```

확인할 내용:

- 앱이 전면에 정상 표시되는지
- Dock, 앱 전환기, Finder, Launchpad에서 앱 이름과 아이콘이 정상인지
- 앱 아이콘이 흐리거나 구식 이미지로 보이지 않는지
- macOS 메뉴바에서 앱 이름과 기본 메뉴가 자연스러운지
- 창을 모두 닫은 뒤 앱 프로세스의 동작이 macOS 관례에 맞는지
- Dock 아이콘을 다시 눌렀을 때 창이 복구되는지
- `⌘W`, `⌘Q`, `⌘M` 동작
- 앱 전환, 숨기기, 다시 표시
- 전체 화면 진입·이탈
- Mission Control과 Spaces 이동
- Stage Manager에서의 창 동작
- 잠자기·깨우기 뒤 상태
- 네트워크 전환 뒤 상태
- 반복 실행 또는 다중 실행 시 데이터 손상 여부
- 종료 중 autosave 완료·실패·timeout 동작
- 크래시 리포트, native load 오류, Avalonia/Skia/Metal 오류, unhandled exception, 로그 폭주 여부

macOS 메뉴 막대에서 다음 표준 명령도 실제로 확인하라. 저장소에 명시적인 NativeMenu 구성이 없더라도 누락 여부를 제품 마감 관점에서 보고해야 한다.

- App: About, Hide, Hide Others, Show All, Quit
- Edit: Undo, Redo, Cut, Copy, Paste, Select All
- Window: Minimize, Zoom, Bring All to Front
- 표준 단축키가 텍스트 입력과 창 수명주기에 맞게 동작하는지

6단계: macOS 네이티브 창 동작

macOS에서는 Windows식 최소화·최대화·닫기 커스텀 버튼이 나타나면 안 된다. 실제 빨강·노랑·초록 traffic lights가 정상적으로 보여야 한다.

다음을 실제로 조작하라.

- 빨강 버튼으로 창 닫기
- 노랑 버튼으로 최소화 및 Dock에서 복원
- 초록 버튼으로 확대·전체 화면·타일링
- 제목 표시줄 드래그
- 제목 표시줄 더블클릭
- 창 가장자리 resize
- 최소 크기 900×640 부근
- 기본 크기 약 1440×900
- 1080, 1280, 1440, 1600 DIP 전후
- 각 반응형 경계의 바로 아래·정확한 경계·바로 위
- 최대화 및 전체 화면
- 외부 모니터로 이동
- 다른 배율의 디스플레이로 이동
- 화면 좌표가 음수인 좌측·상단 외부 모니터
- 화면의 주·보조 지정 변경
- 메뉴바와 Dock 위치가 바뀐 상태
- 디스플레이 연결·해제 후 창 위치 복구

traffic lights가 제품 제목·로고와 겹치지 않는지, 좌측 예약 공간이 충분한지, 창 드래그 영역이 버튼·탭과 충돌하지 않는지도 확인하라. macOS에서는 `WindowDecorations.Full`과 client-area 확장형 제목 표시줄이 네이티브 traffic lights를 유지해야 하며 Windows 전용 커스텀 caption button이나 resize grip이 보이면 결함이다.

Apple Silicon에서는 arm64 앱을 네이티브로 실행한다. x64 앱을 Rosetta에서 실행할 수 있더라도 실제 Intel Mac 검증을 대체했다고 판단하지 마라. Rosetta를 임의로 설치하지 말고 먼저 다음으로 사용 가능 여부만 확인한다.

```bash
arch -x86_64 /usr/bin/true
```

실제 Intel Mac과 Apple Silicon Mac 양쪽을 사용할 수 없다면 누락된 실제 하드웨어 검증을 `BLOCKED`로 남겨라.

7단계: 첫 실행·카탈로그·오프라인·복구

실제 사용자 데이터를 보존한 상태에서 격리 환경으로 다음을 검증하라.

- 첫 실행 `시간표 준비 중` 화면
- 원격 `index.json` 다운로드
- revision, 크기, SHA-256, JSON schema 검증
- catalog-rNNNN.json 다운로드와 원자적 캐시 설치
- 학교명과 실제 학기가 상단 헤더에 반영되는지
- 실제 학기로부터 기본 계획명이 `2026-2학기 시간표`처럼 생성되는지
- 정상 첫 실행 후 재실행
- 네트워크를 끊은 상태에서 캐시로 재실행
- 설정 파일 누락
- 잘못된 URL
- DNS 실패
- TLS 실패
- timeout
- 서버 404/500
- 빈 응답
- malformed index.json
- malformed catalog JSON
- 파일 크기 불일치
- SHA-256 불일치
- 같은 revision에 다른 내용
- 이전 revision으로 downgrade
- 미래 schema
- 새 revision 정상 적용
- 기존 선택 offering/course ID가 삭제된 비호환 revision
- 기존 계획과 호환되는 revision
- 최신 캐시 손상
- workspace 최신 generation 손상
- 이전 안전 generation 복구
- 복구 사실을 사용자에게 알리는 UI
- 다시 시도 버튼
- 로컬 저장 권한 실패
- 디스크 쓰기 실패를 안전한 테스트 방식으로 주입
- 실패한 업데이트가 정상 기존 캐시를 교체하지 않는지

실제 닷홈 파일은 변경하지 마라. 실패 주입은 로컬 HTTP 서버, 테스트 fixture, 환경변수 또는 격리된 프록시를 사용하라. 실제 서비스 URL은 보고서에 마스킹한다.

닷홈에서 CSV나 XLS를 직접 읽는 구조가 아니라 `index.json`과 정규화된 `catalog-rNNNN.json`을 읽는 구조가 유지되는지도 확인한다.

8단계: 계획 전체 기능

다음을 실제 UI에서 검증한다.

- 기본 계획 생성
- 새 계획 추가
- 두 번째 계획 이름이 `2026-2학기 시간표(2)`처럼 생성되는지
- 계획 탭 전환
- 계획마다 과목·개인 일정·추천 상태가 독립적인지
- 계획 탭의 control-click과 트랙패드 두 손가락 클릭
- 탭 우클릭 이름 변경
- 인스펙터 `…` 메뉴의 이름 변경
- 이름 변경 시 기존 텍스트 전체 선택이 아니라 마지막에 caret이 놓이는지
- Enter 저장
- Esc 취소
- 공백 이름 거부
- 81자 이상 거부
- 대소문자를 무시한 중복 이름 거부
- 시간표 비우기와 확인
- 비운 뒤 계획 식별자와 이름은 유지되고 내용만 제거되는지
- 계획 삭제와 확인
- 마지막 계획 삭제 후 빈 화면이 표시되고 재실행 뒤에도 그대로 복원되는지
- 탭이 많을 때 탭만 수평 스크롤되고 `+` 버튼은 고정되는지
- 재실행 시 활성 계획 복원
- 현재 보고 있던 추천 번호가 아니라 정확한 offering ID 집합 복원
- 빠른 계획 전환·수정 후 최신 상태만 저장되는지
- 최대 5세대 저장과 복구

9단계: 과목 검색·필터·선택

다음을 검증한다.

- 과목 코드 정확 검색
- 과목 코드 접두 검색
- 과목명 정확·접두·부분 검색
- 교수·담당자 검색
- 한글·영문·숫자·공백·대소문자
- 한글 두벌식 IME 조합 중 검색
- 한글과 영문 입력기 전환
- 이모지, 결합문자, 긴 영문
- `⌘A`, `⌘C`, `⌘X`, `⌘V`, `⌘Z`, `⇧⌘Z`
- macOS 텍스트 context menu
- 개설 단위 필터
- 이수구분 필터
- 두 필터의 조합
- 검색 결과 0건
- 검색·필터 초기화
- 결과 정렬 안정성
- 긴 개설 단위명이 잘리지 않거나 자연스럽게 표시되는지
- 과목 찾기 패널과 상단 도구의 높이·정렬
- 이미 추가된 과목 상태
- 단일 scheduled 분반 즉시 추가
- 복수 scheduled 분반 편집기 표시
- 시간 미정 분반 직접 선택
- scheduled와 unscheduled가 혼합된 과목
- 단일 분반 영어 비율은 과목 목록에 자연스럽게 표시
- 복수 분반 영어 비율은 과목 목록의 `0–100%` 같은 요약이 아니라 분반 선택 화면에서 각 분반별로 표시
- 담당자·장소 정보가 없을 때 과목 목록 및 편집기의 적절한 fallback
- 시간표와 PNG에서는 없는 행 자체가 생기지 않는지

10단계: 분반 선호와 대안 과목

복수 분반의 초기 상태는 모두 `가능`이어야 한다.

다음을 확인하라.

- 편집기를 처음 열었을 때 첫 번째 `선호` 버튼에 이유 없는 진한 포커스 테두리가 생기지 않는지
- 선호·가능·제외 버튼의 선택 표시가 서로 일관적인지
- 각 offering 행 안에서만 하나의 radio가 선택되고 다른 행에는 영향을 주지 않는지
- VoiceOver가 각 행을 독립된 올바른 radio group으로 읽는지
- 화살표 키가 같은 행의 선호·가능·제외 안에서만 이동하는지
- hover, pressed, selected, focused, disabled 상태
- 마우스 선택과 키보드 선택의 차이가 자연스러운지
- 선택 상태를 색만으로 전달하지 않는지
- 각 분반 행의 텍스트와 버튼이 상하 중앙에 정확히 정렬되는지
- 분반 목록 전체의 행 높이와 구분선
- 최소 하나의 선호 또는 가능 분반이 없으면 저장할 수 없는지
- 제외 분반이 추천에 사용되지 않는지
- 선호가 가능보다 우선되는지
- 편집 후 다시 열었을 때 상태가 복원되는지
- 영어 비율이 각 분반에 정확히 표시되는지
- `월요일: 11:30 ~ 12:15`처럼 요일과 시간 사이에 자연스러운 `:` 표기를 사용하는지
- `·`와 `:`가 문맥에 맞게 일관적으로 사용되는지

대안 과목도 검증한다.

- 서로 다른 과목을 "이 중 하나"로 묶기
- 검색 결과 최대 개수
- 이미 선택된 과목 배제
- 현재 편집 중인 과목 배제
- 시간 미정 전용 과목의 처리
- 대안 추가·제거
- 각 과목별 분반 선호 상태 독립
- 한 그룹에서 정확히 하나의 과목만 추천
- 저장 후 group ID 유지
- 그룹 전체 제거

불필요한 설명 텍스트가 UI에 덕지덕지 남아 있지 않은지도 확인한다. 특히 `선호는 먼저 추천하고, 가능은 충돌할 때 사용합니다.` 같은 삭제하기로 한 문구가 남아 있지 않아야 한다.

11단계: 추천 알고리즘과 실패 상태

실제 UI와 테스트를 함께 사용해 다음을 확인한다.

- 선택 과목 0개
- 과목 1개
- 여러 과목
- 추천 1개
- 추천 여러 개
- 추천 최대 24개
- 이전·다음 탐색
- 끝에서 순환 동작
- 공강 일수
- 선호 우선
- 가능 대안
- 제외 미사용
- 대안 그룹당 정확히 하나
- 과목끼리 충돌 제거
- 과목과 개인 일정 충돌 제거
- 일정 종료와 다음 일정 시작이 같으면 충돌이 아닌지
- 빠르게 선택을 바꿀 때 오래된 계산 결과가 최신 결과를 덮지 않는지
- 계산 중 UI
- 계산 취소
- 계산 실패
- 다시 계산
- 추천 불가능 상태
- 개인 일정이 없는 불가능 상태
- 개인 일정은 있지만 수강 조합이 불가능한 상태
- 불가능 상태에서 개인 일정만 read-only로 보이는지
- 추천이 없을 때 내보내기가 잘못 활성화되지 않는지
- 시간 미정 분반은 충돌이 없다고 보장하지 않으며 주간 보드·PNG에 표시되지 않는지
- 개인 일정만 있는 계획도 정상 표시·내보내기가 가능한지

추천 성능은 실제 515개 과목 카탈로그에서 측정한다. 검색 입력, 분반 변경, 계획 전환, 추천 재계산 도중 UI가 멈추지 않아야 한다.

12단계: 개인 일정

다음을 모두 검증한다.

- 상단 `일정 추가`
- 내 계획 인스펙터의 `추가`
- 시간표 카드 상세 flyout의 수정 아이콘
- 인스펙터의 수정
- 삭제
- 삭제 취소
- 삭제 확인
- 추가·수정 중 내 계획 패널이 이유 없이 닫히지 않는지
- 내 계획 패널은 X 또는 가운데 시간표 클릭 등 명시된 동작에서만 닫히는지
- 모달을 닫은 뒤 이전 인스펙터 상태 복원
- 일정 추가 기본 시간 12:00–13:00
- 입력 순서 오전/오후 → 시 → 분
- 5분 단위
- 월요일부터 일요일까지 복수 선택
- 모든 요일 버튼 폭이 동일한지
- 일요일 오른쪽 끝이 잘리거나 찌그러지지 않는지
- 요일 행 폭이 다른 입력 폼과 정확히 맞는지
- 일정 이름 필수
- 요일 필수
- 시작·종료 필수
- 종료가 시작보다 늦어야 함
- 최소 15분
- 동일 계획 안에서 중복 일정 차단
- 맞닿는 일정 허용
- 자정 경계
- 오전 12시와 오후 12시
- 제목 80자
- 분반 40자
- 담당자 80자
- 장소 120자
- 줄바꿈 거부
- 앞뒤 공백 정리
- 선택 정보가 비어 있을 때 시간표·PNG에서 빈 행과 불필요한 여백이 생기지 않는지
- 60분 미만의 작은 카드
- 긴 일정명
- 상세 flyout
- hover 설명이 `일정명`과 `선택하여 일정 상세 정보 보기` 정도로 간결한지
- 우측 상단 수정 아이콘 정렬
- 계획별 독립 저장
- 자동 저장과 재실행 복원
- 추천 충돌 반영

개인 일정 카드는 과목 카드와 동일한 시각적 계층을 가져야 한다.

13단계: 시간표 보드

다음을 확인하라.

- 기본 월요일–금요일
- 토요일 일정이 있을 때 토요일 표시
- 일요일 일정만 있을 때 토요일과 일요일 모두 표시
- 기본 시작 10:00
- 기본 종료 19:00
- 30분 단위 가로선
- 실제 시간 표시
- 이른 일정이 있으면 이전 시간으로 확장
- 늦은 첫 일정은 `max(10:00, 첫 일정 30분 전을 정각으로 내린 시간)` 기준으로 시작
- 늦은 일정이 있으면 종료 시각 확장
- 추천을 넘길 때 전체 추천의 시간 범위를 기준으로 축이 불필요하게 흔들리지 않는지
- 월–금 열 폭 동일
- 금요일 끝과 스크롤바가 겹치지 않는지
- 토·일 열 폭 동일
- 시간 레이블의 상하 중앙 정렬
- 스크롤바가 콘텐츠를 침범하지 않는지
- 작은 창과 큰 창에서 카드 clipping 없음
- 트랙패드 자연스러운 스크롤 설정 ON/OFF
- 두 손가락 수직·수평 스크롤과 관성
- sticky header와 5분 단위 일정 위치

과목 카드 시각 계층:

- 과목명과 `(01)` 형태의 분반
- `분반`이라는 단어는 붙이지 않음
- 과목명이 중심 정보
- 장소가 그다음 중요도이며 담당자보다 약간 더 강조
- 담당자는 마지막 정보
- 과목명과 장소 사이에 충분한 여백
- 장소와 담당자 사이 간격은 현재 합의값인 2.0 DIP
- 전체 내용 중앙 정렬
- 과목 코드와 학점은 시간표 카드와 PNG에서 제외
- 장소나 담당자가 없으면 `정보 없음`을 만들지 말고 행과 여백 자체를 제거
- 긴 한글·영문 제목의 말줄임과 상세 정보
- 카드 hover, selected, focused 상태
- 라이트·다크에서 카드 테두리와 배경 조화
- 개인 일정 카드도 같은 타이포그래피와 정렬 체계

14단계: 목록 보기

시간표 보기와 목록 보기 전환을 검증한다.

- 같은 이름의 과목 또는 개인 일정을 하나의 그룹으로 표시
- 대소문자와 불필요한 공백 정규화
- 같은 시간·장소·담당 정보라면 여러 요일을 하나의 occurrence로 병합
- 요일이 여러 개인 경우 모든 요일을 읽기 좋게 표시
- 시간이나 장소·담당이 다르면 같은 그룹 안에서 별도 occurrence
- 사용자가 실제로 별도로 만든 동일 이름 일정이 누락되지 않는지
- 요일과 시간 정렬
- `과목`, `장소`, `담당` 같은 불필요한 label 없이 자연스러운 계층
- 없는 값 생략
- 한 항목만 있을 때도 오른쪽 정보의 상하 중앙 정렬
- 긴 이름과 작은 창에서 clipping 없음
- 키보드 및 VoiceOver 탐색

15단계: 반응형 레이아웃과 패널

다음 네 구간을 각각 실제 창 크기로 검증한다.

- 1600 DIP 이상: 과목 패널 312 + 내 계획 288 inline
- 1280–1599: 과목 inline, 내 계획 304 overlay
- 1080–1279: 과목 inline 약 320, 내 계획 overlay
- 1080 미만: 양쪽 overlay

각 breakpoint는 바로 아래, 정확한 경계, 바로 위에서 반복 resize하고 pane 상태와 스크롤 위치가 보존되는지 확인한다.

확인할 내용:

- 시간표가 항상 중심 공간을 충분히 차지하는지
- 과목 찾기와 목록 보기 버튼 순서
- 과목 찾기가 열려 있으면 목록 보기가 도구 영역 가장 왼쪽에 오는지
- 과목 찾기와 일정 추가의 상하 위치
- 과목 패널 닫기
- 내 계획 열기
- 두 패널의 compact 상호 배제
- 내 계획을 열고 일정 추가·수정·과목 수정을 해도 패널이 임의로 닫히지 않는지
- 가운데 시간표 클릭과 X의 닫기 동작
- overlay가 모달보다 앞에 잘못 나타나지 않는지
- 스크롤바와 내용 간 안전 여백
- 창 resize 중 깜빡임·레이아웃 점프·카드 왜곡 없음
- flyout, combo box, context menu가 화면 가장자리·작은 창·전체 화면에서 잘리지 않는지
- popup이 닫힌 뒤 focus 복귀

16단계: 테마와 전체 시각 품질

시스템, 라이트, 다크를 모두 검증한다.

- 화면 모드 flyout
- 시스템 설정 사용
- 라이트
- 다크
- 선택 표시가 체크 원 전체를 자연스럽게 감싸는지
- radio와 텍스트 사이 거리
- radio 중심과 텍스트 중심의 상하 정렬
- 선택 배경이 충분히 왼쪽까지 이어지는지
- 키보드 focus ring
- 실행 중 macOS 시스템 appearance 변경 추종
- 재실행 후 설정 복원
- 손상된 테마 설정에서 System으로 안전하게 복구
- 열린 flyout·보드·코드 생성 카드의 즉시 테마 갱신
- 테마 저장 실패와 다시 저장
- selected 항목 위에 mouse hover 시 선택 색이 회색으로 덮이지 않는지
- disabled 상태의 가독성
- 오류·성공·경고 색상
- 색상 이외의 상태 표현

라이트·다크 각각 다음 화면을 캡처하고 실제 화면과 함께 검수한다.

- 첫 실행 로딩
- 빈 계획
- 과목 검색 결과
- 선택된 과목
- 분반 선택
- 대안 과목
- 개인 일정 추가·수정
- 계획 이름 변경
- 삭제 확인
- 내 계획
- 시간표 보기
- 목록 보기
- 추천 없음
- 오류·복구
- 화면 모드
- 내보내기 메뉴
- toast
- disabled 버튼
- hover, pressed, selected, focus 상태

검토 기준:

- 현대적이고 차분한 색조인지
- 레트로하거나 누렇고 탁한 인상이 없는지
- 주요 동작이 콘텐츠보다 지나치게 튀지 않는지
- 카드·표면·구분선·배경의 명도 계층
- 텍스트 대비
- 아이콘과 텍스트의 상하 중앙 정렬
- 모든 버튼 안의 텍스트 중앙 정렬
- 입력란 placeholder와 입력 텍스트 정렬
- combo box의 텍스트와 화살표 정렬
- modal의 제목·본문·버튼 간격
- 불필요한 설명 텍스트 제거
- 단어 중간의 부자연스러운 줄바꿈 없음
- 긴 문장은 의미 단위로 줄바꿈
- Pretendard Regular/Medium/SemiBold/Bold가 실제로 모두 로드되는지
- 한글·영문·숫자·이모지 fallback
- Retina에서 글꼴과 1px 선이 선명한지
- restored Fluent color PNG/image export 아이콘이 흐리거나 구식으로 보이지 않는지
- Google Calendar 실제 로고가 선명하고 텍스트와 중앙 정렬되는지

텍스트와 아이콘의 정렬은 "대충 맞아 보인다"로 끝내지 말고 실제 bounds와 baseline을 확인하라.

화면 결함은 다음 절차를 모두 거친 뒤 판정한다.

1. 같은 상태에서 라이브 화면을 두 번 이상 직접 관찰한다.
2. macOS 시스템 스크린샷을 native resolution으로 확인한다.
3. 관련되는 경우 앱의 PNG 내보내기 결과와 비교한다.
4. 다른 배율 또는 외부 디스플레이에서 재현한다.
5. 캡처 한 장에서만 나타나면 캡처 artifact 가능성을 분리해 기록한다.

17단계: 키보드와 접근성

Full Keyboard Access를 끈 상태와 켠 상태에서 마우스 없이 전체 핵심 흐름을 수행한다.

- Tab / Shift+Tab
- Enter / Space
- 방향키
- Escape
- `⌘F`와 코드상 보조로 허용하는 `Ctrl+F`
- `⌘W`
- `⌘Q`
- `⌘M`
- context menu 키보드 탐색
- combo box 및 radio group 탐색
- 모달 focus trap
- 모달 최초 focus
- 닫은 뒤 호출 버튼으로 focus 복귀
- validation 실패 시 문제 입력으로 focus 이동
- Escape 닫기 우선순위
- modal이 열린 동안 배경 단축키 차단
- destructive dialog의 기본 focus가 취소인지
- 종료 저장 실패 시 `계속 편집`으로 focus가 이동하는지

실제 VoiceOver와 가능하면 Accessibility Inspector를 사용해 다음을 확인하라.

- Main, Banner, Navigation, Search, Complementary landmark
- H1/H2 HeadingLevel
- rotor를 통한 landmark와 heading 탐색
- 창과 dialog 이름
- 버튼 이름과 역할
- 아이콘 전용 버튼의 접근 가능한 이름
- 계획 탭 selected 상태와 닫기
- 검색 결과 수와 결과 항목
- 과목 추가·선택 상태
- 선호·가능·제외 radio와 올바른 그룹
- 시간표 카드
- 개인 일정 카드
- 상세 flyout
- 수정·삭제
- 목록 보기 그룹
- 시간 미정
- 로딩
- 오류
- autosave 상태
- toast의 live announcement
- modal 읽기 순서
- 중복 또는 누락된 읽기
- 색상 없이 상태 구분 가능 여부

대비를 가능한 한 측정하라.

- 일반 텍스트 4.5:1
- 큰 텍스트 3:1
- 주요 컨트롤 경계·focus 표시 3:1

다음 macOS 접근성 설정에서도 기본 사용이 가능한지 확인하고, 지원되지 않는 부분은 명확히 기록한다.

- Increase Contrast
- Differentiate Without Color
- Reduce Transparency
- Reduce Motion
- grayscale 및 color filter
- Zoom과 Display scaling

18단계: PNG 내보내기

macOS 내보내기 메뉴에는 다음이 보여야 한다.

- PNG 이미지
- Apple Calendar
- Google Calendar

PNG를 실제로 저장하고 Preview로 열어 검증한다.

- 기본 파일명은 현재 계획명
- 파일명 금지문자 정리
- 지나치게 긴 계획명
- 저장 패널 취소
- 기존 파일 덮어쓰기
- iCloud Drive 저장 위치
- 읽기 전용 위치
- 권한 거부
- 용량 부족은 실제 디스크를 채우지 말고 안전한 방식으로 시뮬레이션
- 성공·실패 안내
- 기본 범위 10:00–16:00
- 10시 이전 일정이 있으면 앞쪽 확장
- 16시 이후 일정이 있으면 뒤쪽 확장
- 토요일
- 일요일 단독
- 개인 일정
- 긴 과목명
- 긴 장소·담당자
- 장소·담당자 누락
- 현재 보고 있는 추천만 내보내는지
- 시간 미정은 제외되는지
- 라이트 모드
- 다크 모드
- 월요일 첫 카드가 이유 없이 회색으로 변하는 현상이 없는지
- 카드 배경·테두리·글자색이 실제 화면과 조화로운지
- 투명도·alpha 문제
- Retina 선명도
- PNG 크기와 비율
- 빈 하단 영역이 불필요하게 길지 않은지

PNG 결과는 실제 Preview에서 열어 스크린샷을 남긴다.

19단계: Apple Calendar 내보내기

Apple Calendar 내보내기는 EventKit 직접 쓰기가 아니라 `.ics` 파일을 다음 의미로 여는 구현이다.

```bash
/usr/bin/open -b com.apple.iCal "<absolute-path-to-file.ics>"
```

다음을 검증한다.

- Apple Calendar 항목이 macOS에서만 보이는지
- Calendar 앱이 닫혀 있는 상태와 이미 열려 있는 상태
- `.ics` 생성
- Calendar 앱 실행
- 사용자가 가져오기를 확인하기 전 앱이 Calendar를 직접 변경하지 않는지
- 가져오기 취소
- 가져오기 확인
- 대상 캘린더 선택
- `.ics`가 Calendar가 읽기 전에 삭제되지 않는지
- 임시파일 수명주기와 읽기 권한
- 계획명과 캘린더 표시
- 학기 기간 `2026-08-31`부터 `2026-12-20`
- 첫 수업 날짜
- 마지막 반복 날짜
- 여러 요일의 BYDAY
- 과목명과 `(01)`
- 장소
- 담당자·과목 코드 설명
- 개인 일정의 선택 정보
- 여러 요일
- 반복 가져오기 시 중복 가능성과 사용자 안내
- Apple Calendar에서 실제 표시되는 현지 시각

중요한 요구사항:

캘린더 내보내기 시간대는 사용자의 macOS 로컬 시간대를 따라야 한다. 현재 코드 또는 테스트가 `Asia/Seoul`을 고정하고 있을 가능성이 있으므로 이를 추측하지 말고 실제 `.ics`, Google payload, 코드, 실행 결과를 대조하라.

Mac의 시간대를 다음처럼 서로 다른 IANA 시간대로 바꾸거나 안전한 격리 실행 환경을 사용해 검증한다.

- Asia/Seoul
- UTC
- America/Los_Angeles처럼 학기 중 DST 전환이 있는 지역
- 가능하면 30분 또는 45분 오프셋 시간대

비서울 시간대에서도 항상 `TZID:Asia/Seoul`이 생성된다면 "로컬 시간대를 따른다"는 합의 요구와 불일치하는 실제 결함으로 판정하라. DST의 존재하지 않는 시각과 중복 시각도 검증한다. 시스템 설정 변경은 사용자에게 알리고 원래 값으로 복구한다.

20단계: Google Calendar 내보내기

실제 OAuth end-to-end는 전용 QA Google 계정과 사용자의 승인이 있을 때만 실행한다. 비밀번호와 2단계 인증은 사용자가 직접 입력하게 하고 자동화하거나 기록하지 마라.

다음을 검증한다.

- 설정 파일이 없을 때 브라우저를 열지 않고 자연스럽게 안내
- OAuth Client ID 설정 로딩
- 시스템 기본 브라우저 실행
- 브라우저로 이동한 뒤 앱으로 focus 복귀
- PKCE
- 127.0.0.1 loopback redirect
- state 검증
- 인증 취소
- 접근 거부
- 브라우저 실행 실패
- loopback port 충돌
- firewall 또는 locked Keychain
- timeout
- 네트워크 실패
- 잘못된 authorization code
- 401, 403, 429, 5xx
- access token 만료
- refresh token
- refresh token 취소·폐기
- macOS Keychain 저장
- 앱 재실행 후 Keychain에서 복원
- Keychain 항목 삭제 후 재인증
- 토큰이 평문 JSON, 로그, URL query, 앱 번들에 남지 않는지
- Keychain service 이름 `TimetableGenerator.GoogleCalendar`
- 계획명 기반 별도 캘린더 생성
- 계획 이름 변경 시 캘린더 이름 조정
- 같은 계획 재내보내기 시 중복 calendar 방지
- 같은 일정 재내보내기 시 중복 event 방지
- 변경된 일정 update
- 삭제된 앱 관리 일정 delete
- 사용자가 직접 만든 일정 보존
- binding 파일 유실 시 plan marker로 재탐색
- 동시 export lock
- UI가 OAuth·네트워크 대기 중 멈추지 않는지
- macOS 로컬 시간대와 DST
- Apple Calendar와 동일한 학기 범위

외부 계정을 사용할 수 없다면 unit/integration fixture 검증과 실제 계정 검증을 구분하고 실제 OAuth·API·Keychain end-to-end를 `BLOCKED`로 남긴다.

21단계: 저장·복구·종료

다음을 실제로 검증한다.

- 변경 후 `저장 중`
- `자동 저장됨`
- 빠르게 여러 번 수정할 때 최신 snapshot만 저장
- 저장 실패
- 상단 다시 시도
- 실패 중에도 계속 편집
- 계획, 활성 탭, 분반 선호, 대안 그룹, 개인 일정, 테마 저장
- 현재 추천 offering ID 집합 저장
- 재실행 후 정확한 복원
- 최대 5개 generation
- 최신 파일 손상 후 이전 세대 복원
- 전 세대 손상
- 앱을 종료하는 순간 pending autosave
- 종료 시 입력 차단 overlay
- 10초 timeout
- traffic-light close와 `⌘Q` 모두 저장 안전 경계를 우회하지 않는지
- 저장 실패 또는 timeout 시 창이 닫히지 않는지
- 수정·재시도 후 정상 종료
- 강제 종료 후 다음 실행 복구
- 두 앱 인스턴스가 동시에 실행됐을 때 저장 충돌이나 데이터 손상 가능성

실제 사용자 계획 원본은 절대 직접 손상시키지 마라.

22단계: 성능·안정성

다음을 측정하고 수치를 보고하라.

- cold launch 5회
- warm launch 5회
- 첫 카탈로그 로드
- 캐시된 카탈로그 로드
- 515개 과목 검색 타이핑
- 필터 전환
- 과목 빠른 추가·제거
- 분반 상태 빠른 전환
- 추천 계산과 취소
- 계획 빠른 전환
- 개인 일정 다수
- 과목 다수
- 주말·야간 일정
- 긴 스크롤
- 테마 전환
- 시간표↔목록 전환
- PNG 생성
- Apple Calendar ICS 생성
- Google Calendar export
- 창 resize와 breakpoint 반복 횡단
- Retina 렌더링
- 30분 이상 soak test
- 창·flyout·modal을 반복해서 열고 닫은 뒤 메모리 증가
- idle CPU
- peak memory
- handle/file descriptor 누수
- 네트워크 실패 반복
- OAuth 취소 반복
- sleep/wake와 네트워크 변경 후 응답성

Activity Monitor, Console, `sample`, `leaks` 또는 사용 가능한 Instruments를 필요에 맞게 사용한다. 임의의 숫자로 통과 기준을 만들지는 말고, 측정값과 사용자가 체감할 수 있는 멈춤·지연·누수를 함께 보고한다.

23단계: 보안·개인정보

다음을 검증한다.

- 실제 카탈로그 URL이 소스에 하드코딩되지 않음
- 로컬 설정 파일 Git 제외
- 사용자 계획이 원격 서버로 업로드되지 않음
- OAuth token이 로그·파일·번들에 포함되지 않음
- refresh token은 Keychain에만 저장
- access token은 메모리에만 유지
- OAuth loopback의 state·PKCE 검증
- 민감한 URL query 로깅 없음
- 앱 데이터·binding·임시 ICS·PNG 파일 권한
- 임시 ICS 정리 시점
- 경로 traversal 및 잘못된 계획명 파일명
- 원격 catalog 크기·SHA 검증
- revision downgrade와 동일 revision 변조 차단
- 손상된 업데이트의 원자적 교체
- 배포물에 PDB와 개발용 파일 미포함
- 예상하지 않은 entitlement 없음
- Apple Events 또는 Calendar 권한을 요구하지 않는지
- 사용자가 선택하지 않은 외부 동작 없음

24단계: 서명·공증·Gatekeeper

현재 게시 결과는 의도적으로 unsigned다. 따라서 unsigned 앱의 다음 명령 실패를 일반 기능 실패와 구분하라.

```bash
codesign --verify --deep --strict --verbose=2 "$APP"
spctl --assess --type execute --verbose=4 "$APP"
xattr -lr "$APP"
```

quarantine을 제거해서 실행시킨 뒤 Gatekeeper 검증이 완료됐다고 주장하지 마라.

실제 Developer ID 인증서, 등록 bundle identifier, notary profile이 있을 때만 다음 전체 체인을 검증한다.

- 등록 bundle ID로 재게시
- 내부 Mach-O부터 바깥 bundle 순으로 서명
- hardened runtime
- 메인 실행 파일의 `com.apple.security.cs.allow-jit=true`
- 예상하지 않은 entitlement 없음
- timestamp
- nested Mach-O 서명
- notarization 제출
- notary log
- stapling
- stapler validate
- codesign strict verify
- spctl Gatekeeper 평가
- stapled app으로 최종 ZIP 재생성
- 최종 SHA-256
- 실제 배포 위치에 업로드
- Safari 등으로 다시 다운로드
- quarantine이 붙은 상태에서 최초 실행
- 인터넷이 없는 상태에서 stapled ticket 검증

서명할 때 `codesign --deep`을 사용하지 마라. `--deep`은 검증에만 사용한다.

인증서나 notary profile이 없다면 임시 ad-hoc 서명을 공개 배포 검증으로 포장하지 말고 해당 단계를 `BLOCKED: 실제 배포 자격 증명 부재`로 판정한다.

실제 Apple Silicon과 Intel Mac 각각에서 다운로드 후 최초 실행이 완료되지 않았다면 두 아키텍처 모두 공개 배포 준비 완료라고 판정하지 마라.

25단계: 최종 보고서

`artifacts/qa/macos-validation-YYYYMMDD-HHMM/REPORT.md`에 다음 구조로 상세 보고서를 작성하라.

1. 검증 요약
2. 저장소 커밋·작업 트리 상태
3. Mac 하드웨어·macOS·디스플레이·시간대
4. 사용한 .NET·PowerShell·Xcode 도구 버전
5. 자동 테스트 결과와 테스트 수
6. 게시 산출물·아키텍처·checksum
7. 실제 실행 결과
8. 기능 QA 매트릭스
9. Light/Dark/System 시각 QA
10. 반응형·Retina·다중 디스플레이 결과
11. 키보드·VoiceOver·접근성 결과
12. PNG 결과
13. Apple Calendar 결과
14. Google Calendar·Keychain 결과
15. 저장·복구 결과
16. 성능 측정
17. 보안·개인정보 결과
18. 서명·공증·Gatekeeper 결과
19. Intel·Apple Silicon 실기기 커버리지
20. 문서와 실제 동작의 차이
21. 발견된 결함
22. BLOCKED 및 미검증 항목
23. 출시 판정
24. 권장 수정 순서

각 테스트 행에는 다음을 포함하라.

- 테스트 ID
- 검증 항목
- 사전 조건
- 수행 절차
- 기대 결과
- 실제 결과
- PASS / FAIL / BLOCKED / NOT APPLICABLE
- 스크린샷·로그·파일 경로
- 관련 코드 위치

각 결함에는 다음을 포함하라.

- P0 / P1 / P2 / P3
- 제목
- 재현 절차
- 재현 빈도
- 기대 결과
- 실제 결과
- 실제 앱 문제인지 캡처 문제인지
- 영향 범위
- 스크린샷
- 로그
- 관련 파일과 줄
- 추정 원인
- 최소 수정 제안
- 회귀 테스트 제안

최종 답변에서는 반드시 다음을 명시하라.

- 자동 테스트 총 개수, 실패, skip
- 실제로 검증한 Mac 모델과 macOS 버전
- Apple Silicon native 검증 여부
- Intel native 검증 여부
- Rosetta 검증 여부
- Light/Dark/System 검증 여부
- VoiceOver 검증 여부
- Retina와 외부 모니터 검증 여부
- PNG 검증 여부
- Apple Calendar 실제 가져오기 검증 여부
- Google OAuth/API/Keychain 실제 검증 여부
- Developer ID 서명 여부
- notarization 여부
- Gatekeeper 다운로드 후 최초 실행 여부
- 남은 BLOCKED 항목
- 현재 상태가 로컬 개발용, 내부 배포용, 공개 배포 후보 중 어디까지 가능한지

어떤 항목도 수행하지 않은 채 생략하지 마라. 실행할 수 없는 항목은 반드시 BLOCKED로 남겨라. 모든 체크리스트에 판정과 증거가 생긴 뒤에만 작업을 종료하라.
````
