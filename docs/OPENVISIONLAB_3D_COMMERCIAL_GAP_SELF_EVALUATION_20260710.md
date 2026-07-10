# OpenVisionLab 3D Commercial Gap Self Evaluation

Updated: 2026-07-10

## Purpose

This document records the current self evaluation of OpenVisionLab 3D Studio against commercial 3D inspection, 3D vision, and metrology software. The goal is to choose the next development priorities before moving from viewer completion into deeper 3D algorithms.

This is not a vendor feature-copy list. It is a filter for deciding which viewer/workbench features are required for a practical rule-based 3D inspection product.

## Product Scope Used For This Review

OpenVisionLab 3D Studio remains a local Windows desktop rule-based 3D inspection workbench.

In scope now:

- local 3D data load and review;
- SharpGL viewer reliability;
- measurement, picking, ROI, overlay, and color evidence;
- explicit preview, publish, save, runner replay, and UI/runner comparison;
- MVVM-oriented Viewer and Shell workflow.

Out of scope now:

- live sensor control;
- PLC, robot, fieldbus, HMI deployment, cloud, production database;
- full CAD editing;
- AI anomaly training.

## Sources Checked

Official or vendor-owned sources were preferred.

| Vendor/tool | Relevant evidence for OpenVisionLab 3D | URL |
| --- | --- | --- |
| LMI Gocator | Built-in 3D scan, measurement, and control capability on the inspection platform. | https://lmi3d.com/brand/gocator-3d-smart-sensors/ |
| KEYENCE LJ-X8000 | Inline 2D/3D measurement and inspection, high-resolution profiles, width/height/area/volume, appearance and shape inspection, profile alignment. | https://www.keyence.com/products/measure/laser-2d/lj-x8000/ |
| KEYENCE LJ-S8000 | Height, flatness, position, width, area, volume, angle, and GD&T measurement tools; height-based images for appearance inspection. | https://www.keyence.com/products/measure/laser-2d/lj-s8000/ |
| KEYENCE LJ-X8000 landing | Measurement settings organized as capture settings, inspection tools, and misalignment correction. | https://www.keyence.com/landing/measure/ed_lj-x8000.jsp |
| MVTec HALCON 3D matching | Shape-based matching from CAD model views and surface-based matching from point clouds plus range images. | https://www.mvtec.com/knowledge-base/technologies/3d-vision/3d-matching |
| ZEISS INSPECT Optical 3D | Full-field acquisition, mesh processing, trend analyses, digital assembly, GD&T, reporting, surface defect detection, and mesh editing. | https://www.zeiss.com/metrology/us/software/zeiss-inspect/zeiss-inspect-optical-3d.html |
| ZEISS INSPECT | Parametric repeatable inspection steps, nominal-actual comparison color scales, GD&T checks, reporting, apps, Python customization. | https://www.zeiss.com/metrology/us/software/zeiss-inspect.html |
| PolyWorks Inspector | Universal 3D dimensional analysis, point-cloud device/file input, CAD/QIF GD&T import, Play Inspection sequences, dynamic reports, surface/cross-section best-fit. | https://www.polyworks.com/en-us/products/polyworks-inspector |

## Current Self Evaluation

### What Is Strong Enough To Keep Building On

| Area | Current evidence | Assessment |
| --- | --- | --- |
| Hostable viewer boundary | `OpenVisionLab.ThreeD.Viewer` is separate and Shell hosts it through docking. | Good. Keep the viewer as a reusable control/library. |
| Basic data loading | C3D, GLB, STL, LAS, and LAZ are covered by sample smokes and matrix docs. | Good for viewer MVP. Continue sample-driven gaps only. |
| Camera and interaction | Orbit, pan, zoom, fit, pick, selection modes, and smoke captures exist. | Good, but keep regression coverage when touching input/render code. |
| Measurement visibility | Two-point, ROI step, height delta, coordinate HUD, and Shell mirror panes exist. | Useful baseline. Needs more standard measurement tools. |
| Result evidence | Preview/result layers, metrics, overlays, recipe save/reopen, runner replay, and comparison evidence exist. | Strong direction. Needs run snapshot/report packaging. |
| MVVM direction | Visible commands are moving to ViewModel commands; View code-behind is becoming an OpenGL/UI bridge. | Correct direction. Continue only where a command/state currently lives in View unnecessarily. |

### Gaps Against Commercial Products

