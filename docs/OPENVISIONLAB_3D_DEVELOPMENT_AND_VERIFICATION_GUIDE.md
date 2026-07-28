# OpenVisionLab 3D Studio 개발 및 검증 가이드

이 문서는 공개 README에서 분리한 개발자용 빌드, 실행, 검증 진입점을
정리합니다. 제품 설명과 일반 사용자 흐름은 저장소 루트의
[`README.md`](../README.md)를 사용합니다.

## 1. 개발 환경

- Windows 10/11 x64
- Visual Studio 2022 또는 .NET SDK `10.0.300` 호환 Feature Band
- PowerShell
- Git
- 실제 WPF 포인터 검증을 위한 대화형 Windows 데스크톱

솔루션의 런타임 중립 프로젝트는 `net10.0`, Viewer와 Shell은
`net10.0-windows` 계열을 사용합니다.

## 2. 복원과 빌드

Debug:

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.sln
dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"
```

Release:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"
```

구조 변경을 완료하기 전에는 솔루션 구성과 프로젝트 책임 경계를 함께
검사합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-code-structure.ps1
```

## 3. 애플리케이션 실행

일반 Inspection Workbench:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug
```

독립 Viewer:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug
```

Headless Runner:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug -- --help
```

## 4. Workbench 집중 검증

한 번 빌드한 뒤 `--no-build`로 필요한 검증만 실행합니다.

```powershell
$artifactDir = "artifacts\verification\local-workbench"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-tool-recipe-selections "$artifactDir\tool-recipe-selections.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-tool-height-measurement-workbench "$artifactDir\height-measurement-workbench.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-inspection-workspace-selection "$artifactDir\inspection-workspace-selection.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-tool-recipe-teaching "$artifactDir\tool-recipe-teaching.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-recipe-manager-wpg "$artifactDir\recipe-manager-wpg.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-workbench-docking "$artifactDir\workbench-docking.txt"
```

2026-07-28 dual-ROI 역할 보존 체크포인트의 기대 결과는 다음과 같습니다.
검사 수가 변경되면 최신 완료 문서를 기준으로 갱신합니다.

| 검증 | 기준 |
| --- | ---: |
| Tool Recipe selections | 29/29 |
| Height measurement Workbench | 46/46 |
| Inspection Workspace | 61/61 |
| Tool Recipe teaching | 28/28 |
| Recipe Manager / PropertyGrid | 37/37 |
| Workbench docking | 33/33 |
| Code structure | 17/17 |

완료 근거:
[`OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md`](OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md)

## 5. 실제 UI 영상과 README GIF

다음 스크립트는 최신 Release를 빌드하고 Wide/Compact 실제 포인터 조작,
영상, Contact Sheet와 README GIF를 생성합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-operator-video-self-review.ps1
```

필수 조건:

- 대화형 Windows 데스크톱
- FFmpeg/FFprobe
- 스크립트가 지정한 로컬 C3D 입력
- 캡처 중 화면 크기와 포커스를 유지할 수 있는 환경

이 검증은 마우스 드래그와 키 입력을 실제 WPF 창에 전달합니다. CI의
비대화형 실행으로 대체하지 않습니다. 생성된 영상과 이미지에는 사적인
창, 다른 애플리케이션, 오래된 빌드가 포함되지 않았는지 커밋 전에 직접
확인합니다.

## 6. Runner와 알고리즘 검증

대표 런타임 중립 검증:

```powershell
$runnerProject = "src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj"

dotnet run --no-build --project $runnerProject -c Release -- --verify-c3d-map-fidelity --report artifacts\verification\c3d-map-fidelity.txt
dotnet run --no-build --project $runnerProject -c Release -- --verify-mesh-deviation --report artifacts\verification\mesh-deviation.txt
dotnet run --no-build --project $runnerProject -c Release -- --verify-nominal-actual-comparison --report artifacts\verification\nominal-actual.txt
dotnet run --no-build --project $runnerProject -c Release -- --verify-registration-acceptance --report artifacts\verification\registration-acceptance.txt
```

특정 레시피 실행:

```powershell
dotnet run --no-build --project $runnerProject -c Release -- --recipe <recipe.ov3d-recipe.json> --report artifacts\verification\recipe-run.txt
```

Runner 검증은 Viewer 표시 샘플 수와 독립적인 원본/계약 결과를 사용해야
합니다. Viewer와 Runner의 결과가 같다는 주장은 동일한 입력, 레시피,
단위와 frame identity가 기록된 경우에만 합니다.

## 7. 데이터 로딩과 Viewer 검증

공개 데이터 로딩 매트릭스:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1
```

