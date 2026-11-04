# 🌈 JabOpticalDataDiode (망분리 8-Color JAB Code 광학 클립보드 전송기)

흑백 QR 코드 대비 3배 이상의 데이터 밀도를 제공하는 **8-Color Polychrome 2D 바코드(JAB Code 스타일)**를 채택하여, 망분리 환경에서 MS PowerPoint의 복합 도형/슬라이드 클립보드 데이터를 웹캠을 통해 초고속으로 전송하는 시스템입니다.

---

## 🌟 핵심 특징

1. **8-Color Polychrome 2D 매트릭스 (`Core/JabCode`)**
   - 흑백(1비트/모듈) 대신 Black, Blue, Green, Cyan, Red, Magenta, Yellow, White의 **8가지 컬러(3비트/모듈)**를 사용합니다.
   - 1장의 심볼 매트릭스($65 \times 65$)에 최대 **약 1,400 바이트**를 수용하여 전송 프레임 수를 1/3로 단축합니다.
2. **동적 컬러 캘리브레이션 (Dynamic Color Calibration)**
   - 바코드 테두리의 8색 레퍼런스 트랙을 실시간 감지하여 모니터 백라이트 및 웹캠의 화이트밸런스 왜곡을 자동 보정합니다.
3. **원근 기하 보정 (Perspective Warp Rectification)**
   - 4개 코너의 고유 컬러 파인더 패턴(Red/Green/Blue/Yellow)을 감지하고 `OpenCvSharp4`를 통해 정방형 직사각형으로 자동 평탄화합니다.
4. **Fountain Codes (FEC) 무손실 복원**
   - 웹캠 프레임 누락에 대비하여 Systematic + Robust Soliton 파운틴 코드를 적용, 순서에 관계없이 $K$개 심볼만 캡처되면 점진적 가우스 소거법으로 100% 무손실 복원합니다.
5. **Win32 저수준 클립보드 직렬화 & 자동 주입**
   - PPT 전용 벡터 포맷(`Art::GVML ClipFormat`, `Office Open XML` 등)을 그대로 전송하여 수신 PC 클립보드에 자동 주입합니다.

---

## 🚀 단일 실행 파일 (Single-File Exe) 빌드
- `publish.ps1`을 실행하면 `publish\JabOpticalDataDiode.exe` 단 하나만 생성되며, .NET 8 런타임과 OpenCV가 모두 내장되어 아무것도 없는 PC에서도 바로 실행 가능합니다.
