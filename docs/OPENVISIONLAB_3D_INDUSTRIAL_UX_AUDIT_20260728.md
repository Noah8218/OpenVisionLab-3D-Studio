# OpenVisionLab 3D Studio 산업용 3D 검사 UX 감사

## 2026-07-29 개발 방향 반영

이 문서의 11개 영상 분석은 일회성 참고 자료가 아니라 앞으로의
제품 개발 방향으로 유지한다. 세부 영상별 관찰·타임코드·근거 수준은
아래 감사 기록을 보존하고, 실제 구현 순서와 완료 상태는 다음 문서를
현재 기준으로 사용한다.

- `OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`
- `OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`
- `OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md`

현재 방향은 다음과 같다.

1. GoPxL에서 확인한 작업 책임 분리를 유지한다.
2. SICK에서 확인한 Good/Bad 근거 기반 임계값과 Completeness 흐름을
   개발한다.
3. HALCON에서 확인한 모델/장면 준비, pose, score, 진단 근거를 갖춘
   Surface Matching으로 확장한다.
4. MERLIC에서 확인한 Height Image 기반 셀/충진 검사를 개발한다.
5. Zivid/Photoneo에서 확인한 입력 품질과 결측/범위 진단을 측정 신뢰의
   선행 조건으로 유지한다.

상용 제품의 시각 스타일이나 하드웨어 플랫폼을 복제하지 않는다.
카메라 취득, 스테레오 재구성, PLC/로봇/필드버스, 클라우드/플랜트
관리는 현재 범위 밖이다. 편집에 따른 자동 실행도 도입하지 않는다.

`I-12/I-13/I-15` 임계값 보조 강화와 `L-11` 수정 근거 Run Record
통합은 2026-07-29 완료되었다. 현재 마스터 재고는
`93 Complete / 17 Partial / 99 New / 9 External / 16 Out of scope`, 총
`234`개다. 다음 구현 우선순위는 `H-02/H-03/H-04 Completeness 셀
그리드·coverage·기준 상대 높이 metric`이다.

작성일: 2026-07-28

대상: 제공된 상용 영상 11개와 현재 OpenVisionLab 3D Studio Release

제품 경계: 로컬 파일 기반, 결정론적 2.5D/3D 규칙 검사 워크벤치

이 보고서는 상용 제품의 외형이나 기능 수를 평가하지 않는다. 사용자가 검사 조건을
가르치고, 실행하고, 결과를 신뢰하고, 오검을 재현하는 과정에서 반복되는 구조와
오류 방지 방식을 기준으로 현재 제품을 평가한다.

근거 수준은 다음 세 값만 사용한다.

- `영상에서 확인됨`: 영상의 화면 또는 음성/자막에서 직접 확인했다.
- `화면만으로 추정됨`: 화면 배치로는 보이지만 동작 결과까지 확인되지 않았다.
- `추가 확인 필요`: 영상이나 현재 증거로 판단할 수 없다.

## 1. 종합 결론

OpenVisionLab 3D Studio는 **비전 엔지니어가 3D 검사 레시피를 만들고 근거를
검증하는 작업**에는 사용자 중심으로 발전하고 있다. 특히 `검사 도구 → 검사 구성
→ 선택 도구 → 3D/Height Image → 실행 증거`의 책임 분리, 명시적
Review/Apply/Preview/Run, 동일 ROI의 2D/3D 연동은 상용 제품의 좋은 패턴을
현재 제품 경계에 맞게 재해석한 결과다.

반면 현재 제품은 **양산 오퍼레이터 화면이나 서비스 이력 분석 화면이 아니다**.
오퍼레이터, 서비스 엔지니어, 공정 엔지니어가 같은 편집 중심 화면을 사용하면
정보량과 변경 권한이 과도하다. 양산 OK/NG 집계, 알람, 생산 이력, 장비 통신을
추가하라는 뜻은 아니다. 현재 범위 안에서도 읽기 전용 실행/리뷰 작업공간,
실행 중 편집 잠금, NG 증거 탐색, 레시피 스냅샷 비교는 필요하다.

현재 UX 종합 점수는 **3.4/5**다. 이는 제품 완료율이 아니라 이 보고서의 18개
UX 항목 평균이다. 프로젝트 백로그의 현재 상태는 `87 Complete / 17 Partial /
105 New / 9 External / 16 Out of scope`이며, Inspection Workspace v3는
`7/8`이다. 이 수치를 제품 전체 완료율로 환산하지 않는다.

### 현재 UI의 가장 큰 장점 3가지

1. **명시적 상태 경계**: ROI는 Missing/Drawing/Review/Applied를 구분하고,
   편집이 Preview/Run을 자동 실행하지 않는다.
2. **좌표가 같은 2D/3D 증거**: Height Image와 3D Viewer가 동일한 소스,
   ROI ID, 역할 색, 커서 위치를 공유한다.
3. **결정론적 추적 가능성**: 입력/레시피/출력 식별자, Runner parity,
   Validation Set, Good/Bad/Held-out 및 임계값 오류표가 존재한다.

### 현재 UI의 가장 큰 문제 3가지

1. **역할과 운전 모드가 분리되지 않음**: 편집 기능이 많은 동일 화면을 다섯
   사용자 역할이 공유해야 한다.
2. **범용 변경 복구가 약함**: ROI 후보 Cancel과 마지막 포인트 취소는 있으나
   레시피 전체 Undo/Redo는 없다. 현재 소스에서 단계/ROI 삭제는 즉시 컬렉션을
   변경하며 별도 확인 대화상자가 확인되지 않았다.
3. **NG 원인 추적의 연결 화면이 부족함**: Validation Set 오류표와 3D 비교는
   있으나, NG 이력 → 당시 레시피/파라미터/판정 기준 → 결함 위치 → 수정 전후
   재실행을 한 화면에서 연결하지 못한다.

### 가장 먼저 개선할 항목

진행 중인 제품 우선순위인 `I-09/I-11 실제 실패 초안 → 수동 PropertyGrid 수정
→ 별도 Held-out 재현 기록`을 먼저 닫는다. 그 다음 UX 안전 변경은
**전역 Edit/Review/Running 상태와 실행 중 레시피 변경 잠금**, **삭제 범위
확인**, **파라미터의 단위/범위/저장값/초안값 표시**다.

1. `I-09/I-11 실제 실패·수정·Held-out 증거` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
2. `편집/실행 안전 상태와 파괴 동작 보호` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
3. `파라미터 값 상태·단위·범위 표준화` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `medium`

### 분석 완료 상태

| 항목 | 상태 |
| --- | --- |
| 확인 완료 영상 | 11/11 |
| 미확인 영상 | 0 |
| 총 재생 시간 | 53분 28.759초 |
| 자막 | 10개 영상은 시작부터 종료 직전까지 확인, Zivid Capture Assistant는 전체 화면 시퀀스로 확인 |
| 전 구간 화면 표본 | 영상별 균등 24프레임, 총 264프레임 |
| 현재 Release 근거 | 최신 소스보다 16초 뒤에 생성된 Release EXE로 Source Quality smoke 재실행 |
| 판단이 달라질 수 있는 항목 | 상용 제품의 권한 체계, 장시간 안정성, 숨은 Undo/Redo, 현재 제품의 실제 현장 오퍼레이터 R0 |

## 2. 영상별 분석

### 원본 확인 목록

| # | 영상 | 길이 | 해상도 | 자막 범위 | SHA-256 접두부 |
| ---: | --- | ---: | ---: | --- | --- |
| 1 | GoPxL GUI Walk Through | 244.874 s | 3840×2160 | 00:00.360–04:04.800 | `6D538DA81AFCAB18` |
| 2 | SICK Nova 3D Overview | 417.654 s | 1920×1080 | 00:03.070–06:48.260 | `3005C2899D3D4A41` |
| 3 | SICK Nova 3D Presence Inspection | 234.514 s | 1920×1080 | 00:03.450–03:45.010 | `419F524D0B2223CC` |
| 4 | HALCON Surface-Based Matching Introduction | 315.654 s | 1920×1080 | 00:06.720–05:11.620 | `8161E2C4E8DC1A4D` |
| 5 | HALCON Optimize 3D Surface Matching Data | 416.234 s | 1920×1080 | 00:07.180–06:52.240 | `96218612FEDB9864` |
| 6 | HALCON Edge-Supported Surface Matching | 267.014 s | 1920×1080 | 00:06.360–04:23.080 | `6815FA20473740F8` |
| 7 | HALCON Stereo Surface Reconstruction | 461.634 s | 1920×1080 | 00:06.080–07:40.000 | `1D38A89A0E646278` |
| 8 | Zivid Studio First Point Cloud | 198.323 s | 1920×1080 | 00:03.310–03:14.630 | `B0AE9971D5AFFBB7` |
| 9 | Photoneo PhoXi Control First Scan | 283.533 s | 1920×1080 | 00:00.770–04:41.960 | `EF55FCED4D88F5EB` |
| 10 | MERLIC Height Image Fill Inspection | 306.554 s | 1920×1080 | 00:06.510–05:01.460 | `19BFAED5B145A470` |
| 11 | Zivid Studio Capture Assistant | 62.771 s | 1920×1080 | 자막 없음, 00:00–01:02.771 화면 확인 | `B4B012B2723099E9` |

