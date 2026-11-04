# ⚡ Optical Data Diode (망분리 단방향 광학 클립보드 전송 시스템)

[![GitHub License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0--windows-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20x64-0078D6.svg)](https://microsoft.com/)

> **망분리(Air-Gapped) 환경**에서 네트워크 연결 없이, **원격 데스크톱 화면과 웹캠만으로 MS PowerPoint의 복합 도형, 서식, 벡터 그래픽 클립보드 데이터를 100% 무손실 전송하는 광학 데이터 다이오드(Optical Data Diode)** 시스템입니다.

---

## 📌 목차
1. [프로젝트 개요](#-프로젝트-개요)
2. [핵심 기술 및 작동 원리](#-핵심-기술-및-작동-원리)
   - [Win32 저수준 클립보드 직렬화 & 주입](#1-win32-저수준-클립보드-직렬화--주입)
   - [순방향 오류 정정: 파운틴 코드 (Fountain Codes)](#2-순방향-오류-정정-파운틴-코드-fountain-codes)
   - [단방향 광학 채널: 흑백 QR vs 8색 JAB Code](#3-단방향-광학-채널-흑백-qr-vs-8색-jab-code)
   - [OpenCV 실시간 기하 원근 & 동적 색상 왜곡 보정](#4-opencv-실시간-기하-원근--동적-색상-왜곡-보정)
3. [두 가지 에디션 비교](#-두-가지-에디션-비교)
4. [프로젝트 구조](#-프로젝트-구조)
5. [빌드 및 실행 방법](#-빌드-및-실행-방법)
   - [무설치 단일 실행 파일 (Single-File Exe) 바로 사용](#1-무설치-단일-실행-파일-single-file-exe-바로-사용-권장)
   - [원클릭 자동 빌드 (개발 도구 미설치 환경)](#2-원클릭-자동-빌드-개발-도구-미설치-pc)
   - [CLI 수동 빌드 (.NET 8 SDK 환경)](#3-cli-수동-빌드-net-8-sdk-환경)
6. [사용 가이드](#-사용-가이드)

---

## 📖 프로젝트 개요

보안 규정상 외부 네트워크와 물리적으로 격리된 **망분리 PC** 또는 **원격 가상 데스크톱(VDI)** 환경에서는 파일 복사 및 클립보드 공유가 엄격히 차단됩니다.

본 프로젝트는 보안 규정을 침해하지 않으면서 송신 PC 화면에 표시되는 **초고속 애니메이션 2D 바코드(QR / JAB Code)**를 수신 PC의 **웹캠으로 실시간 스캔**하여, 복잡한 MS Office PowerPoint 벡터 객체를 동일하게 복원 및 자동 주입하는 하드웨어 격리형 단방향 데이터 전송 솔루션입니다.

```mermaid
flowchart LR
    subgraph SENDER_PC ["송신 PC (Air-gapped / VDI)"]
        A[PowerPoint 도형 복사] --> B[Win32 GVML/XML 추출]
        B --> C[GZip 압축]
        C --> D[Fountain Codes 인코딩]
        D --> E[고밀도 QR / 8색 JAB 생성]
        E --> F[12~15 FPS 화면 재생]
    end

    subgraph OPTICAL_CHANNEL ["단방향 광학 채널 (Optical Air-Gap)"]
        F -. "모니터 빛 표출" .-> CAM["웹캠 (Webcam)"]
    end

    subgraph RECEIVER_PC ["수신 PC"]
        CAM --> G[OpenCV 원근 & 컬러 보정]
        G --> H[고속 비디오 디코딩]
        H --> I[GF(2) 가우스 소거법 복원]
        I --> J[GZip 압축 해제]
        J --> K[Win32 클립보드 원본 주입]
        K --> L[PowerPoint 붙여넣기 완료]
    end
```

---

## 🔬 핵심 기술 및 작동 원리

### 1. Win32 저수준 클립보드 직렬화 & 주입
.NET 기본 `System.Windows.Clipboard`는 임의의 사용자 정의 오피스 바이너리를 직렬화할 때 포맷 정보 손실이나 `CLIPBRD_E_CANT_OPEN` 예외를 일으킵니다.
- **해결:** Windows 저수준 API(`OpenClipboard`, `GetClipboardData`, `SetClipboardData`, `RegisterClipboardFormat`, `GlobalAlloc`)를 직접 호출.
- PowerPoint 고유의 네이티브 벡터 그래픽 규격인 **`Art::GVML ClipFormat`**, **`Office Open XML`**, **`XML Spreadsheet`**, **`PNG`**, **`CF_DIB`**의 원시 바이트 블롭(Raw Bytes)을 Windows 이동식 메모리 핸들(`GMEM_MOVEABLE`) 단위로 완벽하게 캡처하여 수신 PC에 정확히 주입합니다.

### 2. 순방향 오류 정정: 파운틴 코드 (Fountain Codes)
- **난제:** 단방향 광학 링크는 모니터 주사율과 카메라 셔터 스피드 불일치, 주변 조명 반사로 인해 **심각한 패킷 유실(Frame Drop 30~60%)**이 발생합니다. 양방향 통신(ACK/NACK)이 불가능하므로 재전송 요청을 할 수 없습니다.
- **해결:** Luby Transform(LT) 및 Robust Soliton 분포를 기반으로 한 **무한 분수 코드(Fountain Codes / RaptorQ-style FEC)** 엔진을 자체 C# 구현.
  1. 원본 압축 데이터를 $K$개의 원시 블록으로 분할.
  2. 송신부는 의사 난수 생성기(PRNG Seed = ESI)를 이용해 무한히 다양한 조합의 XOR 인코딩 심볼을 12~15 FPS로 계속 방출.
  3. 수신부는 어떤 순서로 프레임이 누락되든 상관없이, **선형 독립인 $K$개의 유효 패킷만 포착**되면 $GF(2)$ 상의 **점진적 가우스 소거법(Incremental Gaussian Elimination)**을 통해 단 수 밀리초 만에 원본 데이터를 100% 무손실 복원합니다.

### 3. 단방향 광학 채널: 흑백 QR vs 8색 JAB Code
- **OpticalDataDiode (QR 버전):**
  - 표준 고밀도 QR코드(Version 40, Error Correction Level L)를 채택하여 검증된 신뢰성과 범용적인 인식률 제공.
- **JabOpticalDataDiode (JAB Code 버전):**
  - ISO/IEC 23634 규격에 기반한 다색(Polychrome) 2D 매트릭스 채택.
  - 흑백(모듈당 1비트) 대신 **8색 팔레트(Black, Blue, Green, Cyan, Red, Magenta, Yellow, White)**를 사용하여 **모듈당 3비트**를 기록.
  - 1장의 심볼에 약 1,400 바이트의 대용량 데이터를 적재하여 전송 완료 시간을 1/3 수준(3~5초)으로 대폭 단축.

### 4. OpenCV 실시간 기하 원근 & 동적 색상 왜곡 보정
- **원근 기하 보정 (Perspective Warp Rectification):**  
  웹캠이 비스듬한 각도에서 모니터를 촬영하더라도, 4개 코너에 배치된 고유 컬러 파인더 패턴(Red/Green/Blue/Yellow)을 OpenCV가 감지하여 `Cv2.GetPerspectiveTransform` 및 `Cv2.WarpPerspective`로 정방형 평면 이미지로 자동 왜곡 보정합니다.
- **동적 컬러 캘리브레이션 (Dynamic Color Calibration):**  
  모니터 백라이트의 색온도나 실내 조명, 웹캠의 자동 화이트밸런스(AWB)로 인해 발생하는 컬러 왜곡을 해결하기 위해, 바코드 테두리의 8색 기준 셀(Reference Calibration Track)의 RGB 벡터를 프레임마다 실시간 측정하여 최근접 이웃(Nearest Neighbor) 분류를 수행합니다.

---

## ⚖️ 두 가지 에디션 비교

| 구분 | ⚡ OpticalDataDiode (QR 버전) | 🌈 JabOpticalDataDiode (JAB Code 버전) |
| :--- | :--- | :--- |
| **바코드 규격** | ISO/IEC 18004 QR Code (Version 40) | ISO/IEC 23634 Polychrome 2D Barcode (8-Color) |
| **모듈 색상** | 흑백 (1 bit / module) | **8색 컬러 (3 bits / module)** |
| **심볼당 데이터 용량**| 약 400 바이트 | **약 1,400 바이트 (3.5배 증가)** |
| **전송 소요 시간** | 보통 10~15초 소요 | **3~5초 이내 초고속 전송** |
| **웹캠 요구사항** | 저사양 웹캠 / 조명 무관 인식 | 표준 컬러 웹캠 (720p 이상 권장) |
| **주요 적용 시나리오**| 조명이 어둡거나 왜곡이 심한 환경 | 고속 대용량 PPT 전송이 필요한 환경 |

---

## 📂 프로젝트 구조

```
D:\ppt 원격전송\
├── OpticalDataDiode/             # [에디션 1] 고밀도 흑백 QR 코드 전송기
│   ├── OpticalDataDiode.sln       # Visual Studio 솔루션
│   ├── OpticalDataDiode.csproj    # .NET 8 WPF 프로젝트 파일
│   ├── build.ps1 / build.bat      # 자동 빌드 스크립트
│   ├── publish.ps1                # 단일 파일(Single-File Exe) 패키징 스크립트
│   ├── Core/
│   │   ├── Clipboard/             # Win32 Native 클립보드 직렬화/주입기
│   │   ├── Compression/           # GZip 압축 헬퍼
│   │   ├── Fec/                   # 파운틴 코드(Fountain Codes) 무손실 엔진
│   │   ├── Qr/                    # ZXing.Net 고밀도 QR 생성 및 디코더
│   │   └── Camera/                # OpenCvSharp4 비디오 캡처 파이프라인
│   └── Views/                     # 송/수신 WPF UI 대시보드
│
├── JabOpticalDataDiode/          # [에디션 2] 8-Color JAB Code 컬러 전송기
│   ├── JabOpticalDataDiode.sln    # Visual Studio 솔루션
│   ├── JabOpticalDataDiode.csproj # .NET 8 WPF 프로젝트 파일
│   ├── build.ps1                  # 자동 빌드 스크립트
│   ├── publish.ps1                # 단일 파일(Single-File Exe) 패키징 스크립트
│   ├── Core/
│   │   ├── JabCode/               # 8색 컬러 매트릭스, 동적 캘리브레이션, 왜곡 보정
│   │   └── (Clipboard, Fec, Camera 등 동일한 고성능 아키텍처 공유)
│   └── Views/                     # 8색 JAB Code 디스플레이 및 스캐너 UI
│
├── publish/                       # 최종 배포용 단일 실행 파일 폴더
│   └── OpticalDataDiode.exe       # 무설치 단일 실행 파일 (QR 버전)
│
└── README.md                      # 본 종합 설명서
```

---

## 🛠️ 빌드 및 실행 방법

### 1. 무설치 단일 실행 파일 (Single-File Exe) 바로 사용 (권장)
본 프로젝트는 **완전 독립형 단일 실행 파일(Self-Contained Single-File)**로 패키징할 수 있습니다. .NET 런타임이나 Visual C++ 재배포 패키지가 없는 순수 Windows 환경에서도 **`.exe` 파일 단 하나만 복사해가면 즉시 더블 클릭으로 실행**됩니다.

- **흑백 QR 버전 완제품:** `publish\OpticalDataDiode.exe`
- **컬러 JAB Code 버전 완제품:** `JabOpticalDataDiode\publish\JabOpticalDataDiode.exe`

---

### 2. 원클릭 자동 빌드 (개발 도구 미설치 PC)
다른 컴퓨터에 Visual Studio나 .NET SDK가 전혀 설치되어 있지 않아도, 인터넷만 연결되어 있다면 공식 마이크로소프트 채널에서 프로젝트 전용 격리 SDK를 자동 구성하여 빌드를 완료합니다.

```powershell
# 1. 저장소 복제
git clone https://github.com/Laser-Cho/OpticalDataDiode.git
cd OpticalDataDiode

# 2-A. QR 버전 원클릭 빌드
cd OpticalDataDiode
.\build.ps1

# 2-B. JAB Code 버전 원클릭 빌드
cd ..\JabOpticalDataDiode
.\build.ps1
```

---

### 3. CLI 수동 빌드 (.NET 8 SDK 환경)
.NET 8.0 SDK가 설치된 컴퓨터에서는 표준 `dotnet` 명령어로 배포 바이너리를 생성할 수 있습니다.

```powershell
# 8-Color JAB Code 버전 단일 실행 파일 빌드
cd JabOpticalDataDiode
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# QR 코드 버전 단일 실행 파일 빌드
cd ..\OpticalDataDiode
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

---

## 🎮 사용 가이드

### 1. 송신 PC (Sender)
1. PowerPoint에서 전송할 **도형, 표, 텍스트 상자, 슬라이드**를 선택하고 `Ctrl + C`를 누릅니다.
2. `OpticalDataDiode.exe` (또는 `JabOpticalDataDiode.exe`)를 실행하고 **[📤 송신 모드 (Sender)]**를 선택합니다.
3. **[📋 PPT 클립보드 캡처]** 버튼을 클릭합니다.
   - 포맷 개수 및 GZip 압축률, 파운틴 블록 수($K$)가 계산되며 자동으로 QR/JAB 애니메이션이 화면에 재생됩니다.
   - 필요에 따라 FPS 슬라이더(기본 12 FPS)를 조정할 수 있습니다.

### 2. 수신 PC (Receiver)
1. 웹캠이 연결된 수신 PC에서 프로그램을 실행하고 **[📥 수신 모드 (Receiver)]**로 전환합니다.
2. **[📷 웹캠 스캔 시작]**을 클릭하고 카메라가 송신 PC 모니터의 바코드 영역을 비추도록 맞춥니다.
3. 진행률 바가 100%($K$개 블록 수신)에 도달하면 알림음과 함께 **수신 PC 클립보드에 PowerPoint 객체가 자동으로 주입**됩니다.
4. 수신 PC의 PowerPoint를 열고 `Ctrl + V`를 누르면 원본의 벡터 패스, 서식, 색상이 손실 없이 그대로 붙여넣어집니다.

---

## 📄 라이선스
본 프로젝트는 [MIT License](LICENSE)를 따릅니다.
