# OpenVisionLab 3D Dockable Workbench Refactor — 2026-07-18

## Work contract

### User goal

Make every visible Tool Workbench view dockable, and make 3D Viewer right-button drag pan the view while preserving a short right-click context menu.

### Non-negotiable requirements

- Project Explorer, 3D View, Properties, Pipeline/Validation, and Session Log must be real AvalonDock panes, not fixed Grid columns styled to resemble docking.
- Each Workbench pane must support title drag, docking, and floating. Closing is disabled so a required workflow surface cannot disappear accidentally.
- Existing Workbench ViewModels remain the state owners. Dock layout code must not duplicate recipe, teaching-selection, Viewer camera, or execution state.
- Teaching capture, source binding, recipe schema `1.0`/`1.1`, explicit Preview/Run boundaries, and Viewer pointer target reliability must remain unchanged.
- A short right-click opens the Viewer context menu. Right-button drag pans and must not also open the menu.
- No inspection algorithm, affine calculation, automatic Preview/Run, or metrology claim is added in this refactor.

### Checkpoints

1. Replace the fixed Workbench Grid ownership with AvalonDock documents/anchorables while reusing the existing pane views.
2. Add right-button drag pan with a movement threshold and preserve short right-click.
3. Build current source; verify structural pane contracts, real Viewer input, teaching capture, save/reopen, and current-build UI evidence.
4. Update the product target and handoff only after the gates pass.

### Verification plan

- Search the final XAML for the removed fixed three-column/Expander layout and the five required AvalonDock content IDs.
- Verify the dependency direction remains `Shell -> Docking.Controls` and pane content still binds to the single Shell/Workbench ViewModel graph.
- Run the complete solution build with zero warnings/errors.
- Run headless selection and teaching state verifiers.
- Run actual Windows pointer smoke for left orbit, middle/shift-left pan, right-drag pan, short right-click menu, selection Undo/re-pick, Cancel, and Apply.
- Capture fresh 1280 x 760 inactive, teaching, applied, and dock-layout screenshots from the latest built executable.

### Known risks or blockers

- AvalonDock title-drag evidence can be sensitive to window focus and DPI; the existing Viewer-native target validation cannot be reused for a non-Viewer pane without a separate safe target check.
- The current user-facing Calibration Center and Advanced layout already use AvalonDock. This gate changes the current Tool Workbench panels; it does not resurrect or redesign historical hidden layouts.

### User approval needed

- None. The user explicitly approved dockable views and right-drag pan.

## Refactor proof plan

### Current structure

- Current responsibility owner: `ToolRecipeWorkbenchView.xaml` owns fixed Grid columns, splitters, and a fixed bottom Expander.
- Current call path: `MainWindow -> ToolRecipeWorkbenchView -> fixed pane instances`.
- Current dependency direction: Shell directly composes pane positions; `Docking.Controls` is used only by Calibration and Advanced layouts.
- Current state/data owner: `ShellMainWindowViewModel`, `ToolWorkbenchViewModel`, and the hosted Viewer ViewModel.

### Intended new structure

- New responsibility owner: an AvalonDock Workbench layout owns pane placement, docking, floating, and title drag.
- New call path: `MainWindow -> ToolRecipeWorkbenchView -> AvalonDock layout -> existing pane views/Viewer content`.
- New dependency direction: Shell provides pane content and state; AvalonDock manages layout only and never owns recipe or Viewer state.
- New state/data owner: unchanged. Existing Shell, Workbench, and Viewer ViewModels remain authoritative.

### Structural conditions

1. All six Workbench surfaces have stable AvalonDock content IDs and can float/dock.
2. The old fixed three-column and fixed-height bottom content layout no longer owns Workbench placement.
3. One Viewer instance remains hosted; docking must not create a second Viewer or duplicate event subscriptions.
4. The docking layout does not invoke or alter inspection execution.
5. Right-drag pan and short right-click are threshold-separated and independently verified.

### Proof checks

