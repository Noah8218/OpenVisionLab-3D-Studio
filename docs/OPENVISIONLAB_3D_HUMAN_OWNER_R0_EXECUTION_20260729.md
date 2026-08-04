# OpenVisionLab 3D Studio Human-owner R0 Execution

Date: 2026-08-04
Status: Blocked - the current fixed-input package and launcher are ready, but
the product owner's unaided Wide and Compact runs are still required.

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
- Core assembly:
  `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Core.dll`
- Data assembly:
  `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Data.dll`
- Tools assembly:
  `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Tools.dll`
- Viewer assembly:
  `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Viewer.dll`
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
| Release EXE | `D1439160F7D0A5FA1E980D1A172CA731D8E955729A0D5E948CC992E14A2EECDA` |
| Shell assembly | `D4ABE60BDD90BECA71D658A933EC682A2B0B868B8136150864836D673F558EBA` |
| Core assembly | `2573842B3A7D378E342DE1191665F883857919BFD7A89C2082831201D95DFEE5` |
| Data assembly | `9DE4FCE15F824BF6D666935CFF7BAB9418ABA8C011E091B1E858D0B79A2080C6` |
| Tools assembly | `DB13F20E1CFC0595A5CA4BBC49421129940C807CA439B6EC6775735EEF16AD76` |
| Viewer assembly | `12A9F00E47E5189DD9CEB19AD77CADFCF5B398D6C4B3F9421350EF3B3C1EBDA4` |
| Docking assembly | `3E2B30A8DFB09ED5F51B8F25D9EFFE8DCFB5850B211EDA44DD7CDADA5BF0515B` |
| Completeness recipe | `0DABE2D9A0B1931FD4E5F3E064C8157C02EC6DF60807C84B530128099B3CC461` |
| Fail Run Record | `BAB565978CF786D5C8795D0F8F6898F29D1085820CF032EECC9F315B1544340A` |

The launcher fails closed if an input is missing, any SHA-256 differs from
the fixed table above, or the Release EXE is older than current `.cs`,
`.xaml`, or `.csproj` source.

The 2026-08-04 current-source Release rebuild supersedes every previous fixed
binary set and includes the completed B-12, K-04, L-13, and PL-0002 software
state. R0 must use the hashes above and restart from Wide; no result from an
earlier binary set can close this gate. PL-0002 changes Runner rather than an
R0 fixed input, but the conservative stale-source guard required a full
current-source rebuild before this package could be validated.

The launcher selects the monitor with the smallest `Bounds.Left`, reports its
device name and bounds, places the application there, and fails closed if the
actual application window does not intersect that monitor. On the current
workstation the selected monitor is `\\.\DISPLAY2` with bounds
`[-1920,365,1920,1080]`.

The current Wide handoff launch verified actual window bounds
`[-1920,365,1920,1040]` intersect that monitor and left the application open
for owner operation. This proves launch placement only; the Wide acceptance
rows remain Pending until the owner reports the unaided outcome.

The product owner's 2026-07-31 direction allows dependency-ready software
development to continue before this R0 is performed. This sheet still gates
only `A-01`, Workspace v3 `8/8`, and human-usability acceptance; automated
software evidence does not close those claims.

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
Monitor/window bounds:
Outcome: Pass | Fail
Hesitation or wrong turn:
Misleading label:
Clipped or unreachable action:
Unexpected execution/mutation:
Lost state:
Owner comment:

Compact
Start/end:
Monitor/window bounds:
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
- apply any observed usability findings to the then-current software package.

If either layout fails, keep `A-01` Partial and create a concrete repair item
from the first observed blocking behavior.

## Completion record

Status: Blocked
Scope: Prepared a non-automated current-Release launcher and an observer-only
Wide/Compact R0 acceptance record.
Acceptance criteria: fixed inputs identified -> Pass; input hashes recorded ->
Pass; stale-Release guard -> Pass; Wide/Compact validation-only launch checks ->
Pass; Wide leftmost-monitor launch placement -> Pass; Wide owner run ->
Pending; Compact owner run -> Pending.
Verification: the current source was rebuilt in Release with `0` warnings and
`0` errors on 2026-08-04. Both process-local `-ValidateOnly` commands passed,
enforced the refreshed nine-input fixed hashes above, confirmed the Release
was newer than current source, selected `\\.\DISPLAY2` as the leftmost
monitor, and launched no application. The subsequent owner Wide handoff
launch placed the actual window at `[-1920,365,1920,1040]`, verified its
intersection with that monitor, and left the application open without
performing the task.
Evidence: this document, `scripts/start-human-owner-r0.ps1`, and
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260804-r0-package-refresh\`.
Boundary / next dependency: the product owner must personally complete both
unaided runs before `A-01` or Workspace v3 acceptance can advance. Surface
matching software may proceed independently, but it cannot be used to claim
that this human workflow passed.
