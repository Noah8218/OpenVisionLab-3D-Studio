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
| 3D Inspection View | SharpGL render, camera, picking, ROI overlays, result overlays, color scale legend, viewer-internal coordinate/measurement HUD. | Recipe serialization or runner report parsing. |
| Tool / Inspector | Active tool parameters, selected entity/point, metrics, tolerances, result state. | Camera state or docking layout. |
| Evidence Workbench | UI contract, runner report, screenshot snapshot, comparison status, run history. | Tool execution logic. |
| Linked View Strip | Height map, 2D intensity, profile/section chart synchronized with current selection. | Primary 3D interaction. |

## First Implementation Layout

Implemented in the first code slice:

1. `OpenVisionLab.ThreeD.Docking.Controls` exposes stable docking slots for `Data & Layers`, `3D Inspection View`, `Tool / Inspector`, `Evidence Workbench`, and `Linked View`.
2. The Shell hosts `OpenVisionThreeDViewerControl` in the center `3D Inspection View` slot.
3. The Viewer control keeps its standalone side panels by default, but Shell sets `SidePanelsVisible=false` so workflow panes live at the workbench level.
4. The former `Recipe Comparison` pane is now the first `Evidence Workbench` tab group.
5. The `Linked View` strip exists as a bottom slot for future height-map, section/profile, and camera/pick-linked views.
6. No new algorithmic behavior was added during the layout skeleton pass.

The implemented split is:

1. Current Viewer sidebars become Shell-level workflow panes:
   - `Data & Layers`;
   - `Tool / Inspector`.
2. The Viewer remains responsible for SharpGL rendering, camera, picking, and overlays.
3. Evidence and runner comparison remain outside the viewport.

## Feature Placement Rules

- A feature is not ready to implement until it has a home in one of the layout areas above.
- Viewer rendering features go into the 3D Inspection View, not the Shell.
- Core inspection facts must remain visible inside the Viewer itself: coordinate frame, selected mode, pick state, distance/height measurement summary, and performance state. Shell panes may mirror these facts but cannot be the only UI for them.
- Workflow features go into Shell panes through `OpenVisionLab.ThreeD.Docking.Controls` content slots.
- Tool parameters and metrics belong in Tool / Inspector, not in the Data & Layers tree.
- Result evidence belongs in Evidence Workbench, not only inside viewport text.
- A visible layout change needs a Shell-wide screenshot, not only a Viewer-control screenshot.

## Commercial-Parity Feature Slots

| Feature | Layout home | Priority |
| --- | --- | --- |
| Deviation color scale / tolerance legend | 3D Inspection View | High |
| Point size and render-density controls | 3D Inspection View or Data & Layers | Done |
| Recipe save/edit | App / Job Bar + Tool / Inspector | Done |
| Section/profile tool | 3D Inspection View + Linked View Strip | Done |
| Height-map view | Linked View Strip | Done |
| Run history | Evidence Workbench | Done |
| Viewer-internal coordinate HUD | 3D Inspection View | Done |
| Two-point distance and height delta | 3D Inspection View + Tool / Inspector | Done |
| Transform/alignment state | Data & Layers + Tool / Inspector | Medium |
| Performance HUD | 3D Inspection View | Done, minimal |
| Screenshot/report snapshots | Evidence Workbench | Medium |
| CAD/GD&T | Not in current layout phase | Later |
| Sensor/PLC/robot/HMI | Out of current scope | Later |

## Implementation Sequence

1. Build the layout skeleton. Done.
   - Shell has stable docking slots for center Viewer, left Data/Layers, right Tool/Inspector, bottom Evidence, and future Linked View.
   - Current functionality is rearranged only where needed.
2. Add deviation color scale/tolerance legend. Done.
   - Use existing C3D height deviation rule.
   - Smoke screenshot must show the legend and fail/pass threshold colors.
3. Add point size/render-density controls. Done.
   - Viewer side panel and Shell `Data & Layers` expose point size and C3D render density.
   - Smoke contracts record selected point size, density mode, max rendered points, and rendered C3D point count.
4. Add minimal recipe save/edit. Done.
   - Save current C3D height deviation tolerance/source as JSON.
   - Keep Preview and Publish separate.
   - Smoke confirms saved JSON can replay through Runner.
5. Add section/profile tool. Done.
   - Section line is selected in 3D.
   - Profile chart appears in Linked View Strip.
   - Smoke contract records Linked View profile visibility, sample count, and range.
6. Add height-map pane. Done.
   - The same C3D sample can be reviewed as 2D height image and 3D point cloud.
   - Smoke contract records height-map visibility, bitmap size, and raw-height range.
7. Add run history. Done.
   - History tab shows the current replay evidence row with run time, status, peak deviation, match state, and report path.
   - Smoke can open the tab with `--shell-evidence-tab history`.
