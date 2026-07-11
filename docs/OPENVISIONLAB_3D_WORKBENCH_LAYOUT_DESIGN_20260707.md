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
| Run history | Evidence Workbench | Done, C3D/LAZ |
| Viewer-internal coordinate HUD | 3D Inspection View | Done |
| Two-point distance and height delta | 3D Inspection View + Tool / Inspector | Done |
| Typed point-pair distance / XZ width / signed angle acceptance | 3D Inspection View + Tool / Inspector + Evidence Workbench | Done, explicit C3D cells |
| Distance to fitted reference plane | 3D Inspection View + Tool / Inspector | Done, C3D height-field fit |
| Interactive ROI step-height comparison | 3D Inspection View + Tool / Inspector | Done, minimal |
| Transform/alignment state | 3D Inspection View + Data & Layers | Done, minimal |
| Recipe-persisted ROI/alignment replay | Recipe + Evidence Workbench | Done, minimal |
| Recipe-owned ROI/alignment parameter edit | Tool / Inspector + standalone Viewer side panel | Done, minimal |
| Interactive ROI reference alignment | Tool / Inspector + 3D Inspection View | Done, minimal |
| ROI/alignment validation warnings | Tool / Inspector + recipe save path | Done, minimal |
| External mesh/point-cloud import evidence | 3D Inspection View + Viewer contract | Done, minimal |
| Shell active viewer context mirroring | Data & Layers + Tool / Inspector | Done, minimal |
| Active linked view context | Linked View Strip | Done, minimal |
| Long linked-view status details | Linked View Strip | Done, minimal |
| Performance HUD | 3D Inspection View | Done, minimal |
| Screenshot/report snapshots | Evidence Workbench | Done, minimal |
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
   - History tab shows the current replay evidence row with run time, status, key metric, match state, and report path.
   - C3D key metric is peak deviation; LAZ/LAS key metrics are distance and source-Z height delta.
   - Smoke can open the tab with `--shell-evidence-tab history`.
8. Add Viewer-internal coordinate HUD, two-point measurement, and ROI step-height comparison. Done.
    - The Viewer must show axis meaning and selected measurement state even when Shell side panes are hidden.
    - Two-point measurement should report distance, dX/dY/dZ, model height delta, and raw-height delta for C3D points.
    - ROI step-height comparison should report left/right point counts, mean raw heights, raw-height delta, and model Y delta.
    - Minimal performance HUD should report FPS, draw time, and rendered C3D point count.
9. Add visible recipe-owned ROI/alignment parameter editing. Done.
   - Standalone Viewer and Shell `Tool / Inspector` expose numeric transform and ROI region fields.
   - Edited values update the measurement overlay, save to recipe JSON, and replay through Runner.
10. Add minimal interactive ROI reference alignment. Done.
   - `Align From ROI` uses the current left/right ROI pair to translate the aligned coordinate frame so the ROI pair center and reference height become the local reference.
   - The workflow updates transform fields, ROI regions, viewport overlays, saved recipe JSON, and Runner replay evidence.
11. Add minimal ROI/alignment validation warnings. Done.
   - Invalid overlapped ROI regions show a visible warning in Viewer and Shell.
   - Recipe save is blocked when the active ROI step is invalid.
12. Mirror active Viewer context into Shell side panes. Done.
   - `Data & Layers` shows active entity, coordinate frame, scene contract summary, and live entity layers from the hosted Viewer.
   - `Tool / Inspector` shows active entity, selection mode, measurement summary, pick coordinate, and Viewer status from the hosted Viewer.
13. Switch Linked View by active Viewer context. Done.
   - C3D keeps the Height Map and Profile/Section panels.
   - LAZ/LAS shows Point Cloud Sample plus linked measurement/pick state.
   - GLB shows Mesh Sample plus linked measurement/pick state.
14. Publish LAZ/LAS two-point measurement as result evidence. Done.
   - LAZ/LAS two-point measurement creates a preview layer and publishable result layer.
   - Contract evidence records distance, dX/dY/dZ, source-Z height delta, and point-cloud overlay sources without mutating the source point cloud.
15. Surface basic LAZ/LAS acceptance status in Viewer/Shell inspector. Done.
   - Viewer and Shell Tool / Inspector show the distance/source-Z height acceptance summary for the measured LAZ/LAS sampled points.
   - Contract evidence records `LAZAcceptance|summary=LAZ/LAS acceptance: Pass ...`.
