# Owner first-recipe replay - 2026-07-23

## Status

`Blocked` until the owner completes the current interactive EXE session and
reports the observed result. Automated input must not be substituted for this
evidence.

## Product boundary

This replay evaluates first-task usability in the local, rule-based 3D
inspection recipe workbench. It does not evaluate algorithm accuracy, physical
units, calibration, metrology, camera/PLC integration, or production runtime.

## Exact owner task

Show the owner only the following task. Do not identify control locations or
provide a click sequence while the replay is running.

> 새 검사 레시피를 하나 만들고, 사용할 3D Map을 불러온 다음, 원하는 검사
> 도구 하나를 추가해 티칭과 미리보기를 진행하세요. 레시피를 저장한 후 닫고
> 다시 열어 저장한 내용이 유지되는지 확인하세요.

The selected tool does not have to produce a physically trusted measurement.
The purpose is to determine whether the workflow and explicit execution
boundary are understandable without developer guidance.

## Pass criteria

1. The owner creates and saves a named zero-step recipe before adding a tool.
2. The owner finds and loads a 3D Map without developer navigation guidance.
3. The owner adds exactly one compatible tool and identifies its input,
   parameters, and output.
4. Selection or parameter editing does not execute inspection automatically;
   the owner deliberately invokes Preview.
5. The owner saves, closes, and reopens the recipe and confirms the source,
   step, and parameters are retained.
6. No clipped control, unexplained blocking state, duplicate window/dialog, or
   unexpected mutation prevents completion.
7. Elapsed time and every confusing label, pane, or decision are recorded.

## Session record

| Field | Result |
|---|---|
| Current build | Pass, 0 warnings / 0 errors |
| EXE identity | `artifacts/current/20260723-owner-first-recipe-replay/exe-identity.txt` |
| Session start | `2026-07-23T10:09:11.6182913+09:00`; PID `23808`; responsive window `OpenVisionLab 3D Studio` |
| Completion decision | Blocked: launched process closed; no owner Pass / Revise / Reject result was reported |
| Elapsed time | Awaiting owner |
| Saved recipe path | No recipe created or modified after session start in the project, Desktop, Documents, or known OpenVisionLab temp roots |
| Confusing point or defect | Awaiting owner |
| Unexpected automatic execution/mutation | Awaiting owner |

## Durable record

```text
Status: Blocked
Scope: Owner-only first recipe creation, source load, one-tool teaching/Preview, save, and reopen replay
Acceptance criteria: Seven pass criteria above require direct owner observation
Verification: Current Debug build passed 0 warnings / 0 errors; one normal interactive EXE instance launched responsive as PID 23808; the process later closed; focused known-root search found 0 recent recipe files; owner task result remains unreported
Evidence: artifacts/current/20260723-owner-first-recipe-replay/
Boundary / next dependency: Owner must complete the launched EXE task and report elapsed time plus any confusion or defect
```

The closed process and zero-file observation do not prove a product failure or
an owner failure. They prove only that this session produced no reusable owner
evidence. Do not convert it into a passing first-use claim.
