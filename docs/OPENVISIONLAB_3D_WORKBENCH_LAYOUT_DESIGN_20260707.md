# OpenVisionLab 3D Workbench Layout Design

Updated: 2026-07-07

## Purpose

Before adding more Viewer features, OpenVisionLab 3D Studio should lock a commercial-style inspection workbench layout. New functionality should be placed into this layout deliberately instead of being added wherever it is easiest in the current WPF tree.

This document is the layout contract for the next Viewer phase.

## Design Principle

Commercial 3D inspection tools use the 3D view as the center of a workflow:

```text
source data
  -> alignment / view / ROI
  -> measurement or rule tool
  -> visual overlay + metrics
  -> pass/fail decision
  -> report / replay / comparison
```

The OpenVisionLab 3D layout should make that workflow visible on the first screen. It should not start as a generic model viewer and later bolt inspection panels around it.

## Target Layout

```text
+--------------------------------------------------------------------------------+
| App / Job Bar                                                                  |
| Project | Recipe | Open | Save | Preview | Publish | Run Compare | Status       |
+----------------------+--------------------------------------+------------------+
| Data & Layers        | 3D Inspection View                   | Tool / Inspector |
|                      |                                      |                  |
| Source entities      | View toolbar                         | Active tool      |
| Result entities      | Camera / selection / ROI modes       | Parameters       |
| Visibility toggles   |                                      | Units/tolerance  |
| Color mode           | SharpGL viewport                     | Metrics          |
| Unit/transform state | Source + result overlays             | Selected point   |
|                      | Color scale / tolerance legend       | Result evidence  |
+----------------------+--------------------------------------+------------------+
| Evidence Workbench                                                               |
| Tabs: Result Summary | UI Contract | Runner Report | History | Screenshot        |
+--------------------------------------------------------------------------------+
| Optional Linked View Strip                                                       |
| Height map | Profile / section chart | 2D intensity image when available        |
+--------------------------------------------------------------------------------+
```

## Panel Responsibilities

| Area | Responsibility | Must not own |
| --- | --- | --- |
| App / Job Bar | Project state, recipe state, explicit Preview/Publish/Run commands, global status. | Rendering internals or tool algorithms. |
| Data & Layers | Source/result entity tree, visibility, active source/result, color mode, unit/transform summary. | Tool parameter editing. |
| 3D Inspection View | SharpGL render, camera, picking, ROI overlays, result overlays, color scale legend. | Recipe serialization or runner report parsing. |
| Tool / Inspector | Active tool parameters, selected entity/point, metrics, tolerances, result state. | Camera state or docking layout. |
| Evidence Workbench | UI contract, runner report, screenshot snapshot, comparison status, run history. | Tool execution logic. |
| Linked View Strip | Height map, 2D intensity, profile/section chart synchronized with current selection. | Primary 3D interaction. |

## First Implementation Layout

The next code phase should create the shell/view layout skeleton before adding new algorithms:

1. Keep the existing `3D Viewer` dock document as the center surface.
2. Split the current Viewer sidebars conceptually into:
   - `Data & Layers`;
   - `Tool / Inspector`.
3. Keep `Recipe Comparison` as the first `Evidence Workbench` pane.
4. Reserve a future bottom/secondary pane for `Height Map / Profile`.
5. Add no new algorithmic behavior during the layout skeleton pass except what is needed to keep current smoke checks passing.

## Feature Placement Rules

- A feature is not ready to implement until it has a home in one of the layout areas above.
- Viewer rendering features go into the 3D Inspection View, not the Shell.
- Workflow features go into Shell panes through `OpenVisionLab.ThreeD.Docking.Controls` content slots.
- Tool parameters and metrics belong in Tool / Inspector, not in the Data & Layers tree.
- Result evidence belongs in Evidence Workbench, not only inside viewport text.
- A visible layout change needs a Shell-wide screenshot, not only a Viewer-control screenshot.

## Commercial-Parity Feature Slots

| Feature | Layout home | Priority |
| --- | --- | --- |
| Deviation color scale / tolerance legend | 3D Inspection View | High |
| Point size and render-density controls | 3D Inspection View or Data & Layers | Medium |
| Recipe save/edit | App / Job Bar + Tool / Inspector | High |
| Section/profile tool | 3D Inspection View + Linked View Strip | High |
| Height-map view | Linked View Strip | High |
| Run history | Evidence Workbench | Medium, after save/edit |
| Transform/alignment state | Data & Layers + Tool / Inspector | Medium |
| Screenshot/report snapshots | Evidence Workbench | Medium |
| CAD/GD&T | Not in current layout phase | Later |
| Sensor/PLC/robot/HMI | Out of current scope | Later |

## Implementation Sequence

1. Build the layout skeleton.
   - Shell has stable docking slots for center Viewer, left Data/Layers, right Tool/Inspector, bottom Evidence, and future Linked View.
   - Current functionality is rearranged only where needed.
2. Add deviation color scale/tolerance legend.
   - Use existing C3D height deviation rule.
   - Smoke screenshot must show the legend and fail/pass threshold colors.
3. Add minimal recipe save/edit.
   - Save current C3D height deviation tolerance/source as JSON.
   - Keep Preview and Publish separate.
4. Add section/profile tool.
   - Section line is selected in 3D.
   - Profile chart appears in Linked View Strip.
5. Add height-map pane.
   - The same C3D sample can be reviewed as 2D height image and 3D point cloud.
6. Add run history.
   - Only after at least two saved/executed recipe results exist.

## Acceptance Checklist For Layout Skeleton

- Shell screenshot shows distinct workbench zones.
- Viewer-only smoke still works.
- Shell-wide smoke captures all docking panes.
- Existing recipe load, preview, publish, runner comparison still pass.
- AvalonDock remains owned by `OpenVisionLab.ThreeD.Docking.Controls`.
- `WPF-UI` remains app-level in `OpenVisionLab.ThreeD.Shell`.
- Viewer remains hostable outside the Shell.

## Deferred Decisions

- Exact icons and command styling.
- Final tab names for Evidence Workbench.
- Whether the Linked View Strip is bottom-docked or right-docked on smaller screens.
- Whether the first height-map view is rendered in WPF bitmap, SharpGL texture, or a lightweight custom control.

Do not resolve these until the layout skeleton exists and current smoke evidence shows the workspace is usable.
