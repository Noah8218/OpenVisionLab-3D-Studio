# OpenVisionLab 3D Codebase Structure

Updated: 2026-08-19

This repository contains the SharpGL WPF Viewer Foundation, docked inspection Shell, shared data/tool contracts, typed inspection recipes, headless Runner, and repeatable trust evidence.

The current structural baseline and development rules are recorded in:

- `docs/OPENVISIONLAB_3D_FINAL_REFACTOR_AND_CODE_RULES_20260726.md`
- `docs/OPENVISIONLAB_3D_CODE_RULES.md`

`App` is the WPF lifecycle entry point; `ShellVerificationCommandRouter` owns
Shell verification dispatch. `MainWindow` remains the composition/View-adapter
root. `StudioLayoutController` owns layout persistence,
`ShellRequestCoordinator` owns presentation-request subscription lifetime,
`ShellEvidenceDialogController` owns evidence/Run Record dialogs, and
`RecipeFileDialogService` owns recipe Save/Open selection, while
`ShellWorkbenchLifecycleController` owns recipe/source lifecycle and its smoke
hooks. Within the Workbench, `ViewerWorkspaceSession` owns layout and
auxiliary-slot state transitions; `ToolWorkbenchViewModel` maps recipe/artifact
candidates and synchronizes inspection selection state.
The Shell `Behaviors` folder owns reusable WPF-only interactions; for example,
`ScrollIntoViewOnSelectionChangedBehavior` keeps virtualized recipe and
validation lists aligned without owning workflow state.
`WorkbenchViewerDisplayCoordinator` owns non-teaching
Workbench-to-Viewer display subscriptions and `ShellToolLabSmoke` owns Tool Lab
smoke-window orchestration. `RunnerCommandRouter` selects CLI operations and
`RunnerApplication` owns non-UI recipe execution/reporting. Shared model
transforms are calculated by `Core.ModelTransform.Apply`.

## 1. Existing Structure

