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
- 최종 중앙 우선 Windows 화면: `tests/TimetableGenerator.Desktop.Tests/TestResults/layout-polish-2026-07-17/01-windows-selected-final.png`
- 최종 반응형 인스펙터와 닫기 동작: `tests/TimetableGenerator.Desktop.Tests/TestResults/layout-polish-2026-07-17/03-inspector-dismiss-final.png`
- 최종 다크 상단 작업 영역: `tests/TimetableGenerator.Desktop.Tests/TestResults/action-polish-2026-07-17/05-dark-actions-printwindow.png`
- 최종 내 계획 진입·닫기 흐름: `tests/TimetableGenerator.Desktop.Tests/TestResults/action-polish-2026-07-17/06-dark-inspector-printwindow.png`
- 검증 창 크기: 1718 × 916 px, DWM 가시 프레임 캡처: 1702 × 908 px
- 최종 레이아웃 검증 Windows 프레임: 1818 × 969 px

## 최종 판정

- P0 차이: 0건
- P1 차이: 0건
- P2 차이: 0건
- 상단 구조와 초기 밀도는 기준 시안을 유지하면서 중앙 시간표를 제품의 주 작업 영역으로 재조정했다. 일반 데스크톱 폭에서는 계획 인스펙터가 필요할 때 열리는 오버레이가 되고, 충분히 넓은 창에서만 세 패널을 동시에 표시한다.

## 시각 체계

- 라이트: 순백을 제거하고 창, 상단, 좌우 패널, 중앙 작업 영역, 입력, 떠 있는 표면을 서로 다른 저대비 블루 계층으로 분리했다. 전체가 푸른 분위기를 유지하면서도 세 영역의 경계를 읽을 수 있다.
- 다크: 차가운 청회색 대신 `#1A1A1A` 기반의 따뜻한 흑색과 `#F5EFE0` 크림색 본문을 사용한다. 작은 강조 전경은 `#8BA9FF`, 주요 동작 채움은 절제한 `#1B4FDE`로 분리했다.
- 주요 동작: 라이트 `PNG로 저장`은 브랜드 기준인 `#0047FF`를 유지한다. 다크에서는 채움을 `#1B4FDE`, hover를 `#2D59E4`, pressed를 `#1745CC`로 낮춰 시간표보다 먼저 시선을 빼앗지 않게 했다. 크림색 버튼 텍스트 대비는 각 상태에서 5:1 이상이며 실제 Windows 게시본에서도 상태 위계와 가독성을 확인했다.
- 컨트롤: TextBox, ComboBox, RadioButton, Flyout, ProgressBar, ScrollBar, Expander와 창 캡션 버튼이 제품 팔레트와 같은 Fluent 팔레트를 사용한다.
- 상태: hover, pressed, selected, focus, success, warning, error와 course 카드 색을 역할별 토큰으로 분리했다. 조작 컨트롤 경계는 인접 표면 대비 3:1 이상, 일반 본문과 채움 위 텍스트는 4.5:1 이상을 자동 검증한다.
- 시간표 카드: 좁은 요일 열에서도 긴 영문 담당교원 이름이 한 글자만 다음 줄로 떨어지지 않도록 본문 크기와 줄바꿈 정책을 조정했다. 과목명은 12 DIP SemiBold 한 줄을 유지하고, 정말 긴 이름만 말줄임표로 처리한다. 전체 이름은 접근성 이름, 툴팁과 상세 Flyout에 보존된다.

## 반응형 작업 영역

- 1600 DIP 이상에서는 과목 패널 312 DIP, 계획 패널 288 DIP를 Inline으로 유지한다.
- 1280–1599 DIP에서는 과목 패널 312 DIP만 Inline으로 유지하고 계획 패널은 304 DIP 오버레이로 연다. 기본 1440 DIP 창에서 시간표가 중앙 폭을 우선 확보한다.
- 더 작은 창에서는 기존 단계별 Overlay 전환을 유지하며, 오버레이 과목 패널과 계획 패널 모두 헤더에 명시적인 닫기 버튼을 제공한다.
- `내 계획 열기`는 추천 시간표 제목 아래의 보조 정보 영역이 아니라 상단 작업 영역에서 내보내기 바로 앞에 둔다. 시간표가 비어 있어도 진입점을 유지하고 1080 DIP 폭에서도 제목·추천 탐색·내보내기와 겹치지 않는다.
- 시간표 내부 좌우 여백은 각각 18 DIP, 교시 열은 72 DIP, 카드 바깥 여백은 4 DIP로 정리했다. Windows 게시본에서 `글로벌 기업가정신 입문`이 월·목 카드 모두 한 줄로 표시되는 것을 확인했다.

## 타이포그래피

- Pretendard 1.3.9의 Regular 400, Medium 500, SemiBold 600, Bold 700 정적 글꼴을 앱에 포함하고 모든 제품 텍스트의 기본 글꼴로 사용한다. 설치된 운영체제 글꼴과 무관하게 Windows와 macOS에서 동일한 글자 폭과 굵기 체계를 유지한다.
- Window, TextBlock, Button, TextBox, ComboBox와 별도 PopupRoot의 Flyout까지 같은 Pretendard 계약을 상속한다. FluentIcons 전용 glyph 글꼴은 아이콘 손상을 막기 위해 이 계약에서 제외한다.
- 헤드리스 Skia 환경에서 네 제품 굵기가 각각 Pretendard의 실제 타입페이스로 해석되는지 자동 검사한다. 원문 SIL Open Font License 1.1은 운영체제별 배포 산출물의 `ThirdPartyNotices` 위치에 포함한다.

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
6. 반응형 계획 확인 — 해결: 일반 창에서는 상단 작업 영역의 `내 계획 열기` 버튼으로 인스펙터를 열고, 패널 헤더의 닫기 버튼·바깥 클릭·Esc로 닫을 수 있다.

## 회귀 검증

- Release 솔루션 테스트 344개가 실패와 건너뜀 없이 통과했다.
- 제품 색상 토큰의 라이트·다크 대칭성, 텍스트·상태·경계·포커스 대비와 주요 동작의 실제 ContentPresenter 렌더링을 검사한다.
- 채워진 시간표 상태에서 라이트→다크 전환 후 코드 생성 브러시가 교체되는지 검사한다.
- `dotnet format --verify-no-changes`, `git diff --check`, Windows x64 self-contained 게시 검증을 통과했다.
- 최종 Windows 실행 파일 SHA-256: `A0D8368E7F6668C4AB1EF57D6247B15C7CA62ACE3583911841F1EA6B4B062A21`
- 최종 Windows ZIP SHA-256: `B5B40F0C4ADB1F9EBAE6D7513B0803089C0E75AB673DFD74B08787500287F58F`

화면 캡처만으로 스크린리더 전체 동작이나 모든 WCAG 조건을 확정하지는 않는다. 운영체제 고대비 모드, 실제 Windows 10 기기, macOS Intel·Apple Silicon의 창 장식·시스템 글꼴 폴백과 스크린리더 읽기 순서는 출시 전 실기 검증 대상으로 남긴다.

final result: passed
