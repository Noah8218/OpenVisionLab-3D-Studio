# Selected Tool output evidence and actions

Date: 2026-07-27

## Outcome

Inspection Workspace v3 implementation slice 5 is complete.

The selected step's Outputs section is expanded in the normal workspace and
shows the selected output's identity, freshness, primary value, declared unit,
inspection status, availability, and compare-pin state. The same card exposes
visible `Show`, `Pin`, and `Compare` actions.

These actions reuse the existing Displayed Outputs, Viewer display, and
Output Compare owners. Selecting or operating on an output does not edit the
recipe and does not invoke Preview, Publish, Run, Validation Set, or Save.

Workspace v3 is now `5/8` bounded implementation slices (`62.5%`) complete.

## Problem closed

Before this slice, the Selected Tool view listed only an output contract and
state. The operator could not see the latest measurement value or result and
had to discover separate advanced panes to display or compare a renderable
output. At compact width the output section was also below the initial
viewport without a direct smoke path to expose it.

The corrected normal path keeps output evidence with the selected tool:

- declared output identity and freshness are always visible;
- an executed Thickness result shows its primary `Mean`, unit, and
  Pass/Fail/Error state;
- renderable C3D outputs expose working Show, Pin, and Compare actions;
- measurement and feature outputs retain their real metrics and overlays but
  do not fabricate a standalone 3D surface;
- disabled actions remain visible and explain why they are unavailable.

## Behavior contract

| Output condition | Visible evidence | Show | Pin | Compare |
| --- | --- | --- | --- | --- |
| Declared or stale | Contract, freshness, availability | Disabled | Disabled | Disabled |
| Renderable C3D output, such as Filter | Contract, freshness, display state, pin state | Existing Viewer display request | First empty A/B/C slot | Pin if needed, then activate existing Output Compare pane |
| Measurement result, such as Thickness | Primary value, unit, Pass/Fail/Error, existing metric/overlay evidence | Disabled with evidence-only reason | Disabled | Disabled |
| Feature result, such as EdgePointSet | Contract, freshness, existing feature overlays | Disabled with evidence-only reason | Disabled | Disabled |

`MeasurementResult` and feature outputs currently do not own enough geometry
to create an independent surface Viewer. Showing an invented surface would
misrepresent the inspection evidence, so the UI states:
`Evidence only: no synthetic 3D surface is created`.

## Ownership and MVVM boundary

- `SelectedToolWorkspaceViewModel` projects output identity, value, unit,
  result state, availability, and action state for the selected step.
- `ToolWorkbenchViewModel` resolves the selected projected output back to the
  existing live Displayed Output and owns Show, Pin, and Compare commands.
- `ToolWorkbenchOutputCompareSession` remains the owner of A/B/C pin state.
- `WorkbenchViewerDisplayCoordinator` remains the View adapter for displaying
  a real C3D entity and activating the existing Output Compare dock pane.
- `SelectedToolWorkspaceView` binds presentation and commands only. It does
  not load geometry, execute inspection, or modify recipe state.

The compact measurement-preview smoke explicitly runs Preview only when
started with `--smoke-tool-measurement-preview`; this is test setup, not an
implicit output-card side effect.

## UI evidence

Before:

- `artifacts/current/20260727-selected-output-actions/before-wide.png`
- `artifacts/current/20260727-selected-output-actions/before-compact.png`

After:

- `artifacts/current/20260727-selected-output-actions/after-preview-wide.png`
- `artifacts/current/20260727-selected-output-actions/after-preview-compact.png`
- `artifacts/current/20260727-selected-output-actions/after-filter-actions-compact.png`

The exact Thickness source after explicit Preview shows its real mean,
`raw-height` unit, and Pass state. Its Show/Pin/Compare controls are visible
but unavailable because the output is measurement evidence rather than a
standalone surface. The Filter capture proves the same three actions enabled
for a real renderable C3D output at `1280 x 760`.

## Verification

The current Release source passed:

- `dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release
  -p:Platform="Any CPU"`: `0` warnings, `0` errors;
- selected-output and Artifact Navigator checks: `29/29`;
- Inspection Workspace selection/ROI lifecycle: `21/21`;
- Workbench docking/composition: `31/31`;
- height measurement and output evidence: `45/45`;
- recipe teaching: `28/28`;
- Recipe Manager/WPG: `37/37`;
- teaching capture: `25/25`;
- Validation Set: `25/25`;
- logging: `4/4`;
- code structure: `15/15`;
- display/projection ViewModel: `103/103`;
- exact-source ordered Runner: `8/8`;
- Shell smoke option routing: `10/10`;
- keyboard-accessible Add readiness: `3/3`;
- actual Windows Viewer pointer/menu regression: pass;
- all listed screenshot quality reports: accepted on attempt 1.

The first concurrent logging run missed its unique marker because multiple
verification processes wrote the same log concurrently. The required
sequential rerun passed `4/4`.

The Viewer pointer automation retained five failed diagnostic attempts rather
than hiding them. They exposed compact event delivery, ROI-overlay
interception, missing C3D setup, a stale wireframe-only LOD expectation while
the product default is Surface, and one mouse-up timing miss. After correcting
the harness to expect staged wireframe LOD only in Wireframe mode, the final
Surface run passed all camera, pick, pan, zoom, double-click Fit, context-menu,
GPU-buffer, and event-routing checks.

## Completion record

Status: Complete

Scope: Selected Tool output value/state/unit presentation and applicable
Show/Pin/Compare actions using the existing Viewer and Output Compare
contracts.

Acceptance criteria: output state/value/unit/status visible -> pass in
Thickness Preview captures and `45/45` verification; real C3D Show/Pin/Compare
available -> pass in Filter capture and `29/29` verification; evidence-only
outputs do not fabricate surfaces -> pass with disabled actions and explicit
reason; no implicit recipe/Preview/Publish/Run/Save mutation -> pass in focused
ViewModel verification.

Verification: Release build `0/0`; focused and regression checks listed above;
current wide and compact screenshot quality accepted.

Evidence:
`artifacts/current/20260727-selected-output-actions/` and this document.

Boundary / next dependency: Viewer split/pop-out remains slice 6. Thickness
`4 x 2` repeat authoring remains slice 7, and the owner's unaided exact-source
replay remains slice 8. Physical datum, calibration, traceable units,
uncertainty, and production tolerances remain external prerequisites for
certified thickness claims.