16. Edit and save LAZ/LAS two-point acceptance parameters. Done.
   - Viewer and Shell Tool / Inspector expose expected distance, distance tolerance, expected height delta, and height tolerance fields.
   - Saved point-cloud recipe JSON replays through Runner, and contract evidence records `LAZAcceptanceParameters`.
17. Reopen saved LAZ/LAS two-point recipes. Done.
   - Viewer and Shell `--smoke-recipe` restore the saved LAZ/LAS source, two-point measurement preview, and editable acceptance values.
   - Runner comparison against the reopened Viewer contract passes with the saved acceptance recipe.
18. Surface LAZ saved-recipe comparison in Evidence Workbench History. Done.
    - Shell comparison reads generic `PreviewToolResult` and Runner `ToolResult` evidence instead of C3D-only peak-deviation lines.
    - History shows LAZ `Pass`, distance/source-Z height key metric, matched state, and runner report path.
19. Keep Linked View usable for long loader failure details. Done.
    - Linked View Strip has vertical scrolling so long GLB/STL/LAS/LAZ source paths and loader errors stay reachable inside the docked panel.
    - Tool / Inspector and the Viewer HUD remain the primary failure summary surfaces.
20. Add minimal Evidence Workbench artifact actions. Done.
    - `Run Snapshot` exposes open actions for the current UI contract, Runner report, and Shell screenshot artifact.
    - The Shell code-behind remains the OS-launch bridge; the ViewModel owns command state and target path selection.
    - Smoke can open the tab with `--shell-evidence-tab snapshot` and prove the actions are visible without invoking OS file launch.
21. Add minimal C3D distance-to-plane measurement. Done.
    - `--smoke-measure plane-distance` uses the current C3D mean model-Y as a reference plane and measures the largest absolute point-to-plane distance.
    - The Viewer HUD shows plane summary/details, the viewport draws the reference plane and target distance line, Tool / Inspector mirrors the same measurement summary, and contract text records `PlaneReference`.
    - This is not plane fitting yet; fitted plane and multi-point reference definition remain later work.
22. Upgrade C3D distance-to-plane to a fitted height-field plane. Done.
    - Standalone Viewer and Shell Tool / Inspector expose an explicit `Fit C3D Plane` command.
    - The command fits `y = ax + bz + c` from transformed C3D samples, reports RMS residual and fitted normal, and measures the largest orthogonal point-to-plane distance.
    - A fixed `140,000`-point measurement budget keeps fitted metrics independent from Fast/Balanced/Detailed render-density sampling.
    - Viewer code-behind remains the OpenGL/data bridge; the reusable least-squares calculation belongs to `OpenVisionLab.ThreeD.Tools` and ViewModel owns command/display state.
    - Three picked points, ROI-only fitting, flatness tolerance, recipe persistence, and Runner replay remain later work.
23. Add the first Inspection Recipe v1 vertical slice: reference ROI plane plus flatness. Done for numeric ROI baseline.
    - Standalone Viewer and Shell `Tool / Inspector` own the editable reference ROI center/size and flatness tolerance fields.
    - An explicit `Preview Flatness` command fits the reference plane only from the configured ROI, then evaluates signed orthogonal deviation over a fixed measurement sample set that is independent from render density.
    - The 3D Inspection View owns the reference ROI, fitted plane, signed-deviation color evidence, extrema markers, and an internal flatness summary so hosting the Viewer alone does not hide essential inspection facts.
    - `Publish Result` creates a separate result entity/layer; it does not mutate the C3D source geometry.
    - Recipe JSON owns a stable step ID, explicit source/reference IDs, ROI, tolerance, unit, and sample budget. Viewer reopen and headless Runner replay must produce matching status and key metric evidence.
    - This slice intentionally excludes three-point reference definition, CAD/GD&T, automatic datum construction, and a generic recipe graph editor.
