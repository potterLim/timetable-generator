# Timetable Generator

한동대학교 개설 과목과 개인 일정을 조합해 수강 조건에 맞는 시간표를 만듭니다.  
여러 후보를 비교하고 이미지나 캘린더로 내보낼 수 있는 Windows·macOS 데스크톱 앱입니다.

[제품 안내](https://potterlim.github.io/timetable-generator/) · [최신 버전 다운로드](https://github.com/potterLim/timetable-generator/releases)

> Timetable Generator는 한동대학교가 개발하거나 보증하는 공식 서비스가 아닙니다.  
> 실제 수강 신청 전에는 과목 정보와 수강 가능 여부를 학교의 공식 안내에서 확인하세요.

## 주요 기능

- 과목명·과목 코드·교수 검색과 개설 단위·이수 구분 필터
- 분반별 `선호`·`가능`·`제외` 조건을 반영해 최대 24개의 시간표 후보 자동 구성
- 여러 과목 중 하나만 고르는 수강 선택 기능
- 여러 시간표를 이름별로 관리하고 과목 조건과 개인 일정을 각각 저장
- 수업 시간이 제공되지 않은 분반의 별도 표시
- 현재 시간표 한 장 또는 모든 가능한 시간표를 PNG로 저장
- Windows와 macOS에서 Google Calendar로 내보내기
- macOS에서 Apple Calendar로 내보내기
- 자동 저장과 `시스템 설정 사용`·`라이트`·`다크` 화면 모드

## 지원 환경

| 운영체제 | 공식 지원 대상 | 캘린더 내보내기 |
| --- | --- | --- |
| Windows | Windows 11 x64 | Google Calendar |
| macOS | macOS 14 이상, Apple Silicon | Google Calendar, Apple Calendar |

다운로드와 설치, 자세한 사용 방법은 [Timetable Generator 제품 페이지](https://potterlim.github.io/timetable-generator/)에서 확인할 수 있습니다.

## 개발 및 빌드

### 요구 사항

- .NET SDK 10.0.301

저장소 루트에서 다음 명령을 실행합니다.

```bash
dotnet restore TimetableGenerator.sln --locked-mode
dotnet build TimetableGenerator.sln --configuration Release --no-restore
dotnet test TimetableGenerator.sln --configuration Release --no-build --no-restore
```

카탈로그와 Google Calendar 연결에 필요한 로컬 설정 파일은 Git에서 추적하지 않습니다.  
실행 환경 구성과 배포 절차는 [데스크톱 배포 안내](docs/distribution.md)를 참고하세요.

## 프로젝트 구조

- `TimetableGenerator.Domain`: 강타입 도메인 모델과 시간표 규칙
- `TimetableGenerator.Application`: 시간표 편집과 추천 기능
- `TimetableGenerator.CatalogJson`: 과목 카탈로그 JSON 계약
- `TimetableGenerator.Infrastructure`: 원격 데이터 검증, 로컬 캐시와 영속화
- `TimetableGenerator.Desktop`: Avalonia 기반 Windows·macOS 데스크톱 앱
- `TimetableGenerator.HandongCatalogGenerator`: 학교 원본을 정규화된 과목 카탈로그로 변환하는 운영 도구
- `tests`: 계층별 단위·통합·렌더링 테스트

## 개발자 및 배포 담당자 문서

- [데스크톱 빌드 및 배포](docs/distribution.md)
- [카탈로그 생성기](tools/TimetableGenerator.HandongCatalogGenerator/README.md)
- [Google Calendar 연동 설정](docs/google-calendar-integration-setup.md)

## 지원 및 정책

설치·사용 문제는 [지원 안내](SUPPORT.md), 보안 문제는 [보안 정책](SECURITY.md)을 참고하세요.  
개인정보와 이용 조건은 제품 페이지의 [개인정보처리방침](https://potterlim.github.io/timetable-generator/privacy/)과 [이용약관](https://potterlim.github.io/timetable-generator/terms/)에서 확인할 수 있습니다.

## 라이선스

이 저장소는 별도의 오픈 소스 라이선스를 제공하지 않습니다.  
명시적인 허가 없이 소스 코드와 배포 파일의 복제·수정·재배포 권한은 부여되지 않습니다.

앱과 함께 배포되는 제3자 구성 요소의 라이선스와 고지는 배포물의 `ThirdPartyNotices`에서 확인할 수 있습니다.
