# macOS 캘린더 재검증 및 Windows 복귀 전 인계 지시문

아래 지시문 전체를 macOS의 Codex 작업에 그대로 전달한다. 이 문서는 이전 macOS 전체 QA 이후 Windows에서 추가한 캘린더 설명·소유권·브라우저 전달 변경을 검증하고, 발견된 실제 결함을 macOS에서 수정한 뒤 Windows로 돌아오기 직전까지 진행하기 위한 지시문이다.

---

당신은 Timetable Generator의 macOS 최종 릴리스 후보를 검증하고 필요한 결함을 수정하는 책임자다. 이전 macOS QA 결과를 이어서 진행하되, 과거 결과를 최신 코드의 증거로 간주하지 말고 이번에 받은 `origin/main`의 정확한 SHA를 기준으로 다시 검증하라.

## 1. 이번 작업의 목표와 종료 조건

목표는 다음과 같다.

1. 최신 `origin/main`을 안전하게 받아 Apple Silicon용 앱을 새로 빌드한다.
2. Windows에서 추가된 변경을 macOS 코드·테스트·실제 앱에서 검증한다.
3. Apple Calendar와 Google Calendar 내보내기를 실제 제품 수준으로 검증한다.
4. macOS에서만 드러나는 실제 결함과 UI 불일치를 발견하면 원인을 고치고 검증한다.
5. 수정이 있으면 의미 있는 단위로 커밋하고 `main`에 푸시한다.
6. Windows에서 이어서 검증할 수 있도록 재현 가능한 최종 보고서를 작성한다.

다음 조건을 모두 만족해야 작업 완료로 보고한다.

- 작업 트리가 깨끗하다.
- 로컬 설정, OAuth 자격 증명, 토큰, 사용자 데이터와 QA 산출물이 Git에 포함되지 않았다.
- Release 빌드가 경고 0, 오류 0이다.
- 전체 테스트가 두 번 연속 모두 통과한다.
- Apple Silicon용 `.app`을 새로 게시하고 번들 구조와 실행을 확인했다.
- 최신 코드로 Apple Calendar와 Google Calendar의 필수 실기 검증을 완료했다.
- 발견한 실제 결함을 수정한 경우 영향 범위를 다시 검증했다.
- 최종 SHA와 커밋, 실행 명령, 결과, 스크린샷, 차단 항목이 보고서에 남아 있다.
- 최종 수정 커밋이 필요했다면 `origin/main`에 푸시했다.

이 단계에서는 태그, GitHub Release, 공개 배포를 만들지 않는다. Windows 최종 회귀 검증도 수행하지 않는다. macOS 검증과 필요한 수정·푸시·보고를 마친 시점에서 멈춘다.

## 2. 작업 원칙

다음 원칙은 반드시 지킨다.

1. 일상적인 읽기, 빌드, 테스트, 앱 실행, 스크린샷, QA 데이터 생성, 범위 안의 코드 수정과 커밋은 사용자에게 반복해서 허가를 묻지 말고 진행한다.
2. macOS 보안 확인창, Google 로그인·동의 화면, Apple Calendar 자동화 권한처럼 사용자가 직접 승인해야 하는 보안 UI만 사용자에게 명확히 요청한다. 승인 전에는 실패로 단정하거나 다음 시나리오로 넘어가지 않는다.
3. 실제 사용자 캘린더나 기존 앱 데이터를 훼손하지 않는다. 전용 QA 시간표·QA 캘린더·격리된 데이터 루트를 사용한다.
4. `git reset --hard`, `git clean`, 기존 파일의 무단 삭제, 실제 캘린더 전체 삭제 같은 파괴적 동작을 하지 않는다.
5. 화면 캡처에서만 생긴 렌더링 흔들림과 실제 앱 결함을 구분한다. 같은 상태를 다시 표시하고 실제 화면·접근성 트리·레이아웃 값 중 가능한 근거로 확인한 뒤 수정한다.
6. 네이티브 `NSSavePanel`은 macOS 표준 시스템 UI이므로 앱 내부 대화상자처럼 재디자인하지 않는다. 호출 시점, 기본 파일명, 저장 형식, 취소·덮어쓰기·권한 실패, 앱으로 복귀하는 흐름만 검증한다.
7. 과거 `docs/macos-release-validation-prompt.md`의 "검증 중 소스 변경 금지" 원칙은 이번 단계에는 적용하지 않는다. 이번 지시문은 실제 결함의 수정·커밋·푸시를 명시적으로 허용한다.
8. 검증을 통과시키기 위해 테스트를 약화하거나 실제 오류를 숨기는 예외 처리를 추가하지 않는다.
9. 공개 저장소에 들어가면 안 되는 내용을 출력하거나 커밋하지 않는다. 로컬 JSON의 실제 값, OAuth client secret, 토큰, 계정 주소, 사용자 캘린더 ID를 보고서나 로그에 남기지 않는다.

