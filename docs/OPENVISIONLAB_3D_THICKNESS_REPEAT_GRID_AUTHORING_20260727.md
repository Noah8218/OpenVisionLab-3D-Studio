# Thickness 4 x 2 Repeat-Grid Authoring

Date: 2026-07-27

Status: Complete
Workspace v3 slice: 7 of 8

## Outcome

One complete dual-ROI Thickness inspection can now be expanded into a
reviewable `4 columns x 2 rows` candidate and explicitly applied as eight
ordinary, independently editable Thickness steps.

This closes the repeated-Tab authoring gap identified in the GoPxL workflow
comparison without adding automatic part detection or hidden inspection
execution.

The verified source is:

`3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D`

The verified default request is:

| Field | Value |
| --- | ---: |
| Columns | 4 |
| Rows | 2 |
| X pitch | 228 columns |
| Z pitch | 690 rows |
| Name pattern | `Tab {n}` |

## Operator workflow

1. Select one complete Thickness step with an applied Reference ROI,
   Measurement ROI, and committed parameters.
2. In Selected Tool > ROI / Regions, select `Repeat as grid`.
3. Review or edit columns, rows, X pitch, Z pitch, and the `{n}` name pattern.
4. Review all candidate Reference/Measurement coordinates in the list and in
   the Viewer.
5. Select `Apply repeat` to create independent recipe entities, or
   `Cancel repeat` to discard the candidate.
6. Select any generated Tab to edit its own parameters or ROI pair.
7. Invoke Preview or Run explicitly when inspection evidence is required.

The candidate overlay uses cyan for Reference and orange for Measurement. It
is display-only and has no editing handles. The selected authored ROI retains
the normal yellow active state.

## Recipe and execution contract

Before Apply:

- the authored recipe remains one step and two selections;
- changing the repeat request rebuilds only the candidate;
- Cancel returns to the exact authored state;
- Preview, Publish, Run, Validation Set, and Save are not invoked.

After Apply:

- the source Thickness step becomes `Tab 1 Thickness` while retaining its
  existing step, output, and two selection identities;
- Tabs 2 through 8 receive unique step, output, Reference ROI, and Measurement
  ROI identities;
- all parameter records and ROI dimensions are cloned;
- every ROI is validated against the recorded `1280 x 840` source grid;
- the recipe is marked dirty;
- no measurement result is produced until the operator invokes Preview or
  Run;
- save/reopen restores all eight names, routes, outputs, and 16 ROIs.

The generated default coordinates are:

| Tab | Reference column,row | Measurement column,row |
| ---: | --- | --- |
| 1 | `515,430` | `575,430` |
| 2 | `743,430` | `803,430` |
| 3 | `971,430` | `1031,430` |
| 4 | `1199,430` | `1259,430` |
| 5 | `515,1120` | `575,1120` |
| 6 | `743,1120` | `803,1120` |
| 7 | `971,1120` | `1031,1120` |
| 8 | `1199,1120` | `1259,1120` |

The nominal repeat intentionally uses one constant pitch. Individual generated
ROIs remain editable because the real part can differ from an ideal grid.

## Ownership and MVVM boundary

| Owner | Responsibility |
| --- | --- |
| `ThicknessRepeatGridAuthoringService` in Tools | Pure request validation, coordinate translation, unique identity allocation, and storage-valid candidate document |
| `ThicknessRepeatGridAuthoringSession` in Shell | Mutable review request and display-only candidate lifecycle |
| `ToolWorkbenchViewModel` | Begin, Apply, Cancel commands, recipe dirty transition, logging, and compact group projection |
| `SelectedToolWorkspaceView` | Bound request/review controls only |
| `WorkbenchViewerTeachingCoordinator` | Forwards candidate selections to the Viewer and clears them on Cancel/Apply |
| `OpenVisionThreeDViewerControl` | Renders candidate overlays without making them authored or editable |

The pure authoring service has no WPF or Viewer dependency. WPF code-behind
only scrolls the already bound repeat panel into view for deterministic smoke
capture.

## Verification

Current Release verification:

