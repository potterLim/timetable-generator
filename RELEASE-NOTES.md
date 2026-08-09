# Timetable Generator 1.0.4

내보내기 중 종료 안정성과 배포 파일의 검증 신뢰성을 높인 릴리스입니다.

## 개선 및 수정

- 내보내기 중 앱을 닫아도 진행 중인 작업과 자동 저장을 안전하게 마무리하도록 종료 순서를 개선했습니다.
- Apple Calendar 권한 대기 중인 작업은 빠르게 취소하고, 일정 변경이 시작된 작업은 복구 정보와 함께 끝까지 처리하도록 다듬었습니다.
- 로컬 설정과 저장 파일에 명확한 크기 한계를 적용해 비정상적으로 큰 파일을 안전하게 처리합니다.
- 릴리스 체크섬을 Windows와 macOS 모두에서 같은 형식으로 검증할 수 있게 정리했습니다.
- Windows 압축 파일의 수정 날짜를 릴리스 커밋 시각으로 기록해 자연스러운 파일 정보와 재현 가능성을 함께 보장합니다.

## 다운로드

- Windows 11 x64: `TimetableGenerator-1.0.4-win-x64.zip`
- macOS 14 이상(Apple Silicon): `TimetableGenerator-1.0.4-osx-arm64.zip`
- 파일 무결성 확인: `checksums.sha256`

## 안내

Timetable Generator는 한동대학교가 개발하거나 보증하는 공식 서비스가 아닙니다.  
실제 수강 신청 전에는 과목 정보와 수강 가능 여부를 학교의 공식 안내에서 확인하세요.

## 지원

- [제품 안내](https://potterlim.github.io/timetable-generator/)
- [사용 방법](https://potterlim.github.io/timetable-generator/guide/)
- [문의하기](mailto:potterLim0808@gmail.com?subject=Timetable%20Generator%20%EB%AC%B8%EC%9D%98)
