# OpenVisionLab 3D Final Refactor and Code Rules

Date: 2026-07-26

## Outcome

Status: Complete

이번 범위의 구조 리팩토링 기준선을 닫고 이후 개발에 적용할 코드 규칙을 확정했다. 이는 제품 기능 전체나 물리 계측 준비가 완료되었다는 뜻이 아니다.

## Completed scope

- `App`에서 검증 CLI 분기를 `ShellVerificationCommandRouter`로 이동했다. `App`은 WPF startup/exit와 공용 chart/log lifecycle만 소유한다.
- `MainWindow`의 비-teaching Workbench -> Viewer 표시 event 수명주기와 routing을 `WorkbenchViewerDisplayCoordinator`로 이동했다.
- Tool Lab smoke의 창 준비, 단일 인스턴스 검증, 캡처, 임시 창 정리를 `ShellToolLabSmoke`로 이동했다.
- `MainWindow.EnableShellSmokeFromCommandLine`은 923줄에서 540줄로 줄었고 Tool Lab 반복 책임이 제거되었다.
- Runner의 747줄 명령 선택을 `RunnerCommandRouter`로 이동하고 실제 recipe replay/report는 `RunnerApplication`에 유지했다.
- Viewer와 Runner에 중복되어 있던 model-transform 계산을 `Core.ModelTransform.Apply` 한 곳으로 통합했다.
- 표준 `.sln`에 누락되어 있던 `OpenVisionLab.Wpf.MessageDialogs`를 추가해 `.sln`과 `.slnx`가 같은 12개 코드 프로젝트를 빌드하도록 맞췄다.
- 이후 개발 규칙을 `docs/OPENVISIONLAB_3D_CODE_RULES.md`에 기록했다.

## MVVM boundary decision

일반 화면 상태, selection, command, dirty/apply/discard, review-tab state는 ViewModel 소유다. 다음은 의도적인 View adapter로 남는다.

- WPF 파일/메시지 대화상자
- AvalonDock layout/focus
- PropertyGrid binding flush
- SharpGL/OpenGL lifecycle과 pointer rendering
- 실제 WPF window와 screenshot smoke

따라서 이 완료 판정은 `zero code-behind`가 아니라 `business/presentation state는 ViewModel, 플랫폼 상호작용은 View adapter`라는 경계에 대한 판정이다.

## Structural self-evaluation

| 항목 | 점수 | 근거 |
| --- | ---: | --- |
| 프로젝트/도메인 소유권 | 18/20 | Core/Data/Tools/Viewer/Shell/Runner 방향이 명확하고 두 solution의 프로젝트 집합이 일치한다. |
| MVVM/View adapter 경계 | 18/20 | bindable state/command/request는 ViewModel, WPF/OpenGL은 adapter에 남는다. |
| composition/CLI 경계 | 17/20 | App과 Runner의 명령 라우터가 분리됐다. Runner router는 지원 옵션이 많아 여전히 길지만 책임은 단일하다. |
| 중복/의존성 통제 | 18/20 | transform 중복 제거, 새 DI/event bus/interface 미도입, 기존 구체 owner 재사용. |
| 검증/문서화 | 19/20 | Release 0/0, focused checks, 동일 SHA-256 UI 전후 캡처, 코드 규칙과 완료 기록. |
| 합계 | 90/100 | 신규 기능 개발을 시작할 수 있는 구조 기준선. |

남은 10점은 현재 결함 목록이 아니라 향후 변경 압력이 실제 독립 seam을 증명할 때 개선할 여지다. 파일 길이만을 이유로 추가 분할하지 않는다.

## Acceptance criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Shell startup lifecycle과 검증 routing 분리 | Pass | `App.xaml.cs`, `ShellVerificationCommandRouter.cs`, command-line verification `9/9` |
| MainWindow가 비-teaching Viewer 표시 구독을 소유하지 않음 | Pass | `WorkbenchViewerDisplayCoordinator.cs`; 이전 handler/field 검색 0건 |
| Tool Lab smoke 반복이 MainWindow에서 제거됨 | Pass | `ShellToolLabSmoke.cs`; smoke method 923 -> 540 lines |
| Runner CLI 선택과 recipe 실행 분리 | Pass | `RunnerCommandRouter.cs`, `RunnerApplication.cs`, Runner golden `4/4` |
| Viewer/Runner model transform 단일 소유 | Pass | `ModelTransform.Apply`; 이전 `ApplyModelTransform` 검색 0건 |
| 표준 solution 프로젝트 동기화 | Pass | `.sln`과 `.slnx` 모두 12개 코드 프로젝트; Release build에 MessageDialogs 포함 |
| MVVM/Workbench 회귀 없음 | Pass | docking `28/28`, WPG `34/34`, artifact/output compare `25/25`, validation `24/24`, height measurement `42/42`, teaching `27/27`, logging `4/4` |
| 현재 UI 시각 회귀 없음 | Pass | before/after PNG SHA-256 동일, quality attempt 1 acceptable |
| 재사용 가능한 코드 규칙 존재 | Pass | `docs/OPENVISIONLAB_3D_CODE_RULES.md` |

## Verification

```text
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"
Result: 0 warnings / 0 errors

--verify-shell-smoke-command-line: 9/9
--verify-workbench-docking: 28/28
--verify-recipe-manager-wpg: 34/34
--verify-artifact-navigator: 25/25
--verify-validation-set: 24/24
--verify-tool-height-measurement-workbench: 42/42
--verify-tool-recipe-teaching: 27/27
--verify-logging: 4/4
Runner --verify-c3d-affine-apply: 4/4
Filter Tool Lab current Release smoke: attempt 1 acceptable
```

## UI evidence

- `artifacts/current/20260726-final-refactor-and-code-rules/before-shell.png`
- `artifacts/current/20260726-final-refactor-and-code-rules/after-shell.png`
- `artifacts/current/20260726-final-refactor-and-code-rules/filter-tool-lab.png`
- both SHA-256: `AB108837F5F114467FEDC40B25B5395D8E6C4B63091C60972B03016A78EF002C`
- both screenshot quality: attempt 1, acceptable

## Boundary / next dependency

- 이 완료는 구조/MVVM 기준선에 대한 것이다.
- owner의 unaided first-recipe replay는 아직 외부 제품 증거로 남아 있다.
- physical calibration, uncertainty, Gauge R&R, metrology readiness는 검증되지 않았다.
- camera, PLC, robot, cloud, production-line integration은 현재 제품 범위 밖이다.