8. Add Viewer-internal coordinate HUD and two-point measurement. Done.
   - The Viewer must show axis meaning and selected measurement state even when Shell side panes are hidden.
   - Two-point measurement should report distance, dX/dY/dZ, model height delta, and raw-height delta for C3D points.
   - Minimal performance HUD should report FPS, draw time, and rendered C3D point count.

## Acceptance Checklist For Layout Skeleton

- [x] Shell screenshot shows distinct workbench zones.
- [x] Viewer-only smoke still works.
- [x] Shell-wide smoke captures all docking panes.
- [x] Existing recipe load, preview, publish, runner comparison still pass.
- [x] AvalonDock remains owned by `OpenVisionLab.ThreeD.Docking.Controls`.
- [x] `WPF-UI` remains app-level in `OpenVisionLab.ThreeD.Shell`.
- [x] Viewer remains hostable outside the Shell.

## Implementation Evidence

- Before screenshot: `artifacts/shell_workbench_layout_before.png`
- After screenshot: `artifacts/shell_workbench_layout_after.png`
- Viewer regression smoke: `artifacts/viewer_workbench_layout_regression_after.png`
- Shell embedded Viewer contract: `artifacts/shell_workbench_layout_after.txt`
- Runner comparison report: `artifacts/runner_shell_workbench_layout_after.txt`

## Deviation Legend Evidence

- Before screenshot: `artifacts/shell_color_legend_before.png`
- After screenshot: `artifacts/shell_color_legend_after.png`
- Viewer legend smoke: `artifacts/viewer_deviation_legend_after.png`
- Viewer legend contract: `artifacts/viewer_deviation_legend_after.txt`
- Shell embedded Viewer legend smoke: `artifacts/shell_deviation_legend_viewer_after.png`
- Shell legend contract: `artifacts/shell_deviation_legend_after.txt`
- Runner comparison report: `artifacts/runner_shell_deviation_legend_after.txt`

## Render Controls Evidence

- Before screenshot: `artifacts/shell_render_controls_before.png`
- After screenshot: `artifacts/shell_render_controls_after.png`
- Viewer render controls smoke: `artifacts/viewer_render_controls_after.png`
- Viewer render controls contract: `artifacts/viewer_render_controls_after.txt`
- Shell embedded Viewer render controls smoke: `artifacts/shell_render_controls_viewer_after.png`
- Shell render controls contract: `artifacts/shell_render_controls_after.txt`
- Runner comparison report: `artifacts/runner_shell_render_controls_after.txt`

## Recipe Save/Edit Evidence

- Before screenshot: `artifacts/shell_recipe_save_before.png`
- After screenshot: `artifacts/shell_recipe_save_after.png`
- Viewer recipe save smoke: `artifacts/viewer_recipe_save_after.png`
- Viewer recipe save contract: `artifacts/viewer_recipe_save_after.txt`
- Saved Viewer recipe: `artifacts/saved_c3d_height_deviation.recipe.json`
- Saved Shell recipe: `artifacts/saved_shell_c3d_height_deviation.recipe.json`
- Runner saved Viewer recipe report: `artifacts/runner_recipe_save_after.txt`
- Runner saved Shell recipe report: `artifacts/runner_shell_recipe_save_after.txt`

## Section/Profile Evidence

- Baseline before section/profile: `artifacts/shell_recipe_save_after.png`
- After screenshot: `artifacts/shell_section_profile_after.png`
- Viewer section/profile smoke: `artifacts/viewer_section_profile_after.png`
- Viewer section/profile contract: `artifacts/viewer_section_profile_after.txt`
- Contract evidence: `SectionProfile|visible=True|samples=142`

## Height Map Evidence

- Baseline before height-map: `artifacts/shell_section_profile_after.png`
- After screenshot: `artifacts/shell_height_map_after.png`
- Viewer height-map smoke: `artifacts/viewer_height_map_after.png`
- Viewer height-map contract: `artifacts/viewer_height_map_after.txt`
- Contract evidence: `HeightMap|visible=True|pixels=240x72`

## Run History Evidence

- Closest before screenshot: `artifacts/shell_run_history_before.png`
- After screenshot: `artifacts/shell_run_history_after.png`
- Runner report: `artifacts/runner_run_history_after.txt`
- Smoke command opens the Evidence Workbench `History` tab and shows the current runner/UI matched row.

## Viewer Internal HUD Evidence

- Before screenshot: `artifacts/viewer_two_point_before.png`
- Viewer after screenshot: `artifacts/viewer_two_point_after.png`
- Viewer contract: `artifacts/viewer_two_point_after.txt`
- Shell-hosted after screenshot: `artifacts/shell_viewer_internal_hud_after.png`
- Contract evidence: `CoordinateFrame|visible=True`, `TwoPoint|visible=True`, and `Performance|fps=...|drawMs=...`.

## Deferred Decisions

- Exact icons and command styling.
- Final tab names for Evidence Workbench.
- Whether the Linked View Strip is bottom-docked or right-docked on smaller screens.

Do not resolve these until the layout skeleton exists and current smoke evidence shows the workspace is usable.