전 구간 접촉 시트는
`artifacts/current/20260728-industrial-3d-ux-audit/`에 보존했다.

### 영상별 핵심 관찰

| 영상 | 시간 구간 | 수행 작업 | 좋은 UX | 불편 요소 | 참고 가치 | 근거 수준 |
| --- | --- | --- | --- | --- | --- | --- |
| GoPxL GUI Walk Through | 00:00–00:40, 00:40–01:20, 01:20–02:20, 02:20–03:20, 03:20–04:04 | 브라우저 접속 후 Manage/System/Inspect/Connect/Report의 5개 책임과 세부 페이지 위치를 순회한다. 실제 검사 완료 작업이 아니라 정보 구조 안내다. | 좌측 고정 책임 rail, hover 확장과 pin, Jobs/Backup/Support/Alignment/Tools/Outputs/Health/Measurement/Performance 분리 | 기능 수가 많고 빈 중앙 Viewer를 둔 채 관리 페이지가 연속된다. 처음 사용자는 category 이름을 학습해야 한다. 3D 조작은 나오지 않는다. | 현재 제품은 이 전체 범위를 복제하지 말고 Recipe/Selected Tool/Result/Diagnostics 책임만 유지한다. | 배치와 탐색은 `영상에서 확인됨`; 3D 조작·권한은 `추가 확인 필요` |
| SICK Nova 3D Overview | 02:35–03:20, 03:20–04:30, 04:30–05:30, 05:30–06:10 | Configure → Free running → 2D/3D 전환 → 높이 컬러/바닥/범위 조정 → Blob Finder 추가 → Stop → ROI 조정 → 3D 확인 → 결과 한계 0/0 → Run → Save Permanent의 약 12개 논리 단계 | Configure가 상단에 보이고 Stop 후 ROI를 가르친다. 2D footprint와 3D 높이를 순서대로 확인한다. Reset View, 자동 높이 범위, 결과 limit와 현재 측정값이 선택 도구 주변에 있다. | 2D와 3D를 탭 전환하므로 동시 위치 대응은 기억에 의존한다. lower/upper=0의 의미를 잘못 입력하면 판정이 뒤집힌다. Save Permanent는 영향이 크다. | 현재 제품의 linked Height Image, explicit Run, source quality 방향을 지지한다. 단, 자동 Free run과 영구 저장은 그대로 복제하지 않는다. | `영상에서 확인됨` |
| SICK Nova 3D Presence Inspection | 00:40–01:30, 01:30–02:30, 02:30–03:30 | Good 샘플 취득 → Blob Region Finder ROI → 3D side에서 높이 범위 → 하위 Completeness Check → 셀 2D/3D 확인 → complete/incomplete 학습 → Estimate thresholds → 수동 수정/Apply → good/bad replay의 약 10단계 | 상위 도구의 oriented region을 하위 도구가 재사용한다. 셀 위치, 높이, coverage를 함께 보고 Good/Bad 근거로 임계값을 제안한 뒤 사용자가 Apply한다. | 임계값 창의 학습 샘플 수와 분포가 영상에서 충분히 보이지 않는다. Blob/Completeness의 중첩 ROI는 초보자에게 복잡하다. | 현재 Threshold Candidate/Review/Apply/Held-out 구조와 미래 Completeness의 가장 직접적인 기준이다. | 흐름은 `영상에서 확인됨`; 표본 통계·내부 산식은 `추가 확인 필요` |
| HALCON Surface-Based Matching Introduction | 00:00–01:30, 01:30–03:00, 03:00–04:30, 04:30–05:11 | CAD 읽기 → surface model 생성/샘플링 → scene 검색 → pose/score → CAD overlay → debug result handle → value check/normals/keypoints → score 해석의 약 9단계 | 모델·장면·pose·score·overlay를 분리한다. Automatic Value Check, normal 방향, sampled scene, keypoints를 순차 확인한다. score를 “보이는 표면 비율”로 설명한다. | HDevelop 코드와 여러 parameter/debug 창이 중심이라 오퍼레이터에게 부적합하다. Continue 기반 디버그 흐름도 숙련 지식을 요구한다. | 미래 surface matching은 단일 confidence가 아니라 모델/장면 ID, pose, surface coverage, normals/keypoints 증거를 가져야 한다. | `영상에서 확인됨` |
| HALCON Optimize 3D Surface Matching Data | 00:00–01:30, 01:30–03:00, 03:00–04:30, 04:30–06:00, 06:00–06:52 | 모델 mesh/points 준비 → 내부·중복 면 제거 → symmetry/rotation 범위 → XYZ invalid/noise 확인 → median/Z threshold/background 제거 → multiple match → runtime/score 확인 → MinScore 조정의 약 11단계 | 모델 준비와 장면 준비를 구분한다. invalid data, background, runtime, false match, score를 함께 판단한다. 2D Z-image 전처리가 빠른 이유도 설명한다. | 코드, 문서, histogram, 여러 3D 창을 왕복한다. 잘못된 준비 단계의 영향이 최종 결과에서 늦게 드러난다. | 현재 typed preparation과 source quality를 유지하고 future matcher에 search constraint와 runtime/rejection evidence를 추가한다. | `영상에서 확인됨` |
| HALCON Edge-Supported Surface Matching | 00:00–01:30, 01:30–03:00, 03:00–04:23 | false background match → 3D edge training → surface/edge score 확인 → XYZ mapping/duplicate/invalid 점검 → edge extraction/direction/viewpoint 조정 → 값 복사 → 재매칭의 약 9단계 | surface score와 edge score를 분리한다. acquisition viewpoint와 edge normal을 화면에서 검증하며 실패 원인을 단계별로 좁힌다. | 잘못된 viewpoint나 mapping convention은 초보자가 이해하기 어렵다. debug 결과를 본 뒤 값을 코드로 복사하는 과정도 실수 가능성이 있다. | advanced matching은 독립 score, 자동 검사, 방향 시각화, 재현 가능한 parameter snapshot이 필요하다. | `영상에서 확인됨` |
| HALCON Stereo Surface Reconstruction | 00:00–02:10, 02:10–03:20, 03:20–05:10, 05:10–06:20, 06:20–07:40 | calibration 전제 → camera pair/texture/triangulation trade-off → pairwise/fusion 선택 → camera pair/3D bounding box/disparity 방법 → persistence로 disparity/score 확인 → reconstruction → 한계 확인의 약 10단계 | tight 3D bounding box로 runtime/noise를 줄인다. 중간 disparity/score를 저장해 tuning하고 pairwise/fusion 장단점 및 반사/투명/무늬 없음 한계를 명시한다. | 대부분 개념 설명과 HDevelop 코드다. persistence는 메모리 비용을 늘리며 장시간 UI 안정성은 확인되지 않는다. | 입력 provenance와 quality limitation, invalid/disparity map의 중요성만 채택한다. stereo 엔진은 현재 범위 밖이다. | 흐름은 `영상에서 확인됨`; 장시간 메모리 안정성은 `추가 확인 필요` |
| Zivid Studio First Point Cloud | 00:00–01:40, 01:40–02:20, 02:20–03:15 | 카메라/GPU 준비 → Studio main view/control panel → color/texture point cloud, 2D color, depth 전환 → Assisted Mode 최대 시간 → Analyze & Capture → Manual Mode에서 frame/filter/exposure 수정 → SDK 전달의 약 8단계 | Assisted가 최대 시간을 입력으로 받고 분석/캡처한 뒤 일반 Manual 설정으로 되돌린다. 여러 표현이 하나의 main view 아래 있고 제안 frame을 끌 수 있다. | 오른쪽 control panel이 좁고 작은 설정이 많다. 검사 결과/판정/오류 복구는 다루지 않는다. | `analyze → propose → ordinary editable draft → explicit use` 패턴을 현재 임계값/전처리 보조 흐름에 적용할 가치가 높다. | `영상에서 확인됨` |
| Photoneo PhoXi Control First Scan | 00:35–01:30, 01:30–02:10, 02:10–03:20, 03:20–04:00 | 설치 → 장치 상태 LED/선택/Connect → Trigger Scan → Settings/Structure/output map → Set 또는 Set and Store → save/screenshot → Free Run → viewer zoom/angle/normal/color의 약 10단계 | 연결 전 상태가 보이고 연결 후 UI 책임이 바뀐다. Trigger와 Free Run, 임시 Set과 영구 Set and Store, output map과 viewer display를 구분한다. | `Set`과 `Set and Store`는 이름이 유사해 영구 영향 구분을 놓칠 수 있다. 왼쪽 설정 트리와 오른쪽 viewer 설정이 모두 조밀하다. | 현재 제품은 임시 draft/저장 recipe 차이를 더 명확히 하고 view-only map 전환은 recipe를 변경하지 않아야 한다. | `영상에서 확인됨` |
| MERLIC Height Image Fill Inspection | 00:00–01:20, 01:20–02:30, 02:30–03:30, 03:30–04:40, 04:40–05:01 | disparity load → invalid/outlier 제거 → Level Surface ROI → 8-bit 또는 metric 변환 → box alignment → 두 compartment ROI → Good/Bad 학습 → per-region confidence/aggregate result → designer front-end의 약 12단계 | 준비–정렬–검사가 읽히는 tool chain이다. training/processing을 나누고, 우측 안내가 다음 행동을 설명하며, 영역별 결과와 전체 결과를 함께 제공한다. | 작은 글자와 긴 tool list, tutorial panel로 viewer가 줄어든다. EasyTouch의 자동 결정과 confidence 의미가 영상에서 완전히 설명되지 않는다. | 현재 Remove Outlier/Level Surface/Validation evidence를 유지하고 future Completeness는 영역별 근거와 aggregate를 모두 보여야 한다. | 흐름은 `영상에서 확인됨`; EasyTouch/신뢰도 산식은 `추가 확인 필요` |
| Zivid Studio Capture Assistant | 00:00–00:22, 00:22–00:38, 00:38–00:49, 00:49–01:02 | Connect camera → Select capture mode → Adjust settings → Capture의 4단계와 Assisted Analyze & Capture, Manual tuning을 요약한다. | 4단계 목표가 화면에 직접 표시되고 보조 모드의 결과가 ordinary frame/exposure 설정으로 보인다. | 짧은 홍보성 영상이라 실패, cancel, progress 상세, 저장 경계는 확인할 수 없다. | 보조 기능은 짧고 작업 목적별이어야 하며 마지막은 일반 편집 화면이어야 한다. | 4단계와 화면은 `영상에서 확인됨`; 오류/취소/저장은 `추가 확인 필요` |

