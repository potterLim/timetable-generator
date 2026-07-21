# Public 저장소 전환 점검표

이 문서는 저장소를 Public으로 전환하고 GitHub Releases로 배포하기 전에 확인할 항목을 기록한다. 프로젝트 자체 라이선스 파일은 만들지 않으며, Pretendard와 Fluent UI System Icons의 기존 제3자 라이선스는 유지한다.

## 1. 로컬 저장소 정리

- [ ] 작업 트리가 의도한 변경만 포함하는지 확인한다.
- [ ] 원본 `.xls`·`.xlsx`, 생성 카탈로그, 사용자 작업 공간, 실제 서비스 설정과 QA 산출물이 추적되지 않는지 확인한다.
- [ ] `.env*`, OAuth 정보, 토큰, 인증서, 서명 키가 추적되지 않는지 확인한다.
- [ ] 테스트 fixture와 문서 이미지가 합성 데이터만 사용하는지 확인한다.
- [ ] 커밋 author·committer에는 GitHub noreply 주소를 사용한다.

## 2. Git 전체 이력 정리

- [ ] 저장소 밖에 `git clone --mirror` 백업을 만든다.
- [ ] 과거 커밋 메타데이터와 파일 본문에서 개인 이메일을 GitHub noreply 주소로 교체한다.
- [ ] 과거 배포 ZIP, 디버그 심볼, 로컬 절대 경로가 든 바이너리와 실제 데이터 이미지를 모든 ref에서 제거한다.
- [ ] 재작성 뒤 이전 개인 이메일과 제거 대상 경로가 어느 ref에도 남지 않았는지 확인한다.
- [ ] `gitleaks`로 전체 이력을 검사하고 결과에 비밀 값이 없는지 확인한다.

이력 재작성은 모든 커밋 ID를 바꾼다. Public 전환 전에만 수행하고, 기존 clone을 사용하는 협업자가 있다면 새로 clone해야 함을 먼저 알린다.

## 3. 품질 검증

```powershell
dotnet restore TimetableGenerator.sln
dotnet build TimetableGenerator.sln --configuration Release --no-restore
dotnet test TimetableGenerator.sln --configuration Release --no-build --no-restore
dotnet format TimetableGenerator.sln --no-restore --verify-no-changes
git diff --check
```

- [ ] Windows x64 게시본을 새 디렉터리에 만들고 실제 기기에서 실행한다.
- [ ] macOS Apple Silicon·Intel 게시본을 각각 실제 기기에서 검사한다.
- [ ] 앱 archive와 SHA-256만 Release 자산으로 올리고 PDB·테스트 결과·로컬 설정을 제외한다.
- [ ] Google OAuth와 Apple Calendar는 전용 QA 계정·캘린더에서 검증한다.

## 4. Public 전환 직전 GitHub 설정

- [ ] 정리된 `main`을 private 상태에서 올리고 GitHub 웹의 파일·커밋 이력을 다시 확인한다.
- [ ] Secret scanning, push protection, Dependabot alerts를 활성화한다.
- [ ] `main` 보호 규칙에 pull request와 필수 품질 검사를 연결한다.
- [ ] 비공개 보안 제보 기능을 활성화하고 `SECURITY.md` 링크가 동작하는지 확인한다.
- [ ] 저장소 설명에서 이 프로젝트가 학교 공식 서비스가 아님을 명확히 한다.

## 5. Public 전환과 Release

- [ ] 저장소 공개 범위를 Public으로 바꾼다.
- [ ] Public 전환 직후 로그인하지 않은 브라우저에서 README, 개인정보 안내, 보안 정책과 지원 안내를 확인한다.
- [ ] 서명·공증·실기 검증이 끝난 산출물만 GitHub Release에 첨부한다.
- [ ] Release 본문에 지원 운영체제, SHA-256과 알려진 제한 사항을 기록한다.
- [ ] 별도 디렉터리에 fresh clone하고 빌드·테스트·비밀 검사를 한 번 더 수행한다.
