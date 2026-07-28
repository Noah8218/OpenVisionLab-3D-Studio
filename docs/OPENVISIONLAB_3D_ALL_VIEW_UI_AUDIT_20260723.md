# OpenVisionLab 3D Studio 전체 View UI 감사

- 감사일: 2026-07-23
- 대상: 현재 Debug 빌드의 사용자 접근 가능 View
- 기준 작업 영역: Windows 작업 표시줄을 제외한 `1920 x 1040`
- 보조 확인: 활성 Height Profile을 포함한 `1280 x 760`
- 증거 폴더: `artifacts/current/20260723-all-view-ui-audit`

> 사후 처리: 이 감사에서 발견한 `Output Compare` 기본 높이 결함은 같은 날 `docs/OPENVISIONLAB_3D_OUTPUT_COMPARE_USABLE_DEFAULT_20260723.md`에 기록된 수정으로 해소했다. Calibration의 미구현 기능 노출은 `docs/OPENVISIONLAB_3D_CALIBRATION_AVAILABILITY_UI_20260723.md`에서 정리했고, Tool Lab·Calibration·Expert의 고정 라벨/폼 밀도 문제는 `docs/OPENVISIONLAB_3D_UI_LOCALIZATION_AND_DENSITY_20260723.md`에서 해소했다. 아래 표의 관련 행은 수정 전 감사 화면에 대한 판정이며, 공유 Viewer HUD와 동적 실행 증거의 영문 혼용은 후속 범위다.

## 결론

이번 감사 자체는 완료했다. 현재 소스에서 빌드한 실제 EXE로 Shell, Recipe Center, 공용 메시지 대화상자, 공개된 Tool Lab, Calibration 탭, Expert 도킹 View, 하단 검증/증거 View를 열어 총 39장의 현재 화면을 기록했다. 자동 스크린샷 품질 검사가 적용된 33장은 모두 첫 시도에 통과했다.

프로그램 UI가 결함 없이 완성되었다는 뜻은 아니다. 기본 레시피 생성 흐름, 실제 C3D Height Profile, Validation Set, Run Record 및 구현된 Tool Lab은 역할을 수행한다. 반면 다음 세 항목은 다음 UI 우선순위로 처리해야 한다.

1. `Output Compare`는 기본 도킹 높이에서 3D 비교 화면이 사실상 보이지 않는다.
2. Calibration의 Height Calibration, Sensor Alignment, History/Profile 계열 탭은 입력·생성·실행 경로가 없는 빈 골격인데도 완성된 기능처럼 노출된다.
3. 한국어 모드에서도 Tool Lab, Calibration, Expert, Viewer HUD와 PropertyGrid 대부분이 영어이며 일부 긴 레이블이 잘린다.

## 제품 기준

OpenVisionLab 3D Studio의 정체성은 GoPxL Tools/Tool Chaining에서 배운 구조를 따르는 로컬·센서 중립·규칙 기반 3D 검사 레시피 워크벤치다. 핵심 흐름은 다음과 같다.

`3D 입력 -> 타입이 있는 도구 연결 -> 명시적 티칭/미리보기 -> 출력 게시 -> 반복 검증/실행 -> 실행 기록`

이번 감사 시점의 문서상 성숙도는 다음처럼 서로 다른 분모로 유지한다.

- UI 자체 평가: `85/100`
- 좁은 software MVP: `65–70%`
- GoPxL Tools/Chaining 핵심 대비: 약 `60%`
- 전체 GoPxL 산업 플랫폼 대비: 약 `35–40%`
- 물리 교정·계측 신뢰: `미검증`

카메라, PLC, 로봇, 클라우드, 생산 라인 제어는 현재 제품 범위가 아니다.

## 감사 범위와 제외

### 포함

- 메인 Shell: Workbench, Calibration, Expert
- Recipe Center와 공용 메시지 대화상자
- 공개된 10개 Tool Lab
- 기본 도킹 View와 하단 기능/증거 View
- 한국어 기본 화면과 핵심 영어 화면
- 실제 C3D 포인터 조작으로 갱신된 Height Profile

### 제외

