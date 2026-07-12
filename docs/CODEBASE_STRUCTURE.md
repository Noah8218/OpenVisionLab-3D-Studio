# OpenVisionLab 3D Codebase Structure

Updated: 2026-07-08

This repository contains the initial operating documents and the first SharpGL WPF viewer MVP skeleton.

## 1. Existing Structure

| Path | Status | Responsibility |
| --- | --- | --- |
| `AGENTS.md` | Exists | Codex working agreement for this 3D repository. |
| `README.md` | Exists | Product entry point and document map. |
| `docs/` | Exists | Direction, research, viewer MVP, sample data, and handoff documents. |
| `3D/` | Exists | Local Thickness/Warpage sample C3D files with PNG previews plus `PublicSamples` GLB/STL/LAS/LAZ import-test data. Treat as sample input data, not source code. |
| `OpenVisionLab.ThreeDStudio.slnx` | Exists | Solution file for the 3D Studio app. |
| `scripts/` | Exists | Repeatable local smoke and validation entry points. |
| `src/OpenVisionLab.ThreeD.Core/` | Exists | Minimal 3D source/result/layer/metric/overlay/tool-result contracts plus shared contract-line formatting for Viewer and Runner evidence. Source geometry and result evidence stay separate here. |
| `src/OpenVisionLab.ThreeD.Data/` | Exists | Shared non-UI C3D height-grid loader, imported triangle-mesh data model, minimal GLB mesh/vertex-color/texture loader, ASCII/binary STL triangle loader, LAZ/LAS header metadata parser, and LAZ XYZ/RGB sampled point probe used by Viewer/Runner smoke paths. |
| `src/OpenVisionLab.ThreeD.Docking.Controls/` | Exists | Dedicated WPF docking wrapper project. It owns the AvalonDock package reference and exposes workbench content slots so the Shell app does not use raw docking APIs directly. |
| `src/OpenVisionLab.ThreeD.Runner/` | Exists | Non-UI recipe runner for replaying C3D height deviation and LAZ/LAS two-point measurement recipes, comparing UI contracts, and writing reports. |
| `src/OpenVisionLab.ThreeD.Shell/` | Exists | Minimal WPF main workspace shell that hosts the docking wrapper, the separate 3D viewer module, and the first workbench layout panes. Owns app-level `WPF-UI` package/theme resources. |
| `src/OpenVisionLab.ThreeD.Tools/` | Exists | First rule-tool library. Contains the sample-backed C3D height deviation rule plus C3D/LAZ JSON recipe and acceptance models. Depends on Core, not WPF or SharpGL. |
| `src/OpenVisionLab.ThreeD.Viewer/` | Exists | Separately releasable SharpGL WPF Viewer DLL for Shell, Studio, and external WPF hosting. Owns the viewer UI, ViewModel, render loop, camera/picking/rendering helpers, and screenshot smoke path. `scripts/build-viewer-dll.ps1` emits its validated dependency bundle and hash manifest. User-facing labels use `Imported Mesh` for the shared GLB/STL path and `LAZ/LAS` for point-cloud display while older contract/CLI names stay compatible. |
| `src/OpenVisionLab.ThreeDStudio/` | Exists | Thin WPF desktop host for the reusable viewer control. Keeps the standalone viewer smoke entry point while the main workspace Shell matures. |
| `recipes/` | Exists | Local recipe samples for runner smoke. |

There is a minimal core contract library, shared data loader, first tool library, runner, docking wrapper, shell app, and hostable viewer control. There is no test project yet.

## 2. Reference Repository

`C:\Git\OpenVisionLab_Dev` is the 2D reference repository. Use it for:

- Layer-based workspace behavior.
- Preview versus publish separation.
- Tool result contracts: status, metrics, overlays, and messages.
- Dedicated docking controls ownership. In the Dev repository, AvalonDock is owned by `Library\OpenVisionLab.Docking.Controls`, not directly by the app project.
- App-level WPF UI ownership. In the Dev repository, `WPF-UI` is referenced by the WPF app and its theme resources, not by the docking controls boundary.
- Recipe and runner thinking.
- WPF/MVVM direction, thin view code-behind, and screenshot smoke discipline.

Do not copy the 2D repo structure wholesale. The 3D repo should borrow contracts, not historical folder shape.

## 3. Planned First Structure

Create these folders only when implementation begins.

| Planned Path | Create When | Responsibility |
| --- | --- | --- |
| `scripts/run-data-loading-matrix-smoke.ps1` | Exists | Runs the current 3D data loading matrix smoke, including expected loader failures and contract checks. |
| `3D/PublicSamples/` | Exists | Small GLB/STL/LAS/LAZ sample models for import tests, with source/license/hash notes. |
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

- Viewer code owns rendering, camera, picking, hit testing, viewer data loading, viewer state, and screenshot capture.
- Data code owns shared file parsing that must run both inside and outside the UI.
- Core code owns units, transforms, entity identity, layer identity, metrics, overlays, result status, and shared evidence contract-line formatting. Viewer/Runner code should not duplicate `ToolResult`, metric, overlay, source entity, or entity layer line formats.
- Tool code owns rule parameters, recipe shape, and algorithm execution.
- Runner code owns non-UI recipe replay and report writing.
- App shell owns workflow composition, visible commands, recipe comparison display state, and app-level `WPF-UI` theme resources.
- Docking code owns docking package integration, layout behavior, and workbench content slots; the app shell should consume wrapper APIs.
- Keep the SharpGL viewer separate from the main shell so the viewer can be developed and tested independently.
- Use `scripts/build-viewer-dll.ps1` for distributable Viewer output. A plain Viewer class-library build does not collect every SharpGL runtime dependency.
- External code-behind integration uses `IOpenVisionThreeDViewerHost`, immutable `ViewerHostState`, and `HostStateChanged`; avoid direct `MainWindowViewModel.PropertyChanged` subscriptions.
- `samples/OpenVisionLab.ThreeD.Viewer.BinaryHost` proves an external WPF executable can compile and run from the published DLL bundle without a repository project reference.
- Keep `WPF-UI` out of Viewer and Docking.Controls unless a reusable control has a direct, proven need for it.
- The repository targets .NET 10. Keep non-WPF projects on `net10.0`, WPF projects on `net10.0-windows`, and preserve the compatibility evidence in `OPENVISIONLAB_3D_DOTNET10_MIGRATION_20260712.md`.
- `Directory.Build.props` owns the shared product and Viewer Host API versions stamped into assemblies, Viewer DLL manifests, and durable run evidence.
- A renderer dependency must stay behind a small adapter once a second viewer-related feature needs it. Do not add an adapter before the first prototype proves the library.

## 6. Starting Point For New Work

| Work Type | First Document |
| --- | --- |
| Viewer prototype | `docs/OPENVISIONLAB_3D_VIEWER_MVP_PLAN.md` |
| Product direction | `docs/OPENVISIONLAB_3D_PLATFORM_DIRECTION.md` |
| Library/engine choice | `docs/OPENVISIONLAB_3D_RESEARCH_NOTES_20260706.md` |
| Local sample data | `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md` |
| Next session | `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md` |
