# OpenVisionLab 3D Workbench Layout Design

Updated: 2026-07-14

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

## Nominal / Actual Comparison Layout Contract

The first nominal/actual product slice must use the existing workbench zones. It must not add another floating tool window or move inspection facts out of the separately hostable Viewer.

Commercial workflow evidence behind this placement:

- ZEISS INSPECT presents nominal/actual matching, deviation color scale, editable parametric steps, and reporting as one traceable inspection workflow: https://www.zeiss.com/metrology/us/software/zeiss-inspect.html and https://www.zeiss.com/metrology/us/software/zeiss-inspect/features/parametrics.html
- PolyWorks|Inspector keeps alignment explicit and exposes surface, boundary, section, and thickness measured-to-nominal deviation through color maps: https://www.innovmetric.com/products/polyworks-inspector
- OpenVisionLab should emulate the explicit input/alignment/result relationship, not their full CAD, GD&T, automation, or enterprise scope.

### First Slice Scope

The first slice is deliberately fixed to the local ignored NIST Overhang X4 evidence. It proves the product workflow around the already passed algorithm; it does not introduce a general CAD comparison engine.

| Contract item | First-slice value |
| --- | --- |
| Actual source identity | Original Part 1 measured binary STL, SHA-256 `2108E1B17B2CCE59138C74E5DF4951D407F52A3649C257C3FE942DE874FACA00`, 8,560,096 triangles. |
| Nominal source identity | Original `OverhangPart_9x5x5mm.STL`, SHA-256 `D9FC086CA8C0BC3722709E5C03A39C5C1CF60553845FF62F5699780E1D3C1734`, 2,904 triangles. |
| Inspection query set | CloudCompare-extracted ordered measured-vertex PLY, 4,223,524 points. It is a traceable validation derivative, not replacement source geometry. |
| Display proxy | A deterministic sample of the query set. Render density may change only this proxy, never the inspection query set or result metrics. |
| Units and frame | Millimetres, NIST 3-2-1 part frame, identity transform in OpenVisionLab. |
| Direction | Actual query points to nominal triangles. |
| Signed mode | Robust signed C2M with the passed fixed-sample contract. |
| Alignment | `Identity / source-provided NIST 3-2-1`; read-only in the first slice. No ICP, best-fit, center matching, or scale adjustment. |
| Source policy | Source, derivative, display proxy, preview result, and published result remain separate identities and layers. |

The ignored NIST files remain local trust inputs. No layout work authorizes committing or redistributing them.

### Active Workbench Placement

```text
+--------------------------------------------------------------------------------+
| Job Bar: Recipe | Actual | Nominal | Preview Comparison | Publish | Progress   |
+----------------------+--------------------------------------+------------------+
| Data & Layers        | 3D Inspection View                   | Tool / Inspector |
| Actual source        | Actual display proxy                 | Surface Deviation|
| Actual query set     |   signed deviation colors            | Direction A -> N |
| Nominal source       | Nominal neutral reference toggle     | Alignment: source|
| Preview result       | Fixed-zero tolerance legend          | Lower / upper tol|
| Published result     | Picked deviation + nearest triangle  | Sampling contract|
| Unit/frame/hash      | Source pair + frame + progress HUD    | Preview / Cancel |
+----------------------+--------------------------------------+------------------+
| Evidence: metrics | source hashes | frame/alignment | Runner match | artifacts |
+--------------------------------------------------------------------------------+
| Linked View: deviation distribution and selected-point/profile details          |
+--------------------------------------------------------------------------------+
```

### Panel Responsibilities For Comparison

| Area | Required nominal/actual content |
| --- | --- |
| App / Job Bar | Actual and nominal readiness, recipe dirty state, explicit `Preview Comparison`, explicit `Publish Result`, running progress, and cancel. Loading or editing never runs comparison automatically. |
| Data & Layers | Separate Actual Source, Actual Query Set, Nominal Source, Preview Result, and Published Result rows. Each row exposes identity, visibility, units, frame, and provenance status. Visibility toggles affect rendering only. |
| 3D Inspection View | Actual proxy colored by signed deviation, independently toggled neutral nominal reference, lower/zero/upper legend in millimetres, selected deviation, nearest nominal triangle, workflow state, processed/total count, and essential result metrics in the Viewer-internal HUD. |
| Tool / Inspector | Active `Surface Deviation` tool, actual-to-nominal direction, read-only first-slice alignment, lower/upper tolerances, robust signed mode, inspection query count, Preview/Cancel/Publish command state, and result summary. |
| Evidence Workbench | Source and derivative hashes, frame/alignment identity, algorithm/tolerance fingerprint, Viewer/Runner metric match, run record, screenshot, and report paths. |
| Linked View Strip | Signed-deviation distribution, threshold counts, and selected-point details. It is supporting evidence, not the only place where result state appears. |