- `ThicknessTaskWorkspaceView`: 소스 호환성을 위해 남아 있지만 `IsTaskWorkspaceSelected`가 항상 `false`인 비공개 레거시 View다.
- 카메라·PLC·생산 설비 기능
- 실제 센서 교정, 물리 단위 및 계측 정확도
- 모든 Tool Lab의 `1280 x 720` 실행: Affine Apply와 Re-grid 창의 최소 높이가 `760`이므로 일반적인 `1280 x 720` 작업 영역에 구조적으로 맞지 않는다.

## View별 역할 및 판정

| View 그룹 | 본래 역할 | 실제 확인 | 판정 | 부족한 점 |
|---|---|---|---|---|
| Workbench | 첫 레시피의 입력, 도구 구성, 티칭, 미리보기, 실행 안내 | 입력 전에는 `3D 맵 열기`를 첫 행동으로 안내하고 도구 추가를 막는다. 입력 후 타입 호환 도구를 연결한다. | 역할 적합 | 한국어 모드의 상단 상태, Viewer HUD, Geometry/View 등 혼용 영어 |
| Recipe Center | New/Open/Save/Recent를 별도 창에서 관리 | 빈 레시피 생성·저장 정책과 최근 경로가 보이며 경로는 말줄임 처리된다. | 역할 적합 | 기본 레시피 명칭과 일부 상태에 영문 도메인 명칭이 남음 |
| 공용 메시지 대화상자 | 저장/무시/취소 등 명시적 의사 결정 | 한국어 버튼과 메시지가 정상 표시된다. | 역할 적합 | 이번 화면에서는 잘림 없음 |
| Filter/Edge/2-Point/3-Point/Datum/Intersection Tool Lab | 한 도구의 입력/출력 비교, 파라미터, 명시적 Preview/Publish | 각각 실제 EXE에서 입력과 출력 Viewer, 단계 계약, 속성 및 명시적 실행 경로 확인 | 역할 적합 | 한국어 미적용, 일부 설명 밀도 높음 |
| Correspondence/Affine Solve/Apply/Re-grid Tool Lab | A1/A2/A3 연결과 변환 검증 | 실제 레시피를 열어 매핑·행렬·출력 비교 화면 확인 | 역할 적합 | Solve/Apply/Re-grid의 긴 WPG 레이블·계약 값이 내부에서 잘림 |
| Tool Lab 전체 범위 | 모든 주요 알고리즘을 독립 검증 | 공개된 10개 창은 확인됨 | 부분 충족 | 3D Line Fit 및 Measure 계열 전체에 대해 동등한 독립 Lab이 공개되어 있지는 않음 |
| Calibration Repeatability | 반복 취득 데이터의 통계·차트·판정 | 실제 Study가 로드되고 계산된 표, Run Chart, 요약 확인 | 역할 적합 | Calculate 버튼과 일부 식별자가 잘림. 한국어 모드에서도 대부분 영어 |
| Calibration Overview | 교정 상태 요약과 다음 행동 | 상태 요약은 존재 | 부분 충족 | 넓은 빈 영역, 좌측/상단 탐색 중복, 활성 프로필 생성 경로 불명확 |
| Height Calibration | 높이 교정 티칭/계산 | 빈 표와 `No height target loaded`만 존재 | 역할 미완성 | Load/Create/Teach/Calculate 행동이 없으므로 완성 기능처럼 노출하면 안 됨 |
| Sensor Alignment | 센서 정렬 티칭/계산 | 빈 Transform 표와 `No alignment target loaded`만 존재 | 역할 미완성 | 입력·티칭·계산 행동이 없음 |
| Calibration History/Profile History/Transform | 프로필 이력, 활성화 및 변환 증거 | 빈 상태 화면은 존재 | 역할 미완성 | 프로필 생성/활성화 진입점이 없고 빈 상태만 노출 |
| Expert 도킹 레이아웃 | 고급 사용자가 모든 패널과 증거를 한 화면에서 조합 | 도킹·부동 창 계약과 주요 패널 존재 | 역할 적합 | 한국어 미적용, 비활성 텍스트 대비가 낮고 정보 밀도가 높음 |
| Runner Report | 실행 보고서 열람 | 보고서 원문 표시 | 부분 충족 | 긴 원문이 줄바꿈 없이 수평으로 잘려 읽기 어려움 |
| Steps/History | 실행 단계·이력 조회 | 단계 및 이력 행 존재 | 부분 충족 | Steps 도구명이 잘리고 History 열 의미가 불명확 |
| Flow Map | 타입이 있는 INPUT -> TOOL -> OUTPUT 흐름 설명 | 순서와 타입을 시각적으로 확인 가능 | 역할 적합 | 긴 그래프에서는 탐색 보조가 더 필요할 수 있음 |
| Problems | 오류/경고와 해결 행동 | 문제 설명과 `단계로 이동` 행동 확인 | 역할 적합 | 이번 화면에서는 핵심 결함 없음 |
| Run Record | 다단계 실행 결과, 내보내기 및 최근 기록 | 27단계 기록, JSON/HTML/CSV, 단계별 상태 확인 | 역할 적합 | 일부 도메인 명칭 영어 혼용 |
| Output Compare | 여러 단계 출력을 나란히 3D 비교 | A/B/C 슬롯과 연결 행동은 존재 | 역할 부적합 | 기본 도킹 높이에서 Viewer가 얇은 선 수준이라 비교 작업을 수행하기 어려움 |
| Displayed Outputs | 표시 출력 선택, 3D 표시, 비교 슬롯 고정 | 출력 목록과 `3D에 표시`/비교 고정 행동 확인 | 역할 적합 | 선택 결과와 비교 슬롯의 연결 상태를 더 강조할 여지 |
| Session Log | 세션 이벤트 추적 | 읽기 전용 로그 화면 확인 | 역할 적합 | 설명과 항목이 영어 중심 |
| Height Profile | 3D의 두 점과 하단 높이 프로파일 연동 | 실제 포인터 스모크에서 P1/P2, 선분, 차트, 거리, 높이 차 갱신 확인 | 역할 적합 | 빈 상태와 HUD가 영어 중심 |
| Fit Diagnostics | Line Fit 잔차/인라이어 진단 | 표와 차트, 빈 상태 존재 | 부분 충족 | 빈 상태에서 어느 Line Fit 단계를 Preview해야 하는지 직접 행동이 없음 |
| Intersection Evidence | 교차점과 입력 Line 증거 | 빈 상태와 필요한 상위 결과 설명 존재 | 부분 충족 | 해당 단계로 이동/Preview하는 직접 행동이 없음 |
| Correspondence Evidence | 랜드마크 매핑 증거 | 빈 상태와 필요한 상위 결과 설명 존재 | 부분 충족 | 해당 단계로 이동/Preview하는 직접 행동이 없음 |
| Validation Set | 여러 샘플 반복 실행과 Pass/Fail/Error 비교 | 3개 샘플을 실행해 Pass/Fail/Error 및 실행 기록 확인 | 역할 적합 | 기본 높이에서 선택 샘플과 상세 기록의 관계가 약함 |