## 3. 코드 수정 원칙

결함을 수정할 때 다음 기준을 엄격히 따른다.

- 저장소의 `.editorconfig`, 기존 분석기, 기존 C# 코딩 표준과 근처 코드의 설계를 먼저 읽는다.
- 도메인 의미가 있는 값은 목적에 맞는 강타입을 사용한다. 단순히 타입 수를 늘리기 위한 래퍼는 만들지 않는다.
- 기본 자료형을 전달하더라도 그 값이 표현 계층의 단순 표시 값인지, 도메인 경계를 넘는 식별자·시간·상태인지 구분한다.
- 파일은 책임과 변경 이유가 명확히 갈릴 때만 분리한다. 길다는 이유만으로 분리하지 않는다.
- 단순 대입문, 단순 메서드 호출, 읽기 쉬운 조건식을 줄 길이만 보고 기계적으로 나누지 않는다.
- 반대로 여러 개념이 섞여 읽기 어려운 코드는 의미 단위로 정리한다.
- 플랫폼 분기는 운영체제 경계에만 둔다. 공통 정책을 macOS 전용 코드에 복제하지 않는다.
- 기존 사용자의 데이터, 캘린더와 소유권 마커의 하위 호환성을 보존한다.
- 오류 메시지는 사용자가 다음 행동을 알 수 있게 하되 내부 식별자나 과도한 구현 설명을 노출하지 않는다.
- UI는 Pretendard 계열, 기존 여백·높이·색상 토큰과 라이트·다크·시스템 모드 정책을 유지한다.
- 아이콘과 텍스트의 중심, 버튼 안 텍스트의 상하 정렬, 팝업 여백은 기존 제품 UI와 수치적으로 일관되게 맞춘다.

수정은 의미 있는 단위로 커밋한다. 커밋 메시지는 기존 로그의 `fix: ...`, `refactor: ...`, `docs: ...` 형식을 따른다. 여러 결함을 한 커밋에 무리하게 묶지 않는다.

## 4. 최신 코드 받기와 비밀 파일 확인

먼저 현재 저장소 위치와 상태를 확인한다.

```bash
pwd
git status --short
git branch --show-current
git remote -v
git log -8 --oneline --decorate
```

작업 트리에 기존 변경이 있다면 덮어쓰지 않는다. 변경의 출처와 목적을 확인해 별도 보고하고 안전하게 보존한 뒤에만 진행한다.

작업 트리가 깨끗하면 다음을 실행한다.

```bash
git fetch origin
git switch main
git pull --ff-only origin main
git rev-parse HEAD
git log -8 --oneline --decorate
```

`git pull --ff-only` 직후 `git rev-parse HEAD`와 `git rev-parse origin/main`을 각각 실행해 두 SHA가 정확히 같은지 확인한다. 다르면 로컬 `main`이 앞서 있거나 갈라진 원인을 확인하기 전에는 검증·커밋·푸시를 진행하지 않는다.

최신 이력에는 적어도 다음 기능 커밋이 포함되어야 한다.

- `71119a7 fix: align personal schedule action labels`
- `42f1d13 fix: accept default browser shell handoff`
- `88ea9ad fix: publish friendly calendar descriptions safely`

최종 기준 SHA는 위 커밋 중 하나로 고정하지 말고, `git pull --ff-only` 뒤의 정확한 `HEAD`를 기록한다.

다음 두 파일은 macOS 로컬에 실제 값이 있는지 확인하되 내용을 출력하지 않는다.

```text
src/TimetableGenerator.Desktop/catalog-source.local.json
src/TimetableGenerator.Desktop/google-calendar.local.json
```

파일이 없다면 사용자가 별도로 전달한 로컬 사본을 정확한 위치에 복사한다. 두 파일은 계속 Git에서 무시되어야 한다.

```bash
git check-ignore -v \
  src/TimetableGenerator.Desktop/catalog-source.local.json \
  src/TimetableGenerator.Desktop/google-calendar.local.json

test -z "$(git ls-files -- \
  src/TimetableGenerator.Desktop/catalog-source.local.json)"

test -z "$(git ls-files -- \
  src/TimetableGenerator.Desktop/google-calendar.local.json)"
```

