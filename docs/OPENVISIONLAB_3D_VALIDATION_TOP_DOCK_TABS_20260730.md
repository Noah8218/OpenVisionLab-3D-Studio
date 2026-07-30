# OpenVisionLab 3D Validation Top Dock Tabs

Date: 2026-07-30
Status: Complete

## Purpose

Move the AvalonDock work-surface tab strip from the bottom edge to the top,
where operators can discover and select it before scanning the active content.
Match the strip to the existing OpenVisionLab navy/light/teal visual system.

This concerns the dock tabs such as Pipeline / Validation, Output Compare,
Displayed Outputs, Session Log, Height Profile, Fit Diagnostics, Intersection
Evidence, and Correspondence Evidence. The existing Validation-local Samples,
Run Results, Failure Analysis, Threshold Review, and Held-out navigation
remains directly below it.

## Scope

Included:

- top placement for every multi-item anchorable pane;
- selected, hover, keyboard-focus, disabled, and normal tab states;
- removal of the duplicate selected-pane title when a multi-item top strip is
  present;
- preservation of the normal title bar for a single-item pane;
- current Release Wide and Compact visual evidence;
- docking and Validation Set regression verification.

Excluded:

- changes to tool execution, recipe semantics, Preview, Publish, Run, or
  Validation Set commands;
- changes to the Validation-local section order;
- camera, PLC, robot, cloud, calibration, or metrology scope.

## Design

The dock wrapper remains the stable owner. It supplies one
`OpenVisionTopAnchorablePaneStyle` to AvalonDock:

- tabs are placed in the first template row;
- active content is placed below the strip;
- the strip uses the shared Command Bar and Divider brushes;
- the selected tab uses Selected Surface, Accent border/text, and semibold
  type;
- hover uses Panel Alternate and Divider;
- keyboard focus uses the shared Focus brush;
- disabled tabs use the shared Disabled brush;
- a one-item pane collapses the redundant tab and retains its normal pane
  title;
- a multi-item pane suppresses the duplicate pane title and uses the top strip
  as its single navigation/title owner.

No icon was added. The current titles are short and distinct, while the
existing model has no semantic per-pane icon contract; a repeated decorative
icon would reduce rather than improve recognition.

## Implementation

- `src/OpenVisionLab.ThreeD.Docking.Controls/Views/OpenVisionDockWorkspaceView.xaml`
  owns the top strip and tab visual states.
- `OpenVisionDockWorkspaceView.HasTopThemedDockTabs` exposes the applied
  contract without exposing raw AvalonDock details to the Shell.
- `ToolRecipeWorkbenchView.HasTopThemedDockTabs` forwards the bounded
  presentation contract.
- `ToolWorkbenchDockingVerification` asserts the contract.

## Verification

- Release solution build: Pass, `0` warnings / `0` errors.
- Workbench docking: Pass, `59/59`.
- Validation Set: Pass, `84/84`.
- Actual UI Automation and pointer audit: Pass; all eight dock tabs expose
  their localized title and stable ContentId on the TabItem, and a pointer
  click selects Output Compare.
- Wide actual Release capture: Pass, `1920 x 1040`.
- Compact actual Release capture: Pass, `1280 x 760`.
- Visual comparison:
  - before: the work-surface strip was attached to the bottom window edge;
  - after: the same strip is above Validation content in both layouts;
  - selected Pipeline / Validation is teal-accented;
  - all eight titles remain on one row in Compact;
  - the old bottom strip and duplicate dark pane title are absent;
  - the Validation-local section navigation and sample actions remain visible.

## Evidence

- `artifacts/current/20260730-validation-top-tabs/before/wide-validate-before.png`
- `artifacts/current/20260730-validation-top-tabs/after/wide-validate-after.png`
- `artifacts/current/20260730-validation-top-tabs/after/compact-validate-after.png`
- `artifacts/current/20260730-validation-top-tabs/wide-before-after.png`
- `artifacts/current/20260730-validation-top-tabs/workbench-docking.txt`
- `artifacts/current/20260730-validation-top-tabs/validation-set.txt`
- `artifacts/current/20260730-validation-top-tabs/uia-top-tabs.txt`

## Completion record

Status: Complete
Scope: Moved the multi-pane AvalonDock strip to the top and applied the
OpenVisionLab interaction/theme states without changing inspection behavior.
Acceptance criteria: top placement -> Pass in Wide/Compact current Release;
OpenVision theme -> Pass; one-row Compact fit -> Pass; no lower strip or
duplicate title -> Pass; localized accessible TabItem names/IDs and actual
Output Compare selection -> Pass; docking contract -> 59/59; Validation Set
regression -> 84/84.
Verification: Release build `0/0`, focused verification reports, actual
application-only Wide/Compact screenshots, and direct visual comparison.
Evidence: this document and
`artifacts/current/20260730-validation-top-tabs/`.
Boundary / next dependency: human-owner unaided R0 must restart on this new UI
binary set. SurfaceModel remains gated until that external acceptance passes.
