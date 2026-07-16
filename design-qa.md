# 제품 디자인 QA

## 비교 자료

- 레이아웃 기준: `docs/design/planning-workspace-target.png`
- 라이트 색상 기준: `docs/design/theme-target-light-blue-mist.png`
- 다크 색상 기준: `docs/design/theme-target-dark-obsidian.png`
- 라이트 기준·실행본 병렬 비교: `tests/TimetableGenerator.Desktop.Tests/TestResults/theme-polish-2026-07-17/comparison-light-target-vs-app.png`
- 다크 기준·실행본 병렬 비교: `tests/TimetableGenerator.Desktop.Tests/TestResults/theme-polish-2026-07-17/comparison-dark-target-vs-app.png`
- 최종 라이트 화면 모드 메뉴: `tests/TimetableGenerator.Desktop.Tests/TestResults/theme-polish-2026-07-17/06-light-appearance-final.png`
- 최종 다크 화면 모드 메뉴: `tests/TimetableGenerator.Desktop.Tests/TestResults/theme-polish-2026-07-17/06-dark-appearance-final.png`
- 최종 라이트 과목 선택 상태: `tests/TimetableGenerator.Desktop.Tests/TestResults/theme-polish-2026-07-17/07-light-selected-final.png`
- 최종 다크 과목 선택 상태: `tests/TimetableGenerator.Desktop.Tests/TestResults/theme-polish-2026-07-17/05-dark-selected-product-button.png`
- 검증 창 크기: 1718 × 916 px, DWM 가시 프레임 캡처: 1702 × 908 px

## 최종 판정

- P0 차이: 0건
- P1 차이: 0건
- P2 차이: 0건
- 레이아웃, 패널 비율, 상단 구조와 초기 밀도는 기준 시안과 일치한다. 시각 QA에서는 재배치보다 색상·표면·상태 마감에 집중했다.

## 시각 체계

- 라이트: 순백을 제거하고 창, 상단, 좌우 패널, 중앙 작업 영역, 입력, 떠 있는 표면을 서로 다른 저대비 블루 계층으로 분리했다. 전체가 푸른 분위기를 유지하면서도 세 영역의 경계를 읽을 수 있다.
- 다크: 차가운 청회색 대신 `#1A1A1A` 기반의 따뜻한 흑색과 `#F5EFE0` 크림색 본문을 사용한다. 작은 강조 전경은 `#8BA9FF`, 주요 동작 채움은 `#0047FF`로 분리했다.
- 주요 동작: 실제 Windows 게시본의 라이트·다크 `PNG로 저장` 버튼이 모두 `#0047FF`로 렌더링되는 것을 픽셀 확인했다. Avalonia 기본 accent 버튼 템플릿이 다크 전경색을 채움으로 재사용하던 문제는 제품 전용 ControlTheme으로 차단했다.
- 컨트롤: TextBox, ComboBox, RadioButton, Flyout, ProgressBar, ScrollBar, Expander와 창 캡션 버튼이 제품 팔레트와 같은 Fluent 팔레트를 사용한다.
- 상태: hover, pressed, selected, focus, success, warning, error와 course 카드 색을 역할별 토큰으로 분리했다. 조작 컨트롤 경계는 인접 표면 대비 3:1 이상, 일반 본문과 채움 위 텍스트는 4.5:1 이상을 자동 검증한다.
- 시간표 카드: 좁은 요일 열에서도 긴 영문 담당교원 이름이 한 글자만 다음 줄로 떨어지지 않도록 본문 크기와 줄바꿈 정책을 조정했다. 상세 Flyout에는 전체 정보가 유지된다.

## 동작과 접근성

- 화면 모드: 시스템·라이트·다크를 한 Flyout에서 즉시 전환하며 선택은 로컬에 비동기 저장된다.
- 런타임 재해석: XAML DynamicResource뿐 아니라 코드에서 생성하는 시간표 격자, 교시 보조문구, 과목 상세 Flyout도 테마 변경 시 다시 구성된다.
- 키보드: 제품 전용 채움 버튼은 기존 버튼 계약과 접근성 이름을 유지하고, 포커스에는 채움과 3:1 이상 구분되는 별도 stroke를 사용한다.
- 색상 외 단서: 선택한 과목은 배경색 외에도 왼쪽 indicator, check 아이콘과 계획 목록 항목으로 상태를 전달한다. 오류·경고·성공은 아이콘과 텍스트를 함께 사용한다.

## 핵심 흐름 상태

1. 첫 실행과 빈 계획 — 해결: 첫 과목 추가에 초점을 두고 실행할 수 없는 추천·내보내기 조작을 숨긴다.
2. 과목 선택과 추천 생성 — 해결: 추가 상태, 계획 요약, 추천 탐색, 주간 시간표와 내보내기 동작이 양쪽 테마에서 일관되게 연결된다.
3. 화면 모드 선택 — 해결: 메뉴가 제품 표면 계층을 사용하며 세 모드의 선택 상태를 즉시 반영한다.
4. 테마 전환 중 상태 유지 — 해결: 열린 작업 공간과 코드 생성 시간표가 창을 다시 열지 않고 새 팔레트로 갱신된다.
5. 창 조작 — 해결: 네이티브 최소화·최대화·닫기와 제목 표시줄 드래그를 유지한다.

## 회귀 검증

- Release 솔루션 테스트 339개가 실패와 건너뜀 없이 통과했다.
- 제품 색상 토큰의 라이트·다크 대칭성, 텍스트·상태·경계·포커스 대비와 주요 동작의 실제 ContentPresenter 렌더링을 검사한다.
- 채워진 시간표 상태에서 라이트→다크 전환 후 코드 생성 브러시가 교체되는지 검사한다.
- `dotnet format --verify-no-changes`, `git diff --check`, Windows x64 self-contained 게시 검증을 통과했다.
- 최종 Windows 실행 파일 SHA-256: `B92A9FBACBC8887788D63DD9F6B117DF135EFA719C4A0041DEFB91653216369C`
- 최종 Windows ZIP SHA-256: `08EFB7224583A1D7DE6E0A60477703D225A05F6E663F5E954C0E33433B1D672C`

화면 캡처만으로 스크린리더 전체 동작이나 모든 WCAG 조건을 확정하지는 않는다. 운영체제 고대비 모드, 실제 Windows 10 기기, macOS Intel·Apple Silicon의 창 장식과 스크린리더 읽기 순서는 출시 전 실기 검증 대상으로 남긴다.

final result: passed