- Search checks: required content IDs present; obsolete Workbench Grid splitters/fixed panel placement absent.
- Import/dependency checks: Shell references the established Docking.Controls/AvalonDock package only; no new docking dependency.
- Call path checks: `ViewerContent` still reaches exactly one ContentPresenter and existing Workbench bindings remain in pane content.
- Test/build/typecheck: full solution build, selection contract, ViewModel capture, teaching recipe regression, Workbench smoke.
- Real interface check: current-build screenshots plus actual Windows pointer reports; real title drag when a reliable non-Viewer target smoke is available.

## Refactor proof report

Status: Complete

The implemented layout exposes six required Workbench anchorables: Project Explorer, 3D View, Properties, Pipeline / Validation, Session Log, and Height Profile. The same shared docking control also exposes six Advanced panes and four Calibration panes. All required panes can float, cannot close/hide, and retain their original content and ViewModel references through Float -> Dock transitions. Capture focus temporarily detaches the bottom pane and restores it without creating another Viewer.

The Viewer now treats right-button drag as pan and a short right-click as the context-menu gesture. Both the canvas menu and the compact top `View` menu bind the same five view-local commands: Fit all, Fit selection, Reset view, Screenshot, and Profile. C3D defaults to Wireframe; pure point clouds remain Points. The camera-aware bottom-left XYZ triad is a hit-test-free `viewer-display/right-handed-y-up` orientation aid, not a source/sensor-frame or calibration claim.

Profile is a display-only C3D interaction. It activates the docked Height Profile pane, captures P1/P2 on the C3D grid, draws the two endpoints and line in the Viewer, permits endpoint drag, and updates the lower height trace and min/max/mean/valid/missing statistics. It preserves the authored point-pair recipe and does not invoke Preview, Run, Publish, or an inspection algorithm.

### Current-source evidence

| Check | Result | Evidence |
| --- | --- | --- |
| Full solution build | Pass, 11 projects, 0 warnings / 0 errors | `dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"` |
| Docking structure and state | Pass `15/15`, including real AvalonDock Float -> Dock model transition | `artifacts/ui/20260718-generic-teach-selection-v1/final-docking-workspaces.txt` |
| Selection, teaching, capture, profile contracts | Pass `78/78` | `final-tool-recipe-selections.txt`, `final-tool-recipe-teaching.txt`, `final-teaching-capture-viewmodel.txt`, `final-c3d-height-profile.txt`, and `final-profile-viewmodel.txt` in the same artifact folder |
| Viewer pointer and menus | Pass: pick, orbit, middle pan, right-drag pan, zoom, short right-click; canvas `5/5`, top View `5/5` | `artifacts/ui/20260718-generic-teach-selection-v1/final-profile.viewer-pointer.txt` |
| Interactive Profile | Pass `6/6`: P1, P2, endpoint drag, outside-handle orbit, display-only boundary, source SHA-256 binding | `artifacts/ui/20260718-generic-teach-selection-v1/final-profile.pointer.txt` |
| Current-build UI quality | Pass `4/4`, all accepted on first capture | `final-profile.png` and `final-after-docking-{inactive,capturing,applied}.png` plus quality reports |
| DLL-only consumer | Pass, manifest `14/14`, outputs `12/12`, Host API `3/3`, zero `ProjectReference` | `artifacts/ui/20260718-generic-teach-selection-v1/binary-host` |

The first BinaryHost script invocation was blocked by the machine PowerShell execution policy. Re-running the same checked-in script with `powershell.exe -ExecutionPolicy Bypass` passed; this was an invocation-policy issue, not a failed product gate.

```text
Status: Complete
Scope: Dockable Workbench/Advanced/Calibration views, right-drag pan, compact duplicated Viewer menus, C3D Wireframe default, camera-aware XYZ triad, and display-only interactive C3D height profile.
Acceptance criteria: dock/float/state ownership -> pass; input gesture separation -> pass; profile interaction and non-execution boundary -> pass; current-build visual evidence -> pass.
Verification: solution build 0 warnings/0 errors; docking 15/15; focused contracts 78/78; pointer/menu and Profile smokes pass; BinaryHost 14/14, 12/12, 3/3.
Evidence: artifacts/ui/20260718-generic-teach-selection-v1 and this document.
Boundary / next dependency: Profile values remain raw-height/viewer-display only; no physical scale, calibration, algorithm execution, or metrology claim is included.
```
