# Output Compare 기본 사용 높이 개선

- 완료일: 2026-07-23
- 대상: Workbench/Expert의 도킹 가능한 `Output Compare`
- 기준 작업 영역: `1920 x 1040`
- 증거: `artifacts/current/20260723-output-compare-usable-default`

## 문제

전체 View 감사에서 `Output Compare`의 A/B/C 슬롯은 존재했지만, 기본 하단 도킹 높이에서는 슬롯 A의 3D Viewer가 얇은 띠처럼 표시됐다. 도킹 View를 별도로 키우거나 띄우기 전에는 입력과 출력의 형상을 비교하기 어려웠다.

`OutputCompareView.MinHeight`만 지정하는 첫 시도는 AvalonDock의 기존 `2:1` Star 분할을 바꾸지 못했다. 이 실패 화면을 덮어쓰지 않고 실제 도킹 분할을 수정했다.

## 적용

- Output Compare 선택 시 기존 상단 Workbench와 하단 도킹 영역의 비율을 `2:1`에서 `1.2:1`로 변경한다.
- 다른 하단 View를 선택하면 기존 `2:1` 비율로 복원한다.
- A/B/C 카드에서 반복되던 `고정 산출물` 레이블을 제거해 Viewer 높이를 더 확보했다.
- Output Compare 콘텐츠의 최소 높이는 `390`으로 유지한다.
- 기존 AvalonDock Float/Dock, A/B/C 선택 ComboBox, 명시적 출력 고정 및 레시피 비실행 계약은 변경하지 않았다.

## 판정

수정 전에는 슬롯 A Viewer에서 도구 모음 아래의 실제 3D 영역이 거의 보이지 않았다. 수정 후에는 동일한 `1920 x 1040` 화면에서 슬롯 A의 HUD, 높이 범례와 3D 형상이 함께 보이며 B/C 빈 슬롯도 완전한 비교 카드 높이를 유지한다. 상단 주 Viewer도 티칭 상태와 3D 형상을 계속 확인할 수 있다.

이번 캡처는 source 슬롯 A 한 개를 실제로 표시한 증거다. B/C의 타입이 있는 출력 고정과 소스/Filter 동시 비교 계약은 `ToolArtifactNavigatorVerification`으로 검증한다. 저장된 레시피만으로 실행 산출물을 조작하거나 만들어내지 않았다.

## 검증

| 항목 | 결과 |
|---|---|
| Debug 빌드 | 경고 0 / 오류 0 |
| Workbench 도킹 계약 | `27/27` 통과 |
| Output Compare 선택 높이 | `usableHeight=True` |
| 다른 하단 View 선택 시 복원 | `standardHeight=True` |
| 실제 EXE | 종료 코드 `0` |
| Shell 캡처 | `1920 x 1040`, attempt 1 accepted |
| embedded Viewer 캡처 | attempt 1 accepted |

## 증거 파일

- `before-output-compare-1920x1040.png`: 변경 전 감사 캡처
- `after-output-compare-1920x1040.png`: 현재 빌드의 실제 EXE
- `after-output-compare-1920x1040-quality.txt`: Shell 화면 품질
- `after-output-compare-viewer.png`: 같은 실행의 embedded Viewer
- `after-output-compare-viewer-quality.txt`: Viewer 화면 품질
- `verify-docking.txt`: 도킹 선택·높이·복원 계약

## 완료 기록

Status: Complete

Scope: Output Compare를 선택했을 때 기본 도킹 상태에서 A/B/C 비교 카드와 실제 3D Viewer를 사용할 수 있는 높이를 확보하고, 다른 하단 View에서는 기존 높이로 복원함.

Acceptance criteria: Output Compare 선택 시 사용 가능한 분할 적용 -> 통과; 다른 하단 View 선택 시 기존 분할 복원 -> 통과; 1920 x 1040 실제 EXE에서 3D Viewer 표시 -> 통과; Float/Dock 및 명시적 출력 고정 계약 보존 -> 통과.

Verification: `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug`; `--verify-workbench-docking`; 실제 EXE dual screenshot smoke.

Evidence: `docs/OPENVISIONLAB_3D_OUTPUT_COMPARE_USABLE_DEFAULT_20260723.md`; `artifacts/current/20260723-output-compare-usable-default/`.

Boundary / next dependency: 한 개 source 슬롯을 표시한 고정 해상도 UI 증거이며, 서로 다른 세 개의 실제 Published 출력이나 다중 DPI/모니터, 물리 형상·계측 동등성을 증명하지 않는다.
