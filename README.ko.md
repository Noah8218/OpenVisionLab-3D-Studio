# OpenVisionLab 3D Studio

[English](README.md) | 한국어

3D 검사 조건을 티칭하고 레시피로 저장해 여러 호환 파일에 반복 적용합니다.

로컬 3D 데이터의 규칙 기반 검사 조건을 설정하고 검사를 반복하는 Windows 데스크톱 앱입니다.
도구 입력을 연결하고 검사 영역과 하한·상한을 설정한 뒤, 파일별 측정값과 결과를 확인합니다.

[![CI](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml)
![Windows](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D4) ![.NET](https://img.shields.io/badge/.NET-10-512BD4) [![License](https://img.shields.io/badge/license-Apache--2.0-green)](LICENSE)

![휨 검사, 9점 보정, 두께 검사와 저장·재열기](docs/assets/openvisionlab-3d-linked-inspection.gif)

## 3단계로 시작하기

1. `https://github.com/Noah8218/OpenVisionLab-3D-Studio.git`를 clone하고 저장소 폴더로 이동합니다.
2. [Shell을 빌드하고 실행합니다](#소스에서-빌드).
3. **Recipe Center**에서 [Thickness Coupon Recipe](3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json)를 엽니다.
   연결된 입력 파일도 같은 폴더에 둡니다. 입력 데이터와 `raw-height` 단위는
   [샘플 안내](3D/Samples/ThicknessCouponV1/README.md)를 확인합니다.

자체 실행형 Windows 패키지가 있다면 압축을 풀고
`OpenVisionLab.ThreeD.Shell.exe`를 실행합니다. .NET 런타임이 포함되어 있습니다.
[Windows 패키지 시작 안내](docs/OPENVISIONLAB_3D_WINDOWS_PACKAGE_QUICK_START.md)를 확인합니다.

## 지원 작업

- 호환되는 도구 입력을 연결하고 Recipe의 ROI를 설정합니다.
- HeightMap을 전처리하고 Warpage 또는 기준면에 대한 Thickness를 측정합니다.
- 호환되는 파일을 일괄 검사하고 파일별 결과와 저장된 Run Record를 확인합니다.
- 3D·높이 이미지·단면을 보고, C3D HeightMap에 정렬된 이미지를 텍스처로 표시합니다.

## 입력부터 검사 결과까지

Recipe와 3D 입력을 열고 티칭 조건을 조정한 뒤 검사를 실행해 파일별 결과를 확인합니다.

![실사 재질을 적용한 합성 높이 맵의 보정 전 휨 검사](docs/assets/openvisionlab-3d-warpage-before.png)

1. 저장된 Recipe를 열거나 새 Recipe를 만들고 3D 파일을 불러옵니다.
2. 도구를 추가하고 호환되는 입력을 연결한 뒤 필요한 ROI를 지정합니다.
3. 설정을 적용하고 선택한 도구를 실행해 결과를 확인합니다.
4. 레시피를 저장하고 검사 목록에 호환되는 파일을 추가합니다.
5. 검사를 실행하고 파일별 상태·측정값·하한·상한을 확인합니다.
6. 저장된 Run Record를 열어 당시 조건과 결과를 확인합니다.

파일 열기, 결과 선택, 파라미터 편집, 저장한 설정 복원, 표시 설정 변경만으로 검사나 샘플 검증을 실행하지 않습니다.

![저장·재열기 후 보정 MAP의 9개 두께 검사 결과](docs/assets/openvisionlab-3d-thickness-after-reopen.png)

## 포함된 Thickness Coupon 예제

저장소와 자체 실행형 패키지에는 패드 8개를 각각 검사하는 Thickness Coupon이 포함됩니다.

![기준·측정 영역이 짝을 이루는 Thickness Coupon](3D/Samples/ThicknessCouponV1/source-height-preview.png)

| 항목 | 포함 내용 |
| --- | --- |
| 높이 격자 | 1280 × 840 |
| 배치 | 4 × 2로 배열된 패드 8개 |
| 레시피 | Thickness 단계 8개와 편집 가능한 ROI 16개 |
| ROI 조건 | 기준 영역과 측정 영역은 같은 패드 안에 배치 |
| 단위 | `raw-height` |

**Recipe Center**에서 다음 레시피를 엽니다.

```text
3D\Samples\ThicknessCouponV1\inspection-recipe.ov3d-recipe.json
```

C3D 입력 파일은 레시피 옆에 있습니다. 저장소나 패키지 폴더 전체를 옮기면 예제를 계속 사용할 수 있습니다.

## 도구 순서 구성

Recipe에 전처리·측정 도구를 배치하고 각 단계의 입력·영역·파라미터를 설정합니다.
전체 Recipe를 실행하면 생성한 출력을 연결된 후속 도구에 전달합니다.

![연결된 전처리·측정 단계의 전체 실행](docs/assets/openvisionlab-3d-linked-inspection.gif)

| 작업 | 도구 | 확인할 내용 |
| --- | --- | --- |
| HeightMap 전처리 | Median filter, noise removal, leveling, 9점 곡면 보정, ROI crop | 입력·처리 후 HeightMap |
| 표면 휨 측정 | Warpage | ROI 안의 PV와 RMS 편차 |
| 높이 차 측정 | Thickness | 기준면에서 측정 영역까지의 부호 있는 높이 차 |

사용 가능한 도구는 입력 형식과 필요한 영역에 따라 달라집니다.
HeightMap 측정 도구에는 호환되는 HeightMap 입력이 필요합니다. 메시와 포인트클라우드는 별도로 볼 수 있습니다.

<details>
<summary>다른 검사 도구와 입력 제약</summary>

## 검사 범위

| 구분 | 작업과 제약 |
| --- | --- |
| 입력 확인 | C3D 레시피 입력, Viewer 전용 GLB·STL·LAS·LAZ 가져오기, 진행·취소, Source Quality 근거와 선택적 센서→장면 방향 |
| 전처리 | Median filter, 노이즈 제거, 표면 평탄화, 별도의 불변 HeightField를 만드는 ROI/Crop. 호환 후속 도구에서 생성된 출력 사용 |
| 높이 검사 | Thickness, Warpage, Plane Flatness, Height Deviation, Gap/Flush, Volume, 격자·영역 통계 |
| 형상 검사 | 점 쌍·단면 측정, 선·평면·에지·랜드마크, Nominal/Actual 메시 비교와 보존된 Surface Match 검토 |
| 티칭 | 3D와 전체 해상도 높이 이미지의 GridRectangle·GridCircle·GridPolygon 편집, 명시적 검토·적용·취소·삭제. GridPolygon 윤곽과 별도 Grid Polygon Domain 단계의 명시적 격자 영역 출력을 구분 |
| 검증 | Good·Bad·Held-out 샘플 역할, 실행 결과, 실패 분석, 임계값 검토와 Held-out 재실행 |
| 결과 근거 | Viewer 오버레이, 측정값, 보고서, 출력 비교, 매칭 검토, 헤드리스 Runner 기록과 개인정보를 제외하는 지원 ZIP |
| 레시피 저장 | 단계 순서·입력·파라미터·ROI 역할·출력·검증 설정 저장과 복원 |

측정값은 입력이 선언한 단위를 따릅니다. `raw-height`를 물리 단위로 자동 변환하지 않습니다.
물리 공차를 적용하려면 교정과 판정 기준을 먼저 확인해야 합니다.

3D Viewer 도구 모음에서 로컬 데이터를 가져옵니다. `C3D`는 레시피 입력으로 연결하고,
`GLB`·`STL`·`LAS`·`LAZ`는 Viewer에만 표시해 기존 레시피 입력을 보존합니다.
`.gltf`·OBJ·PCD·XYZ·TIFF·RAW를 일반 가져오기 형식으로 안내하지 않습니다.
Surface Match 실험은 검토 전용이며 일반 검사 도구로 취급하지 않습니다.

</details>

## 입력 품질과 지원 자료

Source Quality의 입력 계약 편집기에서 취득 근거의 유무, 근거와 알려진 제약을 기록합니다.
센서→장면 방향을 아는 경우 소스 좌표계의 XYZ 방향을 입력하고 **Apply source contract**를 선택합니다.
정규화한 방향은 기존 에지 법선의 표시 분류에만 사용합니다. 매칭 결과를 바꾸거나
형상에서 카메라 자세·교정·시점을 추정하지 않습니다.

지원 자료를 공유할 때는 **Results → Run Record**에서 **Export privacy-safe support bundle**을 선택합니다.
ZIP에는 매니페스트, 민감한 정보를 제거한 레시피, 최근 세션 로그 최대 200개, 입력 식별 정보,
기록된 Source Quality와 현재 결과가 포함됩니다. 원본 3D·메시 데이터, 절대 경로,
전체 앱 로그와 사용자·PC 식별 정보는 기본적으로 제외합니다. 전송 전에 내용을 확인합니다.
전체 Run Record 내보내기는 이 지원 ZIP과 별도입니다.

## Warpage 측정

선택한 ROI의 유효한 점으로 기준면을 피팅하고, 표면 편차의 PV와 RMS 값을 확인합니다.
9점 곡면 보정은 후속 검사에 사용할 별도의 보정 HeightMap을 만듭니다.
아래 합성 예제의 휨은 보정 전 538.13 µm, 보정 후 0 µm입니다.

![보정 전 휨 검사, 9점 보정과 보정 MAP 검사](docs/assets/openvisionlab-3d-linked-inspection.gif)

## Thickness 측정

기준 ROI와 측정 ROI를 지정합니다. Thickness는 피팅한 기준면에서 측정 영역까지의
부호 있는 H축 높이 차를 반환합니다. 기준면에 대한 높이 차이며,
서로 대응하는 위·아래 표면 한 쌍의 두께를 측정하는 기능은 아닙니다.
보정 HeightMap을 Thickness 단계에 연결하고 3D Viewer의 측정값과 OK/NG 표시를 확인합니다.
그림의 9개 영역은 7곳 OK, 2곳 NG입니다.


## 3D 보기와 텍스처

3D·높이 이미지·단면 뷰에서 입력과 도구 결과를 확인합니다.
뷰어는 회전·이동·맞춤, 좌표 표시와 ROI 오버레이를 제공합니다.
화면의 숫자는 소수점 둘째 자리까지 표시하며, 계산과 저장 값은 원래 정밀도를 유지합니다.

C3D HeightMap에서는 **Color and height display**를 열고 <strong>Choose image…</strong>로
전체 HeightMap에 정렬된 이미지를 선택합니다. **Show texture**는 이미지와 높이 색상 표시를
전환하고, **Remove**는 텍스처를 해제합니다. 저장한 선택은 같은 Recipe와 입력을 열 때
이미지 파일 검증 후 복원됩니다. 텍스처 표시는 높이 값이나 검사 입력을 바꾸지 않습니다.

<details>
<summary>작은 화면 레이아웃</summary>

![작은 화면에서 확인하는 보정 MAP의 측정 결과](docs/assets/openvisionlab-3d-linked-compact.png)

</details>

시연에는 합성 C3D 데이터와 실사 재질 이미지를 사용했습니다. 이미지 밝기는 높이 측정값이
아니며, 예시는 보정된 카메라 촬영 데이터가 아닙니다.
포함된 [Thickness Coupon](3D/Samples/ThicknessCouponV1/README.md)에는 바로 실행할 수 있는
Recipe가 있으며, 단위는 `raw-height`로 명시되어 있습니다.

## 소스에서 빌드

Windows 10 build 19041 이상 또는 Windows 11 x64, Git, PowerShell 5.1 이상과
[global.json](global.json)이 선택하는 SDK(`10.0.300`, `latestFeature`)가 필요합니다.
뷰어에는 OpenGL 호환 GPU와 드라이버가 필요합니다.
`OpenVisionLab.ThreeDStudio.slnx` 또는 대응하는 `.sln`을 열고
시작 프로젝트를 `OpenVisionLab.ThreeD.Shell`로 지정합니다.

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore
dotnet run --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -c Release --no-build
```

고정된 의존성은 `third_party`에 포함되며 패키지 소스는 [NuGet.Config](NuGet.Config)에 있습니다.
별도 SDK 저장소를 받을 필요는 없습니다. 자세한 내용은
[소스 설정과 프로젝트 안내](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md)를 확인합니다.

Python 3.13은 전체 독립 검증에 필요하며 일반 빌드·앱 실행에는 필요하지 않습니다.
검사 명령은 [개발·검증 안내](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md#local-paths-and-tests)를 확인합니다.

<details>
<summary>소스 빌드 도구 확인·설치</summary>

다음 명령은 설치 상태만 확인합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -CheckOnly -Scope Build
```

누락된 도구는 고정된 `winget` 패키지 ID로 설치합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -InstallMissing -Scope Build
```

Windows·NuGet의 경로 길이 문제를 줄이려면 짧은 로컬 경로에 clone합니다.
설정 안내에는 NuGet 캐시 경로와 재설치 후 복구 절차도 있습니다.

</details>

주 앱은 `src/OpenVisionLab.ThreeD.Shell`이며 `src/OpenVisionLab.ThreeDStudio`는 Viewer 전용 호스트입니다.
시작 프로젝트를 고를 때 [코드 진입 경로](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md#start-here)를 확인합니다.

## 자체 실행형 Windows 패키지 만들기

소스 저장소에서 다음 명령을 실행합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-windows-app.ps1
```

기본 출력은 `artifacts\release\openvisionlab-3d-studio-win-x64`입니다.
앱, 런타임, 샘플, 레시피, 사용자 문서, 라이선스 고지와 SHA-256 매니페스트가 포함된
폴더형 `win-x64` 패키지입니다. [패키지 시작 안내](docs/OPENVISIONLAB_3D_WINDOWS_PACKAGE_QUICK_START.md)를 확인합니다.

## 키보드 단축키

| 단축키 | 동작 |
| --- | --- |
| `Ctrl+N` | 새 레시피 |
| `Ctrl+O` | 레시피 열기 |
| `Ctrl+Shift+O` | C3D 높이 입력 열기 |
| `Ctrl+S` | 레시피 저장 |
| `Ctrl+Shift+S` | 다른 이름으로 레시피 저장 |
| `F5` | 선택 도구 실행 |
| `Ctrl+F5` | 현재 레시피 실행 |
| `Enter` | 현재 ROI 후보 적용 |
| `Esc` | 현재 ROI 후보 취소 |
| `Delete` | 선택한 지원 ROI 또는 레시피 항목 삭제 |

## 문서

**사용자 문서:** [첫 검사](docs/OPENVISIONLAB_3D_USER_TUTORIAL.md) · [샘플 데이터](docs/OPENVISIONLAB_3D_SAMPLE_DATA.md)

**개발자 문서:** [SDK 패키지 경계](docs/OPENVISIONLAB_3D_VISION_SDK_PACKAGE_BOUNDARY_20260805.md) · [실행 프로젝트와 Viewer Host](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md#start-here) · [문서 색인](docs/README.md)

- [개발·검증 안내](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md#local-paths-and-tests)
- [샘플 출처·고지](3D/PublicSamples/README.md)

- [기여 규칙과 버전 관리](AGENTS.md)

현재 버전: `v0.8.1-dev` · [변경 이력](CHANGELOG.md)

## 합성 입력 검사 화면

다음 화면은 합성 높이 맵을 사용합니다. 텍스처는 외관을 나타내며 측정값은
높이 데이터에서 계산합니다. 센서 실측 정확도나 LAS 피킹 검증을 뜻하지 않습니다.

![합성 입력의 도구 선택과 검사 결과](docs/assets/openvisionlab-3d-tools.gif)

![합성 높이 맵의 워페이지 검사](docs/assets/openvisionlab-3d-warpage.gif)

![결과 라벨 9개가 있는 두께 검사](docs/assets/openvisionlab-3d-thickness.gif)

![Wide 레이아웃의 합성 텍스처 검사](docs/assets/openvisionlab-3d-texture-wide.png)

![Compact 레이아웃의 합성 텍스처 검사](docs/assets/openvisionlab-3d-texture-compact.png)

## 상태와 제한

- 최신 원격 Workbench 검사에서 도킹·렌더 정리, 임계값 후보 검토, 속성 편집기 초기 문구 항목이 실패했습니다. 원인은 분류 중이며 전체 CI 통과본이 아닙니다.

- Dock 메뉴의 실제 복귀와 대좌표 LAS의 서로 다른 네 점 피킹은 아직 확인하지 못했습니다. 계산·미디어 검사 완료는 이 두 동작의 통과를 뜻하지 않습니다.

- 현재 버전은 개발용 소스 스냅샷이며, 운영 환경 검증 완료를 뜻하지 않습니다. 태그·릴리스·배포는 별도 승인 단계입니다.
- 측정 단위는 입력 데이터를 따릅니다. 포함된 예시는 `raw-height` 단위이며 보정된 카메라 촬영 데이터가 아닙니다.
- 도구 선택 중 UI가 멈출 수 있습니다. 일부 네이티브 대화상자의 복구, 분리 패널과 버튼을 누른 채 유지하는 상태는 검증되지 않았습니다.

## OpenVisionLab 프로젝트

| 프로젝트 | 용도 |
| --- | --- |
| [2D Workbench](https://github.com/Noah8218/OpenVisionLab) | 규칙 기반 2D 검사 Recipe |
| [3D Studio](https://github.com/Noah8218/OpenVisionLab-3D-Studio) | 3D 티칭, Thickness와 Warpage |
| [Labeling Studio](https://github.com/Noah8218/OpenVisionLab-Labeling-Studio) | 라벨링·학습·모델 비교 |
| [Machine Studio](https://github.com/Noah8218/OpenVisionLab-Machine-Studio) | 하드웨어 준비 전 머신 시뮬레이션 |
| [Vision SDK](https://github.com/Noah8218/OpenVisionLab-Vision-SDK) | 공통 C# 검사 라이브러리 |

## 라이선스

[Apache License 2.0](LICENSE)에 따라 상업적 사용·수정·재배포가 허용됩니다.
배포물에는 `LICENSE`, [NOTICE](NOTICE), 저작권과 필요한 출처 고지를 유지해야 합니다.
외부 구성 요소에는 각각의 라이선스가 적용됩니다.

```text
This project includes software developed by Noah Choi.
Copyright (c) 2026 Noah Choi.
```
