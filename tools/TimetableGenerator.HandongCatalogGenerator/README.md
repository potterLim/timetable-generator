# 한동대학교 과목 카탈로그 생성기

학교가 제공하는 `.xls` 파일을 읽어 한동대학교 과목 카탈로그 JSON과 이를 가리키는
`index.json`을 생성하는 개발·운영 도구입니다. 현재 학교 파일은 확장자와 달리 CP949로
인코딩된 HTML 문서이며, 생성기는 원본 16개 열을 제품용 의미 모델로 정규화합니다.

## 실행 계약

명령은 아래 형식만 지원합니다. 옵션 순서는 바꿀 수 있지만 모든 옵션을 정확히 한 번씩
지정해야 합니다. 알 수 없는 옵션, 중복 옵션, 값이 없는 옵션은 오류로 종료됩니다.

```text
generate --source <path> --term <YYYY-S> --revision <positive-int> --output-root <path>
```

| 옵션 | 의미 |
| --- | --- |
| `--source` | 학교에서 내려받은 원본 `.xls` 파일 경로입니다. |
| `--term` | `2026-2` 형식의 학년도와 학기입니다. |
| `--revision` | 1 이상의 배포 수정 번호입니다. |
| `--output-root` | 닷홈의 `/html/timetable-generator/catalog/v1`에 대응하는 로컬 배포 루트입니다. |

### Windows PowerShell

저장소 루트에서 다음과 같이 실행합니다.

```powershell
dotnet run --project .\tools\TimetableGenerator.HandongCatalogGenerator\TimetableGenerator.HandongCatalogGenerator.csproj -- generate `
  --source "C:\Users\me\Downloads\개설시간표.xls" `
  --term 2026-2 `
  --revision 1 `
  --output-root .\deploy\dothome\html\timetable-generator\catalog\v1
```

### macOS

저장소 루트에서 다음과 같이 실행합니다.

```bash
dotnet run --project ./tools/TimetableGenerator.HandongCatalogGenerator/TimetableGenerator.HandongCatalogGenerator.csproj -- generate \
  --source "$HOME/Downloads/개설시간표.xls" \
  --term 2026-2 \
  --revision 1 \
  --output-root ./deploy/dothome/html/timetable-generator/catalog/v1
```

## 생성 경로

위 예시는 다음 두 파일을 생성합니다.

```text
deploy/dothome/html/timetable-generator/catalog/v1/
├── index.json
└── handong-global-university/
    └── 2026-2/
        └── catalog-r0001.json
```

카탈로그 파일은 UTF-8(BOM 없음)과 LF 줄바꿈으로 결정적으로 직렬화됩니다. 마지막 LF까지
포함한 실제 저장 바이트에서 SHA-256을 계산하며, `index.json`에는 그 바이트 크기와 해시를
기록합니다. 성공 출력에도 두 파일 경로, 원본·카탈로그 SHA-256과 동적 품질 집계가 표시됩니다.
생성 시각이나 실제 업로드 시각을 추측하는 메타데이터는 기록하지 않으므로, 같은 입력과
revision 및 기존 index 상태에서는 두 JSON 파일이 플랫폼과 실행 시각에 관계없이 동일합니다.
아직 외부에 배포된 형식이 없으므로, 시간 필드가 없는 현재 index와 카탈로그 형식을 모두
최초 스키마 v1로 확정합니다.

## revision과 게시 규칙

한 번 게시한 `catalog-rNNNN.json`은 불변 파일입니다. 같은 학기 데이터를 수정할 때 기존
파일을 덮어쓰지 말고 revision을 증가시켜 새 파일을 생성합니다. 예를 들어 revision 1을
게시한 후 수정본은 `--revision 2`로 생성하여 `catalog-r0002.json`으로 게시합니다.
동일 revision을 다시 생성했을 때 바이트가 같으면 안전한 no-op으로 처리하고, 다르면
`OutputConflict` 오류로 중단합니다. 기존 `index.json`의 다른 학기·revision 항목은 보존됩니다.

닷홈에는 다음 순서로 업로드합니다.

1. 새 `catalog-rNNNN.json`을 업로드합니다.
2. 공개 URL에서 파일 크기와 SHA-256이 생성 결과와 같은지 확인합니다.
3. 새 카탈로그를 가리키는 `index.json`을 마지막에 업로드합니다.

새 카탈로그 업로드에 실패해도 기존 `index.json`이 정상 revision을 계속 가리키도록 이
순서를 지켜야 합니다.

## Git 추적 정책

생성된 카탈로그와 `index.json`은 닷홈 배포 산출물이므로 Git으로 추적하지 않습니다.
저장소의 `.gitignore`는 기본 출력 경로인
`deploy/dothome/html/timetable-generator/catalog/`을 제외합니다. 원본 `.xls`도 저장소에
커밋하지 말고 별도의 비공개 원본 보관 위치에 유지합니다.

생성기 C# 코드와 테스트만 Git으로 추적합니다. 다른 `--output-root`를 사용할 때는 해당
경로가 Git 변경 목록에 포함되지 않도록 별도로 확인해야 합니다.

## 종료 코드

| 코드 | 의미 |
| ---: | --- |
| `0` | 생성 성공 |
| `1` | 예상하지 못한 내부 오류 |
| `2` | 명령 또는 옵션 오류 |
| `3` | 원본 파일 읽기·형식·스키마 오류 |
| `4` | 원본 의미·정규화 검증 오류 |
| `5` | 기존 revision 충돌·index 무결성·출력 쓰기 오류 |
