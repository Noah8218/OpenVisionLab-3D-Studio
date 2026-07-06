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
| `src/OpenVisionLab.ThreeD.Core/` | Exists | Minimal 3D source/result/layer/metric/overlay/tool-result contracts. Source geometry and result evidence stay separate here before rule algorithms begin. |
| `src/OpenVisionLab.ThreeD.Docking.Controls/` | Exists | Dedicated WPF docking wrapper project. It owns the AvalonDock package reference so the Shell app does not use raw docking APIs directly. |
| `src/OpenVisionLab.ThreeD.Shell/` | Exists | Minimal WPF main workspace shell that hosts the docking wrapper and reserves a document slot for the separate 3D viewer module. |
| `src/OpenVisionLab.ThreeDStudio/` | Exists | WPF desktop app with SharpGL viewport, generated cube, generated point cloud, color modes, orbit/pan/zoom/fit controls, picking status, selection overlays, measurement/result overlays, and screenshot smoke paths. |
| `src/OpenVisionLab.ThreeDStudio/Data/` | Exists | Minimal local C3D height-grid reader for the inferred sample layout and downsampled viewer points. |
| `src/OpenVisionLab.ThreeDStudio/Rendering/` | Exists | Small SharpGL viewer support classes for camera/picking math and inspection overlay drawing. No renderer abstraction layer yet. |
| `src/OpenVisionLab.ThreeDStudio/ViewModels/` | Exists | MVVM state for the current shell: visibility, color mode, selection mode, camera target/distance, status text, screenshot path, core source/layer contract projection, synthetic ToolResult preview state, and published result entity state. View code-behind remains a thin SharpGL and mouse-event bridge. |

There is a minimal core contract library, docking wrapper, and shell app. There is no separate tool library or test project yet.

## 2. Reference Repository

`C:\Git\OpenVisionLab_Dev` is the 2D reference repository. Use it for:

- Layer-based workspace behavior.
- Preview versus publish separation.
- Tool result contracts: status, metrics, overlays, and messages.
- Dedicated docking controls ownership. In the Dev repository, AvalonDock is owned by `Library\OpenVisionLab.Docking.Controls`, not directly by the app project.
- Recipe and runner thinking.
- WPF/MVVM direction, thin view code-behind, and screenshot smoke discipline.

Do not copy the 2D repo structure wholesale. The 3D repo should borrow contracts, not historical folder shape.

## 3. Planned First Structure

Create these folders only when implementation begins.

| Planned Path | Create When | Responsibility |
| --- | --- | --- |
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
- Docking code owns docking package integration and layout behavior; the app shell should consume wrapper APIs.
- Keep the SharpGL viewer separate from the main shell so the viewer can be developed and tested independently.
- Treat .NET 10 migration as a separate compatibility pass across WPF, SharpGL, docking, and vendored DLL/runtime dependencies.
- A renderer dependency must stay behind a small adapter once a second viewer-related feature needs it. Do not add an adapter before the first prototype proves the library.

## 6. Starting Point For New Work

| Work Type | First Document |
| --- | --- |
| Viewer prototype | `docs/OPENVISIONLAB_3D_VIEWER_MVP_PLAN.md` |
| Product direction | `docs/OPENVISIONLAB_3D_PLATFORM_DIRECTION.md` |
| Library/engine choice | `docs/OPENVISIONLAB_3D_RESEARCH_NOTES_20260706.md` |
| Local sample data | `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md` |
| Next session | `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md` |