| Path | Status | Responsibility |
| --- | --- | --- |
| `AGENTS.md` | Exists | Codex working agreement for this 3D repository. |
| `README.md` | Exists | Product entry point and document map. |
| `docs/` | Exists | Direction, research, viewer MVP, sample data, and handoff documents. |
| `3D/` | Exists | Local Thickness/Warpage sample C3D files with PNG previews plus `PublicSamples` GLB/STL/LAS/LAZ import-test data. Treat as sample input data, not source code. |
| `OpenVisionLab.ThreeDStudio.slnx` | Exists | Solution file for the 3D Studio app. |
| `OpenVisionLab.ThreeDStudio.sln` | Exists | Standard Visual Studio solution for the same 14 code projects as `.slnx`, including shared Presentation, Reporting, Logging, Localization, and MessageDialogs projects. Projects are grouped under the `src` solution folder while `src` remains the on-disk source root. |
| `scripts/` | Exists | Repeatable local smoke and validation entry points. |
| `src/OpenVisionLab.ThreeD.Core/` | Exists | Minimal 3D source/result/layer/metric/overlay/tool-result contracts, typed nominal/actual input/result identities and fingerprints, plus shared contract-line formatting for Viewer and Runner evidence. Source geometry and result evidence stay separate here. |
| `src/OpenVisionLab.ThreeD.Data/` | Exists | Shared non-UI C3D height-grid loader, exact-byte verified full-resolution height-field snapshots, imported triangle-mesh data model, GLB/STL/LAS/LAZ loaders, the one-pass binary-STL inspection reader, and the ordered binary-PLY vertex reader used by Runner evidence and typed execution. |
| `src/OpenVisionLab.ThreeD.Docking.Controls/` | Exists | Dedicated WPF docking wrapper project. It owns the AvalonDock package reference and exposes workbench content slots so the Shell app does not use raw docking APIs directly. |
| `src/OpenVisionLab.ThreeD.Presentation/` | Exists | Shared .NET 10 WPF presentation primitives. It owns the generic `RelayCommand` implementation used by Shell and the Viewer compatibility surface. Feature-specific state, converters, and interactions remain with their owning feature until a second consumer proves a shared boundary. |
| `src/OpenVisionLab.ThreeD.Reporting/` | Exists | Shared runtime-neutral ordered Run Record identity, schema `1.9` composition, and JSON output. Shell and Runner retain route-specific artifact locations, Shell text, and Runner HTML/CSV/environment policy while consuming one exact record owner. |
| `src/OpenVisionLab.ThreeD.Runner/` | Exists | Non-UI recipe runner for typed inspection replay, Viewer contract comparison, durable reports, format probes, and controlled goldens. `RunnerCommandRouter` owns CLI selection; `RunnerApplication` owns recipe execution/reporting. |
| `src/OpenVisionLab.ThreeD.Shell/` | Exists | WPF workspace shell hosting docking, Viewer, and Workbench views. ViewModels own bindable workflow state and commands. `ToolWorkbenchTeachingCaptureSession` owns transient teaching-capture lifetime, progress, and the atomic grid-rectangle ROI draft. `ToolWorkbenchRecipeSession` owns recipe schema/name/path/dirty state and authored/storage/source-binding validation results while the Workbench root retains normalization, persistence, execution invalidation, notifications, source identity validation, and Viewer coordination. `Behaviors/` owns reusable WPF-only presentation interactions. Shell Views retain explicit adapters for file/message dialogs, docking, PropertyGrid flush, OpenGL/pointer hosting, and WPF screenshot smoke. |
| `src/OpenVisionLab.ThreeD.Tools/` | Exists | Render-independent rule algorithms, including the strict C3D Median Filter v1 recipe adapter, recipe/acceptance models, runtime-neutral registration acceptance, the BVH distance index, and full-query nominal/actual execution. Depends on Core and Data, not WPF or SharpGL. |
| `src/OpenVisionLab.ThreeD.Viewer/` | Exists | Separately releasable SharpGL WPF Viewer DLL for Shell, Studio, and external WPF hosting. Owns the viewer UI, ViewModels including the measured/nominal presentation workflow, Viewer-local immutable display settings and normalized display palettes, the C3D sampled-grid display proxy, render loop, camera/picking/rendering helpers, screenshot smoke path, and the smoke-only Windows pointer bridge used to route real WPF click/orbit/pan/zoom evidence. `ViewerDisplaySettingsViewModel` owns geometry/color settings plus point size, render density, display budgets, summary, and revision. `ViewerCameraSession` owns camera/projection state and saved-Perspective snapshot lifetime. `ViewerSelectionSession` owns selection mode, selected entity, pick coordinate, summary, and overlay visibility. `Recipes/HeightDeviationRecipeLoadPlan`, `HeightDeviationRecipeApplyCoordinator`, and `HeightDeviationRecipeSaveCoordinator` own the first Height Deviation recipe load/apply/save policy outside the WPF View; the View retains validation, dialogs, rendering, and callback adapters. The root ViewModel keeps delegating compatibility properties and cross-feature selection-summary policy. Native input and SharpGL proxy rendering remain in the WPF/rendering boundary. The C3D proxy and palettes are display-only and do not replace source-cell or full-query inspection geometry. The Viewer does not own the shared numerical comparison executor. `scripts/build-viewer-dll.ps1` emits its validated dependency bundle and hash manifest. User-facing labels use `Imported Mesh` for the shared GLB/STL path and `LAZ/LAS` for point-cloud display while older contract/CLI names stay compatible. |
| `src/OpenVisionLab.ThreeDStudio/` | Exists | Thin WPF desktop host for the reusable viewer control. Keeps the standalone viewer smoke entry point while the main workspace Shell matures. |
| `recipes/` | Exists | Local recipe samples for runner smoke. |

There is a minimal core contract library, shared data loader, first tool library, shared presentation and reporting primitives, runner, docking wrapper, shell app, and hostable viewer control. There is no test project yet.

## 1.1 Viewer and Main ViewModel Layout

The large Viewer and its main ViewModel are organized as partial classes. These are physical source-file boundaries only: they do not add runtime objects, change public APIs, or move numerical logic across Core/Data/Tools boundaries.

