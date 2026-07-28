# OpenVisionLab 3D Tool Recipe Workbench Refactor Plan

Updated: 2026-07-18

## User Goal

Replace the default Thickness/Warpage task-first entry surface with a 2D OpenVisionLab-style 3D Tool Recipe Workbench. The workbench must let an engineer compose typed 3D tools such as filter, edge extraction, line fit, intersection, XYZ affine transform, re-grid, thickness, and warpage. A title bar must be a dedicated UserControl. Reuse the owned Labeling title-bar pattern and OpenVisionLab logging controls where compatible.

## Non-Negotiable Requirements

- `Thickness` and `Warpage` are measurement tools, not the product's primary navigation model.
- Keep the existing Viewer, task workspaces, Expert diagnostics, calibration center, explicit Preview/Publish behavior, and Viewer-owned camera/selection state.
- Use a Tool Workbench as the default Shell entry point.
- Keep source, derived entities, feature/correspondence evidence, transform evidence, and measurement outputs distinct.
- Do not implement an XYZ affine solver until the required correspondence/fixture rule is supplied and validated.
- Do not reference mutable DLL output folders outside this repository. Vendor only the minimal owned logging projects when the control is used.

## Current Structure

- Current responsibility owner: `MainWindow.xaml` owns the visible in-client header; `ShellMainWindowViewModel` defaults to `Teach`; `OpenVisionDockWorkspaceView` owns the reusable Dock layout.
- Current call path: `MainWindow` selects `ThicknessTaskWorkspaceView` or the existing `Expert` dock surface and moves the one Viewer control between them.
- Current dependency direction: Shell -> Docking Controls / Viewer / Tools; no Shell logging-control dependency.
- Current state owner: the Viewer ViewModel owns camera, selection, source, recipe, and result state; the Shell ViewModel owns workspace selection.

## Intended Structure

- New responsibility owner: `StudioTitleBarView` owns title presentation; `ToolWorkbenchViewModel` owns only catalog, selected-tool presentation, designed pipeline rows, and workbench log presentation.
- New call path: `MainWindow` -> default `ToolWorkbench` dock host -> Toolbox / Entity Explorer, Viewer, Tool Inspector, Recipe Pipeline / Review, Run Log.
- New dependency direction: Shell -> owned copied `OpenVisionLab.Logging.Controls` -> `OpenVisionLab.Logging` and `OpenVisionLab.Localization`. Core/Data/Tools remain independent of Shell controls.
- New state owner: the Shell owns selected workspace; the workbench ViewModel owns UI-only catalog selection; the Viewer remains the owner of camera and scene selection.

## Checkpoints

1. Capture baseline build and default Shell UI before changing layout.
2. Add the title UserControl and UI-only Tool Workbench presentation model.
3. Vendor the minimal OpenVisionLab logging projects without source references back to `OpenVisionLab_Dev`.
4. Add `Workbench` as the default Shell workspace while retaining Task, Expert, and Calibration workspaces.
5. Build, run current verification, capture the new default UI, and compare it with the baseline.

## Structural Conditions

1. `MainWindow.xaml` no longer directly renders the product-title block.
2. The default `ShellWorkspaceMode` is `Workbench`, and the Viewer is hosted by the workbench on first launch.
3. The legacy task-only workspace and Expert layout remain available but are not the default.
4. A selected catalog tool changes the right-hand Tool Inspector and adds a real session event to the Run Log.
5. The shared logging controls build from files contained in this repository and have no project/path dependency on `C:\Git\OpenVisionLab_Dev`.

## Verification Plan

- Search for the old direct title block and ensure `StudioTitleBarView` is hosted by `MainWindow`.
- Search the workspace switch and verify the new viewer-host call path includes `Workbench` while retaining Task and Expert paths.
- Inspect copied project references for no external `OpenVisionLab_Dev` path.
- Build `OpenVisionLab.ThreeDStudio.sln` with zero errors.
- Run the existing Shell ViewModel verification after updating its expected default workspace.
- Capture a fresh current-build Shell screenshot and run the existing screenshot-quality check.

## Known Blocker

The exact full-XYZ affine calculation is intentionally blocked. Two corner anchors alone do not establish a general 3D affine map; the future solver needs four affine-independent source/reference correspondences or an explicit fixture-constrained transform contract.

## Completion State

The UI/ownership refactor checkpoints are complete as of 2026-07-18: baseline and current-build captures exist, the Shell defaults to Workbench, the shared title and logging controls are repository-owned, and Task/Calibration/Expert preservation checks pass. The detailed condition-to-evidence report is `OPENVISIONLAB_3D_TOOL_RECIPE_WORKBENCH_REFACTOR_PROOF_20260718.md`.

The exact affine execution checkpoint remains intentionally blocked by missing real correspondence/fixture evidence; no numerical transform was inferred from the available Warpage input alone.