단일 GLB/STL/LAS/LAZ 샘플:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\probe-3d-sample.ps1 `
  -SamplePath 3D\PublicSamples\PointCloud\interesting.las `
  -ArtifactDir artifacts\verification\probe-las
```

상세 형식별 입력과 실패 fixture는 다음 문서를 사용합니다.

- [`OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`](OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md)
- [`OPENVISIONLAB_3D_SAMPLE_DATA.md`](OPENVISIONLAB_3D_SAMPLE_DATA.md)
- [`../3D/PublicSamples/README.md`](../3D/PublicSamples/README.md)

## 8. Viewer DLL 번들

별도 WPF 호스트에서 사용하는 Viewer DLL 묶음:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-viewer-dll.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-viewer-dll-host.ps1
```

출력은 `artifacts\viewer-dll\net10.0-windows` 아래 생성됩니다. 통합 계약은
[`OPENVISIONLAB_3D_VIEWER_DLL_INTEGRATION.md`](OPENVISIONLAB_3D_VIEWER_DLL_INTEGRATION.md)를
참조합니다.

## 9. CI

`.github/workflows/ci.yml`의 Windows CI는 다음 범위를 검사합니다.

- 솔루션 restore/build
- 취약하거나 deprecated된 직접/전이 NuGet 패키지
- Viewer DLL 외부 호스트
- Viewer/Shell 화면 품질과 포인터 입력
- Headless Runner와 알고리즘 golden
- C3D mapping과 독립 Python 교차 검증
- 선택된 검증 보고서와 화면 증거 업로드

로컬 검증이 통과했더라도 CI를 실행하지 않았다면 원격 통과로 표현하지
않습니다.

## 10. 검증 문서 찾기

| 목적 | 문서 |
| --- | --- |
| 현재 개발 순서 | [`OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`](OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md) |
| 다음 세션 상태 | [`OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`](OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md) |
| 코드/MVVM 규칙 | [`OPENVISIONLAB_3D_CODE_RULES.md`](OPENVISIONLAB_3D_CODE_RULES.md) |
| Source Quality | [`OPENVISIONLAB_3D_SOURCE_QUALITY_WORKSPACE_20260728.md`](OPENVISIONLAB_3D_SOURCE_QUALITY_WORKSPACE_20260728.md) |
| Height Image | [`OPENVISIONLAB_3D_FULL_HEIGHT_IMAGE_VIEWER_20260727.md`](OPENVISIONLAB_3D_FULL_HEIGHT_IMAGE_VIEWER_20260727.md) |
| Dual ROI 실제 조작 | [`OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md`](OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md) |
| Viewer/Runner 어려운 형상 | [`OPENVISIONLAB_3D_PHASE2_DIFFICULT_GEOMETRY_GOLDENS_20260715.md`](OPENVISIONLAB_3D_PHASE2_DIFFICULT_GEOMETRY_GOLDENS_20260715.md) |

## 11. 완료 전 체크리스트

- [ ] 변경 범위가 제품/도메인 책임 경계를 지켰다.
- [ ] Release 또는 요청된 구성으로 빌드했다.
- [ ] 가장 가까운 focused verification이 통과했다.
- [ ] 구조 변경이면 `verify-code-structure.ps1`이 통과했다.
- [ ] UI 변경이면 현재 빌드의 전후 화면을 캡처하고 비교했다.
- [ ] 실제 포인터가 필요한 검증을 합성 이벤트로 대체하지 않았다.
- [ ] `raw-height`, 물리 단위, calibration과 metrology 경계를 과장하지 않았다.
- [ ] `git diff --check`와 변경 파일 범위를 확인했다.
- [ ] 사용자 소유 로컬 데이터와 무관한 변경을 stage하지 않았다.
