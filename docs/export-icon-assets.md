# 내보내기 아이콘 자산

이 문서는 내보내기 메뉴에 사용한 자산의 출처와 라이선스를 기록하는 유지관리자용 문서입니다.  
사용자가 파일 형식과 캘린더 대상을 빠르게 구분할 수 있도록 아래 자산을 사용합니다.

## PNG 이미지 아이콘

- 원본 저장소: <https://github.com/microsoft/fluentui-system-icons>
- 원본 자산: <https://github.com/microsoft/fluentui-system-icons/blob/16a524d18199ddaa81bef6628cafa48f70cbb4f4/assets/Image/SVG/ic_fluent_image_24_color.svg>
- 원본: 24×24 SVG
- 원본 SHA-256: `97d8d9226dac215783c8ce7121ad669eb78c9d79e6bb02b5bddbfe5ef17403b6`
- 배포 파일: `src/TimetableGenerator.Desktop/Assets/Export/PngImageIcon.png`
- 배포 파일 SHA-256: `7bdfc4795cd4b60e62a8bd7c5fd4a30a88af26c693b5e98dd9833528fc3c6dac`
- 라이선스: MIT

원본의 비율과 알파 채널을 유지하면서 `sharp`(libvips)로 96×96 RGBA PNG를 만들었습니다.  
현재 시간표 한 장을 저장할 때는 24 DIP로 표시하고, 모든 가능한 시간표를 저장할 때는 같은 자산을 세 번 배치해 서로 떨어진 여러 이미지로 표현합니다.  
별도의 합성 이미지 파일이나 추가 라이선스 자산은 사용하지 않습니다.  
Microsoft Photos 앱의 제품 로고가 아니라 이미지 파일을 뜻하는 Fluent Color 시스템 아이콘이므로 특정 앱으로 전송하는 동작으로 오인되지 않습니다.  
MIT 고지문은 배포 결과물의 `ThirdPartyNotices` 디렉터리에 포함됩니다.

## 캘린더 내보내기 아이콘

- 원본 저장소: <https://github.com/microsoft/fluentui-system-icons>
- 원본 자산: <https://github.com/microsoft/fluentui-system-icons/blob/16a524d18199ddaa81bef6628cafa48f70cbb4f4/assets/Calendar/SVG/ic_fluent_calendar_24_color.svg>
- 원본: 24×24 SVG
- 원본 SHA-256: `4cd6ce607b4e8c55b40485c31035585077e9d98fc20e31699fe5595c7e36bee1`
- 배포 파일: `src/TimetableGenerator.Desktop/Assets/Export/CalendarExportIcon.png`
- 배포 파일 SHA-256: `4b1b1c05fcd14fd324c4e8b2de706407db5c2180cf84376879559a7b7ed91e47`
- 라이선스: MIT

원본의 비율과 알파 채널을 유지하면서 `sharp`(libvips)로 96×96 RGBA PNG를 만들고 화면에서는 24 DIP로 표시합니다.  
특정 제품 로고가 아닌 일반 Fluent Color 시스템 아이콘으로 Apple Calendar로 내보내는 기능을 나타냅니다.  
Apple의 로고나 Calendar 앱 아이콘을 포함하거나 모방하지 않습니다.  
Fluent UI System Icons의 MIT 고지문이 이 자산에도 적용됩니다.

## Google Calendar 제품 아이콘

- 원본 자산 안내(조직 내부 커뮤니케이션용): <https://knowledge.workspace.google.com/admin/getting-started/brand-your-internal-communications-with-google-workspace?hl=en>
- 공식 자산 묶음: <https://drive.usercontent.google.com/download?id=18IjIi_3-1yqMRcBruNHgpSf0qAI0dN7M&export=download&confirm=t>
- 묶음 내부 파일: `Google-Workspace-logos/Calendar/logo_calendar_2026q2_color_2x_web_32dp.png`
- 원본 및 배포 파일: 64×64 RGBA PNG
- 원본 및 배포 파일 SHA-256: `00ddeb79618160d800ff610dc7ed374c3be6d48072bfa205913676ee68e0c053`
- 배포 파일: `src/TimetableGenerator.Desktop/Assets/Export/GoogleCalendarLogo.png`

Google Calendar는 Google LLC의 상표입니다.  
공식 자산 묶음의 원본 제품 아이콘을 수정하지 않고 Google Calendar 내보내기 동작을 식별하는 용도로만 사용하며, Google의 보증이나 제휴를 의미하지 않습니다.  
이 출처 기록만으로 공개 배포 권한이 자동으로 부여되는 것은 아닙니다. 공개 배포에는 Google의 [제품 아이콘 지침](https://partnermarketinghub.withgoogle.com/brands/google/branding-guidelines/how-to-show-googles-brand/)과 [API 연동 지침](https://about.google/brand-resource-center/guidance/apis/)에 따른 승인 요건이 적용됩니다.
