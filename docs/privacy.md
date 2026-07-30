---
layout: default
title: 개인정보처리방침
description: Timetable Generator의 로컬 데이터와 캘린더 내보내기 정보 처리 방침입니다.
permalink: /privacy/
---

<article class="policy-page" markdown="1">

# Timetable Generator 개인정보처리방침

<p class="policy-meta">시행일: 2026년 7월 21일 · 최종 수정일: 2026년 7월 30일</p>

Timetable Generator는 시간표를 구성하고 사용자가 요청한 내보내기 작업을 수행하는 데스크톱 앱입니다.  
앱은 사용자의 계정 정보·시간표·개인 일정을 개발자에게 전송하지 않으며 개발자 서버에도 저장하지 않습니다.
자체 계정·사용 분석·클라우드 동기화 서비스도 운영하지 않습니다.

<div class="policy-callout" markdown="1">
사용자의 시간표와 개인 일정은 기기에서 처리·보관됩니다.  
Google Calendar 또는 Apple Calendar 내보내기를 선택하고 권한을 승인한 경우에만 필요한 일정 정보가 외부 서비스로 전달됩니다.
</div>

## 1. 처리 목적

앱은 다음 목적으로만 정보를 처리합니다.

- 사용자가 만든 시간표, 과목·분반 선택과 개인 일정 저장
- 충돌 없는 시간표 계산과 PNG 내보내기
- 검증된 과목 카탈로그 다운로드와 로컬 캐시
- 사용자가 요청한 Google Calendar 또는 Apple Calendar 내보내기
- 화면 모드 설정 유지

## 2. 기기에 저장되는 정보

운영체제가 제공하는 로컬 애플리케이션 데이터 디렉터리의 `TimetableGenerator` 폴더에 다음 정보가 저장될 수 있습니다.

- 시간표 이름, 선택 과목·분반·선호 상태, 개인 일정과 마지막으로 선택한 후보 시간표
- 개인 일정의 제목, 요일, 시작·종료 시각과 선택적으로 입력한 분반·담당자·장소
- 마지막으로 검증한 전체 과목 카탈로그 캐시
- 화면 모드 설정(시스템 설정 사용·라이트·다크)

시간표 데이터는 암호화되지 않은 로컬 JSON 파일로 저장됩니다.  
저장 오류에 대비해 최근 시간표 저장본을 최대 5개 보관하며, 파일 정리 실패나 운영체제의 백업 정책에 따라 이전 파일이 더 오래 남을 수 있습니다.  
여러 사람이 같은 운영체제 계정이나 기기 백업을 공유한다면 시간표 이름과 개인 일정이 노출될 수 있으므로 민감한 내용을 입력하지 않는 것을 권장합니다.

앱을 제거해도 로컬 데이터가 남을 수 있습니다.  
완전히 삭제하려면 앱을 종료한 뒤 Windows에서는 `%LOCALAPPDATA%\TimetableGenerator`, macOS에서는 `~/Library/Application Support/TimetableGenerator` 폴더를 삭제하세요.

## 3. 네트워크 연결

앱은 다음 상황에서만 네트워크를 사용합니다.

- 설정된 HTTPS 주소에서 과목 카탈로그를 내려받을 때
- 사용자가 Google Calendar 내보내기를 실행하고 Google의 접근 승인을 진행할 때

과목 카탈로그를 내려받을 때 사용자의 시간표나 개인 일정은 전송하지 않습니다.  
호스팅 제공자와 Google은 IP 주소, 요청 시각 같은 일반적인 접속 정보를 각자의 정책에 따라 처리할 수 있습니다.

## 4. Google Calendar에서 요청하는 권한

Timetable Generator는 Google Calendar 내보내기를 선택한 경우에만 시스템 브라우저를 열고 다음 OAuth 범위를 요청합니다.

- `calendar.calendarlist.readonly`: 구독 중인 캘린더 목록의 메타데이터 조회
- `calendar.app.created`: 이 앱이 만든 보조 캘린더와 일정 관리

이름·이메일·프로필 범위는 요청하지 않습니다.  
인증 요청에는 임의의 `state` 값과 PKCE S256을 사용하며, 인증 결과는 기기의 `127.0.0.1` 임시 포트에서 받습니다.  
앱은 Google 비밀번호를 직접 받지 않습니다.

## 5. Google에서 읽고 사용하는 정보

캘린더 이름 충돌을 확인하기 위해 캘린더 목록의 ID, 기본 표시 이름과 사용자가 바꾼 표시 이름, 설명, 기본 캘린더 여부와 접근 권한을 읽습니다.

다른 일반 캘린더의 일정 내용은 읽지 않습니다.  
이름이 같은 캘린더가 앱이 만든 것인지 확인하거나 사용자가 **기존 캘린더 대체**를 선택한 경우에는 이벤트의 비공개 확장 속성으로 Timetable Generator가 만든 것으로 표시된 일정만 조회합니다.

이 캘린더 목록 메타데이터와 앱 관리 일정 정보는 내보내기 작업 동안 메모리에서만 사용하며 로컬 파일이나 개발자 서버에 저장하지 않습니다.

## 6. Google Calendar에 전송하거나 변경하는 정보

내보내기를 실행하면 다음 정보가 사용자 기기에서 Google Calendar API로 직접 전송될 수 있습니다.