첫 명령은 두 파일의 ignore 규칙을 보여야 하고, 뒤의 두 명령은 각 파일이 추적 목록에 없을 때만 성공해야 한다. 실제 JSON 내용은 터미널·보고서·스크린샷에 표시하지 않는다.

검증 증거는 Git에서 무시되는 별도 폴더에 모은다.

```bash
QA_STAMP="$(date +%Y%m%d-%H%M%S)"
QA_ROOT="$PWD/artifacts/qa/macos-calendar-revalidation-$QA_STAMP"
mkdir -p \
  "$QA_ROOT/logs" \
  "$QA_ROOT/screenshots/light" \
  "$QA_ROOT/screenshots/dark" \
  "$QA_ROOT/screenshots/system" \
  "$QA_ROOT/exports/png" \
  "$QA_ROOT/publish" \
  "$QA_ROOT/release-evidence"
```

실제 사용자 데이터를 삭제하지 않고 첫 실행 상태를 재현한다. 코드에서 `Environment.SpecialFolder.LocalApplicationData`가 macOS에서 해석되는 실제 경로를 먼저 확인하고, 예상 경로와 일치할 때만 다음처럼 디렉터리 전체를 이동한다.

```bash
DATA_ROOT="$HOME/Library/Application Support/TimetableGenerator"
DATA_BACKUP="$HOME/Library/Application Support/TimetableGenerator.before-qa-$QA_STAMP"
QA_DATA="$HOME/Library/Application Support/TimetableGenerator.qa-$QA_STAMP"

if [ -d "$DATA_ROOT" ]; then
  test ! -e "$DATA_BACKUP"
  mv "$DATA_ROOT" "$DATA_BACKUP"
fi
```

예상 경로와 실제 코드 결과가 다르면 이 명령을 실행하지 말고 올바른 경로를 확인해 보고한다. 사용자 데이터는 어떤 경우에도 삭제하지 않는다.

## 5. 기준 빌드와 자동 검증

환경을 기록한다.

```bash
sw_vers
uname -m
dotnet --info
git rev-parse HEAD
```

현재 장비의 공식 검증 대상은 Apple Silicon `osx-arm64`이다. Intel `osx-x64`는 이번 v1.0 필수 범위가 아니다.

다음 순서로 검증한다.

```bash
pwsh ./tests/Distribution/MacOSIcon.Tests.ps1
pwsh ./tests/Distribution/PublishedContent.Tests.ps1

dotnet restore TimetableGenerator.sln --locked-mode
dotnet format TimetableGenerator.sln --verify-no-changes --no-restore
dotnet build TimetableGenerator.sln \
  --configuration Release \
  --no-restore \
  --disable-build-servers \
  --maxcpucount:1 \
  --nodeReuse:false

dotnet test TimetableGenerator.sln \
  --configuration Release \
  --no-build \
  --no-restore

dotnet test TimetableGenerator.sln \
  --configuration Release \
  --no-build \
  --no-restore
```

현재 Windows 기준 전체 테스트는 900개다. macOS에서 테스트 수가 다르면 무조건 실패로 단정하지 말고 프로젝트별 결과와 플랫폼 조건을 확인한다. 실패·skip·경고는 하나도 숨기지 않고 원인을 보고한다.

Apple Calendar, Google Calendar, 일정 표시와 관련된 테스트 결과를 별도로 기록한다.

## 6. 새 Apple Silicon 앱 게시

이전 QA 산출물을 재사용하지 말고 최신 SHA로 새 출력 폴더를 만든다. 저장소의 현재 게시 스크립트와 `docs/distribution.md`를 먼저 확인한 뒤 다음 명령을 사용한다.

```bash
test "$(uname -m)" = "arm64"

pwsh ./scripts/write-release-build-info.ps1 \
  -Version 1.0.0 \
  -RequireClean \
  -OutputRoot "$QA_ROOT/release-evidence"

pwsh ./scripts/publish-desktop.ps1 \
  -Runtime osx-arm64 \
  -Version 1.0.0 \
  -BundleIdentifier "io.github.potterlim.timetable" \
  -OutputRoot "$QA_ROOT/publish" \
  -NoRestore
```

스크립트가 출력한 실제 경로를 권위 있는 결과로 사용하고, 최소한 다음 조건을 검증한다.

