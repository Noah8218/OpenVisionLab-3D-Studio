# OpenVisionLab 3D Calibration Center 및 테마 설계

Updated: 2026-07-17
Status: **Phase A-C 기반선과 Phase D explicit offline Study loading/result binding 로컬 검증 완료**

## 1. 목적

이 문서는 기존 두께·Warpage 검사 UI 설계에 다음 내용을 추가한다.

1. 3D 높이 보정(Calibration)
2. 단일·다중 센서 얼라인(Alignment)
3. 두께 및 3D 측정 반복도(Repeatability)
4. 장기 안정성·드리프트 검토
5. 보정 프로파일 생성·검증·활성화·이력 관리
6. OpenVisionLab 3D Studio 전체 색상과 테마 규칙

Calibration Center의 목적은 보정값을 입력하는 화면을 만드는 것이 아니다. 물리 단위와 좌표계가 어떤 근거로 생성됐고, 현재 검사 결과에 어떤 보정 프로파일이 적용됐는지 추적할 수 있게 만드는 것이다.

초기 구현은 **가져온 반복 측정 데이터와 보정 데이터 파일을 분석하는 센서 중립형 오프라인 워크플로**로 제한한다. 직접 센서 연결, 트리거, 노출, PLC, 생산 통신은 하드웨어 계약이 정해지기 전까지 포함하지 않는다.

## 2. 용어와 제품 경계

### Calibration과 Alignment를 분리한다

| 구분 | 의미 | 대표 결과 |
| --- | --- | --- |
| Calibration | 센서 원시값을 물리값으로 변환하고 bias, scale, linearity를 보정한다. | Z offset/scale, XY scale, 잔차, 유효 범위 |
| Alignment | 센서 좌표계를 시스템 또는 작업물 공통 좌표계로 변환한다. | 4x4 transform, XYZ offset, XYZ rotation, overlap residual |
| Verification | 기존 보정 프로파일이 현재도 허용 기준을 만족하는지 확인한다. | Pass/Fail, 최대 잔차, 반복도, 환경 범위 |
| Repeatability | 같은 센서·작업자·셋업·환경에서 반복했을 때의 단기 변동이다. | 표준편차, 범위, 6-sigma spread, run chart |
| Reproducibility | 작업자, 날짜, 장비 또는 셋업이 달라졌을 때의 변동이다. | 요인별 분산, 장기 변동 |
| Gauge R&R | 부품·작업자·반복 횟수를 계획한 측정시스템 분석이다. | Repeatability + Reproducibility |

같은 센서로 두께를 연속 측정한 결과는 우선 `Repeatability Study`로 표시한다. 작업자·부품·장비·날짜 요인이 없는 데이터에 `Gauge R&R`이라는 이름을 붙이지 않는다.

### 신뢰성 경계

- 인증값과 추적성이 확인된 기준물이 없으면 `physical calibration passed`로 주장하지 않는다.
- 단일 평면 한 장은 Z zero/offset 확인에는 사용할 수 있지만 전체 측정 범위의 선형성 보정 근거가 될 수 없다.
- `R²`가 높아도 최대 잔차와 범위별 잔차가 나쁘면 보정 실패다.
- 보정 프로파일 활성화와 검사 결과 Publish는 별도 명시적 사용자 동작이다.
- Draft 또는 Validation Failed 프로파일은 물리 검사 결과에 적용할 수 없다.
- 기존 Active 프로파일은 새 프로파일이 검증되고 명시적으로 활성화될 때까지 유지한다.

## 3. 전체 작업 모드

상단 WPF-UI 작업 모드를 다음과 같이 확정한다.

```text
Setup | Inspect | Calibrate | Review
```

`Calibrate`는 별도 프로그램이나 팝업이 아니다. 기존 AvalonDock Shell에서 작업공간 프리셋만 바뀐다.

```text
+------------------------------------------------------------------------------------------+
| Job Bar: Open | Import Set | Calculate | Validate | Activate Profile | Export Record    |
| Mode: Setup | Inspect | [Calibrate] | Review                     Active profile: ...   |
+----------------------+------------------------------------------------+------------------+
| Calibration Explorer | Calibration Document                           | Calib Inspector  |
|                      |                                                |                  |
| Sensors              | Overview | Height | Alignment | Repeatability | Study settings   |
| Targets              |                                                | Target metadata  |
| Capture Sets         | 3D residual / repeatability map                | Acceptance gates |
| Studies              |                                                | Environment      |
| Profiles             | value grid + chart or fit/residual chart       | Result summary   |
| History              |                                                | Actions          |
+----------------------+------------------------------------------------+------------------+
| Evidence: source hashes | target certificate | fit report | profile diff | audit log       |
+------------------------------------------------------------------------------------------+
| Status: sensor | unit | frame | active profile | validity | temperature | source count    |
+------------------------------------------------------------------------------------------+
```

## 4. Calibration Explorer

왼쪽 트리는 다음 정보를 관리한다.

```text
Calibration Center
  Sensors
    Sensor A
    Sensor B
  Targets
    Flat Reference Plate
    Step Height Master
    Alignment Plate
  Capture Sets
    2026-07-17 Thickness Repeatability
    2026-07-17 Z Height Levels
  Studies
    Repeatability 01
    Height Calibration 01
    Sensor Alignment 01
  Profiles
    CAL-Z-0001 Draft
    CAL-Z-0002 Active
  History
    Validation Records
    Superseded Profiles
```

