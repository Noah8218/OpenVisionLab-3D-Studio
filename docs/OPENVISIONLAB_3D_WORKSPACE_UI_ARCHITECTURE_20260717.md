# OpenVisionLab 3D Task Workspace UI Architecture

> Superseded as the default product-entry architecture on 2026-07-18. The bounded Task Workspace evidence below remains historical and regression-relevant, but the approved default is now the composable Tool Recipe Workbench documented in `OPENVISIONLAB_3D_TOOL_RECIPE_WORKBENCH_REFACTOR_PLAN_20260718.md`. On 2026-07-18 the owner additionally required every visible Workbench view to be a real dockable pane. This supersedes the earlier fixed-layout sentence below, while preserving a focused default arrangement and the task-scoped information-density rules. Task, Calibration, and Expert surfaces remain available as specialized workspaces.

Updated: 2026-07-17

## Decision

OpenVisionLab 3D Studio must keep a focused default operator arrangement that shows only the information and commands needed for the current inspection task. Its visible views may be AvalonDock panes so a user can resize, move, dock, or float them; docking capability must not turn the default into the dense Expert diagnostic layout. The existing full docked Shell remains valuable as an Expert Layout for development, troubleshooting, and cross-panel diagnosis.

The operator workflow is:

```text
Setup -> Teach -> Inspect -> Review
                  |
                  +-> Publish typed result -> Review

Calibrate is an independent workspace.
Expert is an optional layout, not another workflow phase.
```

This document controls UI information architecture and records the implemented first Shell slice. It does not change the existing typed tool, recipe, Preview/Publish, Viewer/Runner, or calibration claim boundaries.

## Implementation Status — Bounded Task Slices Passed Locally

The `Thickness` and bounded local raw-height `Warpage` Teach -> Inspect -> Review
Shell slices are implemented from the current source.

- `Teach` is the default Shell workspace. A clear task picker selects either C3D Thickness or local raw-height Warpage. Each task shows its own source/frame context, one ROI and acceptance editor, an explicit teach command, an explicit Preview command, compact validation, and recipe save. It does not show unrelated tool editors, Evidence Workbench, or nominal/actual panels.
- `Inspect` shows the same Viewer, the selected typed step/status, explicit Preview, and Publish. Publish delegates to the Viewer host and opens `Review` only after the Viewer confirms publication of the current Preview result.
- `Review` is read-only for the current published result entity and its Viewer overlay. It does not infer a Runner replay or report from stale/default evidence; explicit report/replay diagnostics remain in Expert.
- `Expert` preserves the existing full AvalonDock surface unchanged: Data & Layers, Viewer, Tool / Inspector, Evidence Workbench, and Linked View. The Shell View moves the same Viewer control between task and Expert hosts; Viewer camera, selection, recipe, and result state remain ViewModel-owned.
- `Setup` remains visibly disabled, because source-context authoring is not part of these slices. There is no generic layout engine, multi-run Review list, physical calibration, or hardware integration. The local Warpage task is explicitly limited to the user-designated raw-height C3D input and does not claim physical warpage or GD&T.

Current-source evidence is in `artifacts/task_workspace_20260717`:

- `before_expert_shell.png`, `after_teach_shell.png`, `after_inspect_shell.png`, `after_review_shell.png`, and `after_expert_shell.png` are the fresh before/after Shell captures. Every after capture passed the Shell screenshot-quality gate on its first attempt.
- `workspace_viewmodel_verification.txt` records `63` passing Shell/Calibration ViewModel checks.
- `c3d_thickness_golden.txt` records the C3D Thickness Tool golden `5/5`.
- `teach_pointer_input.txt` records current task-host pointer routing, pick, orbit, pan, and zoom as passed.
- `saved_c3d_thickness.recipe.json`, `viewer_thickness_contract.txt`, `runner_thickness_replay.txt`, and `runner_thickness_run.{json,html,csv}` prove explicit save/reopen and Runner replay of the saved recipe with `Pass` status and accepted Viewer/Runner comparison.
- `../c3d_warpage_20260717/after_teach_warpage_clean.png`, `final_ui_saved_warpage.recipe.json`, `final_ui_saved_runner.txt`, and `final_ui_saved_run.{json,html,csv}` record the bounded local Warpage task once the final current-source verification is complete.

