# ⚡ Optical Data Diode (망분리 단방향 광학 클립보드 전송기)

망분리(Air-gapped) 환경에서 원격 데스크톱 화면과 웹캠만을 이용해 MS PowerPoint의 도형, 서식, 벡터 그래픽 클립보드 데이터를 광학적으로 전송하는 고성능 단방향 전송 시스템입니다.

---

## 🌟 핵심 특징

1. **저수준 Win32 클립보드 직렬화 & 주입 (`NativeClipboardService`)**
   - 일반 텍스트나 비트맵뿐만 아니라 MS PowerPoint의 네이티브 벡터 그래픽 데이터인 `Art::GVML ClipFormat`, `Office Open XML`, `XML Spreadsheet`, `PNG` 등을 Win32 메모리 블록(`GMEM_MOVEABLE`) 단위로 완벽하게 캡처 및 주입합니다.
   
2. **GZip 고속 스트림 압축 (`CompressionHelper`)**
   - 캡처된 다중 포맷 바이너리 데이터를 GZip을 통해 압축하여 QR 코드 전송에 필요한 심볼 수를 최소화합니다.

3. **무손실 순방향 오류 정정 파운틴 코드 (`Fountain Codes / RaptorQ-style FEC`)**
   - **문제:** 웹캠 스캔 시 화면 주사율 불일치, 프레임 드랍, 왜곡, 조명 반사로 인해 심각한 패킷 유실이 발생합니다.
   - **해결:** Luby Transform / Robust Soliton 분포를 기반으로 한 파운틴 인코더를 자체 구현하여 순서와 무관하게 임의의 $K$개(선형 독립 패킷)만 수신되면 점진적 가우스 소거법(Incremental Gaussian Elimination over GF(2))으로 100% 무손실 복원합니다.
   - 송신부는 화면에 패킷을 무한 루프로 애니메이션(10~15 FPS) 표출하고, 수신부는 도중에 프레임이 대량 누락되어도 복원 임계점에 도달하는 즉시 자동으로 클립보드에 주입합니다.

4. **고속 QR 및 영상 처리 파이프라인 (`ZXing.Net` & `OpenCvSharp4`)**
   - `ZXing.Net` 고밀도 QR (Version 40, Error Correction: L) 고속 비트맵 렌더러
   - `OpenCvSharp4`를 통한 웹캠 비디오 스트림 멀티스레드 비동기 캡처 및 그레이스케일 전처리

---

## 📂 프로젝트 구조

```
D:\ppt 원격전송\OpticalDataDiode\
├── OpticalDataDiode.sln           # Visual Studio 솔루션 파일
├── OpticalDataDiode.csproj        # .NET 8.0 WPF 프로젝트 파일
├── App.xaml / App.xaml.cs         # 애플리케이션 진입점 및 리소스
├── MainWindow.xaml / cs           # 송신/수신 통합 탭 메인 윈도우
├── build.bat                      # 원클릭 자동 빌드 스크립트
├── Core/
│   ├── Clipboard/
│   │   ├── NativeMethods.cs       # Win32 Clipboard/GlobalMemory PInvoke
│   │   ├── ClipboardSnapshot.cs   # 다중 포맷 바이너리 직렬화/역직렬화 모델
│   │   └── NativeClipboardService.cs # PPT GVML/XML 캡처 및 클립보드 주입기
│   ├── Compression/
│   │   └── CompressionHelper.cs   # GZip 압축 및 해제 유틸리티
│   ├── Fec/
│   │   ├── FecPacket.cs           # CRC32 검증 및 ESI 헤더 포함 패킷 모델
│   │   ├── FountainEncoder.cs     # Systematic + Robust Soliton 파운틴 인코더
│   │   └── FountainDecoder.cs     # GF(2) 점진적 가우스 소거법 디코더
│   ├── Qr/
│   │   ├── QrEncoderService.cs    # ZXing 고밀도 QR Bitmap 생성기
│   │   └── QrDecoderService.cs    # OpenCV 프레임 기반 초고속 QR 디코더
│   └── Camera/
│       └── CameraCaptureService.cs# DirectShow 기반 비동기 웹캠 캡처기
└── Views/
    ├── SenderView.xaml / cs       # 송신부 컨트롤러 & Animated QR 뷰어
    └── ReceiverView.xaml / cs     # 수신부 웹캠 프리뷰 & FEC 복원 대시보드
```

---

## 🚀 빌드 및 실행 방법

### 방법 1. 원클릭 빌드 스크립트 실행
[build.bat](file:///D:/ppt%20원격전송/OpticalDataDiode/build.bat)을 더블 클릭하여 실행합니다:
- 시스템에 .NET SDK가 없으면 로컬 환경(`.dotnet`)에 공식 .NET 8 SDK를 자동 다운로드하여 빌드 및 패키지 복원을 완결합니다.
- 빌드가 완료되면 `bin\Release\net8.0-windows\OpticalDataDiode.exe`가 생성됩니다.

### 방법 2. 명령줄 수동 빌드 (.NET 8 SDK 설치 시)
```bash
cd "D:\ppt 원격전송\OpticalDataDiode"
dotnet restore
dotnet build -c Release
```

---

## 🎮 사용 방법

### 1. 송신 PC (Sender)
1. PowerPoint에서 전송할 도형이나 슬라이드를 선택하고 `Ctrl + C`로 복사합니다.
2. `OpticalDataDiode.exe`를 실행하고 **[📤 송신 모드]**에서 **[📋 PPT 클립보드 캡처]**를 클릭합니다.
3. 데이터 크기와 블록 수($K$)가 계산되면 자동으로 QR 애니메이션 방송이 시작됩니다.

### 2. 수신 PC (Receiver)
1. 웹캠을 연결하고 `OpticalDataDiode.exe`를 실행한 뒤 **[📥 수신 모드]**로 전환합니다.
2. **[📷 웹캠 스캔 시작]**을 클릭하고 카메라를 송신 PC 모니터의 QR 코드를 향하게 맞춥니다.
3. 진행률 바가 100%에 도달하면 알림음과 함께 수신 PC 클립보드에 데이터가 자동 주입됩니다.
4. 수신 PC의 PowerPoint에서 `Ctrl + V`를 누르면 원본 도형 및 서식이 그대로 붙여넣어집니다.