각 항목은 ID와 이름을 모두 가진다. 표시 이름 변경이 프로파일 ID, 원본 해시 또는 결과 이력을 바꾸지 않는다.

## 5. Calibration Center 문서 탭

### 5.1 Overview

현재 사용 가능한 센서, 활성 프로파일, 유효기간, 마지막 검증 결과, 적용 가능한 단위·좌표계를 한 화면에서 보여준다.

필수 상태:

- `No Profile`
- `Draft`
- `Calculated`
- `Validation Failed`
- `Validated`
- `Active`
- `Superseded`
- `Expired`
- `Invalidated`
- `Metadata Unknown`

### 5.2 Height Calibration

센서 Z 높이값의 offset, scale, linearity를 검토한다.

권장 입력:

- 서로 다른 인증 높이를 가진 여러 기준 단계;
- 각 높이 단계의 반복 측정 파일;
- 기준물 ID, 인증값, 단위, 인증서 정보;
- 센서 ID/Serial, 측정 범위, 해상도, 설정 메타데이터;
- 환경 온도와 측정 시각.

화면 구조:

```text
+--------------------------------------+---------------------------------------+
| 3D / Height Map                      | Certified vs Measured                  |
| 기준 단계와 측정 ROI                 | scatter + fitted line                  |
| 선택 단계, 유효점, 제외점            | ideal y=x + fitted result              |
+--------------------------------------+---------------------------------------+
| Value Grid                           | Residual Chart                         |
| Level | Certified | Mean | StdDev    | residual by height                    |
| Bias | Valid | Run Count | Status    | zero line + acceptance bands          |
+--------------------------------------+---------------------------------------+
```

필수 결과:

| Metric | 의미 |
| --- | --- |
| Z offset | zero reference의 보정량 |
| Z scale | 원시 Z 변화량을 물리 높이로 바꾸는 계수 |
| Maximum absolute residual | 전체 검증 높이 중 가장 큰 절대 잔차 |
| RMS residual | 적합 후 전체 잔차 크기 |
| Linearity | 측정 범위에서 직선 모델로 설명되지 않는 최대 편차 |
| Repeatability per level | 각 인증 높이에서의 반복 표준편차와 범위 |
| Valid range | 실제로 기준값이 존재해 검증된 최소·최대 높이 |
| Coverage | 유효 측정 포인트 또는 ROI 비율 |
| R-squared | 참고값이며 단독 합격 기준으로 사용하지 않음 |

### 5.3 Sensor Alignment

단일 센서의 작업물 좌표 정렬과 다중 센서의 공통 좌표계 결합을 지원한다.

초기 방법:

1. Flat plate 5-DOF alignment
2. Two-feature plate 6-DOF alignment
3. Overlap surface alignment
4. 저장된 외부 4x4 transform 검증

화면 구조:

```text
+------------------------------------------------+-------------------------------+
| 3D Alignment Viewer                            | Transform Inspector           |
| Sensor A/B source colors                       | TX TY TZ                      |
| target model / fitted planes                   | RX RY RZ                      |
| common frame axes                              | source -> system frame        |
| overlap difference surface                     | correspondence / coverage     |
+------------------------------------------------+-------------------------------+
| Residual Heat Map | Residual Histogram | Sensor Pair Grid | Validation Evidence       |
+------------------------------------------------------------------------------------------+
```

필수 결과:

- 센서별 XYZ translation;
- 센서별 XYZ rotation;
- 원본 센서 좌표계와 결과 시스템 좌표계;
- 대응점/평면 수와 유효 coverage;
- RMS, 표준편차, 최대 절대 residual;
- 겹침 영역 signed difference map;
- 기준물 요구조건 충족 여부;
- transform fingerprint와 입력 파일 해시.

얼라인 성공 여부를 transform 값의 존재만으로 판단하지 않는다. 대응점, coverage, residual, target visibility가 모두 acceptance gate를 통과해야 한다.

### 5.4 Repeatability

첫 번째 Calibration 기능으로 권장하는 화면이다. 현재 두께 검사 결과를 반복 실행한 뒤 값 그리드와 차트를 동시에 표시한다.

```text
+------------------------------------------------------------------------------------------+
| 3D Repeatability Map                                                                    |
| point/cell별 표준편차 또는 range heat map | selected ROI | worst repeatability marker   |
+-----------------------------------------+------------------------------------------------+
| Values Grid                             | Run Chart                                      |
| Run | Time | Mean | Min | Max           | thickness value by run                         |
| Range | StdDev | Coverage | Status      | mean, reference, limits, selected-run cursor  |
| Source hash | Profile | Environment     | optional range/stddev chart                    |
+-----------------------------------------+------------------------------------------------+
| Summary: N | Mean | StdDev | Range | 6s spread | Bias* | Drift* | Pass/Fail             |
+------------------------------------------------------------------------------------------+
```

#### 기본 값 그리드 열

