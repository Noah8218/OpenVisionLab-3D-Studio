# OpenVisionLab 3D Code Rules

Effective: 2026-07-26

이 문서는 리팩토링 완료 기준선 이후의 신규 개발과 유지보수에 적용한다. 목표는 기능의 소유 위치, MVVM 경계, 검증 방법을 사람과 LLM이 검색만으로 판단할 수 있게 유지하는 것이다.

## 1. 기본 원칙

1. 가장 단순한 검증 가능한 구현을 선택한다.
2. 파일 크기가 아니라 안정된 책임과 상태 소유권을 기준으로 나눈다.
3. 기존 소유자가 있으면 새 폴더, 서비스, 인터페이스를 만들지 않는다.
4. UI 상태 변경과 검사 실행은 분리한다. ROI 편집, 표시 토글, 패널 전환은 `Preview`, `Publish`, `Run`을 암묵적으로 호출하지 않는다.
5. 구조 변경은 이름이나 파일 이동만으로 완료 처리하지 않는다. 이전 소유자에서 책임이 제거되고 새 경계를 직접 검증해야 한다.

## 2. 프로젝트 의존성

허용되는 주 흐름은 다음과 같다.

```text
Core
  <- Data
  <- Tools
  <- Runner

Core + Data + Tools
  <- Viewer
  <- Shell

Viewer
  <- ThreeDStudio
```

- `Core`: 단위, 좌표 변환, 엔터티, 결과, metric, overlay 같은 런타임 중립 계약을 소유한다.
- `Data`: C3D/GLB/STL/LAS/LAZ 파싱과 파일 기반 데이터 모델을 소유한다.
- `Tools`: WPF/SharpGL에 의존하지 않는 규칙, recipe adapter, 수치 실행을 소유한다.
- `Runner`: CLI 라우팅, 비-UI replay, 보고서 작성을 소유한다.
- `Viewer`: 카메라, picking, OpenGL 렌더링, 화면 전용 상태와 Viewer 입력 adapter를 소유한다.
- `Shell`: WPF composition, docking, 파일 대화상자, Workbench 화면 흐름을 소유한다.
- `OpenVisionLab.Logging`, `Localization`, `Wpf.MessageDialogs`는 공유 기반 기능으로 사용하되 같은 기능을 3D 프로젝트 안에 다시 만들지 않는다.
- `Core`, `Data`, `Tools`, `Runner`에 WPF 또는 SharpGL 참조를 추가하지 않는다.
- 두 개 이상의 계층에서 같은 수학/계약 구현이 발견되면 가장 낮은 런타임 중립 소유자로 이동한다. 예: `ModelTransform.Apply`.

## 3. 폴더와 타입

- 폴더명은 `Workbench`, `Teaching`, `Validation`, `Rendering`, `Verification`처럼 안정된 도메인/책임을 사용한다.
- 임시 작업명, 화면 한 곳의 호출자명, 파일 줄 수만을 이유로 폴더를 만들지 않는다.
- 한 파일에는 하나의 주 타입을 둔다. 작은 private record/enum은 해당 소유자 밖에서 의미가 없을 때만 함께 둔다.
- 새 `interface`는 두 구현, 외부 경계, 또는 독립 테스트 대역이 실제로 필요할 때만 만든다.
- DI 컨테이너, 전역 event bus, mediator를 기본 해법으로 도입하지 않는다. 현재 composition root에서 명시적으로 연결한다.
- `partial`은 생성 코드 공존, 하나의 응집된 WPF/OpenGL 타입, 또는 종료 조건이 기록된 짧은 전환에만 사용한다.
- 새 partial 파일을 만들기 전에 상태 소유자, 의존성, 독립 테스트 seam을 먼저 적는다. 파일 길이만으로는 허용하지 않는다.

## 4. MVVM과 View 경계

ViewModel이 소유해야 하는 것:

- 화면 상태, 선택 상태, enable/disable 조건
- 명령과 검증 메시지
- recipe/step/selection의 상태 전이
- Preview/Publish 가능 여부와 실행 상태
- 표시할 요약과 localization 가능한 presentation 값