- solution build: `0` warnings / `0` errors;
- focused repeat authoring: `20/20`;
- code structure: `17/17`;
- Inspection Workspace: `26/26`;
- Workbench docking and repeat controls: `33/33`;
- Shell smoke options: `14/14`;
- Artifact Navigator/Output Compare: `31/31`;
- generic height measurement: `45/45`;
- recipe teaching/save/reopen: `28/28`;
- Recipe Manager/WPG: `37/37`;
- teaching capture: `25/25`;
- Validation Set: `25/25`;
- Viewer display/projection: `103/103`;
- logging: `4/4`;
- keyboard readiness: `3/3`;
- generated saved recipe Runner replay: `8/8`;
- actual Windows Viewer pointer/menu regression: pass;
- wide Review, compact Review, and wide Applied screenshot quality:
  accepted on attempt 1;
- `git diff --check`: pass.

The generated recipe replay used the exact C3D and returned eight real
`DualSurfaceThicknessRule` records. All returned Pass because the retained
software-connectivity limits are deliberately broad.

Primary commands actually run:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Shell.exe `
  --verify-thickness-repeat-grid `
  artifacts/current/20260727-thickness-repeat-grid/thickness-repeat-grid-verification.txt

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verify-code-structure.ps1 `
  -ReportPath artifacts/current/20260727-thickness-repeat-grid/code-structure-report.txt

OpenVisionLab.ThreeD.Runner.exe `
  --tool-recipe artifacts/current/20260727-thickness-repeat-grid/repeat-grid-applied.ov3d-recipe.json `
  --report artifacts/current/20260727-thickness-repeat-grid/repeat-grid-run.txt `
  --expect-status Pass
```

## Evidence

- `artifacts/current/20260727-thickness-repeat-grid/before-wide.png`
- `artifacts/current/20260727-thickness-repeat-grid/before-compact.png`
- `artifacts/current/20260727-thickness-repeat-grid/after-review-wide.png`
- `artifacts/current/20260727-thickness-repeat-grid/after-review-compact.png`
- `artifacts/current/20260727-thickness-repeat-grid/after-applied-wide.png`
- `artifacts/current/20260727-thickness-repeat-grid/thickness-repeat-grid-verification.txt`
- `artifacts/current/20260727-thickness-repeat-grid/repeat-grid-one-step.ov3d-recipe.json`
- `artifacts/current/20260727-thickness-repeat-grid/repeat-grid-applied.ov3d-recipe.json`
- `artifacts/current/20260727-thickness-repeat-grid/repeat-grid-run.txt`
- `artifacts/current/20260727-thickness-repeat-grid/repeat-grid-run.json`
- `artifacts/current/20260727-thickness-repeat-grid/repeat-grid-run.html`
- `artifacts/current/20260727-thickness-repeat-grid/repeat-grid-run.csv`
- `artifacts/current/20260727-thickness-repeat-grid/viewer-pointer-regression.txt`

## Completion record

Status: Complete

Scope: Bounded Thickness repeat-grid request, display-only Reference and
Measurement candidate review, fail-closed grid validation, explicit
Apply/Cancel, eight independent saved recipe instances, group presentation,
save/reopen, and exact-source Runner replay.

Acceptance criteria: one complete Thickness required -> pass; `4 x 2`
candidate -> pass; 16 display-only candidates before Apply -> pass; invalid
pattern and out-of-grid request rejected -> pass; Cancel unchanged -> pass;
Apply creates 8 steps/16 unique selections -> pass; no automatic inspection ->
pass; save/reopen -> pass; exact-source Runner -> pass; current wide/compact
UI evidence -> pass.

Verification: Release build `0/0`; focused verification `20/20`; structure
`17/17`; docking `33/33`; smoke routing `14/14`; regression suites listed
above; Runner `8/8`; screenshot quality accepted on attempt 1.

Evidence: this document and
`artifacts/current/20260727-thickness-repeat-grid/`.

Boundary / next dependency: Inspection Workspace v3 is now `7/8` slices
(`87.5%`). The owner must complete the exact-source 12-step workflow without
guidance before v3 can be called accepted. Physical datum, calibrated units,
uncertainty, GR&R, and production tolerances remain external prerequisites for
certified thickness or production disposition.
