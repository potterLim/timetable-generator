# Timetable Generator

학교가 게시한 검증 가능한 과목 카탈로그에서 과목을 찾고, 시간이 겹치지 않는 분반 조합을 비교해 수강 계획을 만드는 Windows·macOS 데스크톱 앱입니다. 사용자가 CSV를 만들거나 학교 원본 파일의 열 구조를 이해할 필요가 없습니다.

이 저장소는 한동대학교의 공식 서비스가 아닌 개인 프로젝트입니다. 학교의 학사 운영, 수강 신청 결과 또는 게시 데이터의 완전성을 보증하지 않으므로 최종 수강 정보는 학교의 공식 안내에서 다시 확인해야 합니다.

자세한 제품 사용 방법은 [사용 설명서](instruction.md), 학교 원본 `.xls`를 배포 카탈로그로 만드는 절차는 [카탈로그 생성기 안내](tools/TimetableGenerator.HandongCatalogGenerator/README.md)를 참고하세요.

개인정보와 로컬 데이터 처리는 [개인정보처리방침](PRIVACY.md), 제품 이용 조건은 [서비스 이용약관](TERMS.md), 보안 문제 제보는 [보안 정책](SECURITY.md), 일반적인 사용·설치 문제는 [지원 안내](SUPPORT.md)를 참고하세요.

## 제품 경험

- 과목명·과목 코드·교수 검색과 학부·이수 구분 필터
- 분반별 `선호`·`가능`·`제외` 설정을 반영한 최대 24개의 충돌 없는 추천 시간표
- 서로 다른 과목을 `이 중 하나`로 묶어 정확히 한 과목만 배치하는 수강 선택
- 계획마다 독립적으로 저장되고 추천과 PNG에 함께 반영되는 개인 일정
- 이름을 붙일 수 있는 여러 계획과 원자적 자동 저장
- 학교가 시간을 제공하지 않은 분반의 명시적인 분리 표시
- 현재 또는 모든 가능한 시간표의 고해상도 PNG 저장과 Google Calendar 내보내기
- macOS에서 Apple Calendar로 직접 내보내기
- 검증한 카탈로그의 로컬 캐시와 오프라인 재실행
- 좁은 창에서도 과목 목록과 계획 패널을 열고 닫을 수 있는 반응형 Avalonia UI
- 월–금과 11:00–19:00를 기본으로 일정에 따라 주말·이른 시간·늦은 시간까지 확장되고, 일정이 있으면 가장 이른 일정이 속한 정각보다 30분 앞에서 시작하는 실제 시간 축
- 앱에 포함된 Pretendard 글꼴을 사용하는 일관된 Windows·macOS 타이포그래피

개인 일정은 해당 계획의 모든 추천에서 고정된 일정으로 취급합니다. 시간 미정 분반은 계획에서 보존하지만 충돌 자동 검증에는 포함하지 않으며, 앱은 이 분반을 충돌이 없다고 추측하지 않습니다.

## 카탈로그 전달 모델

첫 실행에서 앱은 설정된 `index.json`을 읽고, 선택된 revision의 카탈로그를 내려받아 파일 크기와 SHA-256 및 JSON 계약을 검증한 뒤 로컬 캐시에 원자적으로 설치합니다. 검증이 실패하면 기존 캐시와 계획을 바꾸지 않습니다. 캐시가 있으면 네트워크 없이도 마지막으로 검증한 카탈로그를 사용할 수 있습니다.

카탈로그 주소는 소스 코드에 넣지 않습니다. 다음 두 방법 중 하나로 설치 환경에서 제공합니다. 환경 변수가 로컬 파일보다 우선합니다.

### 환경 변수

```powershell
$env:TIMETABLE_GENERATOR_CATALOG_INDEX_URI = "https://catalog.example.edu/timetable-generator/catalog/v1/index.json"
dotnet run --project .\src\TimetableGenerator.Desktop\TimetableGenerator.Desktop.csproj
```

```bash
TIMETABLE_GENERATOR_CATALOG_INDEX_URI="https://catalog.example.edu/timetable-generator/catalog/v1/index.json" \
  dotnet run --project ./src/TimetableGenerator.Desktop/TimetableGenerator.Desktop.csproj
```

### 로컬 배포 설정