View code-behind 또는 View adapter에 남겨도 되는 것:

- `OpenFileDialog`, `SaveFileDialog`, 메시지 창의 실제 표시
- AvalonDock layout/focus/floating window 호출
- PropertyGrid binding flush와 WPF control lifecycle
- SharpGL context, OpenGL buffer, pointer capture, hit testing, screenshot capture
- ViewModel의 request event를 실제 View 동작으로 연결하는 얇은 composition 코드

금지:

- click handler 안에서 recipe/domain 상태를 직접 계산하거나 별도 복사본으로 보관
- code-behind에서 command의 enable 조건을 다시 구현
- ViewModel과 View가 같은 세션 상태를 각각 소유
- 화면 표시용 높이/offset을 recipe 또는 측정 입력으로 암묵적으로 저장

예외가 필요한 경우 코드 또는 완료 문서에 `왜 View adapter여야 하는지`와 `어떤 테스트가 경계를 보호하는지`를 기록한다.

## 5. 상태와 실행 소유권

- 하나의 세션 상태는 하나의 concrete owner만 가진다.
- Shell은 현재 recipe/artifact 상태에서 후보를 발견할 수 있지만 Output Compare의 A/B/C pin 상태는 `ToolWorkbenchOutputCompareSession`이 소유한다.
- normal Workbench의 단일/좌우/상하/팝아웃 구성, 보조 슬롯 pin, focused slot은 비-WPF `ViewerWorkspaceSession`이 소유한다. 각 슬롯의 카메라, projection, display mode는 서로 다른 기존 Viewer 인스턴스가 소유하며, `ViewerWorkspaceView`와 재사용 가능한 pop-out window는 WPF/OpenGL host 이동만 담당한다.
- 반복 Thickness의 좌표 변환과 후보 recipe 검증은 WPF 중립
  `ThicknessRepeatGridAuthoringService`가 소유한다. 편집 중인 반복 요청과
  display-only 후보는 `ThicknessRepeatGridAuthoringSession`이 소유하고,
  root Workbench는 명시적 Apply/Cancel만 조합한다. 후보 변경은 recipe를
  수정하거나 Preview, Publish, Run을 호출하지 않는다.
- Viewer 표시 요청의 구독/해제와 화면 반영은 `WorkbenchViewerDisplayCoordinator`가 소유한다.
- 검증 명령 선택은 `ShellVerificationCommandRouter`, Runner CLI 선택은 `RunnerCommandRouter`가 소유한다.
- 실제 recipe 계산과 보고는 command router에 넣지 않는다.
- Validation Set은 독립 실행 seam 없이 callback 묶음으로 추출하지 않는다.
- CancellationTokenSource, running/stale/published/output은 해당 실행 세션 또는 해당 도구 실행 owner에서 함께 관리한다.

## 6. Recipe와 검사 계약

- `GridRectangle`은 X=column/Z=row의 height-field footprint다. XYZ volume으로 재해석하지 않는다.
- 표시 전용 ROI Y offset은 recipe, Preview, Publish, Run, Validation Set 입력에 포함하지 않는다.
- 표시 전용 ROI Y offset은 사용자에게 `오버레이 Y 위치`로 표현한다. 원래 위치와 이동 위치를 함께 그릴 때도 채워진 수직 벽을 사용해 실제 volume처럼 보이게 하지 않는다.
- Thickness는 current schema에서 `HeightField -> Reference ROI -> Measurement ROI` 순서를 유지한다.
- `ToolId`는 카탈로그 알고리즘을 식별한다. 저장된 단계의 `ToolName`은
  `Tab 1 Thickness` 같은 검사 인스턴스를 식별할 수 있으며 저장/재열기
  이후에도 유지되어야 한다.
- Validation Set의 현재 입력 추가는 pending 샘플 목록만 변경한다.
  Preview, Publish, Run 또는 Viewer 입력 교체를 유발하지 않는다.