24. Add the second typed inspection slice: C3D point-pair width, distance, and angle. Done for explicit source-cell baseline.
    - Standalone Viewer and Shell `Tool / Inspector` own expected 3D distance, XZ planar width, signed elevation angle, and a separate tolerance for each metric.
    - The existing two-point pick interaction defines explicit C3D grid-cell references; `Preview Dimensions` evaluates only after both references exist.
    - The Viewer HUD keeps the selected point IDs, distance, width, angle, and acceptance status visible when hosted without Shell panes.
    - The 3D Inspection View owns the endpoint markers and connecting measurement line. `Publish Result` creates a separate result entity/layer without changing source C3D values.
    - Recipe JSON owns a stable step ID, source entity ID, point-reference IDs, source row/column selectors, transform, units, expected values, and tolerances. Runner must resolve the same source cells independently of render density.
    - This slice intentionally excludes automatic edge/feature extraction, feature fitting, CAD dimensions, and GD&T.

## C3D Point Pair Dimensions Evidence

- Before Viewer screenshot/contract: `artifacts/viewer_dimensions_before.png`, `artifacts/viewer_dimensions_before.txt`
- Before Shell screenshot: `artifacts/shell_dimensions_before.png`
- After Viewer screenshot/contract: `artifacts/viewer_dimensions_after.png`, `artifacts/viewer_dimensions_after.txt`
- Saved-recipe reopen screenshot/contract: `artifacts/viewer_dimensions_reopen_after.png`, `artifacts/viewer_dimensions_reopen_after.txt`
- Runner parity report: `artifacts/runner_point_pair_dimensions_after.txt`
- Analytic/error golden report: `artifacts/point_pair_dimensions_golden_after.txt`
- After Shell Viewer/contract/Steps workbench: `artifacts/shell_dimensions_viewer_after.png`, `artifacts/shell_dimensions_after.txt`, `artifacts/shell_dimensions_after.png`

## Reference ROI Plane Flatness Evidence

- Before Viewer screenshot: `artifacts/viewer_flatness_before.png`
- Before Shell screenshot: `artifacts/shell_flatness_before.png`
- After Viewer screenshot/contract: `artifacts/viewer_flatness_after.png`, `artifacts/viewer_flatness_after.txt`
- Saved-recipe reopen screenshot/contract: `artifacts/viewer_flatness_reopen_after.png`, `artifacts/viewer_flatness_reopen_after.txt`
- Runner parity report: `artifacts/runner_flatness_after.txt`
- After Shell Viewer/contract/workbench: `artifacts/shell_flatness_viewer_after.png`, `artifacts/shell_flatness_after.txt`, `artifacts/shell_flatness_after.png`

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
- LAZ/LAS run-history screenshot: `artifacts/shell_laz_run_history_after.png`
- LAZ/LAS run-history runner report: `artifacts/runner_laz_run_history_after.txt`
- C3D run-history regression screenshot: `artifacts/shell_c3d_run_history_regression_after.png`
- C3D run-history regression report: `artifacts/runner_c3d_run_history_regression_after.txt`

## Evidence Artifact Action Evidence

- Before screenshot: `artifacts/shell_evidence_actions_before.png`
- After screenshot: `artifacts/shell_evidence_actions_after.png`
- Viewer contract: `artifacts/shell_run_snapshot_contract_after.txt`
- Runner report: `artifacts/runner_shell_run_snapshot_after.txt`

## Plane Reference Measurement Evidence

- Viewer screenshot: `artifacts/viewer_plane_distance_after.png`
- Viewer contract: `artifacts/viewer_plane_distance_after.txt`
- Shell embedded Viewer screenshot: `artifacts/shell_plane_distance_viewer_after.png`
- Shell contract: `artifacts/shell_plane_distance_after.txt`
- Shell full workbench screenshot: `artifacts/shell_plane_distance_after.png`
- Fitted-plane closest-before screenshots: `artifacts/viewer_plane_fit_before.png`, `artifacts/shell_plane_fit_before.png`
- Fitted-plane after screenshots: `artifacts/viewer_plane_fit_after.png`, `artifacts/shell_plane_fit_viewer_after.png`, `artifacts/shell_plane_fit_after.png`
- Fitted-plane contracts: `artifacts/viewer_plane_fit_after.txt`, `artifacts/viewer_plane_fit_tilt_after.txt`, `artifacts/shell_plane_fit_after.txt`
- Render-density stability contracts: `artifacts/viewer_plane_fit_fast_after.txt`, `artifacts/viewer_plane_fit_detailed_after.txt`

## Viewer Internal HUD Evidence

