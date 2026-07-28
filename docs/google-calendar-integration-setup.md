# Google Calendar 연동 설정

이 문서는 Timetable Generator의 Google Calendar 내보내기를 개발·배포 환경에서 활성화하는 절차를 설명합니다. 배포자는 제품을 식별하는 Google Desktop OAuth 클라이언트 자격 증명을 앱에 포함하며, 실제 사용자는 내보낼 때 자신의 Google 계정으로 직접 로그인하고 권한을 승인합니다. 사용자 계정, 비밀번호, 액세스 토큰과 새로 고침 토큰은 저장소와 배포물에 포함하지 않습니다.

## 1. 배포자가 한 번 준비하는 Google Cloud 설정

1. [Google Cloud Console](https://console.cloud.google.com/)에서 이 제품의 Release 전용 프로젝트를 생성하거나 선택합니다. 개발·테스트용 프로젝트와 Release용 프로젝트를 분리합니다.
2. **API 및 서비스 > 라이브러리**에서 **Google Calendar API**를 찾아 사용 설정합니다.
3. **Google Auth Platform > 대상**에서 사용자 유형을 **외부**로 설정합니다.
4. 앱 이름을 `Timetable Generator`로 설정하고 실제 지원 이메일을 입력합니다.
5. **데이터 액세스**에 `.../auth/calendar.app.created`와 `.../auth/calendar.calendarlist.readonly` 두 범위를 추가합니다.
6. **클라이언트 > 클라이언트 만들기**에서 애플리케이션 유형을 **데스크톱 앱**으로 선택합니다.
7. 생성된 Desktop 클라이언트의 `클라이언트 ID`와 `클라이언트 보안 비밀`을 모두 복사합니다. 현재 생성된 클라이언트는 승인 코드를 토큰으로 교환할 때 두 값을 모두 요구합니다.
8. 개발 검증을 마치면 **대상 > 앱 게시**를 선택해 게시 상태를 **프로덕션**으로 전환합니다.

앱의 게시 상태에 따른 차이는 다음과 같습니다.

- **테스트**는 개발 중에만 사용합니다. 명시적으로 등록한 테스트 사용자만 로그인할 수 있고 사용자 승인도 7일 후 만료됩니다.
- **프로덕션**에서는 테스트 사용자 목록을 운영하지 않습니다. Google 계정을 가진 사용자가 앱에서 직접 로그인하고 동의합니다.
- 민감한 범위에 대한 검증을 마치지 않은 프로덕션 앱은 Google의 미검증 앱 경고와 누적 100명 사용자 제한을 받을 수 있습니다. 지인 대상 시험 배포에는 사용할 수 있지만 완성된 제품 경험으로 보기는 어렵습니다.
- 경고와 사용자 제한을 제거하려면 Google의 브랜드 및 데이터 액세스 검증을 완료해야 합니다. 이 과정에서는 공개 홈 페이지와 개인정보처리방침, 관련 도메인 소유권 확인이 요구될 수 있습니다.

따라서 지인의 계정을 사전에 테스트 사용자로 등록하는 방식은 Release 운영 정책으로 사용하지 않습니다. 각 사용자가 앱의 Google Calendar 내보내기를 선택한 뒤 자신의 계정으로 로그인하는 것이 최종 사용자 흐름입니다.

Desktop 앱은 사용자가 실행 파일과 설정을 열어 볼 수 있으므로 함께 배포하는 Desktop 클라이언트 보안 비밀은 서버 비밀처럼 보호할 수 있는 기밀 보안 경계가 아닙니다. 이 값만으로 사용자 캘린더에 접근할 수도 없습니다. 승인 코드 가로채기를 방어하는 핵심은 요청마다 생성하는 PKCE(S256) 검증자와 임의의 `127.0.0.1` 루프백 포트입니다. 그렇더라도 자격 증명 수명 주기와 저장소 공개 범위를 분리하기 위해 설정 원본은 Git에 추적하지 않습니다. **웹 애플리케이션 유형의 OAuth 클라이언트와 그 보안 비밀은 이 앱에 절대 사용하지 않습니다.**

앱은 시스템 기본 브라우저, PKCE(S256), 임의의 `127.0.0.1` 루프백 포트를 사용합니다. 요청 범위는 앱이 만든 캘린더와 일정에 접근하는 `https://www.googleapis.com/auth/calendar.app.created`와, 같은 이름의 캘린더가 있는지 확인하는 읽기 전용 `https://www.googleapis.com/auth/calendar.calendarlist.readonly`입니다.

## 2. 개발 환경 설정

Google Cloud Console에서 Desktop 클라이언트 보안 비밀만 클립보드에 복사한 직후 저장소 루트에서 다음 명령을 실행합니다.

```powershell
pwsh ./scripts/set-google-calendar-local-configuration.ps1
```

이 도우미는 기존 미추적 로컬 설정의 `clientId`와 클립보드의 보안 비밀을 검증한 뒤 파일을 스키마 v2로 갱신하고 클립보드를 비웁니다. 보안 비밀을 명령줄 인수, 셸 기록이나 문서에 붙여 넣지 않습니다. 기본 경로가 아닌 개발용 파일을 갱신할 때만 `-Path`로 그 파일을 지정합니다.

도우미가 갱신하는 기본 파일은 다음과 같습니다.

`src/TimetableGenerator.Desktop/google-calendar.local.json`

```json
{
  "schemaVersion": 2,
  "clientId": "000000000000-example.apps.googleusercontent.com",
  "clientSecret": "GOCSPX-example"
}
```

제품 설정 스키마 v2에서 허용되는 속성은 `schemaVersion`, `clientId`, `clientSecret` 세 개뿐입니다. `clientSecret`은 비어 있지 않은 1,024자 이하 문자열이어야 하며 앞뒤 공백과 제어 문자를 포함할 수 없습니다. 알 수 없는 속성이나 잘못된 값이 있으면 앱은 설정을 사용하지 않습니다. 이 파일은 `.gitignore`에 포함되어 있으며, 존재할 때만 빌드·게시 출력으로 복사됩니다.

앱은 기존 개발 환경의 단계적 이전을 위해 v1 파일도 읽을 수 있지만, v1에는 토큰 교환에 필요한 `clientSecret`이 없어 현재 제품용 클라이언트로는 인증을 완료할 수 없습니다. 최종 Release 검증기는 정확한 세 속성을 가진 v2 파일만 허용합니다.

파일 대신 다음 환경 변수를 사용할 수도 있습니다. 환경 변수가 있으면 파일보다 우선합니다.

```text
TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_ID=000000000000-example.apps.googleusercontent.com
TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_SECRET=GOCSPX-example
```

환경 변수 방식의 제품 설정에는 두 값을 함께 지정합니다. 보안 비밀만 있거나 값의 형식이 올바르지 않으면 설정을 사용하지 않습니다. 클라이언트 ID만 지정한 기존 개발 환경은 v1 호환 구성으로 읽지만, 현재 제품용 클라이언트의 토큰 교환에는 보안 비밀도 필요하므로 인증을 완료할 수 없습니다.

## 3. 배포 환경 설정

공개 저장소나 닷홈에 OAuth 설정 원본을 올리지 않습니다. 데스크톱 배포물을 만들 때는 다음 중 한 방법을 사용합니다.

1. `dotnet publish` 전에 무시된 `src/TimetableGenerator.Desktop/google-calendar.local.json`을 만들어 Content 항목으로 게시 출력에 복사합니다.
2. 게시가 끝난 뒤 실행 파일과 같은 디렉터리에 `google-calendar.local.json`을 sidecar 파일로 주입합니다.
3. 최종 사용자의 앱 실행 환경에 `TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_ID`와 `TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_SECRET`을 함께 설정합니다.

파일 방식을 사용할 때는 `set-google-calendar-local-configuration.ps1`로 생성한 v2 파일을 게시 전에 준비합니다. 보안 비밀을 게시 명령의 인수나 빌드 로그로 전달하지 않습니다.

환경 변수는 앱 실행 시 읽습니다. 따라서 게시 파이프라인 프로세스에 환경 변수만 설정해도 자격 증명이 산출물에 자동으로 포함되지는 않습니다.

Google Desktop OAuth 클라이언트 ID와 보안 비밀은 모두 미추적 sidecar 설정으로 관리하고 최종 Release 산출물에는 포함합니다. Desktop 클라이언트 보안 비밀은 설치된 네이티브 앱에서 기밀로 유지할 수 있는 값이 아니지만, Git 이력에 고정하지 않으면 자격 증명 교체와 배포 대상 분리가 쉬워집니다. 액세스 토큰과 새로 고침 토큰은 어떤 경우에도 설정 파일이나 배포물에 포함하지 않습니다. 웹 애플리케이션 클라이언트의 보안 비밀은 서버에서만 보관해야 하므로 이 sidecar에 넣어서는 안 됩니다.

## 4. 사용자별 Google 로그인

1. 사용자가 Google Calendar 내보내기를 선택하면 시스템 기본 브라우저가 열립니다.
2. 사용자가 자신의 Google 계정을 선택하고 요청된 권한을 직접 승인합니다. 계정 비밀번호와 2단계 인증 정보는 Google 화면에서만 입력하며 앱은 이를 보거나 저장하지 않습니다.
3. Google 로그인과 동의가 끝나면 브라우저가 로컬 앱의 일회성 루프백 주소로 돌아옵니다. 승인 완료 페이지는 주소 표시줄과 현재 방문 기록에서 인증 코드와 상태 값을 즉시 제거하고 자동으로 닫히기를 시도합니다.
4. 앱은 액세스 토큰을 해당 내보내기 작업 동안 메모리에서만 사용해 캘린더와 일정을 반영합니다.
5. 실제 반영이 성공한 경우에만 시스템 기본 브라우저로 Google Calendar를 엽니다. 브라우저에 여러 Google 계정이 로그인되어 있으면 웹 화면의 활성 계정과 방금 승인한 계정이 다를 수 있으므로, 이때는 Google Calendar의 계정 전환 메뉴에서 대상 계정을 확인합니다.
6. 앱은 새로 고침 토큰이나 시간표와 Google 캘린더의 연결 정보를 저장하지 않습니다. 다음 내보내기에서는 필요할 때 Google 인증 흐름을 다시 시작합니다.

사용자는 별도의 Google Cloud 프로젝트나 OAuth 클라이언트 자격 증명을 만들 필요가 없습니다. 배포물에 포함된 제품용 Desktop 클라이언트 자격 증명은 앱을 식별하며 토큰 교환 요청에 함께 제출됩니다. 실제 데이터 접근 권한은 로그인한 각 사용자가 자신의 계정에 대해 승인합니다.

로컬 설정이 없으면 앱은 브라우저나 네트워크를 열지 않고 `NotConfigured` 결과를 반환합니다.

## 5. 내보내기 동작 확인

다음 항목을 Windows와 macOS에서 각각 확인합니다.

1. 시간표 이름과 같은 이름의 별도 Google 캘린더가 생성되는지 확인합니다.
2. 같은 이름의 캘린더가 있으면 기존 캘린더 대체와 번호를 붙인 새 캘린더 생성 중에서 선택할 수 있는지 확인합니다.
3. 앱이 만든 보조 캘린더만 대체할 수 있고, 기본 캘린더나 사용자가 직접 만든 캘린더에는 대체 선택을 제공하지 않는지 확인합니다.
4. 월·목처럼 여러 요일의 같은 일정이 하나의 반복 일정과 `BYDAY`로 내보내지는지 확인합니다.
5. 일정이 기기의 현재 시간대에 맞게 내보내지는지 확인합니다. 한국 환경에서는 `Asia/Seoul`과 `+09:00`이 적용되고, 2026-08-31부터 2026-12-20까지만 반복되어야 합니다.
6. 기존 캘린더를 대체하면 앱이 만든 일정만 조정되는지 확인합니다. 사용자가 직접 만든 일정은 삭제하지 않아야 합니다.
7. 네트워크 끊김, 로그인 취소, 권한 거부, 브라우저 시작 실패가 각각 오류로 처리되고 앱이 멈추지 않는지 확인합니다.

관련 Google 문서:

- [OAuth 2.0 for Desktop Apps](https://developers.google.com/identity/protocols/oauth2/native-app)
- [OAuth 앱 상태별 동작](https://developers.google.com/identity/protocols/oauth2/production-readiness/overview)
- [Google Auth Platform 대상 및 게시 상태](https://support.google.com/cloud/answer/15549945)
- [OAuth 프로덕션 준비 정책](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance)
- [Create events](https://developers.google.com/workspace/calendar/api/guides/create-events)
- [Recurring events](https://developers.google.com/workspace/calendar/api/guides/recurringevents)
