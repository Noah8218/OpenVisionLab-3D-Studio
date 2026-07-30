# OpenVisionLab 3D Studio Human-owner R0 Execution

Date: 2026-07-30
Status: Blocked — the current Release and inputs are ready, but the product
owner has not yet completed the two unaided runs.

## Purpose

Close the last external acceptance item for Workspace v3 by having the product
owner operate the current Release without click-by-click assistance.

This is not another Codex simulation. The launcher below verifies and opens the
approved inputs, sizes the application window, and then stops intervening.

## Fixed evidence inputs

- Release EXE:
  `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Shell.exe`
- Shell assembly:
  `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Shell.dll`
- Docking assembly:
  `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Docking.Controls.dll`
- Completeness recipe:
  `artifacts/current/20260729-completeness-threshold-assistance/validation-set-fixture/completeness-threshold-fixture.ov3d-recipe.json`
- supplied Fail Run Record:
  `artifacts/current/20260729-completeness-results-overlays/runner-record.json`
- launcher: `scripts/start-human-owner-r0.ps1`

Prepared-input SHA-256:

| Input | SHA-256 |
|---|---|
| Release EXE | `3219C29579F8F93F2D75491F94871CEA7A2FB0472C2A9DF7CF390812FFF70721` |
| Shell assembly | `ED3A5BA964AD7C0FCF388B91524C25A056E7E24653F3A14BAB77D12BBF498782` |
| Docking assembly | `0998512EF9DA6DBE0A1EFB1914736EF3B94907FDEC1ED840FA7FEF4DAF9CA34B` |
| Completeness recipe | `0DABE2D9A0B1931FD4E5F3E064C8157C02EC6DF60807C84B530128099B3CC461` |
| Fail Run Record | `BAB565978CF786D5C8795D0F8F6898F29D1085820CF032EECC9F315B1544340A` |

The launcher fails closed if an input is missing or the Release EXE is older
than current `.cs`, `.xaml`, or `.csproj` source.

The 2026-07-30 top dock-tab change supersedes the previously prepared UI
binary set. R0 must use the hashes above and restart from Wide; no result from
the earlier UI build can close this gate.

## Owner task brief

Give the owner only this goal before each run:

> Run the supplied five-sample Completeness validation, investigate one failure
> in Teach, review Results and Advanced, return to Validation, and determine
> whether the same failure evidence is preserved.

Do not give the owner control names, click coordinates, the expected route, or
recovery instructions while the run is in progress.

## Launch

Run Wide first:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  .\scripts\start-human-owner-r0.ps1 -Layout Wide
```

Close the application after the observation is recorded, then run Compact:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  .\scripts\start-human-owner-r0.ps1 -Layout Compact
```

Prerequisites can be checked without opening the UI:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  .\scripts\start-human-owner-r0.ps1 -Layout Wide -ValidateOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  .\scripts\start-human-owner-r0.ps1 -Layout Compact -ValidateOnly
```

The process-local bypass avoids changing the machine or user execution policy.

## Observer-only acceptance sheet

Record behavior without coaching.

| Criterion | Wide | Compact |
|---|---|---|
| Owner completed the goal unaided | Pending | Pending |
| Five samples and `2 Good / 2 Bad / 1 Held-out` were recognized | Pending | Pending |
| Explicit sample run ended `3 Pass / 2 Fail / 0 Error` | Pending | Pending |
| One failed sample was understood before entering Teach | Pending | Pending |
| Teach restored `Completeness Grid`, Viewer, ROI, and failed-sample context | Pending | Pending |
| No automatic Preview, Run, Publish, or recipe-semantic mutation occurred | Pending | Pending |
| Results showed the supplied one-step Fail Run Record | Pending | Pending |
| Results -> Advanced -> Results worked | Pending | Pending |
| Return to Validation preserved the same failure evidence | Pending | Pending |
| No hesitation, misleading label, clipped required action, or recovery help | Pending | Pending |

Observation notes:

```text
Wide
Start/end:
Outcome: Pass | Fail
Hesitation or wrong turn:
Misleading label:
Clipped or unreachable action:
Unexpected execution/mutation:
Lost state:
Owner comment:

Compact
Start/end:
Outcome: Pass | Fail
Hesitation or wrong turn:
Misleading label:
Clipped or unreachable action:
Unexpected execution/mutation:
Lost state:
Owner comment:
```

## Decision rule

R0 passes only when both Wide and Compact pass every row unaided. Any required
coaching, hesitation that prevents completion, misleading label, clipped or
unreachable required action, unintended execution/mutation, or lost evidence is
a failure. Do not coach around a failure; preserve the observation and reopen
the corresponding product gap.

When both layouts pass:

- change `A-01` from Partial to Complete;
- change Workspace v3 from `7/8` to `8/8`;
- preserve the completed sheet and any app-only recordings as evidence;
- begin `J-01/J-03/J-04 SurfaceModel`.

If either layout fails, keep `A-01` Partial and create a concrete repair item
from the first observed blocking behavior.

## Completion record

Status: Blocked
Scope: Prepared a non-automated current-Release launcher and an observer-only
Wide/Compact R0 acceptance record.
Acceptance criteria: fixed inputs identified -> Pass; input hashes recorded ->
Pass; stale-Release guard -> Pass; Wide/Compact validation-only launch checks ->
Pass; Wide owner run -> Pending; Compact owner run -> Pending.
Verification: both process-local `-ValidateOnly` commands passed on 2026-07-29,
reported the fixed hashes above, confirmed the Release was newer than current
source, and launched no application. A live Wide launcher check then opened the
correct command-line inputs in a responding `1920 x 1040` application window;
no UI action was automated.
Evidence: this document and `scripts/start-human-owner-r0.ps1`.
Boundary / next dependency: the product owner must personally complete both
unaided runs before `A-01`, Workspace v3, or SurfaceModel can advance.