- Before screenshot: `artifacts/viewer_two_point_before.png`
- Viewer after screenshot: `artifacts/viewer_two_point_after.png`
- Viewer contract: `artifacts/viewer_two_point_after.txt`
- Shell-hosted after screenshot: `artifacts/shell_viewer_internal_hud_after.png`
- Contract evidence: `CoordinateFrame|visible=True`, `TwoPoint|visible=True`, and `Performance|fps=...|drawMs=...`.
- ROI step viewer screenshot: `artifacts/viewer_roi_step_after.png`
- ROI step viewer contract: `artifacts/viewer_roi_step_after.txt`
- ROI step shell-hosted screenshot: `artifacts/shell_roi_step_after.png`
- ROI contract evidence: `RoiStep|visible=True`, left/right point counts, left/right mean raw heights, raw-height delta, and model Y delta.
- Interactive ROI before screenshot: `artifacts/viewer_roi_interactive_before.png`
- Interactive ROI after screenshot: `artifacts/viewer_roi_interactive_after.png`
- Interactive ROI contract: `artifacts/viewer_roi_interactive_after.txt`
- Interactive ROI shell-hosted screenshot: `artifacts/shell_roi_interactive_after.png`
- Interactive ROI contract evidence: `RoiStep|mode=Interactive`, edit prompt, and source-to-aligned transform mapping.
- Transform baseline screenshot: `artifacts/viewer_transform_before.png`
- Transform after screenshot: `artifacts/viewer_alignment_after.png`
- Transform contract: `artifacts/viewer_alignment_after.txt`
- Transform shell-hosted screenshot: `artifacts/shell_alignment_after.png`
- Transform contract evidence: `TransformAlignment`, `C3DTransform`, transform translation/rotation/scale, alignment summary, and source-to-aligned mapping.
- ROI/alignment recipe save screenshot: `artifacts/viewer_roi_recipe_save_after.png`
- ROI/alignment recipe roundtrip screenshot: `artifacts/viewer_roi_recipe_roundtrip_after.png`
- ROI/alignment shell roundtrip screenshot: `artifacts/shell_roi_recipe_roundtrip_after.png`
- ROI/alignment runner report: `artifacts/runner_roi_alignment_recipe_after.txt`
- ROI/alignment recipe evidence: `RecipeTransform`, `RecipeRoiStep`, and runner `RoiStepResult` preserve the same transform and ROI step metrics after reload.

## Shell Active Context Evidence

- Closest before screenshot: `artifacts/shell_laz_two_point_after.png`
- After full workbench screenshot: `artifacts/shell_laz_context_after.png`
- Embedded Viewer screenshot: `artifacts/shell_laz_context_viewer_after.png`
- Viewer contract: `artifacts/shell_laz_context_after.txt`
- Evidence: Shell `Data & Layers` and `Tool / Inspector` show `LAZ/LAS Two Point Measurement` while the Viewer contract records `TwoPoint|visible=True` and `LAZPick|selected=True`.

## Linked View Context Evidence

- LAZ full workbench screenshot: `artifacts/shell_laz_linked_after.png`
- LAZ embedded Viewer screenshot: `artifacts/shell_laz_linked_viewer_after.png`
- LAZ Viewer contract: `artifacts/shell_laz_linked_after.txt`
- GLB full workbench screenshot: `artifacts/shell_glb_linked_after.png`
- GLB embedded Viewer screenshot: `artifacts/shell_glb_linked_viewer_after.png`
- GLB Viewer contract: `artifacts/shell_glb_linked_after.txt`
- Evidence: LAZ Linked View shows `Point Cloud Sample` and linked measurement state; GLB Linked View shows `Mesh Sample` and pick state instead of C3D Height Map/Profile.
- Long failure before screenshot: `artifacts/shell_linked_failure_clip_before.png`
- Long failure after screenshot: `artifacts/shell_linked_failure_clip_after.png`
- Normal LAZ regression screenshot: `artifacts/shell_linked_valid_laz_after.png`
- Evidence: long corrupt LAZ loader details remain available through the Linked View scroll region, while normal LAZ linked context keeps the same three-column layout.

## LAZ Result Publish Evidence

