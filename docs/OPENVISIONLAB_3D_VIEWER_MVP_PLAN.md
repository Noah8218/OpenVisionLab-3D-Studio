# OpenVisionLab 3D Viewer MVP Plan

Updated: 2026-07-06

The viewer MVP is the first implementation milestone. It should prove that OpenVisionLab 3D Studio can display, inspect, pick, measure, and capture 3D evidence before rule authoring or 3D algorithm development begins.

## 1. MVP Scope

Required:

- WPF/.NET desktop app shell.
- One 3D viewport.
- Load one redistributable sample mesh.
- Orbit, pan, zoom, reset camera, fit all, and fit selection.
- Object/entity tree with visibility toggles.
- Pick object/point/face and show model coordinates.
- Draw one measurement overlay.
- Capture a screenshot artifact from the current viewer state.

Not required yet:

- CAD editing.
- STEP/IGES import.
- Point-cloud algorithms.
- Recipe XML.
- AI recipe authoring.
- Hardware integration.

## 2. Selected Viewer Stack

Selected first stack: SharpGL with WPF.

Reason:

- The project owner is already familiar with SharpGL and can confidently analyze/debug the source.
- It keeps the first renderer close to explicit OpenGL concepts: buffers, camera matrices, picking, overlays, and screenshot capture.
- It avoids committing the product to a larger CAD/game/scientific framework before the viewer contract is proven.
- It matches the product need for a small, inspectable MVP better than a broad engine.

Risks:

- SharpGL gives less out of the box than HelixToolkit, Eyeshot, or VTK, so camera control, picking, model loading, and overlays must be implemented or wrapped deliberately.
- WPF/OpenGL integration and screenshot capture must be verified on the target PC before feature work expands.
- Large models, point clouds, STEP/IGES, and CAD topology are not part of the first SharpGL MVP.

Deferred alternatives:

- HelixToolkit WPF/SharpDX if SharpGL cannot meet basic WPF rendering, picking, or screenshot needs.
- ActiViz/VTK if point-cloud/volume/scientific visualization becomes dominant.
- Open CASCADE Technology when STEP/IGES/BRep topology becomes a must-have, not for the first viewer.
- AB4D, Eyeshot, or HOOPS only if the project explicitly accepts commercial SDK cost for engineering/CAD viewer acceleration.

## 3. Viewer Acceptance Checks

The MVP is accepted only when these checks have evidence:

| Check | Evidence |
| --- | --- |
| App launches | Build/run command output. |
| Sample model loads | Visible model and loaded entity count. |
| Generated point cloud renders | Point-cloud layer visible with point count and color mode. |
| Camera controls work | Screenshot after orbit/pan/zoom/fit-all/fit-selection or UI smoke interaction. |
| Picking works | Picked entity/coordinate displayed in status panel. |
| Selection state works | Point, box ROI, and section-plane overlays are visible in smoke screenshots. |
| Measurement overlay works | Distance or axis-aligned size overlay visible. |
| Visibility works | Toggle hides/shows selected entity. |
| Color modes work | `Solid`, `Height`, and `Deviation` point-cloud modes can be selected. |
| Screenshot capture works | PNG artifact path produced by command or UI action. |

## 4. Viewer Completion Gate

Do not start 3D algorithm feature work until the viewer gate is stable enough to carry future rule evidence.

Required before algorithm work:

- Orbit, pan, zoom, reset, fit-all, and fit-selection.
- Mesh and generated point-cloud rendering.
- Entity/layer visibility toggles.
- Point picking and coordinate status.
- Point, box ROI, and section-plane selection state.
- Measurement/result overlay primitives.
- Color modes: `Solid`, `Height`, and placeholder `Deviation`.
- Screenshot smoke artifacts for representative viewer states.
- Durable viewer state kept outside heavy view code-behind where practical.

## 5. Suggested Minimal UI

```text
Top command bar: Open, Fit, Reset, Screenshot
Left rail: entity tree + visibility toggles
Center: 3D viewport
Right panel: selected entity, coordinates, measurements, result metrics
Bottom strip: model units, camera mode, pick status, warnings
```

Keep the first UI utilitarian. Do not build a landing page or marketing screen.

## 6. First Sample Asset

Use the smallest redistributable asset that proves the pipeline:

- A cube or bracket mesh generated locally, if no external sample is needed.
- A `.glb` or `.obj` sample only if the license is clear and stored with attribution.
- Local sample data is available under `3D/Thickness` and `3D/Warpage`; see `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md`.

The sample must have known dimensions so the first measurement check can assert expected values.

For the viewer gate, use generated geometry first, then add a minimal C3D height-grid view after the generated mesh and generated point-cloud paths are stable.

## 7. First Implementation Checklist

1. Create solution and app project.
2. Add the SharpGL WPF dependency.
3. Render a generated cube before adding file import.
4. Add camera commands around explicit view/projection matrices.
5. Add picking and coordinate status.
6. Add one measurement overlay.
7. Add screenshot capture.
8. Add a smoke command or small test utility.
9. Update `AGENTS.md` with the exact build/smoke commands.

## 8. Stop Conditions

Stop and reassess before adding more features if:

- The viewer dependency cannot render a simple cube reliably.
- Picking cannot return stable object and coordinate information.
- Screenshot capture cannot be automated.
- WPF overlay integration causes layout or input conflicts.