- 시간표 이름, 로컬 시간대와 학기 반복 종료일
- 과목·개인 일정의 제목, 요일, 시작·종료 시각과 장소
- 과목 코드, 분반, 확인된 담당자
- 학교명과 학기를 포함한 캘린더 설명
- 이벤트의 비공개 확장 속성에 저장되는 시간표·일정 식별 정보
- Google Calendar 기본 알림 사용 설정

새 캘린더를 선택하면 보조 캘린더와 일정을 만듭니다.  
**기존 캘린더 대체**를 선택하면 해당 캘린더의 이름·설명·시간대를 갱신하고 필요한 앱 관리 일정을 만들거나 수정하며, 더 이상 시간표에 없는 앱 관리 일정은 삭제합니다.  
사용자가 직접 만든 일정, 기본 캘린더 또는 앱이 관리하지 않는 캘린더는 변경하지 않습니다.

Google API에서 받은 정보의 사용과 전송은 [Google API Services User Data Policy](https://developers.google.com/terms/api-services-user-data-policy)의 Limited Use 요구사항을 준수합니다.  
개발자는 Google 사용자 데이터를 광고, 판매, 신용 평가 또는 Timetable Generator의 캘린더 내보내기와 무관한 목적으로 사용하거나 제3자에게 이전하지 않습니다.

## 7. OAuth 토큰

액세스 토큰은 사용자가 요청한 내보내기 작업 동안 메모리에서만 사용하고 파일에 저장하지 않습니다.  
앱은 갱신 토큰(refresh token)을 요청하거나 저장하지 않으므로 다음 내보내기에서 다시 승인이 필요할 수 있습니다.  
개발자가 운영하는 중계 서버는 토큰이나 캘린더 내용을 받지 않습니다.

## 8. Apple Calendar

macOS에서 Apple Calendar 내보내기를 실행하면 앱은 운영체제의 캘린더 자동화 권한을 요청하고 로컬 Apple Calendar 앱에 시간표를 반영합니다.  
Timetable Generator 자체는 이 과정에서 외부 서버에 접속하지 않지만 Apple Calendar 앱은 사용자의 iCloud·Google 등 macOS 계정 설정에 따라 동기화할 수 있습니다.  
내보내기에 사용하는 임시 파일은 현재 사용자만 읽을 수 있으며 작업이 끝나면 삭제를 시도합니다.

캘린더 설명에는 학교명과 학기를 표시하고, 일정 URL에는 앱이 만든 일정을 구분하는 식별 정보를 저장합니다.  
**기존 캘린더 대체**를 선택하면 앱이 만든 일정만 새 시간표에 맞게 바꾸며 사용자가 직접 추가한 일정은 유지합니다.

## 9. 보관, 삭제와 권한 철회

- 로컬 데이터: 앱을 종료하고 Windows에서는 `%LOCALAPPDATA%\TimetableGenerator`, macOS에서는 `~/Library/Application Support/TimetableGenerator` 폴더를 삭제합니다.
- Google 접근 권한: [Google 계정의 연결된 앱 관리](https://myaccount.google.com/connections)에서 Timetable Generator의 접근 권한을 제거합니다.
- 내보낸 Google Calendar 데이터: 권한을 철회하거나 앱을 삭제해도 이미 만든 캘린더와 일정은 삭제되지 않습니다. Google Calendar에서 해당 보조 캘린더를 별도로 삭제해야 합니다.
- PNG 이미지: 저장할 때 선택한 위치에서 직접 삭제합니다.
- Apple Calendar 데이터: Calendar 앱에서 직접 삭제합니다.

## 10. 분석, 광고와 진단

현재 제품에는 광고, 사용 분석, 원격 사용 정보 수집 또는 외부 오류 보고 도구가 없습니다.  
앱은 별도의 제품 로그 파일을 만들지 않지만 운영체제가 충돌 진단 정보를 남길 수 있습니다.  
문제를 제보하기 전 로컬 경로·계정 이름·시간표 내용 등 불필요한 정보를 제거해야 합니다.

## 11. 이메일 문의

이메일로 문의하면 발신 이메일 주소와 사용자가 직접 작성한 문의 내용·첨부파일이 개발자에게 전달됩니다.
이 정보는 문의 내용을 확인하고 답변하는 용도로만 사용하며, 삭제를 원하면 같은 이메일 주소로 요청할 수 있습니다.
문의 메일은 발신자와 수신자가 사용하는 이메일 서비스의 시스템을 통해 전달·보관될 수 있습니다.

## 12. 외부 서비스

Google Calendar와 Apple Calendar에는 각 서비스 제공자의 정책이 적용됩니다.

- [Google 개인정보처리방침](https://policies.google.com/privacy)
- [Apple 개인정보 처리방침](https://www.apple.com/kr/legal/privacy/)

## 13. 방침 변경과 문의

기능 또는 데이터 처리 방식이 바뀌면 이 페이지의 최종 수정일과 내용을 갱신하고 릴리스 안내에 반영합니다.

일반 문의는 [{{ site.support_email }}]({{ site.support_mailto }})으로 보내 주세요.

개인정보 또는 보안 문제는 공개 이슈에 상세 내용을 올리지 말고 [GitHub 비공개 보안 제보](https://github.com/potterLim/timetable-generator/security/advisories/new)를 사용해 주세요.

</article>