### Viewer Visual Contract

- Before Preview, actual and nominal inputs use neutral source colors and are clearly labelled.
- After Preview, the actual proxy owns the signed color map. Nominal is independently visible but defaults off so coincident geometry cannot hide deviation colors.
- The legend is centered at zero and always displays lower tolerance, zero, upper tolerance, and `mm`.
- Negative out-of-tolerance, in-tolerance, positive out-of-tolerance, and unavailable/no-correspondence states use distinct colors. Status must never depend on color alone; numeric range and Pass/Fail text stay visible.
- Picking a colored actual point shows its ordered query index, stable actual/query source IDs, signed deviation, unsigned distance, nearest nominal triangle ID, and status. Do not invent an actual-source vertex index when the source/query relationship does not provide one.
- Standalone Viewer and Shell-hosted Viewer show the same essential source pair, direction, unit, frame, progress, and result facts. Shell panes may add detail but cannot be required to understand the active comparison.
- Nominal visibility, actual visibility, color mode, camera, selection, and render-density changes do not run Preview and do not change calculated metrics.

### Workflow State Contract

| State | Entry condition | Enabled commands | Visible evidence |
| --- | --- | --- | --- |
| `NoInputs` | Actual or nominal contract is missing. | Load/Open Recipe only. | Missing input and validation cause. |
| `InputsReady` | Distinct sources, units, frame, query provenance, and hashes are valid. | Preview. | Both input summaries and `NotRun`. |
| `PreviewStale` | A result-affecting input, tolerance, direction, frame, or sampling contract changed. | Preview. | Previous published result may remain visible as an older revision; preview is clearly stale. |
| `PreviewRunning` | Explicit Preview started. | Cancel only for comparison execution. | Responsive UI, processed/total count, elapsed time, and source pair. |
| `PreviewReady` | Full declared query set completed successfully. | Preview again, Publish. | Metrics, color map, legend, extrema, threshold counts, and preview fingerprint. |
| `Published` | Explicit Publish accepted the current Preview fingerprint. | Preview, save recipe, run/compare. | Separate immutable result entity/layer and durable evidence identity. |
| `Failed` | Input validation, cancellation, decode, or calculation failed. | Correct inputs or Preview again. | Controlled cause; no partial result is presented as complete. |

State rules:

1. Actual and nominal files must be distinct and their hashes must match the recipe/evidence contract.
2. Parameter edits invalidate Preview but do not mutate the source or silently update Published evidence.
3. Preview completion is atomic. Cancellation or failure cannot leave a partially colored result labelled ready.
4. Publish is enabled only when the current input/parameter fingerprint matches the completed Preview.
5. The full query set drives metrics. Display sampling remains independent and is reported separately.
6. The current full NIST calculation takes about 162 seconds locally, so execution must be asynchronous, cancellable, and visibly progressive before it becomes an interactive product command.

### View -> ViewModel -> Model Implementation Order

The next implementation must follow this order and keep each checkpoint buildable:

1. **View binding surface**
   - Add the comparison rows, command controls, progress state, result summary, Viewer HUD lines, legend slot, and Linked View distribution slot to `OpenVisionThreeDViewerControl.xaml` and `Shell/MainWindow.xaml`.
   - Bind through a single `NominalActual` child surface instead of adding another large flat property group to the existing Viewer ViewModel.
   - Use only View-owned converters for visibility or presentation. No durable workflow state belongs in XAML code-behind.
2. **ViewModel workflow**
   - Add `NominalActualComparisonViewModel` under the Viewer project and expose it from `MainWindowViewModel`.
   - Own state, validation summaries, command enablement, progress, cancellation, tolerances, visibility, result summaries, and preview/publish fingerprints there.
   - Shell binds through `ViewerContent.ViewModel.NominalActual`; it does not duplicate comparison state.
3. **Model and execution contracts**
   - Add only the input, recipe, result, and execution types required by the proven View/ViewModel surface.
   - Preserve stable IDs for actual source, nominal source, query derivative, alignment reference, comparison step, preview layer, and published result.
   - Runner and Viewer use the same non-WPF algorithm/service contract. Render buffers and display proxies never become measurement inputs.
