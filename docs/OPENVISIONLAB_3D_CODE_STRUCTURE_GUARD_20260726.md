# OpenVisionLab 3D Code Structure Guard

Date: 2026-07-26

Status: Complete

## Scope

`docs/OPENVISIONLAB_3D_CODE_RULES.md`의 핵심 구조 규칙을 외부 분석기나
새 패키지 없이 한 개의 PowerShell 검증으로 고정했다.

`scripts/verify-code-structure.ps1`은 다음을 확인한다.

- `src` 아래 12개 코드 프로젝트가 `.sln`과 `.slnx`에 모두 존재한다.
- Core는 project reference가 없다.
- Data는 Core만 참조한다.
- Tools는 Core와 Data만 참조한다.
- Runner는 Core, Data, Tools만 참조한다.
- Core/Data/Tools/Runner에 WPF, SharpGL, WPF-UI, AvalonDock,
  WindowsDesktop package가 들어오지 않는다.
- `App`과 `Program`이 각각 Shell/Runner command router에 위임한다.
- `MainWindow`가 `WorkbenchViewerDisplayCoordinator`를 생성하고 해제한다.
- 이전 Shell display handler owner와 Viewer/Runner의 중복
  `ApplyModelTransform` 구현이 다시 생기지 않는다.

## Command

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File "scripts\verify-code-structure.ps1"
```

기본 보고서는 다음 경로에 생성된다.

```text
artifacts/current/20260726-code-structure-guard/code-structure-report.txt
```

## Acceptance criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| 현재 구조 기준선 통과 | Pass | `15/15`, exit code `0` |
| solution 누락을 실패 처리 | Pass | 임시 `.slnx`에서 MessageDialogs 제거 시 `14/15`, exit code `1` |
| 실패 원인이 구체적으로 기록됨 | Pass | `missing=src/OpenVisionLab.Wpf.MessageDialogs/OpenVisionLab.Wpf.MessageDialogs.csproj` |
| 테스트용 변경이 작업 트리에 남지 않음 | Pass | 임시 root-level `.slnx`는 검증 직후 삭제 |
| 새 외부 의존성 없음 | Pass | PowerShell/.NET 기본 API만 사용 |

## Evidence

- `artifacts/current/20260726-code-structure-guard/code-structure-report.txt`
- `artifacts/current/20260726-code-structure-guard/intentional-drift-report.txt`
- `artifacts/current/20260726-code-structure-guard/intentional-drift-process.txt`

## Boundary / next dependency

이 검사는 dependency와 명시적 composition owner의 구조적 회귀만
차단한다. 클래스의 응집도, 새 partial의 설계 타당성, MVVM의 의미적
정확성을 자동 판정하지 않는다. 그 판단은 코드 규칙 체크리스트와
focused verification으로 계속 증명해야 한다.
