# Tool Recipe Workbench Refactor Proof

Updated: 2026-07-18

## Approved Scope

The default Shell entry surface must be a composable 3D Tool Recipe Workbench, rather than a Thickness/Warpage task picker. It must use a dedicated title UserControl, retain Task/Calibration/Expert workspaces, and use repository-owned OpenVisionLab logging controls without a build-time path dependency on `C:\Git\OpenVisionLab_Dev`.

This refactor is a UI and ownership change. It does not implement the planned general tool executor, automatic edge/feature detection, or full-XYZ affine calculation.

## Structural Proof

| Acceptance condition | Current implementation evidence |
| --- | --- |
| Title presentation has a dedicated owner | `Views/Shell/StudioTitleBarView.xaml` owns title, subtitle, and status presentation; `MainWindow.xaml` hosts the UserControl. |
| Workbench is the default | `ShellWorkspaceMode.Workbench` is the default in `ShellMainWindowViewModel`; the startup Viewer host is `ToolWorkbench`. |
| Composition is visible | `ToolWorkbenchViewModel` provides separate catalog, entity, pipeline, and run-log state. The docked UI hosts Toolbox & Entities, Viewer, Tool Inspector, Recipe Pipeline / Review, and Run Log. |
| Existing specialized workflows remain | `MainWindow` still hosts the Task surface for Teach/Inspect/Review, Calibration Center, and the Advanced/Expert dock layout; the single Viewer is moved only by the Shell View boundary. |
| Logging has no external project path | `OpenVisionLab.Logging`, `OpenVisionLab.Logging.Controls`, and `OpenVisionLab.Localization` are vendored under `src`; project-reference search reports no `OpenVisionLab_Dev` path. |
| Log view and file sink are usable | `RunLogView` hosts `LogPanelView` and a Workbench-session tab. The vendored log configuration uses stable active files in `Log`; Shell exit flushes and closes the log system. |

## Workbench V2 Teaching Layout (2026-07-18)

The default `Workbench` host in `MainWindow.xaml` uses `Views/Workbench/ToolRecipeWorkbenchView.xaml`, a recipe-first three-pane teaching layout. Its default composition is:

```text
Project Explorer  |  3D View  |  Properties
             Recipe Pipeline / Teach Review (open by default)
```

- The shared title shows the active recipe, source format/unit/frame, alignment teaching state, and saved/modified validation state when Workbench is active.
- `Project Explorer` places the category-grouped inspection-step catalog immediately after recipe New/Open/Save so a tool is visible on the first screen. Source, references, and derived entities remain available as separate expandable sections.
- `Properties` has a strict authored-step boundary and groups the selected step into Inputs, Teaching selections, Parameters & tolerances, Output, Evidence, and collapsed advanced identity/order. It does not fabricate selection or run evidence.
- The 3D View header exposes the explicit `Teach -> Preview -> Run -> Publish` lifecycle. Only Teach is active in this UI-only gate; the other stages remain visible but non-executing.
- `Recipe Pipeline / Teach Review` is open by default and shows order, typed input/output routing, validation state, reordering, and removal without hiding the Viewer.
- The Workbench bridge keeps the Viewer instance unchanged and only sets `HudDetailsVisible` for the presentation mode. The Viewer continues to own rendering, camera, and selection state.
- `Workspace` remains the existing `OpenVisionDockWorkspaceView` for Advanced/Expert. No existing Expert dock is removed or repurposed.

## Viewer Context Commands (2026-07-18)

The view-local Viewer canvas commands formerly shown as a horizontal button row now live in the Viewport right-click menu and the compact top `View` menu:

- **View:** Fit all, Fit selection, Reset view.
- **Capture:** Screenshot.
- **Inspect:** Profile.

Both menus bind the same existing Viewer commands directly. Recipe New/Open/Save remains on the owning Workbench recipe surface instead of being mixed with view navigation. `Geometry` and `HUD Details` remain visible in the compact top strip because they expose persistent Viewer state. A real short right-click menu opening and all five bindings are part of the pointer smoke; left-drag remains orbit, middle-drag and right-drag pan, and wheel remains zoom.

## Docking, Orientation, and Profile Closure (2026-07-18)

Workbench placement is now owned by the established AvalonDock control rather than fixed Grid columns. Project Explorer, 3D View, Properties, Pipeline / Validation, Session Log, and Height Profile are six independent required anchorables. Advanced uses the same six-pane shape; Calibration retains its four specialized panes. Float/Dock retains the existing ViewModel and Viewer references.