### 영상별 조작·상태 보완 기록

| 프로그램 | 화면 전환/자주 쓰는 배치 | 3D 조작 | 파라미터 | 결과·상태 | 반복/위험 |
| --- | --- | --- | --- | --- | --- |
| GoPxL | 좌측 category rail, 중앙 content/viewer | 확인되지 않음 | 페이지별 좌측/중앙 form | Report에서 health/measurement/performance | 너무 넓은 장비·통신 범위를 현재 제품에 복제할 위험 |
| SICK Nova | 상단 Configure/Run, 좌측 Analysis, 중앙 view, 우측 Results | 좌 drag 회전, 우 drag 이동, wheel zoom, Reset, 2D/3D | 선택 도구 form, limit, ROI handle | Pass/Fail와 current result, Save Permanent | 2D↔3D 왕복, limit 오입력, 영구 저장 |
| HALCON/HDevelop | code, operator parameter dialog, 여러 3D debug 창 | 화면 하단 help에 Rotate/Zoom/Move 조합 표시 | operator arguments와 debug procedure | pose, score, surface/edge score, timing, overlay | 값 복사와 창 왕복, expert knowledge |
| Zivid Studio | 중앙 main view, 우측 control panel, 하단 표현 선택 | 영상에서 상세 mouse binding은 확인되지 않음 | Manual exposure/filter/frame, Assisted max time | captured point cloud/frame enable | 작은 control panel, capture 실패 처리는 미확인 |
| PhoXi Control | 좌측 device/settings/structure, 중앙 viewer, 우측 info/view | zoom, angle, normals, color scheme | sliders, Set/Set and Store | device status LED, Trigger/Free Run, save | 임시/영구 적용 혼동 |
| MERLIC | 좌 tool chain, 중앙 training/processing image, 우측 guide | 주로 height image/ROI, 자유 3D 조작은 확인되지 않음 | tool별 training ROI/parameters | per-region result/confidence와 aggregate | tool chain/guide/Viewer 동시 표시로 밀도 증가 |

## 3. 상용 프로그램 공통 패턴

### A. 3개 이상 프로그램에서 반복된 패턴

| UI 패턴 | 적용 프로그램 수 | 사용 목적 | 장점 | 단점 | 우리 프로그램 적용 여부 |
| --- | ---: | --- | --- | --- | --- |
| 책임별 navigation 또는 tool chain + 선택 context | 6/6 프로그램군 | 현재 작업의 소유 영역과 선택 대상을 고정 | 사용자가 “무엇을 편집 중인지” 잃지 않음 | category가 많아지면 장비 설정과 검사 설정이 혼합됨 | 적용 중. Catalog/Chain/Selected Tool 책임을 유지하고 장비·통신 category는 제외 |
| 중앙의 지배적인 2D/3D Viewer + 측면 설정 | 5/6 | 공간 증거를 보면서 선택 항목만 수정 | 시선 이동과 기억 부담 감소 | 설정 패널이 커지면 Viewer가 급격히 축소 | 적용 중. Compact에서는 Selected Tool/Viewer focus preset 필요 |
| Configure/Training/Manual과 Run/Processing/Assisted 상태 구분 | 4/6 | 편집, 실행, 보조 기능의 영향 범위 분리 | 실수와 자동 실행을 줄임 | mode 이름만 있고 권한/잠금이 없으면 오히려 위험 | 부분 적용. Review/Applied/Preview/Run은 있으나 전역 Edit/Review/Running은 필요 |
| 동일 소스의 2D/3D/height/depth/color/normal 표현 전환 | 5/6 | 데이터 품질과 형상을 다른 증거로 검증 | 한 표현의 맹점을 보완 | source identity가 유지되지 않으면 잘못된 비교 | 강하게 적용. C3D Height/3D는 동일 identity, 미지원 channel은 명시 |
| 명시적 Apply/Set/Save/Persist | 4/6 | 초안, 임시 적용, 영구 저장을 구분 | 예기치 않은 설정 변경 방지 | `Set`/`Store`처럼 이름이 비슷하면 혼동 | 적용 중. 현재 draft/saved/current output을 더 한눈에 비교해야 함 |
| ROI 또는 bounding region으로 탐색/검사 범위 제한 | 3/6 | 속도, 노이즈, 대상 위치 안정화 | 계산량과 오검 감소, 관심영역 명확 | footprint와 3D volume을 혼동할 수 있음 | GridRectangle과 OrientedBox3D를 분리해 적용 |
| source/model preparation을 검사 앞단에 둠 | 5/6 | invalid/noise/background/normal/provenance 확인 | downstream 실패 원인을 앞에서 차단 | 준비 tool chain이 길어질 수 있음 | Source Quality, Remove Outlier, Level Surface로 적용 |
| 최종 판정 외에 overlay/score/region 결과를 보존 | 4/6 | 판정 원인을 검증 | 신뢰성과 tuning 가능성 증가 | evidence가 많으면 초보자에게 과부하 | Selected Tool의 기본 요약 + 접힌 상세 evidence로 적용 |

### B. 차별화 기능