- `Timetable Generator.app`가 `osx-arm64`로 새로 생성됨
- `CFBundleIdentifier = io.github.potterlim.timetable`
- `CFBundleDisplayName = Timetable Generator`
- `CFBundleExecutable = TimetableGenerator`
- `NSAppleEventsUsageDescription`가 존재함
- 소스 entitlement 파일에 Apple Events 자동화 권한이 존재함
- 메인 실행 파일이 arm64이며 실행 권한을 가짐
- 로컬 카탈로그·Google Calendar 제품 설정은 앱에 포함되지만 Git에는 포함되지 않음
- 액세스 토큰, 새로 고침 토큰, 실제 사용자 데이터는 앱에 포함되지 않음

생성된 ZIP의 SHA-256과 압축 무결성을 확인하고, 새로운 QA 폴더에 다시 푼 `.app`의 실행 권한도 확인한다. 실제 제품 검증은 가능하면 이 새로 푼 `.app`을 `/usr/bin/open -n`으로 실행해 수행한다. 내부 Mach-O 실행 파일을 직접 실행하는 것은 진단이 필요한 경우에만 사용한다.

Developer ID 서명·공증 자격 증명이 없다면 unsigned RC 검증과 공개 배포 검증을 구분하고 서명·공증·다운로드 Gatekeeper는 `BLOCKED`로 기록한다. 임시 ad-hoc 서명이나 격리 속성 제거 결과를 공개 배포 증거로 사용하지 않는다.

unsigned 앱에는 소스 entitlement 파일이 자동으로 부여됐다고 주장하지 않는다. 서명된 후보가 있을 때만 `codesign -d --entitlements :-`의 실제 결과로 번들 entitlement를 검증한다.

## 7. 최신 변경의 정적 검토

실기 전에 다음 정책이 코드와 테스트에서 일치하는지 확인한다.

### 7.1 공통 메타데이터

- 캘린더 설명은 시간표 이름에서 문자열을 잘라 만들지 않는다.
- 카탈로그의 학교 이름과 학기 정보를 사용한다.
- 현재 데이터의 예상 설명은 정확히 `한동대학교 2026-2 시간표입니다.`다.
- Google과 Apple이 같은 사용자용 설명을 제공한다.

### 7.2 수업과 개인 일정

- Google Calendar의 수업 제목에는 과목명만 들어가며 `(분반)`을 붙이지 않는다.
- Apple Calendar의 수업 제목에는 `과목명(분반)`을 사용한다.
- 수업 설명은 과목 코드, 분반, 교수 순서를 유지한다.
- 개인 일정에는 교수 대신 `담당` 표현을 유지한다.
- 시간표 화면과 일정 목록에서 개인 일정 분반은 제목 뒤 `(분반)` 형식으로 표시한다.
- 분반이 없으면 빈 표식을 만들지 않는다.

### 7.3 소유권

- 사용자에게 보이는 캘린더 설명에 내부 소유권 문자열을 노출하지 않는다.
- Google 소유권은 이벤트의 private extended properties로 관리한다.
- Apple 소유권은 앱 관리 이벤트의 URL 마커로 관리한다.
- Apple v2 URL 마커는 PlanId와 이벤트 식별 정보를 함께 보존한다.
- 기존 v1 마커는 대체 내보내기에서 안전하게 이전된다.
- 앱 소유 마커가 없는 사용자 일정은 수정하거나 삭제하지 않는다.
- 이름만 같다는 이유로 캘린더나 이벤트를 앱 소유로 취급하지 않는다.

## 8. macOS UI 전체 회귀 검증

라이트, 다크, 시스템 모드에서 각각 실제 앱을 눈으로 확인한다. 최소한 다음 흐름을 수행한다.

1. 첫 실행과 카탈로그 로드
2. 시간표 생성·이름 변경·삭제
3. 과목 글자별 실시간 검색
4. 분반의 선호·가능·제외
5. 여러 과목 중 하나 선택
6. 개인 일정 추가·수정·삭제
7. 일정 목록과 주간 시간표 전환
8. 시간표 편집 패널 열기·닫기
9. 후보 이동
10. 토요일·일요일·야간 일정
11. 창 크기 변경, 최소화, 복원, 전체 화면
12. PNG 한 장과 모든 가능한 시간표 PNG 저장
13. 내보내기 메뉴와 충돌 확인창
14. 성공·오류 알림의 유지와 닫힘 정책
15. Calendar.app이 닫힌 상태와 열린 상태

다음 마감 기준을 특히 확인한다.