Reproduce the focused task-workspace captures from a current build:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\task_workspace_20260717\teach.png --smoke-c3d thickness
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-workspace Inspect --shell-smoke-screenshot artifacts\task_workspace_20260717\inspect.png --smoke-recipe recipes\c3d-thickness.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-workspace Inspect --shell-smoke-screenshot artifacts\task_workspace_20260717\review.png --smoke-recipe recipes\c3d-thickness.recipe.json --smoke-publish-result
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-workspace Expert --shell-smoke-screenshot artifacts\task_workspace_20260717\expert.png --smoke-recipe recipes\c3d-thickness.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-workspace Inspect --shell-task Warpage --smoke-publish-result --smoke-save-recipe artifacts\c3d_warpage_20260717\final_ui_saved_warpage.recipe.json --shell-smoke-screenshot artifacts\c3d_warpage_20260717\after_review_warpage_final.png
```

## Commercial Findings

- ZEISS INSPECT separates Inspection and Reporting workspaces and scopes each workspace's commands and visible views. Its workspace contract explicitly supports default-visible views and collapsed tabs.
- PolyWorks Inspector groups inspection controls into small logical Control Views instead of treating every available metric as an always-visible dashboard.
- ZEISS reporting uses a separate result presentation context where a report can choose a graph, table, or restored 3D view.
- Geomagic Control X continues to make docking toolbars optional and improves command context by showing selection count.
- CloudCompare exposes a broad database-tree, properties, multiple-view, and console layout. This is useful as a reference/debug console, not as the default operator flow for this product.

Sources checked on 2026-07-17:

- https://zeiss.github.io/zeiss-inspect-app-api/2025/howtos/adding_workspaces_to_apps/adding_workspaces_to_apps.html
- https://www.zeiss.com/metrology/us/software/zeiss-inspect/features/reporting.html
- https://www.polyworks.com/en-us/products/polyworks-inspector
- https://www.cloudcompare.org/doc/wiki/index.php/GUI
- https://s3.amazonaws.com/dl.3dsystems.com/binaries/support/downloads/ApplicationInstallers/Control%2BX/GeomagicControlX_ReleaseNotes_v2025.0.1_en-US.pdf

## Audience And Default Layout

| Audience | Default entry point | Required density | May use Expert Layout |
| --- | --- | --- | --- |
| Recipe engineer | Setup, then Teach | One source, one selected step, one parameter editor | Yes |
| Inspection operator | Inspect | One current step and its decision | No by default |
| Quality reviewer | Review | Result list, selected result, report/export | No by default |
| Algorithm developer | Expert | Full source/result/contract/diagnostic state | Yes |

No role may receive a physical-thickness, calibration, uncertainty, Gauge R&R, or metrology claim unless its input and evidence contracts independently support it.

## Workspace Contracts

### Setup

Purpose: load and establish data context before teaching.

Visible:

- Source list with source name, type, unit, frame, and load state.
- One 3D Viewer with display style and color-map controls.
- Source/coordinate-frame summary and explicit validation warning.

Hidden:

- Inspection parameters, taught ROI editor, result metrics, Evidence Workbench, and run history.

Commands:

- Open source, select active source, set declared unit/frame, fit view, save project.

Exit condition:

- One valid active source is selected. No inspection runs automatically.

### Teach

Purpose: define one selected recipe step on the active source.

Visible:

- 3D Viewer with the selected source and current ROI/measurement overlay.
- Compact step list showing enabled state and selected step only.
- Selected tool editor only: for Thickness, grid ROI, limits, minimum samples, and `Teach ROI` / `Preview` commands.
- Context line for source, unit, frame, selected step, and explicit validation state.

Hidden:

- Other tool editors, published-result history, runner artifacts, global diagnostics, unrelated nominal/actual panels, and complete Data & Layers tree.

Commands:

- Add/select/delete step, teach ROI, edit selected parameters, explicit Preview, save recipe.

Exit condition:

- A successful explicit Preview enables Inspect. Editing any input invalidates the Preview and remains in Teach.

### Inspect

Purpose: let an operator verify and publish the current prepared result without searching through configuration panels.

Visible:

- 3D Viewer with the current result overlay.
- Current-step card with large Pass/Fail/Error state, one primary metric, compact secondary metrics, and tolerance context.
- `Publish Result` command when a valid Preview exists.
- Expandable details for the selected result only.

Hidden:

- Source loading controls, unrelated recipe-step parameters, runner report text, full evidence history, and calibration controls.

Commands:

- Preview again, publish, inspect selected overlay point/cell, return to Teach.

Exit condition:

- Publish creates a result entity without changing source geometry and opens that result in Review.

### Review

Purpose: compare published results and produce human-readable evidence.

Visible:

- Result grid with step, source, status, key metric, tolerance state, time, and report availability.
- Selected result's 3D overlay and metric details.
- Report/run-record actions, filtering, and result-to-Viewer linking.

Hidden:

- Teaching editor and source frame setup controls.

Commands:

- Select/filter result, open run record, export/open HTML/CSV, restore linked 3D view, return to Teach for a changed step.

Exit condition:

- None. Review is read-only for the published result; changes create a new Preview/Publish cycle.

### Calibrate

Purpose: retain the existing isolated calibration/repeatability workbench.

Visible:

- Study input, Calculate, values grid, chart, and calibration evidence only.

Hidden:

- Inspection recipe teaching and published result review.

Rule:

- Do not mix uncalibrated C3D raw-height inspection output with physical calibration conclusions.

### Expert Layout

Purpose: diagnose source/result contracts, docking behavior, Viewer/Runner evidence, and advanced display state.

Visible:

- Existing AvalonDock layout: Data & Layers, 3D Inspection View, Tool / Inspector, Evidence Workbench, Linked View, and diagnostics as available.

Rule:

- It is opt-in from a `View` or `Layout` command. Do not use it as the operator's initial layout.

## Visibility Matrix

| Surface | Setup | Teach | Inspect | Review | Calibrate | Expert |
| --- | --- | --- | --- | --- | --- | --- |
| 3D Viewer | Required | Required | Required | Required | Hidden | Required |
| Source list/frame | Required | Compact | Hidden | Source column only | Hidden | Required |
| Step list | Hidden | Required | Current step only | Result-step column | Hidden | Required |
| Tool editor | Hidden | Selected tool only | Hidden | Hidden | Hidden | Required |
| ROI/measurement overlay | Hidden | Required | Required | Selected result only | Hidden | Required |
| Result summary | Hidden | Preview compact | Required | Selected row | Hidden | Required |
| Result grid/history | Hidden | Hidden | Hidden | Required | Hidden | Optional |
| Evidence/Runner report | Hidden | Hidden | Hidden | Required | Calibration evidence only | Required |
| Display controls | Required | Compact | Hidden | Hidden | Hidden | Required |
| Diagnostics/contracts | Hidden | Hidden | Hidden | Hidden | Hidden | Required |

## Command Rules

- Global toolbar: Open, Save, Fit All, Fit Selection, Screenshot, View/Layout. Keep it short.
- Workspace toolbar: only commands valid for the active workspace and selected entity.
- Contextual commands use an icon when a familiar icon exists; labels remain for primary process actions such as `Preview` and `Publish`.
- A disabled command explains the missing prerequisite in the current workspace status line.
- Preview and Publish remain explicit. A visibility toggle, color-map change, dock change, or source/result selection must never execute inspection logic.

## State And MVVM Boundary

- The View owns only AvalonDock layout, visibility bindings, OpenGL/WPF input forwarding, and visual presentation.
- A Shell workspace ViewModel owns selected workspace, permitted panels, active source/step/result context, and navigation commands.
- Tool ViewModels own only their tool inputs, Preview state, and presentation summaries.
- Core/Data/Tools keep source, recipe, result, and Runner contracts independent of the current workspace.
- A published result is a first-class result entity. Review binds it; it is not recomputed from displayed text.

Implemented workspace state:

```text
InspectionWorkspace = Setup | Teach | Inspect | Review | Calibrate | Expert
WorkspaceLayoutSnapshot = active workspace + visible surfaces + selected context
```

Do not create a generic layout engine before the Thickness Teach slice requires it. Start with explicit workspace states backed by the existing Shell ViewModel.

## First Implementation Slice — Delivered

The delivered scope is deliberately limited to:

1. `Teach` as a Shell workspace using the existing C3D Thickness recipe.
2. The current Thickness ROI editor moved out of the default surface and retained in Expert.
3. Inspect reduced to the Viewer, selected-step status card, and Preview/Publish actions.
4. The full docked layout preserved behind `Expert`.
5. Current-source before/after screenshots plus explicit recipe save/reopen, Preview/Publish, pointer input, and Runner replay evidence.

The original layout decision was followed by a bounded local Warpage task only after the task-scoped View -> ViewModel -> Tool -> Viewer -> Shell pattern was proven by Thickness. Do not extend that result into multi-step history, physical calibration, or hardware integration without separate evidence.

## Acceptance Criteria

- A user can identify the active source, step, required action, and result state without opening a secondary dock.
- A user sees no unrelated inspector/tool parameters in Teach or Inspect.
- Each bounded C3D Thickness/Warpage recipe loads, teaches an ROI, previews, publishes, saves/reopens, and Runner-replays unchanged within its declared raw-height/display-frame contract.
- Review displays published result data without exposing recipe editing controls.
- Expert Layout preserves existing diagnostic information and can return to the default task workspace.
- Build, focused ViewModel checks, Viewer/Shell screenshots, and Runner comparison pass from the current source.

## Out Of Scope

- Camera, PLC, robot, and production-line control.
- A production kiosk or device-lockdown mode. The simplified operator layout is a foundation only.
- Physical calibration, measurement uncertainty, Gauge R&R, or metrology certification.
- A generic arbitrary dashboard framework.