## 잘림 및 해상도 판정

### `1920 x 1040`

- 모든 최상위 창의 바깥 경계는 작업 표시줄을 침범하지 않았다.
- Main Shell, Recipe Center, Tool Lab, Calibration 및 Expert 캡처에서 창 전체가 표시됐다.
- 내부 잘림은 별도 결함으로 남는다.
  - XYZ Affine Solve PropertyGrid의 긴 항목명
  - XYZ Affine Apply/Re-grid의 단계 계약 및 속성 값
  - Calibration의 Calculate 버튼과 Measurement/ROI 식별자
  - Expert Runner Report 원문과 Steps 도구명
  - Output Compare의 지나치게 낮은 Viewer 영역

### `1280 x 760`

- 실제 Height Profile 포인터 상호작용과 하단 차트는 화면 안에서 동작했다.
- Affine Apply/Re-grid 최상위 창의 최소 크기가 `1280 x 760`이므로, 작업 표시줄을 제외하면 세로 760보다 작은 화면에서 정상 배치를 보장할 수 없다.
- 따라서 “모든 View가 1280 x 720에서도 잘리지 않는다”는 주장은 현재 할 수 없다.

## 언어 판정

- Workbench와 Recipe Center는 한국어/영어 전환의 핵심 흐름이 존재한다.
- 공용 메시지 대화상자는 한국어로 확인됐다.
- Tool Lab, Calibration, Expert, Viewer HUD, PropertyGrid는 한국어 모드에서도 영어 비중이 높다.
- 사용자 데이터 ID와 알고리즘 고유 명칭은 번역하지 않아도 되지만, 버튼, 상태, 설명, 빈 상태, 표 제목, 속성 표시명은 리소스 기반 다국어 적용 대상이다.