- 컨트롤이 포커스를 받았다는 이유만으로 회색 박스가 과도하게 칠해지지 않음
- 버튼·콤보박스·텍스트 입력·아이콘과 텍스트의 수직 중심이 맞음
- 라이트와 다크에서 선택·hover·disabled 상태가 서로 혼동되지 않음
- 패널을 열면 본문 폭이 자연스럽게 재배치되고 내용 위를 임의로 덮지 않음
- 스크롤바가 요일 열이나 카드 내용을 침범하지 않음
- 금요일·토요일·일요일까지 표시될 때 헤더 하단선과 오른쪽 끝 테두리가 끊기거나 넘치지 않음
- 시간표마다 가장 이른 일정과 마지막 일정에 맞는 독립 시간축을 사용함
- 시작은 가장 이른 일정이 속한 정각보다 30분 앞이며 자정 이전으로 넘어가지 않음
- 끝은 마지막 일정 종료보다 늦은 첫 30분 경계임
- 마지막 시간 경계선은 보이지만 마지막 경계의 시간 라벨은 표시하지 않음
- 빈 상태에 중복된 개인 일정 추가 버튼이나 과도한 설명이 없음

macOS 네이티브 `NSSavePanel`에서는 다음만 확인한다.

- 앱에서 패널로 진입하는 동작이 자연스러움
- 기본 파일명과 확장자가 올바름
- 현재 시간표 PNG와 모든 가능한 시간표 PNG의 대상 선택이 구분됨
- 취소하면 오류로 처리하지 않고 원래 화면으로 복귀함
- 기존 파일 덮어쓰기와 쓰기 권한 오류를 운영체제 관례대로 처리함
- 패널 종료 후 앱의 포커스와 모달 상태가 정상 복구됨

네이티브 패널 자체의 시스템 글꼴·버튼·색상은 앱 테마로 강제 재디자인하지 않는다.

시간대 정책은 `TimeZoneInfo.Local`을 따르므로 자동 테스트에서 최소 `Asia/Seoul`, UTC, DST가 있는 지역의 현지 시각·학기 종료·DST 경계를 확인한다. 실제 기기의 시스템 시간대를 바꾸는 실기 검증은 원래 값을 먼저 기록하고 안전하게 복구할 수 있을 때만 수행하며, 변경 전 사용자 확인이 필요한 운영체제 설정은 사용자가 직접 조작하도록 한다.

## 9. Apple Calendar 실제 검증

전용 QA 시간표와 전용 QA 캘린더를 사용한다. 검증 전후의 캘린더·이벤트 수와 식별 정보를 사용자에게 보이지 않는 보고용 값으로 기록하되, 실제 계정 주소와 캘린더 ID는 마스킹한다.

### 9.1 최초 자동화 권한

이번 검증에서 반드시 실제로 확인할 핵심 흐름이다.

1. 앱을 종료한다.
2. 기존 QA 데이터와 실제 사용자 데이터를 안전하게 분리한다.
3. 전용 macOS QA 사용자에서 다음 명령의 적용 범위를 확인한 뒤 Timetable Generator의 Apple Events 권한만 초기화한다. 같은 bundle ID의 다른 설치본도 영향을 받을 수 있으므로 실제 사용자 환경에서는 사용자 확인 없이 실행하지 않는다.

```bash
tccutil reset AppleEvents io.github.potterlim.timetable
```

4. 새로 게시한 `.app`을 실행한다.
5. Apple Calendar로 내보내기를 한 번 누른다.
6. macOS의 Calendar 자동화 권한 확인창이 나타나면 앱이 즉시 오류로 빠지는지, 작업을 취소하는지, 승인 결과를 기다리는지 관찰한다.
7. 사용자가 직접 **허용**한다.
8. 허용한 동일 작업이 두 번째 클릭 없이 이어져 실제 내보내기까지 완료되는지 확인한다.
9. 성공 알림과 Calendar.app의 실제 결과를 확인한다.

Codex 보안 정책이 권한 확인창 제어를 차단하면 자동 클릭을 우회하지 않는다. 사용자에게 허용 버튼만 직접 눌러 달라고 요청하고, 이를 "사용자 보조로 실기 검증 완료"라고 기록한다. 권한을 기다리는 동안의 상태를 실패로 오분류하지 않는다.

가능하면 다음 시나리오도 안전하게 확인한다.