4. **Bridge limits**
   - Viewer code-behind may bridge file dialogs, OpenGL buffer upload, and pointer/render events.
   - Long-running execution, cancellation state, result identity, command behavior, recipe persistence, and evidence formatting do not belong in code-behind.
   - Viewer Host API v1.0 remains binary compatible; do not expose the concrete child ViewModel as an external host API.

### View Binding Surface Checkpoint

The first View-only checkpoint passed on 2026-07-14. `OpenVisionThreeDViewerControl.xaml` and Shell `MainWindow.xaml` now reserve bindings for the source pair, unit/frame summary, visibility, Surface Deviation state, tolerances, progress, explicit Preview/Cancel/Publish commands, Viewer HUD, signed legend, Evidence summary, and Linked View distribution. Missing ViewModel state renders as a disabled `No inputs` surface; hidden result-only slots remain collapsed.

This checkpoint itself added no comparison calculation, durable state, command behavior, recipe/result model, or code-behind workflow logic. The subsequent ViewModel checkpoint below now owns presentation workflow state; shared models and execution remain next.

Current-source evidence is under `artifacts/nominal_actual_view_20260714`:

- before: `viewer_before.png`, `shell_before.png`, and `shell_viewer_before.png`;
- after: `viewer_after.png`, `shell_after.png`, and `shell_viewer_after.png`;
- contracts: `viewer_before.txt`, `shell_before.txt`, `viewer_after.txt`, and `shell_after.txt`.

All three before and all three after Viewer/embedded-Viewer/Shell pixel-quality checks accepted their first capture. Visual comparison confirmed that the neutral NIST nominal mesh, camera area, existing HUD, docking panes, and scroll access remain available without overlap. The post-change report at `artifacts/nominal_actual_view_20260714/regression/matrix_smoke_summary_after.txt` records `128` passes and no failures, and `mesh_deviation_golden_after.txt` records `17/17` passes.

### ViewModel Workflow Checkpoint

The second checkpoint passed on 2026-07-14. `NominalActualComparisonViewModel` is exposed as the single `MainWindowViewModel.NominalActual` child surface and owns `NoInputs`, `InputsReady`, `PreviewStale`, `PreviewRunning`, `PreviewReady`, `Published`, and `Failed` state. It also owns source/frame/alignment summaries, actual/nominal visibility, zero-centred tolerance validation, command enablement, progress/cancellation, request IDs, and input/preview/published fingerprints. Shell continues to bind to the same Viewer-owned child state.

`PreviewRequested` and `PublishRequested` are narrow workflow events. The ViewModel rejects invalid or same-file inputs, prevents a missing executor from leaving Preview permanently running, ignores stale/cancelled completions, and makes Publish available only for the matching completed fingerprint. Code-behind only invokes the deterministic smoke verifier and writes contract evidence; it does not own command behavior.

Current-source evidence is under `artifacts/nominal_actual_viewmodel_20260714`:

- deterministic state/command report: `viewmodel_verification_after.txt` (`50/50` pass);
- before/after Viewer captures and contracts: `viewer_before.png`, `viewer_after.png`, `viewer_before.txt`, and `viewer_after.txt`;
- before/after Shell captures and contracts: `shell_before.png`, `shell_after.png`, `shell_viewer_before.png`, `shell_viewer_after.png`, `shell_before.txt`, and `shell_after.txt`;
- regression reports: `regression/matrix_smoke_summary_after.txt` (`128/128`) and `regression/mesh_deviation_golden_after.txt` (`17/17`).

The after views show ViewModel-owned `-0.300` / `0.300` tolerances, controlled `No inputs` validation, disabled commands, and Shell evidence text without clipping. That checkpoint intentionally preceded the shared Model and execution work below.

### Model And Preview Execution Checkpoint

The third checkpoint passed locally on 2026-07-14. Core now owns immutable actual/nominal/query identities, source and execution fingerprints, tolerances, statistics, display samples, and typed results. Data owns ordered binary-PLY vertex parsing. Tools owns the render-independent full-query executor and depends on Core/Data, not WPF or SharpGL. The Viewer bridge maps the validated fixed NIST input to the executor, forwards progress/cancellation, stores no numerical policy, and renders only the typed result supplied through `NominalActualComparisonViewModel`.

