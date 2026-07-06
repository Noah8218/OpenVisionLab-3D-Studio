# OpenVisionLab 3D Codebase Structure

Updated: 2026-07-06

This repository contains the initial operating documents and the first SharpGL WPF viewer MVP skeleton.

## 1. Existing Structure

| Path | Status | Responsibility |
| --- | --- | --- |
| `AGENTS.md` | Exists | Codex working agreement for this 3D repository. |
| `README.md` | Exists | Product entry point and document map. |
| `docs/` | Exists | Direction, research, viewer MVP, sample data, and handoff documents. |
| `3D/` | Exists | Local Thickness/Warpage sample C3D files with PNG previews. Treat as sample input data, not source code. |
| `OpenVisionLab.ThreeDStudio.slnx` | Exists | Solution file for the 3D Studio app. |
| `src/OpenVisionLab.ThreeDStudio/` | Exists | WPF desktop app with SharpGL viewport, generated cube, generated point cloud, color modes, orbit/pan/zoom/fit controls, picking status, selection overlays, measurement/result overlays, and screenshot smoke paths. |
| `src/OpenVisionLab.ThreeDStudio/Data/` | Exists | Minimal local C3D height-grid reader for the inferred sample layout and downsampled viewer points. |
| `src/OpenVisionLab.ThreeDStudio/Rendering/` | Exists | Small SharpGL viewer support classes for camera/picking math and inspection overlay drawing. No renderer abstraction layer yet. |
| `src/OpenVisionLab.ThreeDStudio/ViewModels/` | Exists | MVVM state for the current shell: visibility, color mode, selection mode, camera target/distance, status text, and screenshot path. View code-behind remains a thin SharpGL and mouse-event bridge. |

There is no separate core library, tool library, or test project yet.

## 2. Reference Repository

`C:\Git\OpenVisionLab_Dev` is the 2D reference repository. Use it for:

- Layer-based workspace behavior.
- Preview versus publish separation.
- Tool result contracts: status, metrics, overlays, and messages.
- Recipe and runner thinking.
- WPF/MVVM direction, thin view code-behind, and screenshot smoke discipline.

Do not copy the 2D repo structure wholesale. The 3D repo should borrow contracts, not historical folder shape.

## 3. Planned First Structure

Create these folders only when implementation begins.

| Planned Path | Create When | Responsibility |
| --- | --- | --- |
| `src/OpenVisionLab.ThreeD.Core/` | First shared model appears | 3D entity, scene, layer, transform, unit, result, metric, overlay contracts. |
| `src/OpenVisionLab.ThreeD.Viewer/` | Viewer dependency is chosen | Rendering adapter, camera controller, picking, overlay drawing, screenshot capture. |
| `src/OpenVisionLab.ThreeD.Tools/` | First validation tool starts | Rule-based 3D tools such as distance, bounds, alignment, plane fit, and mesh deviation. |
| `tools/` | First command check is needed | Build, smoke, sample validation, screenshot, and contract checks. |
| `samples/` | First redistributable public sample is added | Small public sample models and expected results. Current local samples live in `3D/`. |
| `artifacts/` | First smoke captures evidence | Generated screenshots, logs, and reports. Do not treat as source. |

## 4. Intended Runtime Flow

```text
3D source file
  -> scene/entity layer
  -> viewer display
  -> picking/measurement/ROI selection
  -> rule-based tool preview
  -> result metrics + overlays
  -> explicit publish to result entity/layer
  -> recipe step save
  -> runner replays the same rule outside the UI
```

## 5. Ownership Rules

- Viewer code owns rendering, camera, picking, hit testing, and screenshot capture.
- Core code owns units, transforms, entity identity, layer identity, metrics, overlays, and result status.
- Tool code owns rule parameters and algorithm execution.
- App shell owns workflow composition and visible commands.
- A renderer dependency must stay behind a small adapter once a second viewer-related feature needs it. Do not add an adapter before the first prototype proves the library.

## 6. Starting Point For New Work

| Work Type | First Document |
| --- | --- |
| Viewer prototype | `docs/OPENVISIONLAB_3D_VIEWER_MVP_PLAN.md` |
| Product direction | `docs/OPENVISIONLAB_3D_PLATFORM_DIRECTION.md` |
| Library/engine choice | `docs/OPENVISIONLAB_3D_RESEARCH_NOTES_20260706.md` |
| Local sample data | `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md` |
| Next session | `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md` |