- 권한 거부 시 앱이 멈추지 않고 명확한 오류를 표시함
- 시스템 설정에서 권한을 다시 허용한 뒤 재시도가 성공함
- 권한 확인 중 앱을 닫거나 취소했을 때 임시 프로세스와 요청 파일이 남지 않음
- 최초 허용을 기다리는 동안 중복 내보내기 요청이 생성되지 않음
- 전달용 임시 JSON이 `0600` 상당의 사용자 전용 권한으로 생성되고 성공·거부·취소 뒤 제거됨

서명·공증되지 않은 앱의 Gatekeeper 최초 실행을 실제 다운로드 출처까지 재현하지 못하면 자동화 권한 검증과 구분해 `BLOCKED`로 기록한다. Gatekeeper 우회를 위해 임의로 격리 속성을 삭제한 결과를 최종 배포 증거로 사용하지 않는다.

### 9.2 새 캘린더 생성

- 현재 시간표 이름으로 캘린더가 생성됨
- 설명이 정확히 `한동대학교 2026-2 시간표입니다.`임
- 내보낸 이벤트의 제목, 설명, 장소, 시작·종료, 반복 기간, 요일과 로컬 시간대가 정확함
- 수업 제목이 `과목명(분반)` 형식임
- 수업 설명이 과목 코드, 분반, 교수 순서임
- 개인 일정은 `담당` 표현을 사용함
- 빈 내보내기는 의미 없는 캘린더를 생성하지 않음

### 9.3 같은 이름 충돌과 대체

각 시나리오를 별도 QA 캘린더에서 확인한다.

- 같은 이름이 없을 때 새로 생성
- 같은 이름이 있을 때 번호를 붙여 새로 생성
- 앱이 관리하는 같은 이름 캘린더를 기존 캘린더 대체
- 동일 PlanId로 다시 대체해도 중복 이벤트가 늘지 않음
- 다른 PlanId의 시간표로 같은 캘린더를 대체할 때 캘린더의 기존 소유 PlanId를 안전하게 유지함
- 사용자가 직접 만든 URL 없는 이벤트가 보존됨
- 사용자가 직접 만든 외부 URL 이벤트가 보존됨
- 이름만 같은 비관리 캘린더는 대체 대상으로 오인하지 않음
- 읽기 전용 캘린더와 같은 이름의 캘린더가 여러 개인 경우 대체를 허용하지 않음
- 충돌 확인에서 취소하면 어떤 캘린더도 변경하지 않음
- 조회와 적용 사이 대상 캘린더의 이름·소유권·쓰기 가능 상태가 바뀌면 안전하게 중단함

### 9.4 v1에서 v2로 실제 이전

테스트 코드의 상수를 임의로 복사하지 말고 현재 구현을 확인해 전용 QA 이벤트에 유효한 legacy v1 이벤트 URL 마커를 만든다.

- v1 관리 이벤트가 있는 캘린더를 대체함
- v1 이벤트가 제거되고 v2 PlanId 기반 이벤트로 다시 생성됨
- 사용자 이벤트는 보존됨
- 한 번 더 대체해도 이벤트 수가 안정적임
- 캘린더 설명은 내부 v1 소유권 문자열이 아니라 친화적 설명으로 바뀜

### 9.5 Calendar.app에서 읽어 확인

앱의 성공 알림만으로 통과시키지 않는다. Calendar.app 또는 안전한 읽기 전용 JXA로 실제 캘린더를 다시 읽어 다음을 비교한다.

- 캘린더 이름과 설명
- 총 이벤트 수와 앱 관리 이벤트 수
- 사용자 이벤트 보존 여부
- 각 이벤트의 제목·설명·장소·시작·종료·반복·시간대
- v2 URL 마커 존재와 v1 마커 제거
- 재내보내기 뒤 중복 없음

검증용 캘린더만 정리하고 사용자 캘린더는 건드리지 않는다.

## 10. Google Calendar 실제 검증

전용 QA Google 계정과 QA 시간표를 사용한다. 실제 OAuth 설정의 값은 출력하지 않는다.

### 10.1 OAuth와 기본 브라우저