`src/TimetableGenerator.Desktop/catalog-source.local.json`을 아래 형식으로 만듭니다.

```json
{
  "schemaVersion": 1,
  "indexUri": "https://catalog.example.edu/timetable-generator/catalog/v1/index.json"
}
```

이 파일은 Git에서 무시되며, 존재할 때만 빌드·게시 출력에 복사됩니다. 배포 전에 게시 디렉터리의 값을 확인하세요. 카탈로그 URL은 앱이 접속하려면 사용자 기기에서 확인 가능한 정보이므로 비밀 키로 취급하면 안 됩니다.

## 개발

### 요구 사항

- .NET SDK 10.0.301 (`global.json`이 이 버전을 정확히 고정합니다.)
- Windows 10/11 x64 또는 macOS 14 이상

저장소 루트에서 다음 명령을 실행합니다.

```powershell
dotnet restore TimetableGenerator.sln --locked-mode
dotnet build TimetableGenerator.sln --configuration Release --no-restore
dotnet test TimetableGenerator.sln --configuration Release --no-restore
dotnet run --project .\src\TimetableGenerator.Desktop\TimetableGenerator.Desktop.csproj
```

macOS에서는 경로 구분자만 `/`로 바꾸면 같은 명령을 사용할 수 있습니다.

패키지 버전은 `Directory.Packages.props`에서만 변경합니다. 의도적으로 의존성을 갱신할 때는 `dotnet restore TimetableGenerator.sln --force-evaluate`로 프로젝트별 `packages.lock.json`을 갱신하고, lock 변경과 전이 의존성을 검토합니다.

## 게시

게시 산출물은 Git에서 무시되는 `artifacts/publish` 아래에 만듭니다. 전 대상의 self-contained 제품 archive와 SHA-256을 한 번에 만들려면 다음 명령을 사용합니다.

```powershell
pwsh ./scripts/publish-desktop.ps1
```

스크립트는 Windows x64 PE와 macOS Intel·Apple Silicon Mach-O 아키텍처, self-contained 런타임, `.app` 구조와 디버그 심볼 제외 여부를 검증합니다. Windows 아이콘과 manifest는 Windows 대상에만 연결되므로 macOS 교차 게시를 오염시키지 않습니다.

만들어진 플랫폼별 archive는 인증서가 없는 개발 환경에서도 검사할 수 있는 unsigned 산출물입니다. 공개 전에 Windows 코드 서명과 실제 기기 검사, macOS Developer ID 서명·hardened runtime·notarization·stapling이 별도로 필요합니다. 서명 후 `finalize-desktop-release.ps1`로 최종 ZIP과 checksum을 다시 만들며, 전체 명령과 검증 경계는 [데스크톱 제품 배포 안내](docs/distribution.md)를 참고하세요.

배포 설정 파일을 포함할 경우 URL이 올바른지, 실제 카탈로그의 SHA-256과 `index.json`이 일치하는지 함께 확인합니다.

## 구조

```text
src/
├── TimetableGenerator.Domain/          강타입 도메인과 불변 계획 모델
├── TimetableGenerator.Application/     계획 편집과 추천 유스케이스
├── TimetableGenerator.CatalogJson/     엄격한 카탈로그 JSON 계약
├── TimetableGenerator.Infrastructure/  원격 검증, 캐시, 원자적 영속화
└── TimetableGenerator.Desktop/         Avalonia 제품 UI와 플랫폼 진입점
tests/                                  계층별 단위·통합·렌더링 테스트
tools/TimetableGenerator.HandongCatalogGenerator/
                                        학교 원본을 정규화하는 운영 도구
```

사용자 계획과 카탈로그 캐시는 `Environment.SpecialFolder.LocalApplicationData` 아래의 `TimetableGenerator` 디렉터리에 저장됩니다. 저장소에는 원본 `.xls`, 생성된 카탈로그 JSON, 실제 서비스 주소, 사용자 계획을 커밋하지 않습니다.

## 라이선스

이 프로젝트 자체에는 별도의 라이선스를 제공하지 않습니다. 앱에 포함된 자산, UI 프레임워크, native 구성 요소와 self-contained .NET runtime의 제3자 라이선스·notice 원문은 `ThirdPartyNotices`에 모아 배포 산출물과 함께 제공합니다.