| 열 | 설명 |
| --- | --- |
| Run | 반복 실행 순번과 안정 ID |
| Timestamp | 측정 시각 |
| Source | 입력 파일/프레임 ID |
| Calibration Profile | 사용 프로파일 ID와 해시 |
| Thickness Min/Mean/Max | 선택 ROI의 두께 통계 |
| Thickness Range | 같은 Run 내부의 공간적 범위 |
| Run Delta | 전체 평균 또는 인증값 대비 차이 |
| Valid Coverage | 유효점 비율 |
| Environment | 온도 등 확보된 메타데이터 |
| Status | Included, Excluded, Invalid, Pass, Fail |

Warpage 반복도 연구에서는 같은 그리드 구조를 사용하되 `Peak-to-Valley`, `Max Positive`, `Max Negative`, `RMS`를 표시한다.

#### 차트 종류

1. **Run Chart**: 반복 순서별 대표값, 평균선, 기준값, 허용 상·하한
2. **Range/StdDev Chart**: 반복별 공간 range 또는 그룹별 표준편차
3. **Histogram**: 반복값 분포와 outlier 위치
4. **Drift Chart**: 시간 간격이 충분한 연구에서만 시간 대비 변화량
5. **3D Repeatability Heat Map**: 각 점/셀의 표준편차 또는 range

차트와 값 그리드는 선택 상태를 공유한다. 그리드의 Run을 선택하면 차트 cursor와 3D map의 해당 결과가 함께 바뀐다. 차트가 선택된 값을 숨기거나 자체적으로 데이터 제외를 수행하지 않는다.

#### 통계 표시 규칙

- `N`, 평균, 표준편차, 최소, 최대, range는 항상 계산 조건을 같이 기록한다.
- `6s spread`는 `6 x sample standard deviation`으로 명시하고 tolerance와 혼동하지 않는다.
- Bias는 인증 기준값이 있을 때만 표시한다.
- Drift는 실제 시간 축과 충분한 기간이 있을 때만 표시한다.
- 변동계수는 평균이 0에 가깝거나 signed 값일 때 숨긴다.
- 제외된 Run은 원본에서 삭제하지 않고 제외 이유를 기록한다.
- 적은 반복 횟수에서 나온 표준편차를 확정된 센서 성능으로 표시하지 않는다.
- Gauge R&R은 부품·작업자·반복 구조가 정의된 별도 연구에서만 활성화한다.

### 5.5 History

프로파일 비교와 검증 이력을 표시한다.

- 현재 Active와 이전 Active의 offset/scale/transform 차이;
- Validation 결과와 실패 원인;
- 대상 기준물과 인증서 식별 정보;
- 입력 데이터 해시;
- 실행 소프트웨어/알고리즘 버전;
- 생성자, 검토자, 활성화 시각;
- Superseded/Expired/Invalidated 이유;
- 연결된 검사 Run Record 목록.

## 6. Calibration Inspector

오른쪽 Inspector는 선택된 Study 또는 Profile 하나만 편집한다.

공통 섹션:

1. Study identity
2. Sensor and acquisition metadata
3. Target and certified values
4. ROI / valid volume
5. Method and fit options
6. Acceptance gates
7. Environment
8. Result summary
9. `Calculate`, `Validate`, `Activate` 명시적 명령

`Activate`는 현재 검사 결과의 물리 의미를 바꾸는 동작이므로 확인 절차와 이전 Active 프로파일 표시가 필요하다.

## 7. 보정 프로파일 계약

향후 Model은 최소한 다음 정보를 보존해야 한다.

```text
Profile ID / Version / State
Sensor ID / Serial / Firmware / Optical setup
Calibration method / Target ID / Certificate reference
Source capture IDs and hashes
Input unit / Output unit
Sensor frame / System frame
Z offset / Z scale / XY scale and skew when available
4x4 extrinsic transform when available
Validated measurement volume and environmental range
Residual and repeatability metrics
Acceptance thresholds and validation result
Created / validated / activated timestamps
Software, algorithm, recipe and profile fingerprints
```

모든 Published 검사 결과와 Runner Run Record는 사용한 Calibration Profile ID, 버전, 해시, 당시 유효 상태를 기록한다.

## 8. 프로파일 상태 전이

```text
Draft
  -> InputsReady
  -> Calculated
  -> Validated
  -> Active
  -> Superseded / Expired / Invalidated

Calculated
  -> ValidationFailed
  -> Correct Inputs
  -> Calculated
```

상태 규칙:

1. 계산 성공이 검증 성공을 의미하지 않는다.
2. 검증 성공이 자동 활성화를 의미하지 않는다.
3. Active 프로파일 교체는 명시적 동작이다.
4. 센서/렌즈/해상도/설치 위치/좌표 변환이 달라지면 자동 적용하지 않고 `Needs Review` 또는 `Invalidated`로 전환한다.
5. 환경값이 검증 범위를 벗어나면 경고와 함께 물리 검사 Publish를 차단할 수 있어야 한다.
6. 메타데이터를 확인할 수 없으면 `Unknown`으로 표시하며 임의로 동일하다고 간주하지 않는다.

## 9. 최종 테마 결정

### 기본 테마

**Neutral Light Industrial + Dark 3D Canvas**를 기본 테마로 확정한다.

- Shell, Explorer, Inspector, Grid, Chart 배경은 중립적인 밝은 색을 사용한다.
- 3D Viewer만 어두운 charcoal 배경을 사용해 형상, 포인트, 컬러맵, overlay 대비를 확보한다.
- 전체 Dark Theme은 첫 Light Theme 화면 검증 이후 별도 작업으로 미룬다.
- 배경 gradient, 장식용 색상 blob, 과도한 card UI를 사용하지 않는다.