- 앱의 Google Calendar 내보내기를 누르면 macOS 기본 브라우저가 직접 열림
- `https` 연결 프로그램 선택창이나 어색한 중간 앱 선택창이 나타나지 않음
- Google 계정 선택·동의·취소를 정상 처리함
- loopback callback 동안 앱의 인증 작업이 기다리고, 승인 후 내보내기를 계속함
- callback 완료 뒤 앱이 내보내기와 상태 갱신을 끝내고 기본 브라우저에서 Google Calendar 웹을 엶
- 앱 창을 자동으로 foreground로 되돌리는 기능이 없는 현재 계약을 실패로 오인하지 않음
- callback 페이지가 연결 거부 화면으로 끝나지 않음
- OAuth 제한 시간이 지나면 명확한 오류를 표시함
- 오류 알림은 즉시 사라지지 않고 다른 의미 있는 동작 또는 닫기 버튼으로 닫힘
- 액세스 토큰과 새로 고침 토큰을 디스크·로그·Git에 남기지 않음
- 설정 파일이 없거나 유효하지 않으면 브라우저를 열지 않고 앱에서 안내함
- 매 내보내기마다 새 대화형 승인을 시작하고 refresh token을 요청하지 않음
- loopback 포트 사용 불가·방화벽 차단·네트워크 단절에서도 UI가 멈추지 않음
- 동시에 두 번 내보내기를 요청해도 중복 OAuth·캘린더 작업이 실행되지 않음

브라우저 선택과 Google 보안 화면은 사용자가 직접 처리하게 한다. 계정 비밀번호와 2단계 인증 정보는 보거나 기록하지 않는다.

### 10.2 생성·대체·번호 붙이기

- 새 캘린더 생성
- 같은 이름에서 번호를 붙여 새 캘린더 생성
- 앱 관리 캘린더 대체
- 비관리 캘린더와 기본 캘린더를 대체하지 않음
- 쓰기 권한이 없는 캘린더를 대체하지 않음
- 401·403·429·5xx와 이벤트 생성·갱신·삭제의 중간 실패를 구분해 안내함
- 중간 실패에서 기존 stale 이벤트를 조기에 삭제하지 않고, 재시도하면 앱 관리 이벤트가 중복 없이 원하는 상태로 수렴함
- 사용자 이벤트를 보존함
- 반복 실행해도 앱 관리 이벤트가 중복되지 않음

Google Calendar 웹 UI 또는 API의 읽기 결과로 다음을 확인한다.

- 캘린더 이름
- 설명이 정확히 `한동대학교 2026-2 시간표입니다.`임
- 시간대
- 이벤트 제목·설명·장소·반복·기간
- 수업 설명의 과목 코드, 분반, 교수 순서
- 개인 일정의 `담당` 표현
- private extended properties의 앱 소유권이 사용자 설명에 노출되지 않음
- legacy 설명 소유권을 가진 기존 앱 캘린더가 안전하게 현재 정책으로 이전됨
- 사용자 이벤트 보존과 앱 관리 이벤트 중복 없음

실제 API가 네트워크·계정 정책으로 막히면 자동 테스트 결과로 대체하지 말고 정확한 단계와 원인을 `BLOCKED`로 남긴다.

## 11. 결함 수정 후 재검증

실제 결함을 발견하면 다음 순서로 처리한다.

1. 재현 조건과 증거를 남긴다.
2. 캡처 과정의 문제인지 앱의 실제 문제인지 구분한다.
3. 원인을 코드와 상태 전이 수준에서 설명한다.
4. 가장 작은 올바른 범위로 수정한다.
5. 관련 단위 테스트를 추가하거나 강화한다.
6. `dotnet format --verify-no-changes`, 관련 테스트, Release 빌드를 실행한다.
7. 실제 앱에서 같은 시나리오를 다시 확인한다.
8. 의미 있는 단위로 커밋한다.

모든 수정이 끝난 뒤에는 전체 Release 빌드와 전체 테스트를 다시 두 번 실행하고, 새 `osx-arm64` 앱을 다시 게시한다. 이전에 성공한 앱 번들을 재사용하지 않는다.

푸시 전에는 다음을 확인한다.

```bash
git status --short
git diff --check
git diff --cached --check
git diff --cached --name-only
git log -8 --oneline --decorate
git check-ignore -v \
  src/TimetableGenerator.Desktop/catalog-source.local.json \
  src/TimetableGenerator.Desktop/google-calendar.local.json
```

다음 항목이 staged 또는 추적 목록에 있으면 커밋하지 말고 제거한다.

- `catalog-source.local.json`
- `google-calendar.local.json`
- OAuth 토큰·실제 client secret을 출력한 파일
- 사용자 데이터
- Calendar.app에서 읽은 실제 계정 식별 정보
- `artifacts/qa` 아래 보고서·스크린샷·로그
- 빌드 산출물

수정 커밋이 있으면 다음을 실행한다.

```bash
git push origin main
git fetch origin
git rev-parse HEAD
git rev-parse origin/main
```