| Commercial expectation | Current OpenVisionLab 3D state | Gap |
| --- | --- | --- |
| Repeatable inspection plan with editable steps. | One C3D height-deviation recipe and LAZ/LAS two-point recipe paths exist. | No first-class tool/step list in the Shell yet. |
| Inspection run package: screenshot, contract, runner report, status, key metrics, and report/export. | Shell has Evidence Workbench and minimal history row. | No durable run snapshot bundle visible as a single inspection record. |
| Standard measurement toolbox. | Two-point, ROI step height, section/profile, height map, and one height-deviation rule exist. | Missing common tools: plane fit, flatness, width, area, volume, angle, distance-to-plane, point-to-mesh deviation. |
| Nominal-actual comparison. | Current deviation is C3D height-rule based, not CAD/mesh nominal comparison. | No CAD/mesh nominal entity, no point/mesh-to-nominal color map, no best-fit alignment. |
| Alignment beyond translation MVP. | `Align From ROI` applies a minimal translation workflow. | Missing plane fit, best-fit, constrained transform, feature-based frame definition, and alignment confidence evidence. |
| Surface/defect inspection. | Loader failure handling and basic overlays exist. | No surface defect segmentation, dent/burr/chip/warpage classification, or defect clustering. |
| Mesh/point-cloud preparation. | LAZ/LAS sampling and GLB/STL render-density controls exist. | No crop, clip box, denoise, voxel/grid downsample, mesh smoothing, or hole-fill workflow. |
| Parametric inspection replay. | Runner can replay selected recipes and compare contracts. | Need a general step execution model before adding many tools. |
| Reporting/statistics. | Text contracts and screenshots exist as artifacts. | No user-facing report snapshot, PDF/export plan, statistics, trend chart, or batch comparison yet. |

## Priority Decision

The next work should not jump directly into advanced 3D algorithms. Commercial tools show that inspection value comes from repeatability and evidence as much as from algorithms. OpenVisionLab already has raw artifacts, but not a user-facing run package.

The next recommended feature is:

### P0: Evidence Workbench Run Snapshot Bundle

Goal:

Create a durable inspection run record that groups:

- source/recipe path;
- run time;
- status;
- key metrics;
- Viewer screenshot path;
- Viewer contract path;
- Runner report path;
- UI/runner match state.

Why this first:

- It uses existing artifacts instead of inventing a new algorithm.
- It matches commercial reporting/repeatability patterns.
- It gives future tools a common evidence target.
- It improves confidence before adding more measurement tools.

Suggested smallest implementation:

1. View: Add an `Evidence Workbench` `Snapshot` or `Run Record` row/panel in Shell.
2. ViewModel: Add a small `InspectionRunRecord` view state or simple properties in `ShellMainWindowViewModel`.
3. Model: Add a serializable run-record DTO only if the ViewModel data starts needing persistence.
4. Smoke: Run Shell with `--shell-smoke-screenshot` and `--shell-evidence-tab history` or a new snapshot tab option, then verify the screenshot shows the record.

Acceptance:

- A Shell smoke can prove one current C3D or LAZ/LAS run record.
- The record links or displays screenshot, contract, runner report, key metric, status, and match state.
- No source geometry is mutated.
- Preview/publish/run remain explicit.

## Next Development Backlog

| Priority | Item | Why | Verification |
| --- | --- | --- | --- |
| P0 | Evidence Workbench run snapshot bundle | Commercial tools treat reporting/evidence as part of inspection, not an afterthought. | Shell smoke screenshot + contract/report paths displayed. |
| P1 | General inspection step list skeleton | Required before adding many tools without creating one-off UI/state per tool. | Shell shows recipe/tool steps for current C3D and LAZ recipes. |
| P1 | Plane fit and distance-to-plane measurement | Basic 3D metrology primitive used by flatness, step, alignment, and height checks. | Viewer/Shell smoke with plane metrics and overlay. |
| P1 | Clip/section box and crop ROI | Real point clouds and meshes need focused review before measurement. | Viewer smoke proves clipped bounds and unchanged source entity. |
| P2 | Best-fit alignment MVP | Needed for nominal-actual comparison and commercial-style metrology workflows. | Runner and Viewer produce matching transform/alignment evidence. |
| P2 | Point/mesh-to-nominal deviation color map | Commercial metrology depends on color-scale deviation review. | GLB/STL or point-cloud sample produces deviation legend and fail/pass overlay. |
| P2 | Width/area/volume/angle tools | Common KEYENCE/LMI-style measurement set. | One tool at a time with metrics, overlay, recipe, runner. |
| P3 | Mesh/point-cloud preparation controls | Needed for large samples and noisy data, but should follow core inspection evidence. | Crop/downsample/denoise smoke with source/result separation. |
| P3 | Report export | Useful after run snapshot data stabilizes. | Generated report artifact with screenshot + metrics + status. |
| Later | GD&T, CAD kernel, sensor/PLC/robot, HMI, AI defect training | Valuable commercially, but outside current viewer completion gate. | Separate architecture decision and prototype required. |

## Current Maturity Assessment

No numeric maturity score is recorded in the repository as a source of truth. Based on the current handoff and smoke evidence, the project is past a passive viewer MVP and is now an early inspection workbench MVP.

The viewer completion gate is not finished because the following are still weak compared with commercial workflows:

- durable run snapshot/report evidence;
- general inspection step list;
- broader measurement toolbox;
- nominal/actual comparison;
- best-fit/feature alignment;
- crop/clip/large-data review workflow.

The product should stay in viewer/workbench completion until P0 and at least one P1 measurement/alignment item are implemented and smoke-tested.

## Decision

Proceed with P0 next:

```text
Evidence Workbench run snapshot bundle
  -> visible Shell record
  -> screenshot + contract + runner report paths
  -> key metrics + status + match state
  -> Shell smoke evidence
```

This is the smallest commercial-parity improvement that strengthens the whole future toolchain.
