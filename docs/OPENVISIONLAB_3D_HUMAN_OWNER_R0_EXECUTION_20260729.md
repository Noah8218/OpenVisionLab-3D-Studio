# OpenVisionLab 3D Studio Human-owner R0 Execution

Date: 2026-08-18
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
| Release EXE | `5AABE7CD46878D47A1D5FA9440075C15146C09AAB87998E7B5EA2EA6EEC8381A` |
| Shell assembly | `522A48242C28BDFFA7D099B7CF88D098D9459D8EB2D85C8FA7893D5400007AA4` |
| Core assembly | `CEC488E10A277B632CC2A878C209C19D7B3FCFE4D6FDBB5278B56BA2E1B432B8` |
| Data assembly | `27D19CEDDBECD347F9A4743541649D541B218B7A69CCC6F9368037CB26A3FA92` |
| Tools assembly | `1B5766C3CB1C9D62113698C7CF85BC1DF0DF46398FFD9BF1B5849D3A36753026` |
| Viewer assembly | `BECBAE6636AEE1C402FA6278D12247B02CAD537E0733352357158FFC3411A27E` |
| Docking assembly | `5F9C83ECE59BE5E1AC27AD973DB653331395FF560BDAD2D5EAD68E2E9ABC4DC4` |
| Completeness recipe | `0DABE2D9A0B1931FD4E5F3E064C8157C02EC6DF60807C84B530128099B3CC461` |
| Fail Run Record | `BAB565978CF786D5C8795D0F8F6898F29D1085820CF032EECC9F315B1544340A` |

The launcher fails closed if an input is missing, any SHA-256 differs from
the fixed table above, or the Release EXE is older than current `.cs`,
`.xaml`, or `.csproj` source.

The 2026-08-18 current-source Release rebuild supersedes every previous fixed
binary set. In addition to the prior authoring, integrity, SDK-migration, and
evidence corrections, it includes PL-0015 same-grid Thickness variants,
PL-0016 ordered Shell Run with Results evidence, and PL-0017
coordinate-confident Top-view grid ROI teaching, PL-0019 standard per-step
timing evidence, and PL-0020 Source Quality Run Record evidence with Compact
Results density, PL-0021 persistent Viewer selected-coordinate status, and
PL-0022 exact Completeness per-cell Run Record export.
R0 must use the hashes above and restart from Wide; no result
from an earlier binary set can close this gate.

The launcher selects the monitor with the smallest `Bounds.Left`, reports its
device name and bounds, places the application there, and fails closed if the
actual application window does not intersect that monitor. On the current
workstation the 2026-08-18 validation selected `\\.\DISPLAY2` with bounds
`[-1920,365,1920,1080]`.

An earlier 2026-08-18 package Wide launch verified actual window bounds
`[-1920,365,1920,1040]` intersect that monitor and left the application open
for owner operation. PL-0022 refreshed the fixed binaries afterward, so that
placement is superseded. The launcher will recheck placement when the owner
starts the current package; both acceptance rows remain Pending.

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
Pass; stale-Release guard -> Pass; Wide/Compact validation-only checks ->
Pass; current-package actual launch placement -> Pending until owner launch; Wide
owner run -> Pending; Compact owner run -> Pending.
Verification: the PL-0014-, PL-0012-, PL-0013-, run-log-retention-, recipe-step-removal-,
C3D-snapshot-, alignment-status-, PL-0015-, PL-0016-, PL-0017-, PL-0019-,
PL-0020-, PL-0021-, and PL-0022-corrected
current source was rebuilt in Release with `0` warnings and `0` errors on
2026-08-18. Both process-local `-ValidateOnly` commands passed,
enforced the refreshed nine-input fixed hashes above, confirmed the Release
was newer than current source, selected `\\.\DISPLAY2` as the leftmost
monitor, and launched no application. Actual placement will be checked by the
launcher when the owner begins each current-package run.
Evidence: this document, `scripts/start-human-owner-r0.ps1`, and
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260818-pl0022-completeness-cell-export\verification\human-owner-r0-wide-validate.txt`
plus `human-owner-r0-compact-validate.txt` in the same directory. Earlier
launch evidence belongs to a superseded package and does not close the current
owner run.
Boundary / next dependency: the product owner must personally complete both
unaided runs before `A-01` or Workspace v3 acceptance can advance. A newly
approved deterministic software slice may proceed independently, but it
cannot be used to claim that this human workflow passed.