The executor verifier passes `12/12`, and the ViewModel verifier passes `56` checks. A real standalone Viewer and the docked Shell each process all `4,223,524` NIST query points, classify `548,207` below, `2,990,143` within, and `685,174` above `[-0.3, 0.3] mm`, and render `59,487` signed-color display samples at stride `71`. The maximum precise contract-statistic delta from the independently parsed CloudCompare PLY outputs is `1.3381639552001445e-7 mm`, below `1e-6 mm`.

Current-source evidence is under `artifacts/nominal_actual_execution_20260714`. `viewer_before.png` and `shell_before.png` are the pre-execution baseline; `viewer_after_final.png` and `shell_after_final.png` are the accepted final Preview captures. The final View keeps the signed legend inside the Viewer HUD so it does not overlap in the narrow Shell host. ViewModel synchronization also keeps the `Nominal` toggle and imported-mesh source-layer visibility consistent. The isolated post-change matrix passes `128/128`, and the mesh-deviation golden remains `17/17`.

### Publish, Recipe, And Runner Checkpoint

The fourth checkpoint passed locally on 2026-07-14. Explicit Publish creates `result.nominal-actual-surface-deviation` on a separate result layer without mutating actual, nominal, or query source entities. The typed `nominal-actual-surface-deviation` recipe preserves stable actual/nominal/query IDs, original byte lengths and SHA-256 values, `mm`, the NIST part frame, source-provided identity alignment, `full-query` evaluation, direction, and tolerances. Reopen reproduces the stable Viewer contract with zero differences.

Headless Runner dispatches the typed recipe through the same non-WPF executor and independently compares persisted Viewer evidence. For all `4,223,524` points it reproduces the expected `Fail` result, `548,207` below, `2,990,143` within, `685,174` above, and 13 metrics plus one signed color-map overlay. `ViewerRunnerComparison|Matched` passes, and schema `1.2` JSON plus HTML/CSV carry actual source, nominal reference, query measurement, step, metric, overlay, and execution identity. Shell parses nominal/actual Viewer and Runner status plus signed mean and out-of-tolerance count before showing `recipe comparison matched`.

Current-source UI and workflow evidence is under `artifacts/nominal_actual_publish_20260714` and `artifacts/nominal_actual_render_density_20260714`. The latter preserves fresh before and accepted after Fast/Balanced/Detailed Viewer captures. Executor/recipe/result verification passes `26/26`, ViewModel verification passes `60` checks, the fixed matrix passes `128/128`, and existing typed algorithm/map goldens and BinaryHost remain green. Fast/Balanced/Detailed render `24,992` / `59,487` / `145,639` signed samples while all normalized measurement and published evidence remains byte-identical. See `docs/OPENVISIONLAB_3D_NIST_NOMINAL_ACTUAL_END_TO_END_20260714.md`.

### First-Slice Acceptance Checklist

- [x] Fresh before screenshots exist for standalone Viewer and full Shell before the first visible edit.
- [x] Actual, nominal, query derivative, preview, and published result are separate visible identities.
- [x] Source names, hashes, `mm`, NIST frame, identity alignment, and actual-to-nominal direction are visible in UI and contract evidence.
- [x] The executor/recipe/result matrix covers display-budget invariance plus missing recipe/direct sources, empty unit/frame declarations, corrupt/truncated query PLY, same-file inputs, hash/byte-length mismatch, invalid direction, and invalid tolerance (`26/26`).
- [ ] A non-empty but semantically wrong unit/frame declaration cannot be compared with source truth until the source contract carries independently derived unit/frame metadata.
- [x] Camera, visibility, color, and render-density changes never trigger Preview.
- [x] Result-affecting edits produce `PreviewStale`; Preview and Publish remain explicit separate actions.
- [x] Preview runs asynchronously, reports progress, supports cancellation, and publishes no partial result on failure/cancel.
- [x] Full-query metrics and published evidence remain identical across Fast/Balanced/Detailed Viewer modes while signed display samples change independently (`24,992` / `59,487` / `145,639`).
- [x] Viewer and Runner reproduce the passed fixed-sample unsigned/signed metrics and status within declared tolerances.
- [x] Actual signed colors, zero-centered legend, tolerance limits, result state, and selected-point provenance are readable in standalone Viewer and Shell. The fixed Balanced NIST smoke records query index, source IDs, signed/unsigned deviation, nearest nominal triangle, closest nominal point, sign path, and tolerance status.
- [x] Publish creates a separate result layer/entity and does not modify either source.
- [x] Recipe save/reopen preserves source/query hashes, units, frame, direction, alignment, tolerances, sampling contract, and stable IDs.
- [x] Current-source Viewer and Shell screenshot-quality gates accept the after captures.
- [x] Existing `128/128` data-loading/Viewer/Shell matrix and mesh-deviation golden remain green.

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
| Measured/nominal signed surface comparison | Data & Layers + 3D Inspection View + Tool / Inspector + Evidence Workbench + Linked View | Fixed NIST identity-frame baseline done |
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
25. Add the third typed inspection slice: C3D Gap / Flush. Done for explicit two-region baseline.
    - Standalone Viewer and Shell `Tool / Inspector` own expected signed gap, gap tolerance, expected signed flush, and flush tolerance.
    - The existing recipe-owned left/right ROI fields define the two explicit regions. Gap is the signed aligned-X distance from the left ROI's right edge to the right ROI's left edge; flush is right mean raw height minus left mean raw height.
    - `Preview Gap / Flush` evaluates a fixed recipe-owned sample budget independent from render density. `Publish Result` creates a separate result entity/layer without changing source C3D values.
    - The Viewer HUD and viewport retain both ROI regions, signed results, sample counts, units, and acceptance status when hosted without Shell panes.
    - Recipe JSON owns stable step/source/reference IDs, both regions, transform, units, expected values, tolerances, and sample budget. Runner must reproduce the same status and key metrics.
    - This slice intentionally excludes automatic seam/edge detection, arbitrary-direction gap, CAD nominal comparison, and calibrated physical-unit claims.

