# OpenVisionLab 3D Product Target And Self Evaluation

Updated: 2026-07-11

Status: current product-direction source of truth. Older market reviews remain useful as history, but this document controls current priorities when they conflict.

## Executive Decision

OpenVisionLab 3D Studio should target an explainable, local, rule-based 3D inspection recipe workbench for height maps, point clouds, and meshes.

The product workflow is:

```text
Load measured 3D data and optional nominal data
  -> define units, coordinate frame, references, and ROIs
  -> add ordered inspection steps
  -> Preview explicitly
  -> review metrics, tolerance state, and 2D/3D overlays
  -> Publish an explicit result entity/layer
  -> save the recipe
  -> replay the same recipe in the headless Runner
  -> review a durable run record and report
```

Current maturity is **early inspection workbench MVP**. No repository-backed percentage is used.

- Viewer Foundation v1: **passed for the current fixed sample matrix**.
- Inspection Recipe v1: **baseline passed for two independent typed C3D slices: numeric-reference-ROI plane flatness and explicit-cell point-pair dimensions**.
- Nominal/actual metrology: **not started as a product workflow**.
- Production integration: **intentionally out of scope**.

Passing Viewer Foundation v1 does not mean the viewer is production-complete. It means rendering, camera, visibility, picking, selection, measurement/result overlays, color modes, Shell hosting, and screenshot evidence are stable enough to protect as a regression baseline while inspection workflow development begins.

## Evidence Checked

