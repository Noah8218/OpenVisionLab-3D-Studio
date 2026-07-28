# OpenVisionLab 3D Platform Direction

Updated: 2026-07-07

Status: foundational direction. Use `OPENVISIONLAB_3D_PRODUCT_TARGET_AND_SELF_EVALUATION_20260711.md` for the current commercial comparison, maturity decision, and development gates.

OpenVisionLab 3D Studio should become a rule-based 3D inspection workbench. The first deliverable is not a full inspection platform; it is a reliable 3D viewer that can support measurable validation. 3D algorithm development starts after the viewer completion gate passes.

## 1. Product Position

The 2D reference product, `C:\Git\OpenVisionLab_Dev`, works because the user can see the input, tune a tool, preview the result, inspect metrics and overlays, then save a repeatable recipe. The 3D product needs the same loop:

```text
Load 3D data
  -> inspect source entity
  -> select faces/points/regions
  -> tune rule parameters
  -> preview validation result
  -> review metric + overlay evidence
  -> publish result layer
  -> save recipe
  -> replay from runner
```

Commercial 3D vision tools use their 3D viewers as inspection workbenches, not passive model previews. The recurring pattern is acquisition review, region teaching, alignment, measurement, deviation/color-map review, tolerance judgment, and report evidence. See `docs/OPENVISIONLAB_3D_VIEWER_USAGE_RESEARCH_20260706.md` for the checked source notes and the derived development track.

## 2. Non-Goals For The First Phase

- No camera/PLC/robot integration.
- No production database.
- No cloud collaboration.
- No full CAD editing.
- No AI-owned final judgment.
- No broad plugin architecture.

These can be added later if the local viewer and rule contract hold.

## 3. Core 3D Concepts

| 2D Reference Concept | 3D Studio Equivalent |
| --- | --- |
| Image layer | 3D entity layer: mesh, point cloud, CAD body, measurement overlay, result overlay. |
| ROI | 3D selection: point, edge, face, region, bounding box, clipping volume, section plane. |
| Tool preview | Temporary 3D result overlay and metrics. |
| Published output layer | Explicit result entity/layer. |
| Pixel/GV coordinate status | World/model coordinates, unit, normal, distance, face/object identity. |
| Result image | Result scene state plus screenshot evidence. |
| Metrics | Distance, angle, area, volume, deviation, count, fit error, bounding size, pass/fail reason. |
| Recipe XML | Serializable 3D recipe with source identity, transforms, units, parameters, metrics, and acceptance rules. |

## 4. Stable Contracts

- Source geometry is immutable during validation.
- Result geometry is explicit and can be toggled, compared, exported, or cleared.
- Preview does not equal publish.
- Every rule result has status, message, elapsed time, metric list, and overlay list.
- Camera and selection state are visible and recoverable.
- Units and transforms are part of the data contract, not hidden UI state.
- Rule execution must become callable outside the UI before the platform is considered stable.

## 5. First Rule Families

Start with geometry rules that can be verified visually and numerically:

1. Distance and angle measurement.
2. Bounding box and size tolerance.
3. Plane/line/circle fit with residual error.
4. Mesh-to-mesh or scan-to-reference deviation.
5. Point-cloud filtering and region count.
6. Section/profile comparison.

Do not start with advanced CAD feature recognition until source import, picking, units, and overlays are reliable.

## 6. Architecture Direction

Use a desktop WPF/.NET direction first because the 2D reference product is already WPF-oriented and the user workflow is local inspection. The first viewer implementation should use SharpGL because the project owner can confidently inspect and debug that code. The app contract should still stay narrow enough that a future rendering backend can be swapped only if evidence requires it:

- App shell: workflow and commands.
- Core: 3D entity/result/metric/overlay contracts.
- Viewer: render, camera, picking, overlays, screenshots.
- Tools: rule parameters and execution.
- Runner: replay recipe without UI.

## 7. Roadmap

### Phase 0: Documentation Foundation

- Agent rules.
- Codebase map.
- Platform direction.
- Viewer MVP plan.
- Research notes and next-session handoff.

### Phase 1: Viewer MVP

- Create the first WPF/.NET app.
- Load or generate one small sample model.
- Render with camera controls.
- Support picking and coordinate readout.
- Draw at least one measurement overlay.
- Capture a screenshot smoke artifact.

### Phase 2: Viewer Completion Gate

- Add pan, fit-selection, and stable reset/fit behavior.
- Render mesh and generated point-cloud entities.
- Add entity/layer visibility and result overlay toggles.
- Add viewer color modes such as `Solid`, `Height`, and placeholder `Deviation`.
- Add point, box ROI, and section-plane selection concepts as viewer state.
- Keep durable viewer state in MVVM-friendly classes.
- Keep screenshot smoke evidence current for visible viewer changes.

No 3D algorithm feature work should start before this gate is stable.

### Phase 3: 3D Core Contracts

- Define `Scene`, `EntityLayer`, `Transform`, `Unit`, `ToolResult`, `Metric`, and `Overlay`.
- Current first slice defines source/result entities, layers, metrics, overlays, tool results, and model transforms in `src/OpenVisionLab.ThreeD.Core`.
- Preserve source/result separation.
- Add sample data with expected values.
- Add point-cloud and scalar-field concepts before CAD topology.

### Phase 4: First Validation Tool

- Implement one simple point-cloud rule such as distance-to-plane, height tolerance, or bounding-box tolerance.
- Show preview overlay, deviation/height coloring, metric table, OK/NG reason, and publish action.
- Add a command or smoke check that fails if the rule output changes unexpectedly.

### Phase 5: Recipe And Runner

- Save/load the first recipe format.
- Run the same rule outside the UI.
- Produce a machine-readable report with metrics and screenshot path.
- Current first slice saves the C3D height deviation parameters as JSON and replays them through `OpenVisionLab.ThreeD.Runner`; runner-driven screenshots are still future work.

### Phase 6: Format And Algorithm Expansion

- Add glTF/OBJ/STL for mesh workflows.
- Evaluate STEP/IGES through a CAD kernel only when CAD topology is required.
- Evaluate Open3D/PCL/CGAL/VTK-style processing only when the first tool family needs it.

## 8. Definition Of Done For New 3D Tools

A 3D tool is platform-ready only when:

- It can run from the UI and from a non-UI path.
- It reports status, metrics, elapsed time, and reason text.
- It draws reviewable overlays in the viewer.
- It preserves source geometry.
- It can be serialized and reloaded.
- It has at least one sample-backed check.