## 기능 증거

| 검증 | 결과 |
|---|---|
| 현재 Debug 빌드 | 통과, 경고 0 / 오류 0 |
| 도킹 계약 | 통과, `27/27` |
| 레시피 티칭 | 통과, `25/25` |
| Recipe Center/WPG/메시지 대화상자 | 통과, `27/27` |
| Height Profile 계산 | 통과, `10/10` |
| Height Profile ViewModel | 통과, `8` checks |
| 실제 Height Profile 포인터 스모크 | P1, P2, 끝점 이동, 핸들 밖 회전, 표시 전용 경계, 소스 바인딩 통과 |
| Run Record History | 통과, `8/8` |
| Validation Set | Pass/Fail/Error 3개 샘플과 전체 27단계/상위 오류 중단 계약 통과 |
| Tool verifiers | Edge, 2-Point Line, 3-Point Plane, Datum, Line Fit, Intersection, Height Measurements 보고서에 FAIL 없음 |
| 스크린샷 품질 | 자동 검사 대상 `33/33` accepted, 총 캡처 `39` |

Calibration ViewModel 검증은 7개 검사를 통과한 뒤 실패했다. 현재 제품은 범용 Recipe Workbench만 공개하지만 검증기가 폐기된 `ThicknessTaskWorkspaceView` 선택과 이전 요약 문구를 아직 기대하기 때문이다. 이는 현재 Repeatability 계산 실패 증거가 아니라 **검증기 계약이 제품 구조를 따라오지 못한 테스트 부채**다.

## 우선순위

1. `Output Compare`의 기본 높이, 최소 사용 영역, 확대/분리 진입을 고쳐 실제 3D A/B/C 비교가 즉시 가능하게 한다.
2. Calibration의 미완성 탭을 숨기거나 `준비 중`으로 명시하고, 공개하려면 입력·티칭·계산·저장/활성화 흐름을 완성한다. 동시에 오래된 Thickness workspace 검증 계약을 범용 Workbench 기준으로 교체한다.
3. Tool Lab, Calibration, Expert, Viewer HUD, PropertyGrid의 한국어/영어 리소스를 완성하고 긴 레이블의 폭·줄바꿈·툴팁을 정리한다.
4. 3D Line Fit과 Measure 계열이 독립 Tool Lab을 정말 필요로 하는지 공통 Lab 템플릿 기준으로 결정하고, 필요한 도구만 동일한 입력/출력/Preview/Publish 검증 경험을 제공한다.
5. `1280 x 720`을 지원 대상으로 유지하려면 Tool Lab 최소 높이와 내부 ScrollViewer 계약을 낮춘다. 지원하지 않을 경우 최소 지원 해상도를 제품 문서와 UI에 명시한다.

## 완료 기록

Status: Complete

Scope: 현재 Debug EXE에서 사용자 접근 가능한 Shell, Recipe Center, 공용 메시지 대화상자, 공개 Tool Lab, Calibration, Expert 및 하단 도킹 View의 역할·가시성·기능 노출·다국어·잘림 상태를 감사하고 현재 화면 증거와 결함 우선순위를 기록함.

Acceptance criteria: 현재 소스 빌드 성공 -> 통과; 실제 EXE 화면 캡처 -> 39장 생성; 주요 한국어/영어 화면 확인 -> 통과; 역할/기능/잘림 판정 -> 본 문서에 기록; 기능 검증과 미검증 경계 분리 -> 기록 완료.

Verification: `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug` 경고 0/오류 0; 도킹 `27/27`; 레시피 티칭 `25/25`; Recipe Center/WPG `27/27`; Height Profile `10/10`; Run Record `8/8`; Validation Set 3상태 실행; 자동 스크린샷 품질 `33/33`.

Evidence: `docs/OPENVISIONLAB_3D_ALL_VIEW_UI_AUDIT_20260723.md`; `artifacts/current/20260723-all-view-ui-audit/`.

Boundary / next dependency: 감사 작업은 완료했지만 발견된 프로그램 결함은 수정하지 않았다. 실제 센서/물리 교정/계측 정확도와 모든 View의 `1280 x 720` 적합성을 증명하지 않는다.
