# 한동대학교 과목 카탈로그 생성기

학교가 제공하는 `.xls` 파일을 읽어 한동대학교 과목 카탈로그 JSON과 이를 가리키는 `index.json`을 생성하는 개발·운영 도구입니다.  
학교 파일은 확장자와 달리 CP949로 인코딩된 HTML 문서이며, 생성기는 원본 16개 열을 프로그램에서 사용하는 구조로 정규화합니다.

## 명령 형식

명령은 아래 형식만 지원합니다.  
옵션 순서는 바꿀 수 있지만 모든 옵션을 정확히 한 번씩 지정해야 합니다. 알 수 없는 옵션, 중복 옵션, 값이 없는 옵션은 오류로 종료됩니다.

```text
generate --source <path> --term <YYYY-S> --revision <positive-int> --output-root <path>
```

| 옵션 | 의미 |
| --- | --- |
| `--source` | 학교에서 내려받은 원본 `.xls` 파일 경로입니다. |
| `--term` | `2026-2` 형식으로 학년도 2000~9999와 학기 1·2 중 하나를 지정합니다. |
| `--revision` | 1~2,147,483,647 범위의 배포 수정 번호입니다. |
| `--output-root` | 닷홈의 `/html/timetable-generator/catalog/v1`에 올릴 파일을 만드는 로컬 출력 폴더입니다. |

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

카탈로그 파일은 UTF-8(BOM 없음)과 LF 줄바꿈으로 항상 같은 형식으로 저장됩니다.  
카탈로그 JSON의 마지막 LF까지 포함한 전체 바이트를 기준으로 SHA-256을 계산하고, 그 바이트 크기와 해시를 `index.json`에 기록합니다.  
성공 결과에는 두 파일 경로, 원본·카탈로그 SHA-256과 데이터 품질 통계를 표시합니다.  
원본 바이트, `--term`, `--revision`과 기존 `index.json`의 유효한 항목이 모두 같으면 카탈로그와 `index.json`은 플랫폼과 실행 시각에 관계없이 동일한 바이트로 생성됩니다.  
현재 `index.json`과 카탈로그 형식은 생성·업로드 시각 필드가 없는 스키마 v1입니다.

## 수정 번호와 게시 규칙

한 번 게시한 `catalog-r<수정 번호>.json` 형식의 파일은 변경하지 않습니다. 파일 이름의 수정 번호는 최소 네 자리로 표시하고, 네 자리보다 짧으면 앞을 `0`으로 채웁니다.  
같은 학기 데이터를 수정할 때는 기존 파일을 덮어쓰지 말고 `--revision` 값을 높여 새 파일을 생성합니다. 예를 들어 수정 번호 1을 게시한 뒤 수정본은 `--revision 2`로 생성하여 `catalog-r0002.json`으로 게시합니다.  
같은 `--revision` 값으로 다시 생성했을 때 카탈로그 바이트가 같으면 기존 카탈로그를 그대로 사용하고 정상 종료하며, 다르면 `OutputConflict` 오류로 중단합니다. 기존 `index.json`의 다른 학기·수정 번호 항목은 보존합니다.

닷홈에는 다음 순서로 업로드합니다.

1. 새 `catalog-r<수정 번호>.json` 형식의 카탈로그 파일을 업로드합니다.
2. 공개 URL에서 파일 크기와 SHA-256이 생성 결과와 같은지 확인합니다.
3. 새 카탈로그를 가리키는 `index.json`을 마지막에 업로드합니다.

새 카탈로그 업로드에 실패해도 기존 `index.json`이 이전에 정상 게시된 수정 번호를 계속 가리키도록 이 순서를 지켜야 합니다.

## Git 추적 정책

생성된 카탈로그와 `index.json`은 닷홈 배포 파일이므로 Git으로 추적하지 않습니다.  
저장소의 `.gitignore`는 기본 출력 경로인 `deploy/dothome/html/timetable-generator/catalog/`을 제외합니다. 원본 `.xls`도 저장소에 커밋하지 말고 별도의 비공개 위치에 보관합니다.

생성 결과가 아닌 생성기 자체의 소스·프로젝트 설정·테스트·문서는 Git으로 추적합니다.  
다른 `--output-root`를 사용할 때는 해당 경로가 Git 변경 목록에 포함되지 않는지 확인해야 합니다.

## 종료 코드

| 코드 | 의미 |
| ---: | --- |
| `0` | 생성 성공 |
| `1` | 예상하지 못한 내부 오류 |
| `2` | 명령 또는 옵션 오류 |
| `3` | 원본 파일 읽기·형식·스키마 오류 |
| `4` | 학기 불일치·원본 데이터 검증·정규화·카탈로그 직렬화 오류 |
| `5` | 기존 수정 번호 충돌·`index.json` 무결성·출력 쓰기 오류 |