### Semantic color tokens

| Token | Color | 용도 |
| --- | --- | --- |
| `AppBackground` | `#F3F4F6` | 전체 작업영역 배경 |
| `PanelBackground` | `#FFFFFF` | Explorer, Inspector, Grid |
| `PanelBackgroundAlt` | `#F9FAFB` | 선택되지 않은 하위 영역 |
| `Divider` | `#D1D5DB` | splitter, border, grid line |
| `TextPrimary` | `#111827` | 제목과 주요 값 |
| `TextSecondary` | `#4B5563` | 보조 설명과 metadata |
| `TextMuted` | `#6B7280` | 비활성·부가 정보 |
| `Accent` | `#0F766E` | 선택, Active profile, 주요 navigation |
| `AccentHover` | `#115E59` | Accent hover/pressed |
| `Focus` | `#2563EB` | 키보드 focus와 running selection |
| `Pass` | `#15803D` | 검증·검사 합격 |
| `Warning` | `#B45309` | Stale, Needs Review, 범위 경고 |
| `Fail` | `#B91C1C` | 검증·검사 실패 |
| `Info` | `#0369A1` | 실행 중, 정보 상태 |
| `Disabled` | `#9CA3AF` | 사용할 수 없는 명령 |
| `ViewportBackground` | `#202124` | 3D Viewer 배경 |
| `ViewportGrid` | `#4B5563` | 바닥 grid와 보조선 |

### 축 색상

| Axis | Color |
| --- | --- |
| X | `#E05252` |
| Y | `#2F9D62` |
| Z | `#3B82F6` |

축 색상은 테마와 무관하게 동일하게 유지하고 축 문자 `X/Y/Z`를 같이 표시한다.

### 과학 데이터 컬러맵

애플리케이션 상태 색과 측정 컬러맵을 분리한다.

| 데이터 | 기본 컬러맵 | 규칙 |
| --- | --- | --- |
| Height / Thickness raw value | Viridis | 연속값, 낮음 -> 높음 |
| Thickness tolerance status | Blue / Green / Red / Gray | 하한 미만 / 정상 / 상한 초과 / invalid |
| Warpage / signed deviation | Blue - White - Red | 0을 중앙에 고정하고 대칭 범위 사용 |
| Alignment residual | Blue - White - Red | signed residual, 0 중심 |
| Repeatability sigma/range | Cividis | 낮은 변동 -> 높은 변동 |
| Source color | Original Colors | 원본 RGB가 있을 때 |

ImageJ 스타일 요구를 위해 `Original Colors`, `Grayscale`, `Spectrum`, `Fire`, `Thermal`, `Gradient`, `Blue`, `Orange` 선택지는 유지할 수 있다. 그러나 검사 기본값은 위 표의 의미 기반 컬러맵으로 고정한다.

모든 컬러맵은 다음을 포함한다.

- 숫자 최소·최대;
- 단위;
- tolerance 또는 zero marker;
- invalid/no-data 색상;
- 선택점의 실제 값;
- 색 이외의 Pass/Fail 텍스트.

### 타이포그래피와 밀도

- Font: `Segoe UI`
- Pane title: `14-15 px`, SemiBold
- Body/value: `12-13 px`
- Metadata/axis tick: `11 px`
- Toolbar height: `36-40 px`
- Icon button: 안정적인 고정 크기
- Corner radius: 기본 `2-4 px`
- Panel은 card로 감싸지 않고 splitter와 section header로 구분한다.

### WPF 리소스 소유권

향후 View 구현 시 다음 원칙을 사용한다.

- Shell이 WPF-UI `ThemesDictionary Theme="Light"`와 `ControlsDictionary`를 로드한다.
- 색상은 XAML에 반복 하드코딩하지 않고 Shell의 semantic theme resource로 정의한다.
- Docking.Controls는 검사 상태를 모르며 host theme resource만 소비한다.
- Viewer의 scientific color map은 Shell accent color와 독립적으로 관리한다.
- 첫 View-only 단계에서는 새 Dark Theme와 테마 전환 기능을 만들지 않는다.

## 10. View -> ViewModel -> Model 구현 순서

사용자 지시에 따라 전체 Calibration 기능도 다음 순서를 지킨다.

### Phase A - View

1. 전역 semantic theme resource와 WPF-UI Job Bar
2. `Calibrate` 작업 모드와 AvalonDock Calibration Center 레이아웃
3. Overview, Height, Alignment, Repeatability, History 문서 탭
4. Repeatability의 3D map + 값 그리드 + 차트 동시 배치
5. `1600 x 900`, `1280 x 760` 현재 소스 화면 캡처

이 단계에서는 보정 계산을 구현하거나 가짜 결과를 표시하지 않는다.

### Phase B - ViewModel

1. 선택 Study/Profile 상태
2. Calculate/Validate/Activate Command와 활성 조건
3. Grid/Chart/3D selection 동기화 상태
4. profile 상태 전이와 validation 오류 표현
5. View 코드비하인드에는 시각 브리지 외 비즈니스 상태를 두지 않는다.

### Phase C - Model

1. Repeatability Study 입력/결과 계약
2. Calibration Profile과 Validation Record
3. Z offset/scale/linearity 계산 계약
4. Sensor Alignment transform/residual 계약
5. Runner 재실행과 Run Record profile fingerprint