| 기능 | 목적/유용 사용자 | 시간·오류 효과 | 적용 판단 | 구현 복잡도/예상 효과 |
| --- | --- | --- | --- | --- |
| SICK `Estimate thresholds` | 공정/비전 엔지니어가 complete/incomplete 표본으로 범위를 제안 | 초기 수치 탐색 단축, arbitrary default 감소 | 수정 적용. 현재 Good/Bad/Held-out, exact error table, Review/Apply를 유지하고 실제 실패→수정 근거를 완결 | 중간~높음 / 매우 높음 |
| Zivid Assisted → Manual | 초보 비전 엔지니어에게 bounded draft 제공 | 설정 시작 시간 단축, 최종 통제권 유지 | 패턴만 적용. 카메라 capture가 아니라 threshold/preparation assistant에 사용 | 중간 / 높음 |
| HALCON `debug_find_surface_model` | 비전/서비스/개발자가 value check→normal→keypoint→matching result 순으로 원인 축소 | trial-and-error 감소, 잘못된 aggregate score 방지 | 미래 matcher에 수정 적용. 각 증거를 별도 점수/overlay로 제공 | 높음 / 높음 |
| Photoneo Set vs Set and Store | 임시 시험과 영구 profile 분리 | 의도하지 않은 영구 변경 감소 | 명칭은 더 명확히 `초안 적용`/`레시피 저장`으로 수정 적용 | 낮음 / 높음 |
| MERLIC per-region + aggregate | 공정/오퍼레이터가 어느 compartment가 실패했는지 확인 | NG 원인 파악 시간 단축 | future Completeness에 그대로 필요한 원칙. confidence 의미는 계약으로 명시 | 중간 / 높음 |
| GoPxL Performance responsibility | 서비스/개발자가 실행 시간을 검사 설정과 분리해 확인 | 병목 탐색 단축 | 하단 Performance/Run Record에 한정 적용, 전역 장비 관리 page는 제외 | 낮음~중간 / 중간 |

### C. 따라 하지 말아야 할 상용 UX 문제

| 문제 | 확인 프로그램 | 영향 | 현재 제품 원칙 |
| --- | --- | --- | --- |
| 장비·통신·검사·보고 기능이 한 제품 navigation에 과다 축적 | GoPxL, PhoXi | 오퍼레이터의 탐색 비용과 권한 위험 증가 | 로컬 검사 범위를 유지하고 camera/PLC/robot/cloud를 추가하지 않음 |
| 개발 코드와 다중 debug 창이 정상 workflow | HALCON | 강력하지만 초보/오퍼레이터에게 부적합 | typed Selected Tool과 단계별 evidence로 감싸고 code 복사를 요구하지 않음 |
| 2D/3D를 탭으로만 왕복 | SICK | 위치 대응을 기억해야 함 | linked split/stack/pop-out와 shared cursor 유지 |
| 자동/연속 실행이 편집과 가까움 | SICK, PhoXi | 실행 중 설정 변경 및 원인 불명확 위험 | Preview/Run은 계속 명시적으로 유지 |
| 임시/영구 명령의 이름과 위치가 유사 | PhoXi | profile을 잘못 영구 저장할 수 있음 | Draft/Apply/Save를 상태와 함께 명확히 표시 |
| 작은 글자와 다수 panel이 Viewer를 압박 | MERLIC, PhoXi, Zivid | 장시간 피로, compact 사용성 저하 | focus preset, progressive disclosure, 최소 1개 지배적 evidence surface |
| 하나의 confidence/score가 충분히 설명되지 않음 | MERLIC 일부, 일반 matcher 관행 | 낮은 점수의 원인과 허용값을 오판 | 점수 의미, 분모, 허용 범위, 구성 요소를 계약으로 표시 |

### 3D Viewer 집중 비교

#### 기본 조작

| 항목 | 상용 영상 관찰 | 현재 제품 | 발견 가능성/판단 |
| --- | --- | --- | --- |
| 회전/이동/확대 | SICK가 좌 drag/우 drag/wheel을 명시, PhoXi도 zoom/angle 확인 | 실제 pointer smoke로 orbit/pan/zoom 확인 | HUD와 하단 도움말이 있으나 처음 진입 시 gesture sheet가 더 명확해야 함 |
| 화면 맞춤/초기화 | SICK Reset View, 현재 제품 Fit all/Fit ROI | 둘 다 제공 | 현재 버튼명이 직접적이라 양호 |
| 표준 시점 | SICK 2D/3D, 현재 제품 Top/Perspective 직접 버튼 | Top/Perspective 확인 | Front/Side/Isometric 직접 버튼은 현재 화면에서 확인되지 않아 추가 필요 |
| ROI 중심 | 상용 영상은 3D side에서 ROI 확인 | 현재 Fit ROI 제공 | 양호 |
| 선택 중심 회전 | 제공 영상에서 명확히 확인되지 않음 | 현재 증거에서 확인되지 않음 | `추가 확인 필요`; 필요 시 selected-pivot 표시 |
| 현재 조작 모드 | HALCON은 window help, 현재 제품은 HUD/상태 문구 | 부분 제공 | ROI editing 중 cursor/handle 종류와 camera mode 충돌을 더 명확히 표시 |
| 성능/프레임 | 상용 영상은 정량 FPS 없음 | 상세 HUD에 FPS/draw time 표시 가능 | 개발/서비스에는 유용, 오퍼레이터 기본 화면에서는 접어서 유지 |

#### 데이터 표현

| 표현 | 상용 영상 | 현재 제품 | 평가 |
| --- | --- | --- | --- |
| Point cloud/Surface/Mesh | SICK, HALCON, Zivid, PhoXi | Surface 기본, Points/Wireframe/Edges, GLB/STL/LAS/LAZ/C3D | 강함. 검사 증거와 단순 viewer format을 구분해야 함 |
| Height/Depth/2D | SICK height, Zivid depth/color, MERLIC disparity/height | coordinate-true Height Image, auto/manual range, shared cursor | 현재 제품의 핵심 강점 |
| Intensity/Color/Normal/Confidence | Zivid/PhoXi/HALCON에서 일부 제공 | 현재 C3D source quality가 미지원 channel을 `unavailable`로 표시 | 데이터를 만들지 않는 fail-closed가 올바름 |
| 높이 컬러맵/범위 | SICK auto range와 palette, PhoXi color scheme | Height/Grayscale/Thermal, auto/manual Min/Max, legend | 기능은 충분. raw-height와 calibrated unit의 시각 구분 필요 |
| 결함 overlay/OK-NG | SICK, HALCON, MERLIC | ROI 역할, metric overlay, output compare | 판정 목록→공간 위치 jump는 아직 약함 |
| invalid/missing | HALCON/MERLIC이 문제로 설명 | magenta overlay, count/ratio/mask identity | 매우 강함. 색만이 아니라 pattern/label 선택도 필요 |
| Z축 배율/배경/texture | 일부 상용 viewer에서 추정 가능 | 현재 화면 근거로 확정할 수 없음 | `추가 확인 필요`; 검사 의미를 바꾸지 않는 view-only 계약 필요 |

#### 분석 기능

| 기능 | 현재 상태 | 발견 가능성 | 판단 |
| --- | --- | --- | --- |
| 포인트 높이/좌표 | Viewer HUD와 shared Height cursor | 높음 | 제공 |
| 두 점/높이 차이 | 2-Point Line, Point Pair, Height Difference | 중간 | Catalog 검색과 compatible next tool이 도움 |
| 단면/Cross Section | Cross-section dimensions와 Height Profile surface 존재 | 낮음~중간 | 별도 3D analysis preset에서 노출 필요 |
| 평면/평탄도/기준 평면 | 3-Point Plane, Plane Flatness, Datum/Level Surface | 중간 | typed evidence가 강점 |
| 체적/면적/각도 | Volume 및 line/plane 결과군 존재, 면적 단독 도구는 화면에서 불명확 | 중간/추가 확인 | 목적별 이름과 단위 metadata 필요 |
| ROI 통계 | Source histogram, sample distributions, threshold error table | 중간 | 하단 accordion에 묻히므로 선택 도구 요약 필요 |
| 결과↔3D 위치 | output Show/Pin/Compare, ROI overlay | 부분 | defect row 선택 시 카메라 focus와 2D crosshair 동기화 필요 |
| NG 목록 위치 이동 | Validation issue 이전/다음과 3D compare는 있음 | 낮음/부분 | 생산/오검 분석용 통합 NG Review가 필요 |

