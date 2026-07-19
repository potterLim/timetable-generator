# 내보내기 아이콘 자산

내보내기 메뉴에는 사용자가 파일 형식과 캘린더 대상을 빠르게 구분할 수 있도록 아래 공식 자산을 사용합니다. 두 자산은 2026년 7월 19일에 내려받았습니다.

## PNG 형식 로고

- 공식 안내 페이지: <https://www.libpng.org/pub/png/pngpic2.html>
- 원본 자산: <https://www.libpng.org/pub/png/img_png/pnglogo--povray-3.7--black833--800x600.png>
- 원본: 800×600 RGBA PNG
- 원본 SHA-256: `16eba198f4f534b0a39ea3f7bc87755d65ef746bb5014559a4401f69e90ee4d3`
- 배포 파일: `src/TimetableGenerator.Desktop/Assets/Export/PngFormatLogo.png`
- 배포 파일 SHA-256: `31c52c6324ae3aaa3820d289d3df5b963bb7404a567f93f4f163dd4259e9dfda`

원본의 비율과 알파 채널을 유지하면서 Pillow의 LANCZOS 리샘플링으로 96×72 RGBA PNG를 만들었습니다. 메뉴 크기에서 필요 이상의 원본 해상도를 포함하지 않기 위한 변환이며, 그림을 다시 그리거나 색을 변경하지 않았습니다.

## Google Calendar 로고

- 공식 자산 안내: <https://knowledge.workspace.google.com/admin/getting-started/brand-your-internal-communications-with-google-workspace?hl=en>
- 공식 자산 묶음: <https://drive.usercontent.google.com/download?id=18IjIi_3-1yqMRcBruNHgpSf0qAI0dN7M&export=download&confirm=t>
- 묶음 내부 파일: `Google-Workspace-logos/Calendar/logo_calendar_2026q2_color_2x_web_32dp.png`
- 원본 및 배포 파일: 64×64 RGBA PNG
- 원본 및 배포 파일 SHA-256: `00ddeb79618160d800ff610dc7ed374c3be6d48072bfa205913676ee68e0c053`
- 배포 파일: `src/TimetableGenerator.Desktop/Assets/Export/GoogleCalendarLogo.png`

Google Calendar는 Google LLC의 상표입니다. 이 로고는 Google Calendar로 내보내는 동작을 식별하는 용도로만 사용하며 Google의 보증이나 제휴를 의미하지 않습니다. 제품을 공개 배포하기 전에는 당시의 [Google 브랜드 지침](https://about.google/brand-resource-center/guidance/)과 필요한 승인 절차를 다시 확인해야 합니다.