C3D starts in Wireframe, while pure point-cloud sources continue to start in Points. A bottom-left camera-aware XYZ triad reports only the Viewer display frame. `Profile` opens/selects the docked Height Profile pane and supports P1/P2 placement plus endpoint drag over the C3D grid. Its chart and statistics are live display state and do not modify the taught recipe or run an inspection.

## Current-Build Verification

| Check | Result | Evidence |
| --- | --- | --- |
| Solution build | Pass, 11 projects, 0 warnings / 0 errors | `dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform='Any CPU'` |
| Teaching recipe contract | Pass, 14/14 checks, including saved/modified state and same-source reload | `artifacts/ui/20260718-workbench-v2/tool-recipe-teaching-verification.txt` |
| Shell/ViewModel regression | Pass, 66 checks | `artifacts/ui/20260718-workbench-v2/workspace-viewmodel-verification.txt` |
| Workbench V2 structure | Pass: recipe-first title bindings, grouped catalog, workflow Inspector, explicit stage strip, visible typed pipeline, six real dock panes, and XAML parse | Current-source structural verification on 2026-07-18 |
| Viewer pointer and menus | Pass: pick, orbit, middle/right pan, zoom, real menu open; canvas `5/5`, top View `5/5` | `artifacts/ui/20260718-generic-teach-selection-v1/final-profile.viewer-pointer.txt` |
| Docking, selection, and Profile contracts | Pass: docking `15/15`, focused contracts `78/78`, Profile pointer `6/6` | `artifacts/ui/20260718-generic-teach-selection-v1` |
| Current-build before/after | Pass, both screenshot-quality gates accepted on first attempt | `artifacts/ui/20260718-workbench-v2/before.png`, `after-workbench.png`, and their `.quality.txt` files |
| Advanced/Expert and legacy Teach preservation | Pass, current-build screenshot-quality gates accepted on first attempt | `artifacts/ui/20260718-workbench-v2/after-advanced.png`, `after-legacy-teach.png`, and their `.quality.txt` files |

## Visual Comparison

- Current-build baseline immediately before Workbench V2: `artifacts/ui/20260718-workbench-v2/before.png`.
- Current-build Workbench V2: `artifacts/ui/20260718-workbench-v2/after-workbench.png`.
- The first screen now exposes recipe identity, the grouped tool catalog, selected-step inputs, explicit lifecycle, and typed pipeline. The specialized Thickness Teach flow remains available only when explicitly selected.

## Preserved and Intentionally Unchanged

- The Viewer remains the owner of rendering, camera, scene, pick, and selection state. `ToolWorkbenchViewModel` owns the separate authored teaching recipe and its saved/modified state.
- Generic `Preview`, `Run`, `Publish`, and Runner behavior remain explicit and unimplemented in this gate; selecting a catalog row does not execute a tool.
- Existing typed Thickness/Warpage task behavior, Calibration Center, and Expert diagnostics were not removed.
- No project under `C:\Git\OpenVisionLab_Dev` was changed.

## Completion Record

```text
Status: Complete
Scope: Workbench V2 information architecture, recipe state context, grouped tool/Inspector/pipeline presentation, six dockable panes, five duplicated Viewer menu commands, Viewer-display XYZ triad, and display-only interactive Profile.
Acceptance criteria: first-screen tool composition -> pass; ownership boundaries preserved -> pass; explicit non-running lifecycle -> pass; current-build UI and pointer evidence -> pass.
Verification: 11-project build 0 warnings/0 errors; focused contracts 78/78; docking 15/15; Profile pointer 6/6; canvas/top menu bindings 5/5 each; XAML/structure checks pass; current captures accepted.
Evidence: artifacts/ui/20260718-workbench-v2, artifacts/ui/20260718-generic-teach-selection-v1, and this document.
Boundary / next dependency: no edge/fit/intersection executor, XYZ affine solve, physical calibration, or metrology claim is included.
```

## Known Blocker and Next Gate

The catalog's `XYZ Affine Transform` is deliberately descriptive only. A general 3D affine map cannot be solved from two corner anchors: it requires four affine-independent source/reference correspondences, or a supplied fixture-constrained transform definition.

Before implementation, the owner must provide the old correspondence/fixture rule and real aligned source-to-correspondence evidence. The next implementation gate is then a typed `Feature/Correspondence -> XYZ Affine -> derived map` contract with rank, residual, determinant, and source-provenance validation, followed by a goldens-first execution slice.