## 4. 우리 프로그램 점수표

점수는 `1=작업 완료가 어렵거나 위험`, `3=핵심은 가능하나 학습/우회 필요`,
`5=근거·복구·역할까지 일관되게 지원`으로 해석한다.

| 평가 항목 | 점수 | 근거 | 주요 문제 | 개선 방향 |
| --- | ---: | --- | --- | --- |
| 정보 구조 | 4 | Catalog→Chain→Selected Tool→Viewer와 하단 evidence 책임이 고정 | advanced explorer와 하단 tab이 많아지면 책임 경계가 흐려짐 | GoPxL의 책임 분리를 좁은 제품 범위에만 적용; 난이도 2, P2 |
| 화면 배치 | 3 | Wide에서 4열 구조가 한 화면에 보임 | Compact에서 Recipe Chain이 접히고 Selected Tool/Viewer/Validation이 서로 높이를 경쟁 | role/focus layout preset; 난이도 3, P1 |
| 기능 발견 가능성 | 3 | compatible next tool, text button, help, keyboard shortcut 존재 | 긴 Catalog와 accordion 아래 기능, View 메뉴 안 기능은 초보자가 놓침 | context command search와 teaching hint; 난이도 2, P2 |
| 작업 흐름의 자연스러움 | 4 | README의 10단계 lifecycle과 Missing→Review→Applied가 일치 | 기존 레시피 수정/오검 분석은 동일한 편집 화면을 왕복 | 시나리오별 workspace preset; 난이도 3, P1 |
| 3D Viewer 조작성 | 4 | orbit/pan/zoom, Top/Perspective, Fit all/ROI, HUD, box handles | Front/Side/Isometric 직접 전환과 defect focus가 약함 | view preset bar와 selected pivot; 난이도 2, P2 |
| 검사 항목과 파라미터 연결성 | 4 | 선택 step 이름, role, regions, typed PropertyGrid, output가 한 Selected Tool에 있음 | 긴 scroll에서 header와 편집 값이 분리되고 저장값/초안값을 나란히 보지 못함 | sticky context header와 value-state columns; 난이도 3, P0 |
| 2D와 3D 데이터의 연계성 | 5 | 같은 source SHA, native grid, selection ID, role color, cursor, Review draft 공유 | source type이 height grid가 아닐 때 동등한 linkage는 없음 | 현 계약 유지; 지원 안 되는 경우 명시, P2 |
| 검사 결과 가독성 | 3 | per-step metrics, overlay, Pass/Fail, Run Record, compare 제공 | Viewer/Selected Tool/하단 결과가 분산되고 production summary가 없음 | result summary strip와 selected-result focus; 난이도 3, P1 |
| NG 원인 분석 편의성 | 2 | Validation error table, previous/next issue, 3D compare 존재 | 이력 검색, defect type filter, 당시 parameter/threshold/location 연결 화면 없음 | NG Review workspace; 난이도 4, P1 |
| 레시피 생성 편의성 | 4 | first recipe guide, compatible tool, linked ROI, repeat grid, shortcut | 도구 수가 늘수록 올바른 intent/tool 선택이 어려움 | task template와 목적 기반 filter; 난이도 3, P1 |
| 파라미터 변경 전후 비교 | 2 | output pin/compare와 threshold Before/Proposed는 일부 존재 | 일반 PropertyGrid 값, metric, overlay의 before/after를 하나의 session으로 비교하지 못함 | immutable recipe snapshot 기반 A/B compare; 난이도 4, P1 |
| 상태 및 진행 상황 피드백 | 4 | saved/modified, Ready/pending, Review/Applied, async load와 Validation progress/cancel | 모든 장시간 job에 공통 ETA/단계/잠금 정책은 없음 | unified job state host; 난이도 4, P1 |
| 오류 예방 | 3 | typed validation, fail-closed, explicit Apply/Preview/Run, close guard | 단계/ROI 삭제 확인과 일반 실행 중 mutation guard가 소스에서 확인되지 않음 | command-level safety policy; 난이도 3, P0 |
| 오류 발생 후 복구 | 3 | Review Cancel, 마지막 point 취소, unsaved close Cancel | 일반 레시피 Undo/Redo와 saved revision 복구가 없음 | undoable recipe command와 version restore; 난이도 4, P1 |
| 오퍼레이터 사용성 | 2 | 상태 text와 Viewer evidence는 명확 | 편집 control과 diagnostics가 과다하고 read-only 운전/리뷰 mode가 없음 | 권한이 적용된 Operator Review preset; 난이도 4, P1 |
| 엔지니어 사용성 | 4 | source quality, typed tools, ROI, validation, threshold evidence, Runner | 긴 scroll과 before/after 부족 | selected-tool compare와 command palette; 난이도 3, P1 |
| 장시간 사용 시 피로도 | 3 | dark Viewer, dock/split/pop-out, 한/영 지원 | 작은 text, 높은 정보 밀도, cyan/orange/heat palette 의존 | density preset, font scale, color+shape encoding; 난이도 3, P1 |
| 유지보수 및 기능 확장 가능성 | 4 | Core/Data/Tools/Shell 분리, typed contract, WPF-neutral VM, Runner parity | Workbench owner가 크고 plugin/version/undo contract는 아직 없음 | domain별 job/history/plugin seam; 난이도 5, P2/P3 |

평균: `61 / 90 = 3.39`, 반올림 `3.4/5`.

### 필수 UX 위험 18개 확인 결과

| 위험 | 현재 판정 | 근거와 조치 |
| --- | --- | --- |
| 현재 모드가 불명확 | 부분 존재 | Recipe/Calibrate와 step state는 보이나 전역 Edit/Review/Running은 없음. P0 상태 badge와 mutation lock 필요 |
| ROI/검사/파라미터 연결 불명확 | 대체로 해소 | 동일 selection/role과 Selected Tool이 연결됨. sticky breadcrumb를 추가하면 더 안전 |
| 수정값과 저장값 구분 | 부분 존재 | header의 modified/saved와 Review draft는 있음. 일반 PropertyGrid에는 Saved/Draft 병렬값이 없음 |
| 변경 결과 즉시 확인 어려움 | 의도적으로 명시 실행 | 자동 실행하지 않는 것은 장점. 대신 stale result와 `Preview 필요`를 더 강하게 표시 |
| 미저장 상태로 화면 이동 | 부분 보호 | New/Open/Close guard는 있음. dirty parameter draft를 둔 step 이동은 별도 실제 replay 필요 |
| 실행과 위험 버튼 근접 | 상단은 양호, 국소 위험 | Preview/Run/Save는 삭제와 멀다. ROI Edit/Fit/Delete는 같은 card에 있음 |
| 삭제/초기화/전체 적용 보호 부족 | 존재 | 소스의 RemoveSelectedStep/Selection은 직접 mutation. count/scope 확인과 session undo 필요 |
| 실행 중 편집 가능/불가 구분 | 부분 존재 | Validation command는 running guard가 있으나 일반 recipe mutation command에는 공통 guard가 없음 |
| 진행 상태/남은 시간 부족 | 부분 존재 | load와 Validation은 progress/cancel, 공통 ETA와 stage contract는 없음 |
| 처리 중 멈춘 것처럼 보임 | 감소했으나 잔존 가능 | C3D load와 여러 Preview에 async/cancel이 있으나 모든 도구의 공통 검증은 아님 |
| 다수 검사 항목 비교 어려움 | 부분 존재 | Show/Pin/Compare는 있으나 parameter/metric matrix가 없음 |
| NG와 검사 조건의 인과관계 부족 | 존재 | exact error table은 강하나 production/run history와 당시 조건 jump가 없음 |
| 단위/유효 범위 불명확 | 부분 존재 | source unit/raw-height는 보이나 모든 PropertyGrid field의 unit/range가 일관되지 않음 |
| 기본/추천/현재값 구분 부족 | 존재 | threshold Before/Proposed에만 명확. 일반 parameter도 동일 contract 필요 |
| Undo/Redo 부족 | 존재 | ROI 후보 Cancel/Undo last point만 제공 |
| 단축키/반복 작업 부족 | 부분 해소 | Ctrl+N/O/S, F5, Ctrl+F5, Enter/Esc/Delete, 4×2 repeat가 있음. discoverability와 clone/batch edit는 약함 |
| 해상도/다중 모니터 저하 | 부분 존재 | Wide/Compact와 pop-out은 검증. Compact의 지배적 Viewer 면적과 작은 text는 여전히 문제 |
| 색각 이상 구분 어려움 | 부분 존재 | role/Pass/Fail text가 색을 보완하지만 height/invalid/role overlay는 색 의존도가 높음 |

