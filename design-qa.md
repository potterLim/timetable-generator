# 제품 디자인 QA

## 비교 자료

- 기준 시안: `docs/design/planning-workspace-target.png`
- 기존 Windows 문제 화면: `tests/TimetableGenerator.Desktop.Tests/TestResults/product-design-audit-2026-07-16-theme/01-current-titlebar.png`
- 최종 시스템·다크 화면: `tests/TimetableGenerator.Desktop.Tests/TestResults/product-design-audit-2026-07-16-theme/16-published-final-system-dark.png`
- 최종 화면 모드 메뉴: `tests/TimetableGenerator.Desktop.Tests/TestResults/product-design-audit-2026-07-16-theme/17-published-final-system-flyout.png`
- 최종 라이트 복원 화면: `tests/TimetableGenerator.Desktop.Tests/TestResults/product-design-audit-2026-07-16-theme/18-published-final-light-persisted.png`
- 최종 비교 해상도: 1802 × 961 px

## 검증 결과

- 정보 구조: 과목 탐색, 추천 시간표, 내 계획의 3개 작업 영역과 시각적 우선순위가 기준 시안과 일치한다.
- 왼쪽 위 구성: 앱 아이콘, 제품명, 학교·학기만 두 줄로 묶었다. 중복 네이티브 제목, 불필요한 구분선, 상시 자동 저장 표시는 제거해 창 정체성을 한눈에 읽을 수 있다.
- 오른쪽 위 구성: 기능을 알 수 없던 도움말과 전체화면 표시는 제거했다. 현재 선택한 화면 모드를 나타내는 아이콘 하나와 Windows 기본 최소화·최대화·닫기 버튼만 남겼다.
- 창 버튼: 실제 게시된 Windows 실행 파일에서 최소화, 최대화, 닫기 영역이 각각 `WM_NCHITTEST`의 `HTMINBUTTON(8)`, `HTMAXBUTTON(9)`, `HTCLOSE(20)`으로 확인된다.
- 창 이동: 헤더의 비조작 영역은 `HTCAPTION(2)`, 화면 모드 버튼은 `HTCLIENT(1)`로 구분된다. 따라서 헤더는 네이티브 창 이동 영역으로 동작하고 화면 모드 버튼과 창 버튼은 드래그에 가로채이지 않는다.
- 화면 모드: `System`, `Light`, `Dark`를 강타입 값으로 모델링했다. 시스템 모드는 Windows 또는 macOS 설정을 따르고, 라이트와 다크는 즉시 적용된다.
- 화면 모드 메뉴: 276 DIP의 간결한 flyout에 시스템·라이트·다크만 표시한다. 선택 상태는 라디오 버튼, 현재 모드는 데스크톱·해·달 아이콘으로 구분한다.
- 설정 영속성: 화면 모드는 `%LOCALAPPDATA%/TimetableGenerator/Settings/appearance-v1.json`에 버전 계약으로 원자 저장한다. 실제 게시본에서 라이트 선택, 파일 저장, 종료, 재실행 후 라이트 복원을 확인했다.
- 저장 내구성: 화면 전환은 UI 스레드에서 즉시 처리하고 디스크 쓰기는 직렬 백그라운드 큐에서 수행한다. 쓰기 실패 시 사용자에게 재시도 동작을 제공하며 종료 전 보류 중인 저장을 완료한다.
- 개인정보·배포 정보: 화면 모드 메뉴에는 데이터 주소, 카탈로그 메타데이터, 배포 서버 정보가 노출되지 않는다.
- 계획 관리: 계획이 하나뿐일 때 의미 없는 비활성 닫기 버튼을 숨긴다. 닫을 수 있는 계획에서만 충분한 크기의 닫기 동작을 제공하고 확인 대화상자는 안전한 취소로 시작한다.
- 빈 상태: 추천이 없을 때 실행할 수 없는 탐색·내보내기 동작과 중복 설명을 숨기고 첫 과목 추가에 집중시킨다.
- 시각 체계: 앱 아이콘의 명도 대비를 보강하고 텍스트·채움·포커스 색 토큰을 분리해 라이트·다크 양쪽에서 위계와 가독성을 유지한다.
- 키보드·접근성: 실제 조작에는 구체적인 접근성 이름과 포커스 표시를 제공한다. 화면 모드 선택은 보조 기술의 선택 동작으로도 저장되며 검색 단축키와 Escape 흐름을 유지한다.

## 핵심 흐름 상태

1. 실행과 창 조작 — 해결: 기본 창 버튼, 네이티브 드래그 영역, 작업 영역 내 초기 배치를 Windows 게시본에서 확인했다.
2. 화면 모드 선택 — 해결: 시스템·라이트·다크를 한 번의 flyout에서 즉시 전환한다.
3. 설정 복원 — 해결: 비동기 원자 저장과 재실행 복원, 저장 실패 재시도를 자동 및 실기 검증했다.
4. 빈 계획 시작 — 해결: 첫 과목 추가에 시선을 모으고 불필요한 상태와 설명을 제거했다.
5. 계획 생성과 닫기 — 해결: 새 계획과 닫기 동작의 위치·노출 조건·확인 흐름을 일관되게 만들었다.

화면 캡처만으로 스크린리더 전체 동작이나 WCAG 준수를 확정하지는 않는다. 실제 스크린리더별 읽기 순서, 운영체제 고대비 모드, macOS 실기 창 동작은 출시 전 수동 검증 대상으로 남긴다.

## 회귀 검증

- 화면 모드 선택·저장 실패 재시도·UI 스레드 비차단·종료 대기·JSON 손상 복구·경로 계약을 자동 테스트한다.
- 창 버튼 역할, 드래그와 사용자 조작 영역, 마지막 계획 닫기 표시, 빈 계획과 일반 시간표 상태, 검색 단축키, 색 대비를 자동 테스트한다.
- Release 솔루션 테스트 333개가 실패나 건너뜀 없이 통과했다.
- `dotnet format --verify-no-changes`, `git diff --check`, Windows x64 게시 검증을 통과했다.
- 최종 Windows 실행 파일 SHA-256: `56A25A02134A675CBAA488D31C97B667B441E9770FAE2FE3C1DEC9FD7ED6D1B2`

final result: passed