- Viewer publish screenshot: `artifacts/laz_two_point_publish_after.png`
- Viewer publish contract: `artifacts/laz_two_point_publish_after.txt`
- Shell embedded Viewer publish screenshot: `artifacts/shell_laz_two_point_publish_viewer_after.png`
- Shell publish contract: `artifacts/shell_laz_two_point_publish_after.txt`
- Shell full workbench publish screenshot: `artifacts/shell_laz_two_point_publish_after.png`
- Runner replay pass report: `artifacts/runner_laz_two_point_after.txt`
- Runner replay fail report: `artifacts/runner_laz_two_point_fail_after.txt`
- Contract evidence: `layer.preview.laz-two-point-measurement`, `layer.result.laz-two-point-measurement`, 5 published metrics, and 2 published overlays tied to `source.public-laz-manuscript`.
- Acceptance inspector evidence: `artifacts/laz_acceptance_inspector_viewer_after.txt`, `artifacts/shell_laz_acceptance_inspector_after.txt`, and `artifacts/shell_laz_acceptance_inspector_after.png`.
- Acceptance edit/save evidence: `artifacts/laz_acceptance_edit_save_viewer_after.txt`, `artifacts/saved_laz_two_point_acceptance.recipe.json`, `artifacts/runner_laz_acceptance_edit_save_after.txt`, and `artifacts/shell_laz_acceptance_edit_after.png`.
- Acceptance recipe reopen evidence: `artifacts/laz_acceptance_recipe_reopen_viewer_after.txt`, `artifacts/runner_laz_acceptance_recipe_reopen_after.txt`, and `artifacts/shell_laz_acceptance_recipe_reopen_after.png`.
- Evidence Workbench history evidence: `artifacts/runner_laz_run_history_after.txt` and `artifacts/shell_laz_run_history_after.png`.

## Recipe Parameter Edit Evidence

- Before Viewer screenshot: `artifacts/viewer_recipe_parameter_edit_before.png`
- After Viewer screenshot: `artifacts/viewer_recipe_parameter_edit_after.png`
- Before Shell screenshot: `artifacts/shell_recipe_parameter_edit_before.png`
- After Shell screenshot: `artifacts/shell_recipe_parameter_edit_after.png`
- Edited recipe: `artifacts/saved_roi_alignment_edited.recipe.json`
- Viewer contract: `artifacts/viewer_recipe_parameter_edit_after.txt`
- Runner report: `artifacts/runner_recipe_parameter_edit_after.txt`
- Contract evidence: edited `RecipeTransform`, `RecipeRoiStep`, and runner `RoiStepResult` match the saved edited recipe.

## Interactive Alignment Evidence

- Before Viewer screenshot: `artifacts/viewer_interactive_alignment_before.png`
- After Viewer screenshot: `artifacts/viewer_interactive_alignment_after.png`
- Before Shell screenshot: `artifacts/shell_interactive_alignment_before.png`
- After Shell screenshot: `artifacts/shell_interactive_alignment_after.png`
- Saved aligned recipe: `artifacts/saved_roi_alignment_auto.recipe.json`
- Viewer contract: `artifacts/viewer_interactive_alignment_after.txt`
- Runner report: `artifacts/runner_interactive_alignment_after.txt`
- Contract evidence: `AlignmentWorkflow`, `RecipeTransform`, `RecipeRoiStep`, and runner `RoiStepResult` match after `Align From ROI`.

## ROI Validation Evidence

- Before Viewer screenshot: `artifacts/viewer_roi_validation_before.png`
- Before Shell screenshot: `artifacts/shell_roi_validation_before.png`
- Valid Viewer screenshot: `artifacts/viewer_roi_validation_valid_after.png`
- Valid Viewer contract: `artifacts/viewer_roi_validation_valid_after.txt`
- Valid saved recipe: `artifacts/saved_roi_validation_valid.recipe.json`
- Valid Runner report: `artifacts/runner_roi_validation_valid_after.txt`
- Invalid Viewer screenshot: `artifacts/viewer_roi_validation_invalid_after.png`
- Invalid Viewer contract: `artifacts/viewer_roi_validation_invalid_after.txt`
- Invalid Shell screenshot: `artifacts/shell_roi_validation_invalid_after.png`
- Contract evidence: `RecipeValidation` reports overlap, and the invalid save path does not create `artifacts/saved_roi_validation_invalid.recipe.json`.

## Deferred Decisions

- Exact icons and command styling.
- Final tab names for Evidence Workbench.
- Whether the Linked View Strip is bottom-docked or right-docked on smaller screens.

Do not resolve these until the layout skeleton exists and current smoke evidence shows the workspace is usable.