## 5. 사용자 역할별 평가

| 사용자 | 주요 작업 | 현재 장점 | 현재 불편 | 필요한 개선 |
| --- | --- | --- | --- | --- |
| 오퍼레이터 | 현재 recipe 확인, Run, 결과/NG 확인 | 명시 Run, 상태 text, Viewer overlay | 편집/삭제/고급 evidence가 동시에 보여 위험하고 OK/NG review 흐름이 없음 | 읽기 전용 Operator Review preset, 실행 중 edit lock, 큰 상태/다음 NG |
| 공정 엔지니어 | sample 준비, 허용 범위, 반복 pad, validation | 4×2 반복, Good/Bad/Held-out, threshold error table | 공정 변경 전후 비교와 revision note가 없음 | parameter+result A/B, recipe revision, 승인/메모 |
| 비전 엔지니어 | source quality, ROI, algorithm, tuning | 현재 제품과 가장 잘 맞음. linked 2D/3D와 typed evidence가 강함 | 많은 accordion/도구와 일반 Undo 부재 | intent template, compare, undo, advanced diagnostics preset |
| 서비스 엔지니어 | 이상 원인, source/성능/log, 재현 | source mask/hash, log, Runner, performance evidence | session timeline, 지원 bundle, 당시 recipe/환경 snapshot 탐색이 분산 | read-only diagnostic bundle과 Run Record→source/recipe/log jump |
| 소프트웨어 개발자 | 기능 추가, golden, Runner/UI parity | typed contract, 계층 분리, verification command가 매우 유용 | 큰 Workbench owner, 공통 job/undo/plugin seam 부족 | unified job host, domain command history, tool registration contract |

## 6. 작업 시나리오별 분석

클릭 수는 현재 화면과 문서화된 workflow를 기준으로 한 **UI click/gesture
추정치**다. 파일 대화상자의 경로 입력, 숫자 타이핑 횟수, ROI drag 중 pointer
move는 제외한다. 실제 초보/숙련 사용자 시간 측정은 R0 또는 별도 usability
replay가 필요하다.

| 시나리오 | 현재 단계 수 | 문제 구간 | 오류 가능성 | 권장 흐름 |
| --- | ---: | --- | --- | --- |
| 신규 레시피 생성 | 10개 논리 단계, 약 16–26 click + ROI drag 2회, 화면 전환 2–4회 | New/Open input 뒤 Catalog→두 ROI→Parameter→Preview→Run→Save. 기준 좌표/단위가 source metadata와 분리되어 보일 수 있음 | 잘못된 도구/ROI role, raw-height를 물리 단위로 오해, Preview stale 상태에서 Save | `Source trust → Intent/template → ROI roles → Parameter metadata → Preview evidence → Validation → Save`. 현재 같은 workspace를 유지하고 상단 stepper는 현재 단계만 강조 |
| 기존 레시피 수정 | 7개 논리 단계, 약 9–16 click, 화면 전환 1–3회 | recipe 검색/step 선택은 빠르지만 일반 parameter의 Before/After 및 revision history가 없음 | 다른 step을 편집하거나 결과가 이전 parameter에서 생성된 사실을 놓침 | sticky breadcrumb + Saved/Draft/Previewed values + A/B snapshot + explicit Save revision note |
| 양산 중 결과 확인 | 현재 제품 범위에서는 완결 불가. 로컬 Run/결과 확인만 6개 단계, 약 8–15 click | OK/NG 수량, 알람, 생산 이력, 이전/다음 NG 중심 화면이 없음 | 편집 control을 잘못 누르거나 현재 output을 production result로 과신 | camera/PLC HMI가 아닌 **읽기 전용 local Operator Review**: recipe ID, source/run ID, Pass/Fail, failed steps, 이전/다음 result, export만 제공 |
| 오검/미검 분석 | 현재 Validation 흐름 9개 단계, 약 14–24 click, 화면 전환 3–6회 | Good/Bad/Held-out, error table, 3D compare는 강하나 production NG 검색과 당시 parameter snapshot이 연결되지 않음 | 현재 recipe로 과거 sample을 열어 당시 판정을 잘못 재현, tuning sample이 Held-out에 섞임 | Run Record/Validation sample을 immutable snapshot으로 열고 `Expected↔Actual↔Rule↔Metric↔3D location`을 연결한 뒤 clone draft에서만 재실행 |

### 시나리오별 질적 평가

| 시나리오 | 기억 부담 | 진행 단계 가시성 | 이전 단계/복구 | 결과 신뢰 | 초보/숙련 균형 |
| --- | --- | --- | --- | --- | --- |
| 신규 레시피 | 중간. ROI 역할과 raw-height 의미를 기억해야 함 | 높음. Missing/Review/Applied와 selected step 표시 | ROI Cancel은 좋으나 일반 Undo 없음 | 높음. source/metric/overlay/Runner evidence | compatible tool과 guide는 초보에 유리, 긴 Catalog는 숙련자도 search 필요 |
| 기존 수정 | 높음. 저장값/초안값/마지막 Preview 값을 머릿속에서 비교 | 중간 | close guard는 있으나 revision restore 없음 | 중간. output identity는 있으나 before/after session이 없음 | 숙련자는 빠르지만 초보자는 stale result를 놓칠 수 있음 |
| 양산 결과 확인 | 매우 높음. 편집 화면에서 필요한 result만 찾아야 함 | 낮음 | 이전/다음 production result 개념이 없음 | 낮음~중간. 단일 Run evidence는 강하지만 운영 이력은 아님 | 현재 구조는 엔지니어 전용에 가까움 |
| 오검/미검 | 중간~높음 | Validation progress와 error table은 높음 | Held-out 분리는 좋지만 recipe revision 복구가 없음 | 개발 sample evidence에는 높음, 과거 production 재현에는 낮음 | 비전 엔지니어에는 강함, 공정/서비스에는 설명 계층 필요 |

## 7. 개선 요구사항 목록

점수 열은 `사용자 영향도(I) / 작업 빈도(F) / 오류 감소(E) / 시간 감소(T) /
구현 난이도(D) / 기존 코드 영향도(C)`이며 모두 1–5다.

