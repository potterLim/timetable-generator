# Google Calendar 연동 설정

이 문서는 개발자와 배포 담당자를 위한 설정 안내입니다.  
배포 파일에는 Timetable Generator를 식별하는 Google 데스크톱 OAuth 클라이언트 설정을 포함합니다.  
사용자는 내보내기를 시작할 때 자신의 Google 계정으로 직접 로그인하고 권한을 승인합니다.  
사용자 계정, 비밀번호, 액세스 토큰과 갱신 토큰은 저장소나 배포 파일에 포함하지 않습니다.

## 1. 배포자가 한 번 준비하는 Google Cloud 설정

1. [Google Cloud Console](https://console.cloud.google.com/)에서 이 제품의 배포용 프로젝트를 생성하거나 선택합니다. 개발·테스트용 프로젝트와 배포용 프로젝트를 분리합니다.
2. **API 및 서비스 > 라이브러리**에서 **Google Calendar API**를 찾아 사용 설정합니다.
3. **Google Auth Platform > 대상**에서 사용자 유형을 **외부**로 설정합니다.
4. 앱 이름을 `Timetable Generator`로 설정하고 실제 지원 이메일을 입력합니다.
5. **데이터 액세스**에 `.../auth/calendar.app.created`와 `.../auth/calendar.calendarlist.readonly` 두 범위를 추가합니다.
6. **클라이언트 > 클라이언트 만들기**에서 애플리케이션 유형을 **데스크톱 앱**으로 선택합니다.
7. 생성된 데스크톱 클라이언트의 `클라이언트 ID`와 `클라이언트 보안 비밀`을 확인합니다.
8. 개발 검증을 마치면 **대상 > 앱 게시**를 선택해 게시 상태를 **프로덕션**으로 전환합니다.

앱의 게시 상태에 따른 차이는 다음과 같습니다.

- **테스트**는 개발 중에만 사용하며 명시적으로 등록한 테스트 사용자만 로그인할 수 있습니다.
- **프로덕션**에서는 테스트 사용자 목록을 운영하지 않습니다. Google 계정을 가진 사용자가 앱에서 직접 로그인하고 동의합니다.
- 미검증 앱 화면이 표시되는 프로덕션 앱은 신규 사용자 100명 한도를 적용받을 수 있습니다.
- 경고와 사용자 제한을 제거하려면 Google의 브랜드 및 데이터 액세스 검증을 완료해야 합니다. 이 과정에서는 공개 홈 페이지와 개인정보처리방침, 관련 도메인 소유권 확인이 요구될 수 있습니다.

공식 릴리스에서는 테스트 사용자 방식이 아닌 프로덕션 게시 상태를 사용합니다.

데스크톱 앱에 포함된 클라이언트 보안 비밀은 설치된 파일에서 확인할 수 있으므로 서버 자격 증명처럼 기밀로 유지할 수 없습니다.  
이 값만으로는 사용자 캘린더에 접근할 수 없습니다.  
앱은 요청마다 PKCE(S256) 검증자와 챌린지를 만들어 승인 코드를 보호합니다. 인증 결과는 임의의 `127.0.0.1` 루프백 포트에서 받고, 응답의 무작위 `state` 값을 앱에서 검증합니다.  
설정 원본은 자격 증명을 교체하기 쉽도록 Git에서 추적하지 않습니다.  
**웹 애플리케이션 유형의 OAuth 클라이언트와 보안 비밀은 이 앱에 사용하지 않습니다.**

앱은 다음 두 범위만 요청합니다.

- `https://www.googleapis.com/auth/calendar.app.created`: 앱이 만든 캘린더와 일정 관리
- `https://www.googleapis.com/auth/calendar.calendarlist.readonly`: 같은 이름의 캘린더가 있는지 확인

## 2. 로컬 설정 준비

먼저 다음 경로에 도우미 입력용 임시 JSON 파일을 만듭니다.  
이 형식은 아직 앱이나 최종화 스크립트에서 사용하는 제품 설정이 아닙니다.

`src/TimetableGenerator.Desktop/google-calendar.local.json`

```json
{
  "clientId": "000000000000-example.apps.googleusercontent.com"
}
```

예시 `clientId`를 Google Cloud Console에서 발급한 실제 데스크톱 클라이언트 ID로 반드시 바꿉니다.  
`example`이 포함된 값은 최종화 단계에서 거부됩니다.

Google Cloud Console에서 같은 데스크톱 클라이언트의 보안 비밀만 클립보드에 복사한 직후 저장소 루트에서 다음 명령을 실행합니다.

```powershell
pwsh ./scripts/set-google-calendar-local-configuration.ps1
```

이 도우미는 파일의 `clientId`와 클립보드의 보안 비밀을 검증한 뒤 제품 설정 스키마 v2로 저장하고 클립보드를 비웁니다.  
보안 비밀은 명령줄 인수, 셸 기록이나 문서에 붙여 넣지 않습니다.  
기본 경로가 아닌 개발용 파일을 갱신할 때만 `-Path`로 경로를 지정합니다.

완성된 파일은 다음 형식입니다.

```json
{
  "schemaVersion": 2,
  "clientId": "000000000000-example.apps.googleusercontent.com",
  "clientSecret": "GOCSPX-example"
}
```

제품 설정 스키마 v2에서는 `schemaVersion`, `clientId`, `clientSecret` 세 속성만 허용합니다.  
`clientId`는 영문자·숫자·하이픈으로 된 식별자 뒤에 `.apps.googleusercontent.com`이 붙은 형식이어야 하며 예시 값은 사용할 수 없습니다.  
`clientSecret`은 비어 있지 않은 1,024자 이하 문자열이어야 하며 앞뒤 공백이나 제어 문자를 포함할 수 없습니다.  
알 수 없는 속성이나 잘못된 값이 있으면 앱은 설정을 사용하지 않습니다.  
이 파일은 `.gitignore`에 포함되어 있으며 존재할 때만 빌드·게시 출력으로 복사됩니다.

로컬 개발이나 진단에는 다음 환경 변수를 사용할 수 있습니다.  
공식 v2 구성과 현재 제품용 클라이언트에서는 두 값을 함께 설정하며, 환경 변수는 파일보다 우선합니다.

```text
TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_ID=000000000000-example.apps.googleusercontent.com
TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_SECRET=GOCSPX-example
```

환경 변수는 공식 릴리스 파일을 대신하지 않습니다.

## 3. 배포 환경 설정

공식 릴리스에서는 스키마 v2의 `google-calendar.local.json`을 게시 전에 준비해 제품에 포함합니다.  
최종화 단계는 이 파일이 없거나 형식이 올바르지 않으면 중단됩니다.

설정 원본은 Git 저장소나 공개 문서에 올리지 않으며 보안 비밀을 게시 명령의 인수나 빌드 로그로 전달하지 않습니다.  
액세스 토큰과 갱신 토큰은 어떤 경우에도 설정 파일이나 배포 파일에 포함하지 않습니다.

## 4. 사용자별 Google 로그인

1. 사용자가 Google Calendar 내보내기를 선택하면 시스템 기본 브라우저가 열립니다.
2. 사용자가 자신의 Google 계정을 선택하고 요청된 권한을 직접 승인합니다. 계정 비밀번호와 2단계 인증 정보는 Google 화면에서만 입력하며 앱은 이를 보거나 저장하지 않습니다.
3. 승인이 끝나면 브라우저에 완료 화면이 표시되고 앱이 내보내기를 계속합니다.
4. 앱은 액세스 토큰을 해당 내보내기 작업 동안 메모리에서만 사용해 캘린더와 일정을 반영합니다.
5. 반영에 성공하면 시스템 기본 브라우저로 Google Calendar를 엽니다. 여러 Google 계정에 로그인한 상태라면 계정 전환 메뉴에서 대상 계정을 확인합니다.
6. 앱은 갱신 토큰이나 시간표와 Google Calendar의 연결 정보를 저장하지 않습니다. 다음 내보내기에서 필요하면 승인을 다시 요청합니다.

사용자는 별도의 Google Cloud 프로젝트나 OAuth 클라이언트를 만들 필요가 없습니다.  
배포물에 포함된 제품용 데스크톱 클라이언트 설정은 앱을 식별하며 실제 데이터 접근 권한은 로그인한 사용자가 자신의 계정에 대해 승인합니다.

제품 설정이 없으면 브라우저나 네트워크 연결을 시작하지 않고 “Google 캘린더 연결을 아직 사용할 수 없습니다.”라는 안내를 표시합니다.

## 5. 내보내기 동작 확인

다음 항목을 Windows와 macOS에서 각각 확인합니다.

1. 시간표 이름과 같은 이름의 별도 Google 캘린더가 생성되는지 확인합니다.
2. 같은 이름의 대체 가능한 앱 관리 캘린더가 정확히 하나 있으면 기존 캘린더 대체와 번호를 붙인 새 캘린더 생성 중에서 선택할 수 있는지 확인합니다.
3. 앱이 만든 보조 캘린더만 대체할 수 있고, 기본 캘린더나 사용자가 직접 만든 캘린더에는 대체 선택을 제공하지 않는지 확인합니다.
4. 월·목처럼 여러 요일의 같은 일정이 하나의 반복 일정과 `BYDAY`로 내보내지는지 확인합니다.
5. 일정이 기기의 현재 시간대에 맞게 내보내지고 선택한 학기의 시작일과 종료일 사이에서만 반복되는지 확인합니다.
6. 기존 캘린더를 대체하면 앱이 만든 일정만 조정되는지 확인합니다. 사용자가 직접 만든 일정은 삭제하지 않아야 합니다.
7. 로그인이나 권한 승인을 취소해도 오류 안내가 남지 않고 앱을 계속 사용할 수 있는지 확인합니다. 네트워크 끊김과 브라우저 시작 실패에는 적절한 오류 안내가 표시되는지도 확인합니다.

관련 Google 문서:

- [OAuth 2.0 for Desktop Apps](https://developers.google.com/identity/protocols/oauth2/native-app)
- [OAuth 앱 상태별 동작](https://developers.google.com/identity/protocols/oauth2/production-readiness/overview)
- [Google Auth Platform 대상 및 게시 상태](https://support.google.com/cloud/answer/15549945)
- [OAuth 프로덕션 준비 정책](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance)
- [Create events](https://developers.google.com/workspace/calendar/api/guides/create-events)
- [Recurring events](https://developers.google.com/workspace/calendar/api/guides/recurringevents)
