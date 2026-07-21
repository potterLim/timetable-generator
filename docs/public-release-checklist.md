# Public 저장소 전환 점검표

이 문서는 저장소를 Public으로 전환하고 GitHub Releases로 배포하기 전에 확인할 항목을 기록한다. 프로젝트 자체 라이선스 파일은 만들지 않으며, 배포 의존성과 자산의 제3자 라이선스·notice 원문은 유지한다.

## 1. 로컬 저장소 정리

- [ ] 작업 트리가 의도한 변경만 포함하는지 확인한다.
- [ ] 원본 `.xls`·`.xlsx`, 생성 카탈로그, 사용자 작업 공간, 실제 서비스 설정과 QA 산출물이 추적되지 않는지 확인한다.
- [ ] `.env*`, OAuth 설정 원본, 사용자 토큰, 인증서, 서명 키가 추적되지 않는지 확인한다. 제품용 Desktop OAuth sidecar는 미추적 상태로 유지한다.
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
dotnet restore TimetableGenerator.sln --locked-mode
dotnet build TimetableGenerator.sln --configuration Release --no-restore
dotnet test TimetableGenerator.sln --configuration Release --no-build --no-restore
dotnet format TimetableGenerator.sln --no-restore --verify-no-changes
git diff --check
```

- [ ] Windows x64 게시본을 새 디렉터리에 만들고 실제 기기에서 실행한다.
- [ ] macOS Apple Silicon·Intel 게시본을 각각 실제 기기에서 검사한다.
- [ ] Windows와 macOS의 실제 빌드 호스트에서 깨끗한 커밋을 기준으로 `write-release-build-info.ps1 -Version 1.0.0 -RequireClean`을 실행한다.
- [ ] Windows 서명과 macOS 서명·공증·stapling 뒤 `finalize-desktop-release.ps1`의 플랫폼별 단계와 `Aggregate` 단계를 통과시킨다.
- [ ] GitHub Release에는 최종화된 플랫폼별 ZIP 3개와 `checksums.sha256`만 첨부한다. PDB·테스트 결과·빌드 증거·설정 원본은 별도 자산으로 올리지 않는다.
- [ ] 최종 ZIP 내부에는 검증된 `catalog-source.local.json`과 제품 설정 스키마 v2의 `google-calendar.local.json`을 포함한다. Google 설정은 정확히 `schemaVersion`, Desktop OAuth `clientId`, Desktop OAuth `clientSecret` 세 속성만 포함해야 한다.
- [ ] Desktop OAuth client secret을 클립보드에 복사한 뒤 `scripts/set-google-calendar-local-configuration.ps1`로 미추적 v2 sidecar를 갱신하고, 값이 명령 기록이나 빌드 로그에 남지 않았는지 확인한다.
- [ ] Desktop OAuth client secret은 네이티브 앱에서 기밀 보안 경계가 아니며 토큰 교환에 필요함을 확인한다. 설정 원본은 Git에 추적하지 않고 미추적 sidecar로만 주입하며, 액세스 토큰·새로 고침 토큰·웹 애플리케이션 OAuth client secret은 포함하지 않는다.
- [ ] Google OAuth 승인 요청이 PKCE(S256)와 임의의 `127.0.0.1` 루프백 포트를 사용하는지 확인한다.
- [ ] Google Auth Platform의 사용자 유형은 `외부`, 게시 상태는 `프로덕션`으로 설정하고 지인 계정을 테스트 사용자 목록으로 운영하지 않는다.
- [ ] 배포물의 Google OAuth client ID는 Release 전용 Desktop 클라이언트이며, 각 사용자가 자신의 Google 계정으로 로그인하는 흐름을 실제 계정으로 확인한다.
- [ ] Google OAuth 검증 상태와 미검증 앱 경고·누적 사용자 제한을 확인하고, 현재 Release에 허용할 상태를 명시적으로 결정한다.
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