| ID | 화면 또는 기능 | 현재 문제 | 개선안 | 기대 효과 | I/F/E/T | D | C | 우선순위 |
| --- | --- | --- | --- | --- | --- | ---: | ---: | --- |
| UX-00 | Threshold correction | 추천 임계값 Review/Apply/Held-out은 있으나 실제 실패 초안의 수동 수정 증거 I-09/I-11이 남음 | 실제 실패를 보존하고 ordinary PropertyGrid에서 수동 수정, before/proposed/manual/held-out/Runner parity를 한 record로 저장 | 제안값을 맹신하지 않고 오검 수정 과정을 재현 | 5/4/5/4 | 4 | 3 | P1 |
| UX-01 | 전역 command/state bar | Edit/Review/Previewing/Running 상태와 허용 command가 한 contract가 아님 | `Edit`, `Review`, `Running`, `Cancelling` session state를 만들고 Running에서는 recipe mutation command를 fail-closed | 실행 중 조건 변경과 원인 불명확 방지 | 5/5/5/3 | 3 | 4 | P0 |
| UX-02 | 단계/ROI/box 삭제 | 삭제가 즉시 collection을 변경하고 범위 확인이 없음 | 이름, downstream 영향, 함께 삭제될 orphan 수를 확인; 삭제 직후 1회 Undo toast 제공 | 잘못된 recipe 손실 감소 | 5/3/5/3 | 2 | 2 | P0 |
| UX-03 | PropertyGrid | 일부 field에 unit/range/default/current/recommended 구분이 없음 | parameter metadata contract와 `Saved / Draft / Previewed / Suggested`, unit, valid range, validation reason 표시 | 단위/범위 오입력과 stale result 감소 | 5/5/5/4 | 3 | 3 | P0 |
| UX-04 | Result header/Run Record | NG metric은 있어도 당시 recipe/threshold/output의 인과관계가 한눈에 안 보임 | result에 source SHA, recipe revision, step ID, parameter fingerprint, threshold, metric, decision, overlay ID를 breadcrumb로 연결 | NG 신뢰와 감사 가능성 향상 | 5/4/5/5 | 4 | 4 | P0 |
| UX-05 | Recipe edit history | 후보 Cancel 외 범용 Undo/Redo가 없음 | `IUndoableRecipeCommand` 기반 add/remove/move/parameter/ROI action history, 실행 결과는 history 밖 immutable snapshot | 편집 탐색과 복구 속도 향상 | 5/4/5/4 | 5 | 5 | P1 |
| UX-06 | Before/After compare | 일반 parameter와 metric/overlay를 같은 비교 session으로 보지 못함 | saved baseline과 draft preview를 side-by-side, changed fields만 filter, Pass/Fail delta 표시 | tuning 시간과 잘못된 저장 감소 | 5/4/5/5 | 4 | 4 | P1 |
| UX-07 | NG Review | error table, log, 3D compare가 서로 다른 tab에 분산 | read-only NG list/filter + failed step + metric/limit + 2D/3D focus + previous/next + export | 공정/서비스의 원인 분석 시간 단축 | 5/4/5/5 | 4 | 4 | P1 |
| UX-08 | 역할별 workspace | 오퍼레이터와 엔지니어가 같은 편집 control을 봄 | Operator Review/Process Validation/Vision Authoring/Service Diagnostics layout과 command permission 분리 | 오조작 감소, 교육 범위 축소 | 5/5/5/4 | 4 | 4 | P1 |
| UX-09 | 장시간 작업 | load/Validation에는 progress가 있으나 모든 job의 stage/ETA/lock/cancel이 동일하지 않음 | 공통 `BackgroundJobSession`에 stage, count, elapsed, 안정적인 경우 ETA, cancel, disposal, UI throttling | freeze 인식과 중복 명령 감소 | 4/3/4/4 | 4 | 4 | P1 |
| UX-10 | 3D Viewer preset | Top/Perspective는 직접 보이나 Front/Side/Isometric과 selected pivot이 약함 | 표준 시점, Reset, Fit selection, pivot marker를 같은 toolbar에 배치 | ROI 높이/volume 검증 속도 향상 | 4/4/3/4 | 2 | 2 | P2 |
| UX-11 | Defect spatial focus | 문제 row 선택이 2D crosshair/3D camera/overlay focus로 완전히 연결되지 않음 | issue selection을 shared spatial selection으로 전달하고 source mismatch 시 fail-closed | NG 위치 탐색 시간 단축 | 5/4/4/5 | 3 | 3 | P1 |
| UX-12 | 다중 step 비교 | Show/Pin/Compare는 output 중심, 다수 step metric/limit 비교는 어려움 | step×metric matrix, Fail first, changed only, same-unit grouping | 반복 pad와 다수 검사 비교 가속 | 4/4/3/5 | 3 | 3 | P2 |
| UX-13 | Compact/다중 모니터 | 1280 폭에서 pane 높이와 Viewer가 경쟁 | 역할별 density preset, selected task focus, 최소 hit target/font, monitor별 dock layout 저장 | 장시간 피로와 compact 오류 감소 | 4/5/3/4 | 3 | 3 | P1 |
| UX-14 | 접근성 | role/height/invalid/Pass-Fail 일부가 색에 의존 | color+text+icon+line pattern, colorblind palettes, contrast check, accessible name | 색각 이상과 저대비 환경에서 판독 개선 | 4/5/4/3 | 2 | 2 | P1 |
| UX-15 | 반복/숙련자 명령 | 단축키는 있으나 UI에서 찾기 어렵고 clone/batch edit가 제한 | tooltip에 shortcut, command palette, duplicate step/ROI, multi-edit는 compatible same-unit field만 | 숙련자 작업 시간 단축 | 3/4/2/5 | 3 | 3 | P2 |
| UX-16 | Recipe version/replay | Save는 있으나 named revision, compare, restore, action note가 없음 | immutable recipe revision + migration + Run Record reference + clone-to-edit | 과거 판정 재현과 승인 추적 | 5/3/5/4 | 5 | 5 | P1 |
| UX-17 | Service diagnostics | log/source quality/Runner evidence가 여러 위치에 분산 | read-only support bundle: app/build, recipe/source IDs, last jobs, warnings, performance, optional screenshot | 서비스 대응과 재현 시간 단축 | 4/2/4/5 | 3 | 3 | P2 |
| UX-18 | Tool extensibility | typed tool 추가가 여러 catalog/adapter/verification owner를 수정 | manifest가 아니라 typed registration contract와 generated discovery test부터 도입 | 기능 확장 시 누락 감소 | 3/2/3/4 | 5 | 5 | P3 |

### .NET 10 적용 원칙

- **현실성**: UX-01~04, 10, 13~15는 현재 WPF/MVVM command, style,
  validation metadata 위에서 단계적으로 구현할 수 있다.
- **UI thread**: 파일 hash, C3D 분석, Preview, Validation, compare는
  `Task`/`CancellationToken`/`IProgress` owner에서 실행한다. UI에는
  immutable snapshot만 전달하고 progress update는 10–20 Hz로 제한한다.
- **3D 성능**: 카메라 drag 중 full metric 재계산을 금지한다. Viewer sample과
  deterministic full-data Runner 결과를 계속 분리한다.
- **thread safety**: 한 job session이 cancellation source, state,
  result snapshot, cleanup을 함께 소유한다. 취소 후 늦게 도착한 결과는
  source/revision token mismatch로 버린다.
- **migration**: recipe schema는 additive optional field로 올리고 기존
  schema를 읽을 수 있어야 한다. revision/history 저장은 기존 recipe를
  자동 덮어쓰지 않고 sidecar 또는 새 container version으로 도입한다.
- **권한 분리**: pane을 숨기는 것만으로 권한을 구현하지 않는다.
  command `CanExecute`, service/API boundary, audit record가 같은 role policy를
  사용해야 한다.
- **장시간 안정성**: bounded log, image/mesh cache eviction, cancellation
  disposal, GPU resource release, repeated-run soak 및 memory plateau를
  acceptance gate로 둔다.

## 8. Quick Win

1. 상단에 `편집 / 검토 / 실행 중 / 취소 중` badge를 추가하고 허용되지 않는
   command의 tooltip에 이유를 표시한다.
2. 단계/ROI/3D box 삭제 버튼을 위험 색상과 휴지통 icon으로 통일하고
   `대상 이름 + 영향 수` 확인을 추가한다.
3. Selected Tool 상단에
   `Step → ROI role → Parameter draft → Preview output` breadcrumb를 고정한다.
4. PropertyGrid description에 단위, 유효 범위, 저장값을 표시하고 변경된
   field에 `Draft` chip을 붙인다.
5. `Preview stale`을 단순 text가 아니라 Viewer/result header의 icon+text로
   반복 표시한다.
6. 버튼 tooltip에 `F5`, `Ctrl+F5`, `Enter`, `Esc`, `Delete` 등 기존
   shortcut을 같이 표시한다.
7. Pass/Fail/Warning/Invalid를 색뿐 아니라 icon, text, outline pattern으로
   구분한다.
8. Viewer toolbar에 Front/Side/Isometric/Reset을 text tooltip과 함께
   노출한다.
9. 진행 표시를 `%`만 쓰지 않고 `현재 단계 · n/N · 경과 시간 · 취소 가능`으로
   통일한다.
10. Wide/Compact에 `Viewer focus`, `Authoring`, `Validation` 3개 layout
    preset을 제공하고 사용자가 현재 preset을 확인할 수 있게 한다.

## 9. 권장 화면 구조

현재 4열 Workbench를 버리지 않는다. 역할과 작업에 따라 같은 ViewModel
projection을 다른 density/permission으로 제시한다.