## C3D Gap / Flush Evidence

- Viewer Preview/Publish screenshot and contract: `artifacts/viewer_gap_flush_after.png`, `artifacts/viewer_gap_flush_after.txt`
- Saved-recipe reopen screenshot and contract: `artifacts/viewer_gap_flush_reopen_after.png`, `artifacts/viewer_gap_flush_reopen_after.txt`
- Runner parity report: `artifacts/runner_gap_flush_after.txt`
- Analytic/error golden report: `artifacts/gap_flush_golden_after.txt`
- Shell Viewer/contract/Steps workbench: `artifacts/shell_gap_flush_viewer_after.png`, `artifacts/shell_gap_flush_after.txt`, `artifacts/shell_gap_flush_after.png`

26. Add the fourth typed inspection slice: C3D Volume above/below a reference plane. Done for the explicit height-field ROI baseline.
    - The existing Flatness reference ROI defines the fitted height-field plane; the recipe-owned left ROI defines the measurement region.
    - Standalone Viewer and Shell `Tool / Inspector` own expected signed net volume and tolerance with explicit Preview and Publish.
    - Results include above-plane, below-plane magnitude, and signed net volume in `model^3`; physical volume remains unavailable without calibrated pitch and height units.
    - Recipe JSON owns stable step/source/reference IDs, both regions, expected value, tolerance, unit, and fixed sample budget. Runner must reproduce Viewer status and all three volume metrics.
    - This slice excludes watertight mesh volume, arbitrary closed solids, CAD nominal volume, and calibrated physical-volume claims.

## C3D Volume Evidence

- Before Viewer/Shell screenshots: `artifacts/viewer_volume_before.png`, `artifacts/shell_volume_before.png`
- Viewer Preview/Publish screenshot and contract: `artifacts/viewer_volume_after.png`, `artifacts/viewer_volume_after.txt`
- Saved-recipe reopen screenshot and contract: `artifacts/viewer_volume_reopen_after.png`, `artifacts/viewer_volume_reopen_after.txt`
- Runner parity report: `artifacts/runner_volume_after.txt`
- Analytic/error golden report: `artifacts/volume_golden_after.txt`
- Shell Viewer/contract/Steps workbench: `artifacts/shell_volume_viewer_after.png`, `artifacts/shell_volume_after.txt`, `artifacts/shell_volume_steps_after.png`

27. Add the fifth typed inspection slice: C3D Cross-section Dimensions. Done for the exact source-row/range baseline.
    - Recipe JSON owns one exact source row and an inclusive start/end column range; Runner reads those source cells independently from render density.
    - Standalone Viewer and Shell `Tool / Inspector` own row/range selectors, expected aligned-X width, expected raw-height range, and a separate tolerance for each metric.
    - `Preview Cross-section` reports valid sample count, aligned-X width, raw minimum/maximum, and raw-height range; `Publish Result` creates a separate result entity without changing source cells.
    - The Viewer HUD, section-plane overlay, and linked profile retain the selected row/range and both acceptance results when hosted without Shell panes.
    - This baseline excludes automatic edge/feature finding, threshold crossings, arbitrary section planes, fitted profile primitives, calibrated physical dimensions, and CAD nominal sections.

