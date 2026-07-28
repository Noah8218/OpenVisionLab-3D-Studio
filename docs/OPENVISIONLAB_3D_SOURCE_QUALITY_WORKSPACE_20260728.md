# OpenVisionLab 3D Source Quality Workspace

Date: 2026-07-28

Backlog item: `B-08`

Status: Complete

## Outcome

The normal Inspection Workbench now exposes Source Quality as the default
Selected Tool workspace whenever an identified C3D source is loaded and no
inspection step is selected.

The Recipe Chain source card has a familiar histogram action that returns to
Source Quality after an inspection step has been selected. Selecting or
opening the workspace is read-only:

- it does not edit or dirty the recipe;
- it does not change the source identity;
- it does not invoke Preview, Publish, Run, Validation Set, or Save;
- it does not fabricate unsupported source channels.

When the operator selects an inspection step, the existing Selected Tool
Inputs/Parameters/Regions/Outputs/Help workspace remains unchanged.

## Operator workflow

1. Open an identified C3D source.
2. Review Source Quality before adding or previewing a tool.
3. Confirm the native grid and total cell count.
4. Review valid and missing counts and ratios.
5. Review raw-height range, mean, and the 32-bin distribution.
6. Confirm the invalid-cell map byte count and SHA identity.
7. Confirm which channels are available and why unsupported channels are
   unavailable.
8. Expand identity, coordinates, and provenance when exact traceability is
   needed.
9. Select an inspection step to continue authoring.
10. Select the source card histogram action to return to Source Quality.

## Visible evidence

The unified workspace shows:

| Section | Evidence |
| --- | --- |
| Header | Source file, analysis state, read-only/non-execution boundary |
| Grid and coverage | Native width/height, total cells, valid/missing counts and ratios |
| Invalid-cell map | Packed byte length and short SHA with full SHA tooltip |
| Height statistics | Raw-height minimum, maximum, mean, 32-bin histogram, peak bin |
| Channels | Available/Unavailable state plus the source-owned reason |
| Identity | Source bytes/SHA, frame, unit, coordinate convention, provenance |

Only Height is available for the supported C3D grid. Intensity, Color, Depth,
Normal, Confidence, and SNR remain visibly unavailable; the UI does not infer
or synthesize them.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `SourceQualityWorkspaceViewModel` | Async/cancellable report load, read-only presentation state, coverage/statistic/channel rows |
| `SourceQualityWorkspaceView` | Compact scrollable WPF presentation for wide and 1280 × 760 layouts |
| `ToolWorkbenchViewModel.SourceQuality` | Source/step workspace selection and non-mutating source-card navigation |
| `C3DSourceQualityAnalyzer` | Existing deterministic report and invalid-map identity |
| `SourceQualityWorkspaceVerification` | Synthetic report, presentation, navigation, clear/error, and non-execution boundaries |
| Shell Source Quality smoke | Exact-source readiness, UI selection, state preservation, and screenshot evidence |

The ViewModel consumes the existing Core/Data report contract. It does not
recalculate measurement statistics in WPF and does not own recipe execution.

## Exact owner-source evidence

Source:

```text
3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D
```

| Field | Value |
| --- | ---: |
| Width | 1,466 |
| Height | 2,269 |
| Native cells | 1,075,200 |
| Valid cells | 908,436 (`84.5%`) |
| Missing cells | 166,764 (`15.5%`) |
| Raw-height range | `-1179.4000244140625` to `2348.60009765625` |
| Raw-height mean | `664.5656229231487` |
| Distribution | 32 bins, zero-based peak index 18 |
| Packed invalid-cell bytes | 134,400 |
| Invalid-cell SHA-256 | `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358` |
| Channels | 7 declared, Height available |

Wide and compact exact-source smoke reports both record:

```text
viewOnly=true
recipeChanged=false
inspectionRun=false
steps=0->0
selections=0->0
previewRunning=False->False
```

## Verification

Commands:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release --disable-build-servers

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-source-quality-workspace `
  artifacts/current/20260728-source-quality-workspace/source-quality-workspace-verification.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-shell-smoke-command-line `
  artifacts/current/20260728-source-quality-workspace/shell-smoke-options.txt