### 메인 검사 화면

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ 작업공간: 검사 편집 | Recipe r17 | Saved/Draft | EDIT | Preview Run Stop │
├───────────────┬───────────────────────────────────┬──────────────────────┤
│ 검사 구성      │ 3D / Height / Split Viewer        │ 선택 결과 요약         │
│ Step + 상태    │ ROI · defect · cursor overlay     │ Value / Limit / Status │
│ Failed first  │                                   │ Evidence / Export      │
├───────────────┴───────────────────────────────────┴──────────────────────┤
│ 결과 | Validation | Compare | Run Record | Log          Job n/N · Cancel │
└──────────────────────────────────────────────────────────────────────────┘
```

### 레시피 편집 화면

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ Authoring | Source trusted | Step 03/08 | Draft | Preview stale | Save    │
├───────────────┬───────────────────────────────────┬──────────────────────┤
│ Intent/도구    │ 2D/3D linked teaching             │ Step 03 Thickness      │
│ Compatible     │ Reference cyan / Measure orange   │ Regions                │
│ Recipe chain   │ Review candidate vs Applied       │ Saved | Draft | Unit  │
│ + Add/Move     │                                   │ Apply/Cancel/Preview   │
├───────────────┴───────────────────────────────────┴──────────────────────┤
│ Before/After metric · overlay · changed fields · undo history             │
└──────────────────────────────────────────────────────────────────────────┘
```

### 3D 분석 화면

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ 3D Analysis | Surface Points Height Invalid | Top Front Side Iso Fit ROI │
├──────────────────┬──────────────────────────────────┬────────────────────┤
│ Selection/Issue   │ Dominant 3D Viewer                │ Display evidence   │
│ ROI / Output      │ pivot · section · measurement     │ Palette/Range      │
│ Previous/Next     │ linked Height crosshair           │ Point/ROI stats    │
├──────────────────┴──────────────────────────────────┴────────────────────┤
│ Profile | Cross section | Distribution | Performance                     │
└──────────────────────────────────────────────────────────────────────────┘
```

### NG 결과 리뷰 화면

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ READ ONLY | Run 2026… | Recipe r17 SHA… | Source SHA… | Export / Clone    │
├──────────────────┬──────────────────────────────────┬────────────────────┤
│ NG list/filter    │ 2D + 3D synchronized evidence    │ Why NG              │
│ Type/Step/Time    │ selected defect auto focus       │ Metric 3.2          │
│ Prev/Next         │ overlay + expected region        │ Limit 2.0..3.0      │
│ Same type         │                                  │ Step/Param snapshot │
├──────────────────┴──────────────────────────────────┴────────────────────┤
│ Action note | Compare with draft replay | Do not modify recorded evidence │
└──────────────────────────────────────────────────────────────────────────┘
```

### 검사 항목 및 파라미터 편집 화면

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ Step 03 > Measurement ROI > Thickness | Applied ROI | Preview stale       │
├───────────────────────────────────┬──────────────────────────────────────┤
│ Parameter                         │ Evidence                              │
│ Name       Saved Draft Unit Range │ Before Preview | Draft Preview        │
│ Min        2.0   2.2   raw  0..10 │ Metric/Status/Overlay                 │
│ Max        4.0   4.0   raw  0..10 │ Changed only / Reset field            │
│ [Cancel draft] [Apply to recipe]  │ [Preview] [Accept]                    │
└───────────────────────────────────┴──────────────────────────────────────┘
```

## 10. 개발 적용 로드맵

### 선행 조건

- R0 Owner unaided replay는 현재 Release를 실제 소유자가 조작해야 한다.
  소유자와 실행 환경이 준비되기 전에는 이 항목에 모델 token을 쓰지 않는다.
- UI 변경은 현재 Release build의 fresh before/after capture와 실제 pointer/
  keyboard replay를 남긴다.
- camera, stereo reconstruction, PLC/robot/fieldbus/HMI, cloud, plant
  management, physical metrology는 이번 UX 로드맵의 구현 범위가 아니다.

### 1차: 기존 화면을 유지한 상태에서 개선

| 순서 | 작업 | 선행 조건 | 완료 기준 | 권장 모델 |
| ---: | --- | --- | --- | --- |
| 1 | `I-09/I-11` 실제 실패 초안→수동 수정→Held-out 기록 | 실제 실패 sample/draft | before/proposed/manual/held-out와 Workbench/Runner parity | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |
| 2 | 전역 Edit/Review/Running state와 mutation lock | I-09/I-11 bounded slice 종료 | 실행 중 add/remove/move/ROI/parameter가 command와 service 양쪽에서 거부되고 이유가 보임 | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |
| 3 | 삭제 보호, parameter unit/range/value-state, shortcut tooltip, 접근성 encoding | UX-01 state contract | Wide/Compact before/after, keyboard, invalid range, delete scope replay | Recommended model: `gpt-5.6-sol` · Reasoning effort: `medium` |

### 2차: 핵심 작업 흐름 개선

| 순서 | 작업 | 선행 조건 | 완료 기준 | 권장 모델 |
| ---: | --- | --- | --- | --- |
| 4 | Saved/Draft Preview A/B compare | immutable recipe/output snapshot ID | changed fields, metric/overlay delta, stale mismatch fail-closed | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |
| 5 | NG Review와 spatial focus | Run Record에 step/metric/overlay/source identity | issue row→2D/3D 위치→rule/limit→export 연결 | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |
| 6 | Operator/Process/Vision/Service workspace projection과 command permission | UX-01 command policy | pane 숨김이 아니라 같은 policy가 command/service/audit에 적용 | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |
| 7 | Completeness/repeated-cell inspection | 첫 threshold skill의 correction gate 완료 | cell별 metric/coverage/height, aggregate, Validation/Runner parity | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |

### 3차: 구조적 개선 및 고급 기능

| 순서 | 작업 | 선행 조건 | 완료 기준 | 권장 모델 |
| ---: | --- | --- | --- | --- |
| 8 | Undoable recipe command와 named revision/replay | migration/retention 정책 | 기존 schema reopen, undo/redo golden, recorded run immutable, clone-to-edit | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |
| 9 | 공통 background job host와 soak | 모든 장시간 owner inventory | cancel race, stale completion, memory plateau, repeated-run soak 통과 | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |
| 10 | Surface matching foundation과 debug evidence | source/model preparation 및 region artifact gate | model/scene ID, bounded pose, overlay, separate score, timing/rejection, Runner parity | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |
| 11 | Typed tool registration/plugin seam | 기존 tool owner/adapter inventory | registration 누락 자동 검출, 기존 recipe/Runner 호환 | Recommended model: `gpt-5.6-sol` · Reasoning effort: `high` |

### 검증과 증거

- 11개 원본은 `ffprobe`로 duration/resolution을 확인했다.
- 10개 자막의 cue 시작/종료와 영상 길이를 비교했다.
- 각 영상의 전 길이를 균등한 24개 timestamp frame으로 다시 추출했다.
- 현재 Release EXE는 마지막 관련 source 수정 `22:15:31` 이후
  `22:15:47`에 생성된 것을 확인했다.
- 같은 EXE로 Source Quality smoke를 재실행해
  `viewOnly=true`, `recipeChanged=false`, `inspectionRun=false`,
  screenshot quality `acceptable=True`를 확인했다.

증거:

- `artifacts/current/20260728-industrial-3d-ux-audit/`
- `artifacts/current/20260728-industrial-3d-ux-audit/current-source-quality-smoke.txt`
- `artifacts/current/20260728-industrial-3d-ux-audit/current-release-source-quality-wide.png`
- `artifacts/current/20260728-threshold-review-heldout/after-review-apply-heldout-tall.png`
- `docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`
- `docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md`

### 완료 기록

```text
Status: Complete
Scope: 제공된 상용 영상 11개와 현재 OpenVisionLab 3D Studio의 역할별·시나리오별 산업용 3D 검사 UX 감사
Acceptance criteria: 11/11 전 구간 확인 -> pass; 5개 역할 -> pass; 4개 시나리오 -> pass; 18개 점수/위험 -> pass; 공통/차별/문제 패턴 -> pass; 개선 점수/P0-P3/Quick Win/와이어프레임/로드맵 -> pass
Verification: ffprobe 11개; 자막 cue 범위 10개; timestamp contact sheet 11×24; current Release Source Quality smoke pass; Markdown 구조/링크 검증
Evidence: docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md 및 artifacts/current/20260728-industrial-3d-ux-audit/
Boundary / next dependency: 상용 제품의 숨은 권한·Undo·장시간 동작은 영상만으로 확정하지 않음. 제품 구현은 I-09/I-11에서 재개하며 R0는 소유자 외부 선행 조건임.
```