- 단위가 기록되었다는 사실을 물리 calibration 또는 metrology 증명으로 표현하지 않는다.
- Output layer 생성이나 visibility 변경은 input layer를 변경하거나 검사 실행을 유발하지 않는다.
- Preview와 Run은 항상 사용자 명시 동작으로 남긴다.

## 7. 명명과 구현

- public identifier와 저장 계약은 영어를 사용한다. 사용자 표시 문자열은 localization service를 통한다.
- `Async` 메서드는 실제 비동기 작업을 하고 이름에 `Async`를 붙인다.
- bool은 `Is`, `Has`, `Can`, `Should`로 시작한다.
- 시간은 단위를 이름에 포함한다. 예: `ElapsedMilliseconds`.
- 측정 좌표와 단위는 변수/metric 이름에서 모호하지 않게 한다.
- 예외는 복구 가능한 경계에서만 잡고, 실패 원인과 대상 ID/path를 구조화 로그에 남긴다.
- 같은 변환, formatter, validation contract를 Viewer와 Runner에 복사하지 않는다.

## 8. 로깅

- 사용자 재현에 필요한 작업은 `key=value` 구조로 기록한다.
- recipe/step/source/selection ID, action, success, `viewOnly`, `recipeChanged`, `inspectionRun`처럼 원인 추적에 필요한 값을 포함한다.
- 로그에 원본 대용량 데이터, 비밀값, 전체 recipe JSON을 무조건 덤프하지 않는다.
- 실패를 catch하고 숨기지 않는다. 사용자 메시지와 로그의 상세 원인을 분리한다.

## 9. 변경 검증

최소 검증:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\verify-code-structure.ps1"
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"
```

변경 범위에 맞춰 다음 중 필요한 것을 추가한다.

- ViewModel/Workbench: 해당 `--verify-*` focused verification
- recipe/runner/수치 변경: Runner golden과 UI/Runner parity
- docking/navigation: `--verify-workbench-docking`
- PropertyGrid/명령: `--verify-recipe-manager-wpg`
- Output Compare/artifact: `--verify-artifact-navigator`
- Validation Set: `--verify-validation-set`
- UI/문구/layout: 현재 Release 빌드의 before/after screenshot과 quality report

UI 변경 후 source가 다시 바뀌면 기존 after screenshot을 현재 증거로 사용하지 않는다.

## 10. 구조 변경 체크리스트

- [ ] 기존 owner와 호출 경로를 검색했다.
- [ ] 새 owner의 책임, 입력, 출력, 상태를 한 문장으로 설명할 수 있다.
- [ ] 이전 owner에서 이동한 상태/구독/계산이 제거되었다.
- [ ] 새 interface/folder/partial이 실제 필요하다는 근거가 있다.
- [ ] `Preview`, `Publish`, `Run`, recipe 저장 계약이 보존되었다.
- [ ] `scripts/verify-code-structure.ps1`이 통과했다.
- [ ] Release solution build가 경고/오류 없이 통과했다.
- [ ] 변경 경계의 focused verification이 통과했다.
- [ ] UI 영향이 있으면 현재 before/after 증거를 남겼다.
- [ ] 완료 문서에 범위, 명령, 결과, 증거, 과장 금지 경계를 기록했다.

## 11. 예시

나쁜 변경:

```text
MainWindow가 크므로 MainWindow.Part2.cs를 만들고 private 필드를 그대로 공유한다.
```

허용되는 변경:

```text
Workbench의 Viewer 표시 event 구독과 해제, overlay routing이 하나의 수명주기를 이룬다.
이를 WorkbenchViewerDisplayCoordinator로 이동하고 MainWindow에서는 생성/Dispose만 수행한다.
기존 handler가 남아 있지 않은지 검색하고 focused verification과 screenshot parity를 확인한다.
```

## 12. 완료 보고 형식

```text
Status: Complete | Blocked | Incomplete
Scope: <완료한 동작만>
Acceptance criteria: <기준 -> pass/fail 증거>
Verification: <실제로 실행한 명령과 결과>
Evidence: <artifact, log, 문서>
Boundary / next dependency: <증명하지 않은 범위 또는 정확한 blocker>
```
