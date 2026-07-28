# Validation Set 실패 샘플 분석 UX

- 완료일: 2026-07-24
- 제품 영역: Inspection Recipe Workbench / Validation Set / Output Compare
- 현재 증거: `artifacts/current/20260724-validation-failure-analysis/`

## 목적

`Validation Set`은 여러 C3D를 실행하는 기능만으로 끝나지 않는다. 작업자가
전체 결과에서 실패와 오류를 빠르게 찾고, 실패 단계의 수치와 오버레이 근거를
확인하고, 원본과 해당 샘플을 별도 3D Viewer에서 비교할 수 있어야 한다.

이 기능은 새 실행 엔진이나 자동 실행 경로를 만들지 않는다. 기존
`ToolRecipeOrderedGraphExecution` 결과를 읽기 전용 분석 화면으로 투영한다.
레시피, 메인 Viewer 입력, Preview/Publish/Run 명시성은 변경하지 않는다.

## 확정된 작업 흐름

1. 작업자가 같은 Grid의 C3D 샘플을 명시적으로 추가한다.
2. `전체 실행`으로 현재 레시피의 지원되는 모든 단계를 순서대로 재생한다.
3. 상단 집계에서 `전체 / 통과 / 실패 / 오류` 수를 확인한다.
4. 상태 필터 또는 `이전 문제 / 다음 문제`로 Fail과 Error만 순회한다.
5. 선택한 샘플의 단계 기록에서 최초 실패/오류 단계가 자동 선택된다.
6. 선택 단계의 Metric과 Overlay 근거를 읽기 전용으로 확인한다.
7. `3D 비교 열기`로 원본 C3D와 선택 샘플을 기존 Output Compare A/B 슬롯에
   고정한다.

## 구현 계약

- 샘플 상태: `Pending`, `Pass`, `Fail`, `Error`
- 실행 중 진행률과 현재 샘플을 표시하고 작업자가 취소할 수 있다.
- 취소, 필터, 선택, 비교 열기는 레시피나 메인 Viewer 입력을 변경하지 않는다.
- 결과 행은 Pass/Fail/Error 색으로 구분한다.
- 선택한 Fail/Error의 첫 문제 단계를 자동 선택하고 목록 안으로 스크롤한다.
- `Metric`은 이름, 값, 단위, 판정 상태를 보존한다.
- `Overlay`는 종류, 라벨, 판정 상태를 보존한다.
- 비교 후보는 실제로 존재하는 선택 샘플 C3D만 사용한다.
- Output Compare A는 레시피 원본, B는 선택 샘플, C는 비움 상태로 연다.
- 중간 단계의 3D 산출물이 존재하지 않으면 화면을 만들어내지 않는다.
- Validation Set 탭은 분석용 높이를 사용한다. `1280 x 760`에서는 더 큰
  하단 비율을 적용하고 다른 Evidence 탭을 선택하면 표준 비율로 복원한다.
- Output Compare는 `1920 x 1040`에서 두 Viewer가 완전히 보이도록
  Workbench:Evidence 비율 `0.82:1`을 사용한다.

## 고정 검증 샘플

`Synthetic Affine Inspection Plate v1`의 27단계 레시피를 사용했다.

| 샘플 | 예상 | 확인 내용 |
|---|---|---|
| `graph-pass.C3D` | Pass | 27단계 순서 실행 완료 |
| `graph-measurement-fail.C3D` | Fail | Thickness 허용범위 실패 후 후속 단계 근거 유지 |
| `graph-upstream-error.C3D` | Error | Step 2 Height Difference Edge 오류에서 의존 실행 중단 |

## 검증

| 항목 | 결과 |
|---|---|
| Debug 전체 솔루션 빌드 | 경고 0 / 오류 0 |
| Validation Set 집중 검증 | `24/24` 통과 |
| Workbench 도킹 회귀 | `27/27` 통과 |
| Recipe Teaching 회귀 | `25/25` 통과 |
| Synthetic Affine 전체 체인 | `18/18` 통과 |
| 한국어 실제 EXE `1920 x 1040` | attempt 1 accepted |
| 영어 실제 EXE `1280 x 760` | attempt 1 accepted |
| 실패 샘플 Output Compare 실제 EXE | attempt 1 accepted |

## 화면 증거

- 변경 전: `before-ko.png`
- 한국어 결과 분석: `after-ko.png`
- 영어 좁은 화면: `after-en-1280.png`
- 원본/실패 샘플 3D 비교: `after-compare-ko.png`
- 자동 검증: `validation-set-verification.txt`
- 회귀 검증: `workbench-docking-verification.txt`,
  `tool-recipe-teaching-verification.txt`,
  `synthetic-affine-verification.txt`

## 과장 금지 경계

이 체크포인트는 고정된 로컬 합성 C3D 세트에서 실패 분석 UX와 기존 순차
그래프 실행 결과의 보존을 증명한다. 다음을 증명하지 않는다.

- 생산용 Batch 스케줄러나 결과 데이터베이스
- 임의 DAG 실행
- 중간 산출물의 자동 영속화
- 실제 여러 부품의 정렬 신뢰성
- 물리 단위 교정, Gauge R&R, 계측 정확도
- Release, 모든 데이터 크기, 다중 GPU 성능

## 완료 기록

Status: Complete

Scope: Validation Set 결과 집계, 상태 필터, 문제 이동, 선택 단계
Metric/Overlay, 취소/진행률, 선택 실패 샘플의 읽기 전용 Output Compare 연결,
두 해상도 분석 레이아웃.

Acceptance criteria: Pass/Fail/Error를 구분하고 문제를 순회할 수 있음 -> 통과;
실패 단계의 Metric/Overlay 근거가 보존됨 -> 통과; 원본과 선택 실패 샘플이
두 Viewer에 표시됨 -> 통과; 레시피와 메인 Viewer 입력 불변 -> 통과;
`1920 x 1040` 및 `1280 x 760`에서 핵심 결과가 보임 -> 통과.

Verification: `dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug
-p:Platform="Any CPU"`; `--verify-validation-set`;
`--verify-workbench-docking`; `--verify-tool-recipe-teaching`;
`--verify-synthetic-affine-inspection-plate`; 실제 EXE 3종 캡처.

Evidence: `docs/OPENVISIONLAB_3D_VALIDATION_FAILURE_ANALYSIS_UX_20260724.md`;
`artifacts/current/20260724-validation-failure-analysis/`.

Boundary / next dependency: 실제 다중 부품 네 랜드마크 데이터가 없으므로
정렬·물리 계측 신뢰성은 외부 차단 상태다. 다음 내부 개발은 Release/대용량/
다양한 C3D에 대한 Viewer 성능 일반화와 프로파일링이다.