## C3D Cross-section Dimensions Evidence

- Before Viewer/Shell screenshots: `artifacts/viewer_cross_section_before.png`, `artifacts/shell_cross_section_before.png`
- Viewer Preview/Publish screenshot and contract: `artifacts/viewer_cross_section_after.png`, `artifacts/viewer_cross_section_after.txt`
- Saved-recipe reopen screenshot and contract: `artifacts/viewer_cross_section_reopen_after.png`, `artifacts/viewer_cross_section_reopen_after.txt`
- Runner parity report: `artifacts/runner_cross_section_after.txt`
- Analytic/error golden report: `artifacts/cross_section_golden_after.txt`
- Shell Viewer/contract/Steps workbench: `artifacts/shell_cross_section_viewer_after.png`, `artifacts/shell_cross_section_after.txt`, `artifacts/shell_cross_section_steps_after.png`

28. Add the first durable Run Snapshot bundle. Done for JSON plus simple HTML/CSV.
    - Shell `Evidence Workbench -> Run Snapshot` owns explicit open commands for UI contract, Runner TXT, screenshot, run JSON, HTML report, and CSV metrics.
    - Shell ViewModel owns all artifact paths and command state; code-behind remains only the shared OS file-open bridge.
    - Runner JSON owns schema/run identity, UTC time, recipe/source identity and SHA-256, status/message/duration, every metric and overlay, Viewer/Runner match state, and artifact paths.
    - HTML is a human-readable one-run metric table and CSV is a machine-friendly one-row-per-metric export. This baseline excludes PDF, database persistence, batch trends, SPC, signing, and retention policy.

29. Add the first measured/nominal signed surface-comparison slice. Done for the fixed NIST identity-frame baseline.
    - Use the ignored NIST Overhang X4 source pair and passed 4,223,524-point identity-frame parity as the first controlled workflow.
    - Preserve the completed View binding surface, `NominalActualComparisonViewModel`, Core contracts, Data parser, Tools executor/typed recipe, separate Publish result, and Runner parity.
    - Keep actual source, nominal source, validation query derivative, display proxy, preview, and published result as separate traceable identities.
    - Run full-query Preview asynchronously with progress/cancel, preserve explicit Publish, and keep metrics independent from render density.
    - Standalone Viewer, Shell, recipe reopen, Runner parity, screenshot-quality, existing matrix/golden evidence, and schema `1.2` Run Record now pass. A second pair and non-identity alignment remain separate gates.

30. Clarify current versus next-Preview nominal/actual display density. Done for the fixed NIST baseline.
    - The standalone Viewer density control, Viewer-internal HUD, Shell `Data & Layers`, Shell `Tool / Inspector`, and linked deviation summary expose the display density used by the completed result separately from the density selected for the next explicit Preview.
    - `Current display` records the completed density name, display-point budget, actual display sample count, and stride. `Next Preview` records the selected density name and budget and states when another explicit Preview is required.
    - Changing render density must not rerun nominal/actual comparison, replace the completed display samples, change full-query metrics, or mutate a published result. The new density is snapshotted only when the user invokes `Preview Comparison`.
    - Smoke evidence must complete Balanced Preview, change only the next density to Detailed, prove the existing `59,487` samples and stride `71` remain current, and then explicitly rerun Preview to prove Detailed becomes current.
    - Current Viewer/Shell, contract, ViewModel, full density-regression, fixed matrix, and BinaryHost evidence is under `artifacts/nominal_actual_density_state_20260715`.

## Durable Run Bundle Evidence

- Before Shell Run Snapshot: `artifacts/shell_run_record_before.png`
- After Shell Run Snapshot: `artifacts/shell_run_record_after.png`
- JSON record: `artifacts/run_record_cross_section/run.json`
- HTML report: `artifacts/run_record_cross_section/report.html`
- Browser-rendered HTML evidence: `artifacts/run_record_cross_section/report.png`
- CSV metrics: `artifacts/run_record_cross_section/metrics.csv`
- Runner TXT: `artifacts/run_record_cross_section/runner.txt`

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

Keep these deferred until nominal/actual before/after evidence exposes a concrete usability problem; the existing layout skeleton alone is not a reason to redesign docking or command styling.
