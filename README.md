# Timetable Generator

CSV로 수강 대안을 불러와 시간이 겹치지 않는 조합을 최대 10,000개까지 비교하고 PNG로 내보내는 Windows 데스크톱 앱입니다. 처음 실행해도 별도 폴더 준비 없이 환영 화면에서 바로 시작할 수 있습니다.

자세한 사용 방법은 [사용 설명서](instruction.md)를 참고하세요.

## 주요 흐름

1. 환영 화면에서 CSV 파일을 선택하거나 끌어 놓습니다.
2. 앱이 입력을 검증하고 가능한 시간표 조합을 최대 10,000개까지 생성합니다. 상한에 도달하면 하단 상태 영역에 안내가 표시됩니다.
3. 왼쪽 목록이나 이전·다음 버튼으로 조합을 비교합니다.
4. 필요한 경우 현재 시간표 또는 전체 시간표의 저장 폴더를 선택해 PNG로 내보냅니다.

파일을 불러오거나 내보내는 중 문제가 발생해도 앱은 닫히지 않습니다. 오류 내용을 확인한 뒤 다른 파일을 선택하거나 다시 시도할 수 있습니다.

## CSV 형식

CSV는 UTF-8로 저장해야 하며, 헤더는 아래 두 형식 중 하나와 정확히 일치해야 합니다.

```text
CourseId,Section,Name,TimeSlots
CourseId,Section,Name,TimeSlots,Classroom
```

| 열 | 필수 | 설명 |
| --- | --- | --- |
| `CourseId` | 예 | 양의 정수입니다. 같은 값의 행은 함께 듣는 강의가 아니라 서로 대체 가능한 분반입니다. |
| `Section` | 예 | 분반 코드입니다. |
| `Name` | 예 | 화면과 PNG에 표시할 과목명입니다. |
| `TimeSlots` | 예 | `한국어 요일+교시` 형식이며 여러 시간은 `/`로 구분합니다. 사용할 수 있는 범위는 1~10교시입니다. |
| `Classroom` | 아니요 | 5열 형식을 선택했을 때 사용할 수 있는 강의실입니다. 값은 비워 둘 수 있으며, 입력할 때는 마지막 공백을 기준으로 건물명과 호실을 나눈 `<건물명> <호실>` 형식이어야 합니다. 공백 없는 단일 값은 사용할 수 없습니다. |

`TimeSlots` 예시는 `월요일1교시/수요일1교시`입니다. 요일은 `월요일`부터 `일요일`까지 정확히 입력하며 중간에 공백을 넣지 않습니다.

저장소의 [기본 예제 CSV](data/example_course_schedule.csv)는 세 과목에 각각 두 개의 대안 분반이 있어 `2 × 2 × 2 = 8`개의 유효 조합을 만듭니다.

```csv
CourseId,Section,Name,TimeSlots,Classroom
1,01,알고리즘,월요일1교시/수요일1교시,공학관 301
1,02,알고리즘,화요일1교시/목요일1교시,공학관 302
2,01,자료구조,월요일3교시/수요일3교시,미래관 204
2,02,자료구조,화요일3교시/목요일3교시,미래관 205
3,01,데이터베이스,금요일2교시,과학관 401
3,02,데이터베이스,금요일4교시,과학관 402
```

## 내보내기

시간표는 자동으로 저장되지 않습니다. 사용자가 현재 시간표 또는 전체 시간표 내보내기를 선택하고 대상 폴더를 지정한 경우에만 PNG를 만듭니다.

기본 파일명은 `{CSV 이름}_시간표_{번호}.png` 형식입니다. 대상 폴더에 같은 이름이 있으면 기존 파일을 덮어쓰지 않고 고유한 번호를 붙여 새 파일로 저장합니다.

## 키보드 단축키

| 단축키 | 동작 |
| --- | --- |
| `Ctrl+O` | CSV 불러오기 |
| `Ctrl+E` | 현재 시간표를 PNG로 내보내기 |
| `Ctrl+Shift+E` | 전체 시간표를 PNG로 내보내기 |
| `Alt+Left` / `Alt+Right` | 이전 / 다음 시간표 |
| `Esc` | 진행 중인 불러오기 또는 내보내기 취소 |

## 개발자 안내

### 요구 사항

- Windows 10 또는 Windows 11
- .NET 10 SDK
- 선택 사항: .NET 데스크톱 개발 워크로드가 설치된 Visual Studio 2026 18.0 이상

앱은 .NET 10 기반 WinForms 프로젝트이며 Windows에서 실행됩니다.

### 빌드와 테스트

저장소 루트에서 다음 명령을 실행합니다.

```powershell
dotnet build TimetableGenerator.sln --configuration Release
dotnet test TimetableGenerator.sln --configuration Release
dotnet run --project TimetableGenerator.csproj
```

Release 실행 파일은 `bin/Release/net10.0-windows/` 아래에 생성됩니다.

### 코드 구조

데이터 흐름은 `CSV Infrastructure → Core → Application Documents → Presentation → Product UI` 순서입니다.

- `Core/Domain`, `Core/Application/Scheduling`: 강타입 도메인 값과 충돌 없는 조합 생성
- `Infrastructure/Csv`: UTF-8 CSV 파싱과 행·열 단위 진단
- `Application/Documents`: 가져오기, 생성, 화면 모델 조립과 취소 상태 통합
- `Presentation/Schedules`: 불변 시간표 그리드 모델과 교시 시간 정책
- `Infrastructure/Exporting`: UI와 분리된 PNG 렌더링과 충돌 없는 파일 저장
- `UI/Product`: 시작, 로딩, 오류, 탐색, 내보내기 제품 경험

코드는 프로젝트 C# 코딩 표준에 따라 파일 범위 네임스페이스, 의미 있는 강타입 매개변수, 불변 모델, 명시적 실패 상태와 일관된 네이밍을 사용합니다. 변경 후에는 Release 빌드와 전체 테스트를 모두 통과해야 합니다.