```

Exact-source UI smoke adds:

```text
--smoke-async-c3d-load 3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D
--smoke-source-quality
--smoke-source-quality-report <report>
--shell-smoke-screenshot <png>
--shell-screenshot-quality-report <quality-report>
```

Current evidence:

| Gate | Result |
| --- | --- |
| Release build | `0` warnings / `0` errors |
| Source Quality ViewModel/workspace | `18/18` |
| Shell smoke options | `16/16` |
| Exact-source wide Source Quality smoke | Pass |
| Exact-source compact Source Quality smoke | Pass |
| Wide screenshot quality | accepted on attempt 1 |
| Compact screenshot quality | accepted on attempt 1 |
| Inspection Workspace regression | `30/30` |
| Docking/composition regression | `33/33` |
| Recipe teaching regression | `28/28` |
| Artifact Navigator regression | `31/31` |
| SourceQualityReport regression | `13/13` |
| Invalid-cell map regression | `15/15` |
| Height Image regression | `14/14` |
| Executable structure guard | `17/17` |

## UI evidence

Before: the current Release loaded the exact C3D but left Selected Tool at a
generic “no inspection step selected” placeholder.

- `artifacts/current/20260728-source-quality-workspace/before-wide-no-source-quality.png`
- `artifacts/current/20260728-source-quality-workspace/before-compact-no-source-quality.png`

After: the same normal workspace shows the exact Source Quality report while
the dominant 3D Viewer remains available.

- `artifacts/current/20260728-source-quality-workspace/after-wide-source-quality.png`
- `artifacts/current/20260728-source-quality-workspace/after-compact-source-quality.png`

Visual comparison:

- the previously empty Selected Tool surface now contains actionable source
  trust evidence;
- the normal four-pane wide composition is preserved;
- the compact composition keeps Source Quality and the dominant Viewer
  visible without adding a new permanent dock column;
- source-card histogram and folder icons remain distinguishable, tooltipped,
  and accessible.

## Boundaries and next dependencies

This completion does not claim:

- a visible invalid/missing overlay in the Height Image (`C-11`);
- topology, monotonicity, duplicate-locator, or malformed-coordinate
  diagnostics (`B-10`);
- real intensity, color, depth, normal, confidence, or SNR data for the
  supported C3D layout;
- manual/auto shared display range (`C-07`);
- shared Height Image and 3D hover/crosshair state as part of `B-08`; the
  separate `C-08` slice is now complete;
- synchronized Height Image / 3D ROI editing (`C-09`, `C-10`);
- physical calibration, traceability, uncertainty, GR&R, or certified
  metrology.

Next dependency-correct order:

`C-07` and `C-08` were completed later on 2026-07-28. Current next
priorities:

1. `C-09/C-10 synchronized Height Image / 3D ROI editing` | Recommended model: `gpt-5.6-sol` | Reasoning effort: high

2. `E-07/E-08 typed OrientedBox3D contract and numeric editing` | Recommended model: `gpt-5.6-sol` | Reasoning effort: high

## Completion record

Status: Complete

Scope: `B-08` unified, read-only Source Quality workspace in the normal
Inspection Workbench, including source-card navigation, exact report
presentation, wide/compact layout, and non-execution evidence.

Acceptance criteria:

- operator can discover and inspect current source quality in the normal
  Workbench -> pass, current wide/compact screenshots;
- grid, coverage, raw-height statistics/distribution, mask identity,
  frame/unit/provenance, and channel availability use the existing report ->
  pass, `18/18` and exact-source smoke;
- selecting Source Quality preserves recipe and execution state -> pass,
  synthetic and actual boundary reports;
- wide and 1280 × 760 layouts remain usable -> pass, screenshot quality on
  attempt 1;
- established recipe/viewer/data behavior remains intact -> pass, regression
  table above.

Verification: commands and results listed above.

Evidence:

- this document;
- `artifacts/current/20260728-source-quality-workspace/`.

Boundary / next dependency: `C-07` and `C-08` were completed later on
2026-07-28, followed by `C-09/C-10` synchronized ROI editing. `C-11` visible
invalid-cell overlay is next. R0 owner replay, physical calibration, and
metrology remain external or unverified.