차트 구현은 직접 그린 임시 Polyline을 확대하지 않는다. Phase A 캡처에서 요구 상호작용을 확정한 뒤, 라이선스와 대용량 성능을 검토한 검증된 WPF chart library를 선택한다.

## 11. 권장 개발 순서

1. 전역 테마와 Calibration Center View-only 화면
2. 오프라인 Thickness Repeatability ViewModel
3. Repeatability typed Model과 검증 계산
4. Repeatability Run Chart와 Grid/Chart/3D 공통 선택
5. Z Height Calibration ViewModel/Model
6. Sensor Alignment ViewModel/Model
7. Profile 활성화와 검사 Run Record 연동
8. 직접 센서 연결은 별도 하드웨어 아키텍처 승인 후 검토

물리 신뢰성 관점에서는 두께·Warpage 알고리즘을 늘리기 전에 최소한 Repeatability와 Active Calibration Profile 추적 기능을 먼저 확보하는 것이 안전하다.

## 12. 수용 기준

- [x] 상단 모드에 Setup, Inspect, Calibrate, Review가 명확히 구분된다.
- [ ] Calibration Center에서도 3D Viewer가 주요 시각 근거로 유지된다.
- [x] Repeatability 화면에 3D map, 값 그리드, Run Chart가 동시에 보인다.
- [ ] Grid, Chart, 3D 선택이 같은 Run/point/ROI를 가리킨다.
- [ ] Calibration과 Alignment가 별도 도구와 결과 계약을 가진다.
- [x] 같은 셋업 반복 측정을 Gauge R&R로 잘못 표시하지 않는다.
- [ ] Height Calibration은 offset, scale, residual, linearity, repeatability를 함께 표시한다.
- [ ] Alignment는 transform 외에 coverage와 residual을 필수로 표시한다.
- [ ] 새 프로파일은 Calculate, Validate, Activate를 명시적으로 거친다.
- [ ] 모든 Published 검사와 Run Record가 Calibration Profile fingerprint를 가진다.
- [ ] 물리 보정이 없거나 metadata가 불명확하면 raw/model unit 상태가 유지된다.
- [x] Light Shell과 Dark Viewer의 색상 대비가 `1600 x 900`, `1280 x 760`에서 검증된다.
- [x] 상태는 색상뿐 아니라 텍스트와 숫자로도 표현된다.
- [x] Calibration Center Phase A-C에서 View -> ViewModel -> Model 순서와 프로젝트 경계를 지킨다.

## 13. 공식 참고 자료

2026-07-17에 공식 또는 기관 자료를 확인했다.

