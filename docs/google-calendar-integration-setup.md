# Google Calendar 연동 설정

이 문서는 시간표 앱의 Google Calendar 내보내기를 개발·배포 환경에서 활성화하는 절차를 설명합니다. OAuth 클라이언트 비밀 값이나 사용자 토큰은 저장소에 커밋하지 않습니다.

## 1. Google Cloud 준비

1. [Google Cloud Console](https://console.cloud.google.com/)에서 이 제품용 프로젝트를 생성하거나 선택합니다.
2. **API 및 서비스 > 라이브러리**에서 **Google Calendar API**를 찾아 사용 설정합니다.
3. **Google Auth Platform**에서 앱 이름, 지원 이메일, 대상 사용자를 설정합니다.
4. **데이터 액세스**에 `.../auth/calendar.app.created`와 `.../auth/calendar.calendarlist.readonly` 두 범위를 추가합니다.
5. 앱이 테스트 상태라면 실제로 로그인할 Google 계정을 테스트 사용자에 추가합니다.
6. **클라이언트 > 클라이언트 만들기**에서 애플리케이션 유형을 **데스크톱 앱**으로 선택합니다.
7. 생성된 `클라이언트 ID`만 복사합니다. 이 네이티브 앱 흐름에는 클라이언트 보안 비밀이 필요하지 않으며, 보안 비밀을 앱 파일에 넣어서는 안 됩니다.

앱은 시스템 기본 브라우저, PKCE(S256), 임의의 `127.0.0.1` 루프백 포트를 사용합니다. 요청 범위는 앱이 만든 캘린더와 일정에 접근하는 `https://www.googleapis.com/auth/calendar.app.created`와, 로컬 연결 정보가 사라졌을 때 앱이 만든 캘린더를 다시 찾기 위한 읽기 전용 `https://www.googleapis.com/auth/calendar.calendarlist.readonly`입니다.

## 2. 개발 환경 설정

다음 파일을 새로 만듭니다.

`src/TimetableGenerator.Desktop/google-calendar.local.json`

```json
{
  "schemaVersion": 1,
  "clientId": "000000000000-example.apps.googleusercontent.com"
}
```

허용되는 속성은 `schemaVersion`과 `clientId` 두 개뿐입니다. 알 수 없는 속성이나 잘못된 값이 있으면 앱은 설정을 사용하지 않습니다. 이 파일은 `.gitignore`에 포함되어 있으며, 존재할 때만 빌드·게시 출력으로 복사됩니다.

파일 대신 다음 환경 변수를 사용할 수도 있습니다. 환경 변수가 있으면 파일보다 우선합니다.

```text
TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_ID=000000000000-example.apps.googleusercontent.com
```

## 3. 배포 환경 설정

공개 저장소나 닷홈에 OAuth 클라이언트 ID 설정 파일을 올리지 않습니다. 데스크톱 배포물을 만들 때는 다음 중 한 방법을 사용합니다.

1. `dotnet publish` 전에 무시된 `src/TimetableGenerator.Desktop/google-calendar.local.json`을 만들어 Content 항목으로 게시 출력에 복사합니다.
2. 게시가 끝난 뒤 실행 파일과 같은 디렉터리에 `google-calendar.local.json`을 sidecar 파일로 주입합니다.
3. 최종 사용자의 앱 실행 환경에 `TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_ID`를 설정합니다.

환경 변수는 앱 실행 시 읽습니다. 따라서 게시 파이프라인 프로세스에 환경 변수만 설정해도 클라이언트 ID가 산출물에 자동으로 포함되지는 않습니다.

Google Cloud의 데스크톱 OAuth 클라이언트 ID 자체는 비밀 값이 아니지만, 저장소와 배포 대상의 결합을 피하기 위해 제품별 배포 설정으로 관리합니다. 클라이언트 보안 비밀, 액세스 토큰, 새로 고침 토큰은 어떤 경우에도 배포 파일에 포함하지 않습니다.

## 4. 최초 연결과 저장 위치

1. 사용자가 Google Calendar 내보내기를 처음 선택하면 시스템 기본 브라우저가 열립니다.
2. Google 로그인과 동의가 끝나면 브라우저가 로컬 앱의 일회성 루프백 주소로 돌아옵니다.
3. 액세스 토큰은 메모리에만 유지합니다.
4. 새로 고침 토큰은 Windows Credential Manager 또는 macOS Keychain에 저장합니다.
5. 계획 ID와 Google 캘린더 ID의 비밀이 아닌 연결 정보만 제품 데이터 디렉터리에 저장합니다.

로컬 설정이 없으면 앱은 브라우저나 네트워크를 열지 않고 `NotConfigured` 결과를 반환합니다. 지원하지 않는 운영체제에서는 보안 자격 증명 저장소를 사용할 수 없으므로 연동이 안전하게 실패합니다.

## 5. 내보내기 동작 확인

다음 항목을 Windows와 macOS에서 각각 확인합니다.

1. 계획 이름과 같은 이름의 별도 Google 캘린더가 생성되는지 확인합니다.
2. 같은 계획을 다시 내보내도 캘린더와 일정이 중복 생성되지 않는지 확인합니다.
3. 계획 이름을 바꾼 뒤 다시 내보내면 기존 캘린더 이름이 갱신되는지 확인합니다.
4. 월·목처럼 여러 요일의 같은 일정이 하나의 반복 일정과 `BYDAY`로 내보내지는지 확인합니다.
5. 한국 시간 일정에 `Asia/Seoul`과 `+09:00`이 적용되고, 2026-08-31부터 2026-12-20까지만 반복되는지 확인합니다.
6. 앱에서 일정을 수정하거나 삭제한 뒤 다시 내보내면 앱이 만든 일정만 조정되는지 확인합니다. 사용자가 직접 만든 일정은 삭제하지 않아야 합니다.
7. 네트워크 끊김, 로그인 취소, 권한 거부, 브라우저 시작 실패가 각각 오류로 처리되고 앱이 멈추지 않는지 확인합니다.

관련 Google 문서:

- [OAuth 2.0 for Desktop Apps](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Create events](https://developers.google.com/workspace/calendar/api/guides/create-events)
- [Recurring events](https://developers.google.com/workspace/calendar/api/guides/recurringevents)