Local checks performed on 2026-07-11:

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug`: passed with zero warnings and zero errors.
- Viewer fitted-plane smoke: `artifacts/self_eval_viewer_plane_20260711.png` and `artifacts/self_eval_viewer_plane_20260711.txt`.
- Shell hosted-viewer smoke: `artifacts/self_eval_shell_plane_20260711.png`.
- Runner replay: `artifacts/self_eval_runner_c3d_20260711.txt`; the configured sample intentionally reports `Fail` because peak deviation exceeds tolerance.
- Analytic plane/flatness verification: `artifacts/plane_flatness_golden_after.txt`; exact plane coefficients, signed extrema, flatness, RMS, Pass/Fail thresholds, and six controlled error paths pass.
- Point-pair dimensions evidence: `artifacts/viewer_dimensions_after.*`, `artifacts/viewer_dimensions_reopen_after.*`, `artifacts/runner_point_pair_dimensions_after.txt`, and `artifacts/shell_dimensions_after.png`; the saved source cells replay the same distance, XZ width, signed elevation angle, and status.
- Analytic point-pair verification: `artifacts/point_pair_dimensions_golden_after.txt`; a known `(3,4,4)` vector, signed descending angle, tolerance failure, and six controlled invalid-input paths pass (`9/9`).
- Current data matrix: C3D, GLB, STL, LAS, and LAZ with positive and controlled-failure paths.
- Current architecture: separate Core, Data, Tools, Viewer, Docking.Controls, Shell, Runner, and app-host projects.

The worktree also contains uncommitted plane/flatness, point-pair-dimensions, Viewer, and Evidence Workbench changes. This review treats the current working tree as the evaluated product state and does not claim those changes are published.

## Commercial Product Findings

Official product material was checked on 2026-07-11.

| Product | Commercial pattern | Direction for OpenVisionLab 3D |
| --- | --- | --- |
| ZEISS INSPECT | Parametric inspection steps are traceable, repeatable, editable, dependency-aware, and reusable as templates. It also connects nominal/actual color maps, GD&T, reporting, and trend analysis. | Make the inspection plan and its dependencies first-class. A result must explain which source, reference, ROI, parameters, and earlier step produced it. |
| PolyWorks Inspector | Uses reusable inspection projects, explicit sequences, feature/datum/best-fit alignment, measured-to-nominal color maps, dimensional controls, multipiece review, and certified math. | Treat coordinate/reference definition as part of the recipe, show real ordered steps, and keep Viewer/Runner results deterministic. Do not claim metrology-grade accuracy without validation. |
| Geomagic Control X | Focuses on scan-to-CAD or scan-to-scan comparison, visual scripting, repeated automated inspection, annotations, dimensions, and understandable reports. | Add nominal/actual comparison only after basic references and recipe steps are stable. Preserve learned inspection intent in recipes rather than UI-only state. |
| LMI Gocator | Chains masks/ROIs into surface tools such as plane, flatness, dimensions, holes, volume, gap, and flush, then applies thresholds for decisions. | Build small, inspectable ROI-based tools with explicit inputs, metrics, overlays, and tolerances. `Reference Plane + Flatness` is the correct first complete surface tool. |
| Cognex VisionPro 3D | Provides reference-plane height, volume, cross-section, alignment, and graphical application flow for industrial 3D data. | Prioritize reference-relative height, volume, cross-section, and ordered tool flow before broad CAD or AI features. |

The common commercial lesson is not the number of tools. It is the complete chain:

```text
reference -> alignment -> tool input -> measurement -> tolerance -> visual evidence -> replay -> report
```

## Target Position

### Target Users

- Vision and automation engineers developing offline 3D inspection recipes.
- Quality engineers reviewing 3D measurements and evidence without needing a full CAD metrology suite.
- Developers extending transparent rule-based tools and validating Viewer/Runner parity.

### Product Differentiators

- Local-first and sensor-neutral for imported height maps, point clouds, and meshes.
- A separately reusable SharpGL Viewer with inspection facts visible inside the Viewer itself.
- Explicit source/result separation and explicit Preview/Publish behavior.
- Human-readable evidence contracts plus a headless Runner for deterministic replay.
- Inspectable rule algorithms and small end-to-end tool slices instead of hidden automatic tuning.
- Future LLM assistance may draft recipe steps only after the recipe schema and validators are stable; it must never bypass validation or explicit Preview/Run/Publish.

### Explicit Non-Goals For The Current Product Phase

- Full CAD kernel, broad STEP/IGES/PMI import, or standards-complete GD&T.
- Scanner/camera acquisition, robot programming, PLC/I/O, production HMI, or line control.
- Enterprise data lake, cloud collaboration, account management, or plant-wide SPC platform.
- AI defect training or automatic recipe tuning before rule-based evidence is trustworthy.
- Claims of calibrated or certified metrology accuracy without units, calibration, uncertainty, and algorithm-validation evidence.

## Capability Scorecard

Scale: `0` absent, `1` prototype, `2` working MVP, `3` operational baseline, `4` commercial-mature. Scores are directional and are not combined into a marketing percentage.

| Capability | Current | Evidence | Main gap |
| --- | ---: | --- | --- |
| Data loading and 3D display | 3 | C3D, GLB, STL, LAS/LAZ fixed matrix; render density and controlled loader failures. | Clip/crop workflow, broader formats, and out-of-core scale are not yet operational. |
| Camera, picking, selection, overlays | 3 | Orbit/pan/zoom/fit, point/mesh picks, ROI/section, measurement and result overlays, Viewer HUD. | Interaction regression coverage remains smoke-oriented rather than automated gesture testing. |
| Reference and alignment | 2 | Transform state, translation-only Align From ROI, fitted C3D height-field plane, and numeric recipe-owned reference ROI. | No interactive plane ROI, 3-point frame, plane-derived rotation, 3-2-1, or best-fit. |
| Measurement toolbox | 2 | Two-point, height delta, ROI step, section/profile, height map, fitted-plane distance, ROI-reference flatness, and explicit-cell distance/XZ-width/signed-angle acceptance. | Automatic feature-based dimensions, area, volume, gap/flush, and nominal deviation are incomplete. |
| Recipe and inspection-step model | 2 | Typed flatness and point-pair-dimensions slices with stable step/source/reference IDs, save/reopen, explicit Preview/Publish, Runner replay, and Shell step evidence. | The slices use tool-specific recipe families; there is no proven multi-step dependency executor. |
| Runner and evidence parity | 2 | Headless replay, contract comparison, screenshots, result layers, Shell history/snapshot views. | No durable machine-readable run bundle shared by every tool and no batch replay. |
| Nominal/actual comparison | 0 | A C3D mean-height deviation color mode is not CAD/scan nominal comparison. | Nominal entity, alignment strategy, point-to-mesh distance, deviation map, and tolerances. |
| Reporting and multipiece review | 1 | Text reports and visible evidence paths. | User-facing HTML/PDF/CSV report, batch table, trends, and statistics. |
| Metrology assurance | 1 | Deterministic smoke values, explicit raw/model units in selected paths, and analytic plane/flatness plus point-pair-dimensions golden suites. | Formal unit contract, calibration provenance, uncertainty, external golden datasets, feature-fitting validation, and broader independent algorithm validation. |
| Architecture and maintainability | 2 | Separate Viewer/Shell/Core/Data/Tools/Runner boundaries; MVVM direction; CI build. | Viewer code-behind remains large, recipe logic is tool-specific, and automated unit/integration tests are limited. |

## Gate Decision

### Viewer Foundation v1: Passed

The current fixed matrix demonstrates the contracts originally required for the viewer gate:

- reliable display of representative height-grid, mesh, and point-cloud samples;
- camera control and fit behavior;
- object/layer visibility;
- picking and selection;
- measurement and result overlays;
- color modes and legends;
- standalone Viewer and docked Shell hosting;
- screenshot and contract smoke evidence;
- controlled loader failures.

Future viewer changes must preserve this baseline, but routine development should no longer add viewer-only features without an inspection workflow need.

### Inspection Recipe v1: Current Gate

The next release target is one complete reusable inspection plan, not another isolated smoke-only measurement.

Required acceptance scenario:

1. Load the C3D sample.
2. Define a reference plane from an operator-selected ROI or three valid points.
3. Add a flatness/deviation step with explicit units and tolerance.
4. Preview without mutating the source entity.
5. Show plane normal, sample count, RMS, min/max signed deviation, flatness, status, and deviation overlay.
6. Publish a separate result entity/layer explicitly.
7. Save the reference and tool as recipe steps with stable IDs and input references.
8. Reopen the recipe and reproduce the same Viewer result.
9. Run the same recipe headlessly and match metrics/status against the Viewer contract.
10. Show the actual ordered steps and the resulting run record in Shell.

Inspection Recipe v1 passes only when all ten items have current build and smoke evidence.

Status on 2026-07-11: the baseline passes for `recipes/c3d-plane-flatness.recipe.json` using a numeric operator-configured reference ROI. Current evidence is `artifacts/viewer_flatness_after.*`, `artifacts/viewer_flatness_reopen_after.*`, `artifacts/runner_flatness_after.txt`, and `artifacts/shell_flatness_after.png`. This does not validate calibrated accuracy or a general multi-step graph.

Algorithm hardening status: `artifacts/plane_flatness_golden_after.txt` passes an analytic plane with known signed offsets and controlled invalid-reference/input cases. This validates the current plane/flatness mathematics against known answers, but not calibration, uncertainty, or external metrology software.

Second typed-slice status on 2026-07-11: `recipes/c3d-point-pair-dimensions.recipe.json` passes explicit Preview/Publish, source-cell recipe save/reopen, Viewer/Runner parity, Shell step evidence, and render-density-independent source-cell resolution. `artifacts/point_pair_dimensions_golden_after.txt` passes `9/9` known-answer and controlled-error cases. This measures two selected C3D cells; it does not perform edge detection, line/circle fitting, CAD dimensions, or GD&T.

## Development Priorities

### P0: Reference Plane + Flatness End-To-End Slice - Baseline Done

- View: reference mode/ROI or three-point selection, flatness parameters, and visible step placement.
- ViewModel: commands, selection validation, tolerance state, metric/result state, and step summary.
- Model/Tools: fitted reference result and flatness evaluation using the smallest shared step shape required by this tool.
- Evidence: overlay, result layer, recipe save/reopen, Runner parity, Viewer/Shell screenshots, and contract checks.
- Do not build a speculative workflow engine first; let this first complete step define the minimum reusable contract.

### P1: Real Inspection Plan In Shell - Single-Step Evidence Baseline Done

- Actual flatness and point-pair recipe step rows show enabled state, source/reference inputs, status, and Viewer/Runner evidence.
- Multi-step order, dependencies, blocked-step state, and one combined recipe remain unproven.
- Keep Preview, Publish, Save, and Run explicit commands.

### P1: Basic Surface Measurement Set

Add one complete tool at a time in this order:

1. Flatness and signed deviation to selected plane. Baseline done for a numeric reference ROI.
2. Explicit-cell width/distance/signed elevation angle. Baseline done; automatic feature extraction remains out of scope.
3. Gap/flush or two-region step height. Next priority.
4. Volume above/below a reference plane.
5. Cross-section dimensions.

Each tool requires Viewer/Shell UI, metrics, overlay, tolerance, recipe persistence, Runner replay, and evidence before the next tool starts.

### P2: Nominal/Actual Inspection v1

- Distinguish measured and nominal entities.
- Add explicit alignment strategy and transform evidence.
- Implement measured-to-nominal point/mesh deviation and a signed color map.
- Start with one local mesh/point-cloud pair; do not add a CAD kernel first.

### P2: Durable Run Record And Report

- Define a serializable run record containing recipe identity, source identity/hash, time, status, metrics, artifact paths, and Viewer/Runner match state.
- Generate simple JSON plus HTML or CSV before considering PDF or enterprise reporting.
- Add batch/trend views only after multiple real runs use the same stable record.

### P3: Metrology Credibility

- Make model/display/source units and conversions explicit in every measurement path.
- Add synthetic golden datasets with known plane, angle, distance, flatness, and volume answers. Plane/flatness baseline done; angle, distance, and volume remain.
- Record algorithm version, sample policy, calibration provenance, and uncertainty assumptions.
- Do not use terms such as certified, calibrated, or metrology-grade until independently justified.

## Engineering Direction

- Preserve the Viewer as a separate SharpGL library and preserve the docking/WPF-UI ownership boundaries.
- For visible work, follow View -> ViewModel -> Model. View contains bindings, commands, behaviors, and converters; code-behind remains only the UI/OpenGL/OS bridge.
- Build vertical inspection slices. A new tool is incomplete without parameters, validation, metrics, overlay, tolerance status, recipe persistence, Runner parity, and evidence.
- Keep source geometry immutable. Preview and published result geometry remain separate.
- Use stable step IDs and explicit entity/reference inputs. Never depend on display names or implicit active-selection state during replay.
- Keep measurement sampling independent from render density.
- Make invalid references, insufficient points, unit mismatch, degenerate fits, and missing inputs controlled result states rather than unhandled exceptions.
- Prefer known synthetic truth before adding another public sample or external geometry dependency.
- Update this document and `AGENTS.md` when a gate passes or the product target changes.

## Official Sources Checked

- ZEISS INSPECT parametric concept: https://www.zeiss.com/metrology/en/software/zeiss-inspect/features/parametrics.html
- ZEISS INSPECT Optical 3D: https://www.zeiss.com/metrology/en/software/zeiss-inspect/zeiss-inspect-optical-3d.html
- PolyWorks Inspector: https://www.polyworks.com/en-us/products/polyworks-inspector
- Geomagic Control X: https://hexagon.com/products/geomagic-control-x
- Geomagic Control X automated inspection: https://hexagon.com/products/geomagic-control-x/automated-inspection
- LMI Gocator emulator scenarios and built-in tools: https://lmi3d.com/testing-purpose/
- LMI Surface Mask and Flatness workflow: https://lmi3d.com/blog/introducing-surface-masking/
- Cognex VisionPro 3D-A5000 tools: https://www.cognex.com/products/machine-vision/3d-machine-vision-systems/3d-a5000-series-area-scan/software