| Path | Responsibility |
| --- | --- |
| `src/OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.xaml.cs` | WPF control state, dependency properties, and construction. |
| `...ViewerControl.Host.cs` | Host API, ViewModel event subscriptions, and shell-facing visibility state. |
| `...ViewerControl.Smoke.cs` | Command-line smoke configuration and smoke-only pointer regression setup. |
| `...ViewerControl.Viewport.cs` | SharpGL viewport lifecycle, draw-loop entry points, pointer gestures, and frame telemetry. |
| `...ViewerControl.Recipes.cs` | WPF recipe commands, validation, ROI editing, file-dialog/rendering adapters, and capture orchestration; Height Deviation load/apply/save policy is delegated to the non-WPF recipe owners below. |
| `src/OpenVisionLab.ThreeD.Viewer/Recipes/HeightDeviationRecipeLoadPlan.cs` | Non-WPF Height Deviation recipe source resolution, C3D loading, and controlled rule preparation. |
| `src/OpenVisionLab.ThreeD.Viewer/Recipes/HeightDeviationRecipeApplyCoordinator.cs` | Non-WPF Height Deviation recipe ViewModel state/application sequence; WPF rendering and preview callbacks are supplied by the View. |
| `src/OpenVisionLab.ThreeD.Viewer/Recipes/HeightDeviationRecipeSaveCoordinator.cs` | Non-WPF Height Deviation recipe construction, source-path mapping, persistence, and saved-state update. |
| `...ViewerControl.Inspection.cs` | Sample inspection setup plus C3D preview/measurement orchestration. |
| `...ViewerControl.Rendering.cs` | OpenGL camera, overlays, C3D/mesh/LAZ rendering, and render caches. |
| `...ViewerControl.Picking.cs` | Ray picking, two-point/ROI selection, and camera pan helpers. |
| `...ViewerControl.Data.cs` | Viewer-local sample loading, texture upload, height-map, and profile projection. |
| `...ViewerControl.Contracts.cs` | Scene-contract output and shared Viewer formatting helpers. |
| `src/OpenVisionLab.ThreeD.Viewer/ViewModels/MainWindowViewModel.cs` | Shared state, bindable properties, command construction, and property notification. |
| `...MainWindowViewModel.Scene.cs` | Scene fit/reset, smoke-scene selection, and two-point/plane-reference state. |
| `...MainWindowViewModel.Inspection.cs` | Typed plane, thickness, point-pair, gap/flush, volume, and cross-section state transitions. |
| `...MainWindowViewModel.ViewState.cs` | ROI teaching, alignment, loaded-source metadata, and render telemetry. |
| `...MainWindowViewModel.Presentation.cs` | Preview publishing, nominal/actual presentation, camera feedback, contract summaries, and legends. |
| `...MainWindowViewModel.Recipes.cs` | Recipe load/save state, recipe summaries, parameter invalidation, and formatting. |

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
| `scripts/verify-code-structure.ps1` | Exists | Verifies solution project parity, runtime-neutral Core/Data/Tools/Runner dependencies, command-router ownership, Shell lifecycle ownership, Workbench and Viewer display-state ownership, reusable WPF Behavior ownership, and removal of duplicate model-transform implementations. |
| `scripts/verify-c3d-geometry-performance.ps1` | Exists | Runs the fixed 4-style by 3-density C3D 31-frame performance matrix and verifies static-cache, topology, screenshot, and measurement contracts. |
| `scripts/verify-nist-nominal-actual-render-density.ps1` | Exists | Runs the fixed ignored NIST comparison in Fast/Balanced/Detailed Viewer modes and proves distinct display sampling with identical normalized full-query measurement and published evidence. |
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

- Viewer code owns rendering, camera, picking, hit testing, viewer data loading, viewer state, and capture of its own visual surface. The standalone host lets the Viewer own smoke shutdown; the Shell owns shutdown when hosting it and requests embedded Viewer capture before full-Shell capture.
- `NominalActualComparisonViewModel` owns comparison presentation state, validation, commands, progress/cancellation, and fingerprints. `MainWindowViewModel` owns the active typed input and published result/entity state. Core owns shared input/result/inspection-step contracts, Data owns file parsing, and Tools owns recipe serialization plus numerical execution outside WPF and SharpGL.
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
| Release and version policy | `docs/OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md` |
| Product direction | `docs/OPENVISIONLAB_3D_PLATFORM_DIRECTION.md` |
| C3D Geometry Style performance evidence | `docs/OPENVISIONLAB_3D_C3D_GEOMETRY_STYLE_PERFORMANCE_20260715.md` |
| C3D Grayscale/Thermal Color Map evidence | `docs/OPENVISIONLAB_3D_C3D_COLOR_MAPS_20260715.md` |
| Fixed nominal/actual Part 1 product evidence | `docs/OPENVISIONLAB_3D_NIST_NOMINAL_ACTUAL_END_TO_END_20260714.md` |
| Fixed nominal/actual Part 2 product evidence | `docs/OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md` |
| Local sample data | `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md` |
| Next session | `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md` |
| Code and MVVM rules | `docs/OPENVISIONLAB_3D_CODE_RULES.md` |