`HEAD`와 `origin/main`이 일치해야 한다. GitHub Actions가 시작되면 macOS와 Windows 검사가 완료될 때까지 확인하고, 실패하면 로그를 분석해 수정·재검증·커밋·푸시를 반복한다. 푸시한 정확한 최종 SHA를 기록한다. 수정이 없으면 불필요한 빈 커밋을 만들지 않는다.

## 12. 최종 보고서

증거 수집이 끝나면 QA 중 생성된 앱 데이터를 별도 보존하고 원래 사용자 데이터를 복원한다.

```bash
if [ -d "$DATA_ROOT" ]; then
  test ! -e "$QA_DATA"
  mv "$DATA_ROOT" "$QA_DATA"
fi

if [ -d "$DATA_BACKUP" ]; then
  test ! -e "$DATA_ROOT"
  mv "$DATA_BACKUP" "$DATA_ROOT"
fi
```

QA용 Apple·Google 캘린더는 증거를 남긴 뒤 QA 계정에서만 정리한다. 사용자 캘린더는 삭제하지 않는다. 자동화 권한을 초기화하거나 변경했다면 최종 권한 상태를 보고서에 기록한다.

다음 위치처럼 Git에서 무시되는 QA 폴더에 보고서를 작성한다.

```text
artifacts/qa/macos-calendar-revalidation-YYYYMMDD-HHMM/REPORT.md
```

보고서에는 최소한 다음 절이 있어야 한다.

1. 장비와 macOS·SDK 정보
2. 시작 SHA와 최종 SHA
3. pull한 커밋 목록
4. 로컬 설정 파일의 존재·ignore·비추적 확인 결과
5. restore·format·build 결과
6. 두 번의 전체 테스트 결과와 프로젝트별 개수
7. 새 게시 앱의 경로·아키텍처·번들 메타데이터
8. 라이트·다크·시스템 UI 검증 결과
9. PNG와 `NSSavePanel` 검증 결과
10. Apple Calendar 최초 권한 허용·거부·재시도 결과
11. Apple Calendar 생성·번호 붙이기·대체·v1→v2 이전·사용자 이벤트 보존 결과
12. Apple Calendar 실제 읽기 검증 결과
13. Google OAuth·callback·취소·시간 초과 결과
14. Google Calendar 생성·번호 붙이기·대체·legacy 이전·사용자 이벤트 보존 결과
15. Google Calendar 실제 읽기 검증 결과
16. 발견 결함과 원인
17. 수정 파일·테스트·커밋
18. 보안 도구 또는 Codex 정책으로 중단된 작업
19. 남은 `BLOCKED` 항목
20. Windows로 복귀 가능한지에 대한 결론

같은 폴더에 `HANDOFF.md`도 작성하고 다음 정보를 짧게 정리한다.

- 시작 SHA, 최종 SHA, `origin/main` SHA
- macOS에서 만든 커밋과 각 목적
- push와 GitHub Actions 결과
- 실제 검증한 `.app`과 unsigned ZIP 경로 및 SHA-256
- Apple·Google Calendar 실기 결과
- 남은 결함과 차단 항목
- Windows에서 처음 실행할 `git pull --ff-only` 명령과 필수 회귀 항목

각 항목을 `PASS`, `FAIL`, `BLOCKED`, `NOT APPLICABLE` 중 하나로 표시한다. 실행하지 않은 항목을 `PASS`로 기록하지 않는다.

보안 정책이 UI 자동화나 임시 파일 정리를 막았다면 실제 원인을 "사용자 메시지로 중단"처럼 바꾸지 말고, 차단된 명령·의도·대체 검증·잔여 영향을 별도 로그에 정확히 적는다.

## 13. 최종 응답과 중단 지점

최종 응답에는 다음만 명확히 요약한다.

- 시작 SHA와 최종 SHA
- 수정 커밋 목록 또는 수정 없음
- 전체 테스트 두 번의 결과
- Release 빌드 경고·오류 수
- 새 `.app`과 QA 보고서 경로
- Apple Calendar 실제 검증 결과
- Google Calendar 실제 검증 결과
- 남은 `BLOCKED`와 이유
- `origin/main` 푸시 여부
- "Windows에서 최신 main을 pull해 최종 Windows 회귀 검증을 시작해도 됨" 또는 "아직 Windows로 돌아가면 안 됨"

최종 상태가 성공이어도 태그, GitHub Release, 공개 배포는 만들지 않는다. Windows에서 동일한 최종 SHA를 다시 검증하기 전에는 v1.0을 확정하지 않는다.