1. [LMI GoPxL Performing the Alignment](https://am.lmi3d.com/manuals/gopxl/gopxl-1.0/G3/Content/WebInterface/Acquire/AlignmentPanel/Aligning_with_Alignment_panel.htm) - alignment target, 5/6-DOF, sensor transform, Z zero, target quality 조건.
2. [LMI Gocator 2300 specifications](https://am.lmi3d.com/manuals/gopxl/gopxl-1.1/LMILaserLineProfiler/Content/Specs/G2/Gocator2300Series/Gocator2300Series.htm) - Z linearity, resolution, repeatability의 구분과 반복 측정 조건.
3. [LMI Surface Align Wide](https://am.lmi3d.com/manuals/gopxl/gopxl-1.1/LMILaserLineProfiler/Content/Inspect_toolTopics/SurfaceAlignWide.htm) - 다중 센서 target, alignment uncertainty, difference surface와 transform 출력.
4. [ZEISS ATOS](https://www.zeiss.com/metrology/en/systems/optical-3d/3d-scanning/atos.html) - calibration 상태, transform 정확도, 환경 변화, 부품 움직임의 지속 피드백.
5. [ZEISS surface-based decalibration check](https://www.zeiss.com/metrology/en/software/zeiss-inspect/zeiss-inspect-optical-3d/zeiss-inspect-optical-3d-release.html) - 측정 중 calibration 유효성 감시.
6. [PolyWorks|Inspector](https://www.polyworks.com/en-us/products/polyworks-inspector) - repeatability, Gauge R&R, SPC, alignment/coordinate 기반 control view.
7. [NIST Gauge R&R studies](https://www.itl.nist.gov/div898/handbook/mpc/section4/mpc4.htm) - artifact, operator, gauge/configuration을 포함하는 연구 설계와 repeatability/reproducibility 구분.
8. [NIST uncertainty from gauge studies](https://www.itl.nist.gov/div898/handbook/mpc/section4/mpc46.htm) - repeatability, reproducibility, stability, bias, linearity, drift를 불확도 평가와 연결할 때의 경계.

## 14. 승인안

권장 최종 방향은 다음과 같다.

```text
Neutral Light WPF-UI Shell + Dark SharpGL Viewer
  -> Inspect: Thickness / Warpage
  -> Calibrate: Height / Alignment / Repeatability / History
  -> explicit Calculate -> Validate -> Activate
  -> every Published result records the active calibration profile
```

승인 후 첫 구현은 **전역 테마 리소스와 Calibration Center의 View-only 레이아웃**이며, 계산이나 센서 연결 없이 화면 캡처 검토에서 다시 멈춘다.

## 15. Phase A View-only 구현 후보 (2026-07-17)

현재 구현은 사용자 화면 검토를 위한 후보이며, Calibration 기능 완료를 뜻하지 않는다.

사용자는 2026-07-17 이 화면 구조를 승인했다. 아래 내용은 승인 당시 Phase A 범위를 기록한 것이다.

구현한 범위:

- WPF-UI Light Shell에 의미 기반 색상 리소스를 추가했다.
- 상단에 `Setup | Inspect | Calibrate | Review` 모드를 배치했다. 현재 `Inspect`와 `Calibrate`만 선택 가능하다.
- Calibration 전용 AvalonDock 레이아웃을 `Explorer | Workspace | Inspector | Evidence` 구조로 추가했다.
- Repeatability 화면에서 빈 3D map, 빈 Values Grid, 빈 Run Chart를 동시에 표시한다.
- 값이 없을 때 `Not calculated`, `No repeatability runs`, `No active calibration profile`을 표시하며 가짜 측정값은 만들지 않는다.
- Phase A의 `Calculate`, `Validate`, `Activate`는 ViewModel 계약이 생길 때까지 비활성 상태로 고정했다.

아직 구현하지 않은 범위:

- Calibration Center 상태와 선택을 소유하는 ViewModel
- 반복도 입력, 통계, 허용 기준, 프로파일 상태 전이 Model
- 실데이터 3D 반복도 맵, Grid/Chart/3D 연동 선택
- Height Calibration, Sensor Alignment 계산
- Active Calibration Profile fingerprint와 Published result/Run Record 연결
- 센서 직접 연결과 물리 캘리브레이션 신뢰성 검증

현재 소스 검증:

- solution build: 경고 `0`, 오류 `0`
- 고정 데이터/로더/Shell matrix: `128 PASS`, `0 FAIL`
- Viewer BinaryHost: `ProjectReference=0`, manifest `13/13`, outputs `12/12`, Host API commands `3/3`, exit `0`
- 캘리브레이션 화면 품질: `1600 x 900`, `1280 x 760` 모두 첫 캡처에서 accepted
- 기존 Inspect C3D Shell 화면도 현재 빌드에서 accepted

증거 경로:

- before: `artifacts/calibration_center_view_20260717/before/shell_inspect_1280x800.png`
- after: `artifacts/calibration_center_view_20260717/after/calibration_center_1600x900.png`
- after compact: `artifacts/calibration_center_view_20260717/after/calibration_center_1280x760.png`
- Inspect regression: `artifacts/calibration_center_view_20260717/after/shell_inspect_1280x800.png`
- matrix: `artifacts/calibration_center_view_20260717/regression/matrix/matrix_smoke_summary_after.txt`
- BinaryHost: `artifacts/calibration_center_view_20260717/regression/viewer-dll-host`

화면 구조 승인은 완료되었다. 이후에도 구현 순서는 **ViewModel -> Model**이며, Model 통계가 검증되기 전에는 실제 값이나 Pass/Fail을 화면에 연결하지 않는다.

## 16. Phase B ViewModel 기반선 (2026-07-17)

Phase B는 승인된 View 구조에 상태와 명령 계약만 연결한 단계다. Calibration 계산 기능 완료나 물리 신뢰성 통과를 뜻하지 않는다.

구현한 범위:

- `ShellMainWindowViewModel`이 `Inspect | Calibrate` 작업공간 상태, 파생 선택 상태, 작업공간 요약, 명시적 선택 Command를 소유한다.
- `CalibrationCenterViewModel`이 `Overview | Height Calibration | Sensor Alignment | Repeatability | History` 선택과 중앙 탭 동기화를 소유한다.
- Repeatability metric, 빈 입력/결과/프로파일 표시, Inspector 표시 값, Grid/Evidence 공통 Run 선택을 ViewModel 속성으로 노출한다.
- `Calculate`, `Validate`, `Activate` Command와 요청 이벤트를 만들었지만, 실제 Model이 계산 가능 상태를 제공하기 전에는 실행 불가다.
- View는 `TwoWay` 선택 바인딩과 읽기 전용 `OneWay` 표시 바인딩만 사용한다. Calibration View code-behind에는 동작이나 업무 규칙을 추가하지 않았다.
- 값 그리드는 향후 Model 결과를 받을 typed presentation row를 사용하지만, 빈 상태에서는 측정값이나 Pass/Fail을 만들지 않는다.

검증 결과:

- solution build: 경고 `0`, 오류 `0`
- Calibration Center ViewModel 계약: `33 PASS`, `0 FAIL`
- 실제 WPF 입력: `Inspect -> Calibrate` 전환 성공, `Repeatability -> Height Calibration` 전환 시 Explorer와 중앙 탭 선택 일치
- 캡처 품질: `1280 x 760` Repeatability와 Height Calibration 모두 첫 캡처에서 accepted
- 고정 데이터/로더/Shell matrix: `128 PASS`, `0 FAIL`
- Viewer BinaryHost: `ProjectReference=0`, manifest `13/13`, outputs `12/12`, Host API commands `3/3`, exit `0`

증거 경로:

- ViewModel: `artifacts/calibration_center_viewmodel_20260717/calibration_center_viewmodel_verification.txt`
- 작업공간/섹션 입력: `artifacts/calibration_center_viewmodel_20260717/after/mode_section_interaction.txt`
- Repeatability: `artifacts/calibration_center_viewmodel_20260717/after/calibration_repeatability_1280x760.png`
- Height Calibration: `artifacts/calibration_center_viewmodel_20260717/after/calibration_height_1280x760.png`
- matrix: `artifacts/calibration_center_viewmodel_20260717/regression/matrix/matrix_smoke_summary_after.txt`
- BinaryHost: `artifacts/calibration_center_viewmodel_20260717/regression/viewer-dll-host`

다음 게이트는 오프라인 Thickness Repeatability typed Model이다. 입력 식별자와 단위/프레임, Run별 대표값, 평균, 표준편차, 범위, 최소 Run 수, 허용 기준, 불완전·비유한 입력 거부를 먼저 순수 계산 계약과 golden test로 고정한다. 그 검증이 끝나기 전에는 Grid, Chart, 3D map에 실제 결과를 연결하지 않는다.

## 17. Phase C offline Thickness Repeatability Model (2026-07-17)

Phase C는 View와 ViewModel을 변경하지 않고 순수 Model/계산 경계만 추가했다. 화면에 실제 값이나 Pass/Fail을 표시하지 않으며 Calibration 기능 완료를 뜻하지 않는다.

계약과 소유 경계:

- `OpenVisionLab.ThreeD.Core/ThicknessRepeatabilityContracts.cs`가 Study ID, Measurement Definition ID, Reference ROI ID, unit/frame, Run ID, source entity ID, capture timestamp, 두께값, 최소 Run 수, 표본 표준편차 한계, range 한계와 결과 계약을 소유한다.
- `OpenVisionLab.ThreeD.Tools/ThicknessRepeatabilityRule.cs`가 렌더링과 독립된 입력 검증과 계산을 소유한다.
- `OpenVisionLab.ThreeD.Runner/ThicknessRepeatabilityGoldenVerification.cs`가 analytic/acceptance/error 정답을 소유한다.
- `.github/workflows/ci.yml`은 다음 push부터 이 golden을 필수 단계로 실행한다. 이 로컬 작업에서는 원격 Windows Actions를 아직 실행하지 않았다.

계산 계약:

- 입력 순서는 보존하며 평가 결과는 검증된 Run과 Input의 별도 스냅샷을 가진다.
- 평균, 최소, 최대, range를 계산한다.
- 표준편차는 population이 아닌 `N-1` sample standard deviation을 사용한다.
- `6s spread`는 tolerance가 아니라 `6 x sample standard deviation`으로 별도 기록한다.
- sample standard deviation과 range는 독립된 최대 허용 기준으로 판정하며 경계값은 Pass다.
- Welford 누적 계산을 사용하고 큰 공통 오프셋에서도 작은 변동을 보존한다.
- 같은 source entity를 두 Run으로 재사용하지 못하게 하여 한 취득 데이터를 반복 측정처럼 중복 집계하지 않는다.
- 이 결과는 같은 셋업의 repeatability이며 operator/part/reproducibility 요인이 없는 Gauge R&R가 아니다.

오류 방어:

- 누락된 Study/Measurement/ROI identity, unit/frame, acceptance policy, Runs를 거부한다.
- 최소 Run 수는 sample standard deviation을 위해 2 이상이어야 하며, 실제 Run 수가 policy보다 적으면 통계를 표시하지 않는다.
- 중복 Run ID, 중복 source entity, 누락 timestamp, unit/frame 불일치, null Run, NaN/Infinity 두께값을 거부한다.
- 음수 또는 비유한 허용 기준과 비유한/overflow 통계 결과를 거부한다.

현재 소스 검증:

- solution build: 경고 `0`, 오류 `0`
- Thickness Repeatability golden: `34 PASS`, `0 FAIL`
- 기존 알고리즘 golden suite 9개: 모두 Pass
- Calibration Center ViewModel: `33 PASS`, `0 FAIL`
- 고정 데이터/로더/Shell matrix: `128 PASS`, `0 FAIL`
- Viewer BinaryHost: `ProjectReference=0`, manifest `13/13`, outputs `12/12`, Host API commands `3/3`, exit `0`

증거 경로:

- primary golden: `artifacts/thickness_repeatability_model_20260717/thickness_repeatability_golden.txt`
- all goldens: `artifacts/thickness_repeatability_model_20260717/regression/goldens`
- ViewModel preservation: `artifacts/thickness_repeatability_model_20260717/regression/calibration_viewmodel.txt`
- matrix: `artifacts/thickness_repeatability_model_20260717/regression/matrix/matrix_smoke_summary_after.txt`
- BinaryHost: `artifacts/thickness_repeatability_model_20260717/regression/viewer-dll-host`

Phase C 시점의 다음 게이트는 **실제 offline study input -> ViewModel result binding**이었으며, 아래 Phase D에서 완료했다. 명시적으로 로드된 유효 Study가 없으면 `Calculate`를 비활성화하고 빈 Grid/Chart/3D map을 유지한다. 검증용 synthetic 값은 명시적 smoke에서만 사용하며 제품 화면 기본값으로 노출하지 않는다.

## 18. Phase D explicit Study loading 및 결과 바인딩 (2026-07-17)

Phase D는 제품 기본값을 만들지 않고 사용자가 선택한 offline Study만 검증해 `CalibrationCenterViewModel`에 연결한다. View의 `Load Study`는 Command 바인딩이며, `MainWindow` code-behind는 WPF `OpenFileDialog`와 ViewModel 메서드를 잇는 얇은 플랫폼 브리지만 소유한다.

입력 계약:

- JSON은 `studyType=thickness-repeatability`, `schemaVersion=1.0`의 closed schema다. 알 수 없는 속성과 지원하지 않는 type/version은 거부한다.
- 각 Run은 Run/source identity, 상대 또는 절대 source path, byte length, SHA-256, capture timestamp, unit/frame, 대표 두께값을 가진다.
- source 파일은 읽기 전용 stream에서 길이와 SHA-256을 다시 계산한다.
- 같은 경로 또는 같은 SHA-256의 파일은 별도 취득으로 집계하지 않는다. 현재 저장된 두 C3D 파일처럼 바이트가 동일한 데이터는 반복도 입력으로 사용할 수 없다.
- 로더는 파일 근거를 검증하고, Tools의 `Validate`는 identity, unit/frame, acceptance, 최소 Run 수를 통계 계산 없이 검증한다.
- unit/frame은 Study 내부에서 정확히 일치하는지 검증하는 declared metadata다. 독립 센서 metadata나 calibration evidence가 없으면 물리적 진실로 승격하지 않으며 UI도 `Declared unit / frame`으로 표시한다.

명시적 상태 전이:

```text
No Study
  -> Load Study
  -> Invalid: Calculate disabled, prior result cleared
  -> Ready: verified Run rows visible, Calculate enabled, no statistics
  -> Calculate
  -> Calculated: aggregate statistics and Pass/Fail visible
```

ViewModel 소유 범위:

- `LoadStudyCommand`는 파일 선택 요청만 발생시킨다.
- `LoadStudy(path)`는 로더와 입력 검증을 실행하고 이전 결과를 먼저 제거한다.
- `CalculateCommand`는 유효 Study에서만 Model을 실행한다. 로드만으로 통계를 자동 계산하지 않는다.
- Run Grid와 Evidence Grid는 하나의 `SelectedRepeatabilityRun`을 공유한다.
- `Validate`와 `Activate`는 Calibration Profile 계약이 없으므로 계속 비활성화한다.
- 대표값 Study에는 per-point 표준편차가 없으므로 3D map은 `Aggregate result`로 명시하고 per-point map을 가장하지 않는다.

현재 소스 검증:

- solution build: 경고 `0`, 오류 `0`
- Thickness Repeatability Model: `34 PASS`, `0 FAIL`
- closed-schema/file-identity Study loader: `13 PASS`, `0 FAIL`
- Calibration Center ViewModel workflow: `48 PASS`, `0 FAIL`
- Runner verification options: `11/11` suites Pass, including all established algorithm/map gates and the new Study loader
- fixed data/loading/interaction/Shell matrix: `128 PASS`, `0 FAIL`
- Viewer BinaryHost: `ProjectReference=0`, manifest `13/13`, outputs `12/12`, Host API commands `3/3`, exit `0`
- `1280 x 760` Study loaded/not-calculated 및 calculated 화면: 모두 첫 캡처 accepted
- CI workflow YAML parse: Pass
- 원격 Windows Actions는 이 미커밋 작업에 대해 아직 실행하지 않았다. workflow에는 Model, loader, ViewModel 단계를 필수 gate로 추가했다.

증거 경로:

- before: `artifacts/thickness_repeatability_study_binding_20260717/before/calibration_repeatability_1280x760.png`
- loader: `artifacts/thickness_repeatability_study_binding_20260717/model/thickness_repeatability_study_loader.txt`
- Model: `artifacts/thickness_repeatability_study_binding_20260717/model/thickness_repeatability_golden.txt`
- ViewModel: `artifacts/thickness_repeatability_study_binding_20260717/viewmodel/calibration_center_viewmodel_verification.txt`
- loaded/not calculated: `artifacts/thickness_repeatability_study_binding_20260717/after/study_loaded_not_calculated_1280x760.png`
- calculated: `artifacts/thickness_repeatability_study_binding_20260717/after/study_calculated_1280x760.png`
- all Runner gates: `artifacts/thickness_repeatability_study_binding_20260717/regression/goldens`
- matrix: `artifacts/thickness_repeatability_study_binding_20260717/regression/matrix/matrix_smoke_summary_after.txt`
- BinaryHost: `artifacts/thickness_repeatability_study_binding_20260717/regression/viewer-dll-host`

화면에 사용한 세 값은 verifier가 artifact 폴더에 명시적으로 생성한 synthetic smoke fixture다. 일반 실행의 초기 상태는 계속 빈 상태이며, 이 fixture는 실제 센서 반복도나 물리 보정 증거가 아니다.

현재 loader는 source 길이와 SHA-256을 UI thread에서 동기적으로 계산한다. 작은 offline Study에는 충분하지만 대형 반복 취득 묶음에는 progress/cancel 가능한 background loading이 필요하다.

다음 게이트는 대표값 Run Chart를 실제로 그린 뒤 Grid/Chart 선택을 공유하고, aligned per-point 반복 취득 계약이 준비되면 동일 선택을 3D map까지 확장하는 것이다. 그 다음에 Height Calibration과 Sensor Alignment를 각각 별도 typed slice로 진행한다.
