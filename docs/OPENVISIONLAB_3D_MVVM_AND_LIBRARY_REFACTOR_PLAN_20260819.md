# OpenVisionLab 3D MVVM and Library Refactor Plan

Date: 2026-08-19

Status: Superseded by `PL-0026` after the 2026-08-19 whole-repository audit

The original `PL-0025` checkpoints remain historical evidence for the concrete
owners they introduced. They do not prove repository-wide MVVM completion. The
current remaining work and low-cost-model execution order are recorded in
section 12 and `.proofline/issues/PL-0026.json`.

## 1. User goal and product boundary

Refactor the full OpenVisionLab 3D Studio codebase so stable responsibilities
have explicit View, ViewModel, Model, Command, Converter, Behavior, controller,
and reusable-library owners. Preserve observable product behavior and the
public Viewer hosting contract.

OpenVisionLab 3D Studio remains a local, file-first, deterministic rule-based
3D inspection workbench. The retained operator loop is:

```text
load -> source quality -> teach -> explicit Preview -> explicit Publish
     -> explicit Run -> evidence -> save/reopen
```

Camera acquisition, lighting, PLC, industrial I/O, robot, cloud, account,
deployment, and production-line control remain out of scope. Raw-height and
synthetic software evidence do not establish calibrated metrology, Gauge R&R,
or production approval.

## 2. Work contract

### Non-negotiable requirements

- Preserve explicit Preview, Publish, Run, and Validation actions.
- Preserve source/result separation and stable recipe, step, source, frame,
  unit, artifact, and content identities.
- Presentation, layout, visibility, and restored settings must not execute an
  inspection or mutate recipe, source, ROI, result, or selection state.
- New or changed numerical and geometric algorithms remain owned by the
  vendored OpenVisionLab Vision SDK and its typed Studio adapters.
- Keep `Core`, `Data`, `Tools`, `Runner`, and the planned `Reporting` project
  free from WPF, SharpGL, AvalonDock, and Window APIs.
- Preserve Viewer zoom, pan, orbit, picking, ROI teaching, rendering, docking,
  screenshots, localization, and the versioned Viewer host API.
- Do not introduce a DI container, mediator, event bus, speculative interface,
  or one-project-per-feature structure.
- Do not split a cohesive type solely because it is long. A partial file is not
  an architecture boundary.
- Preserve the existing PL-0024 dirty worktree and do not commit or push
  without separate owner authorization.

### Checkpoints

1. Shared Presentation command boundary.
2. Runtime-neutral Reporting boundary.
3. Thin Shell composition boundary.
4. Workbench and Viewer state-owner extraction.
5. MVVM View, Converter, and Behavior cleanup.
6. Verification, documentation, and vendor ownership cleanup.
7. Full structural qualification and durable closure.

### Verification plan

- Search for every old owner, import, direct call, subscription, and state
  field that each checkpoint intends to remove.
- Verify the new project-reference direction in both `.sln` and `.slnx`.
- Run `scripts/verify-code-structure.ps1` with D-backed report output.
- Run the smallest focused verification for each moved responsibility.
- Run the full Release solution build and `git diff --check` at every
  independently releasable checkpoint.
- If visible UI changes, capture current-build before/after Wide `1920 x 1040`
  and Compact `1280 x 760` evidence on the dynamically selected EXE monitor.
- If an R0 binary changes, rebuild the fixed package, refresh hashes, and rerun
  both R0 `-ValidateOnly` modes before handoff. Owner R0 remains external.

### Known risks

- Shell, Viewer, and Workbench currently expose broad public and internal
  surfaces. A big-bang move could break Viewer binary hosts or smoke routing.
- Run Record schema `1.9` output and privacy-safe support evidence are exact
  compatibility contracts; reporting movement must not normalize or rerun.
- PL-0024 changes are complete but currently uncommitted. Refactor changes must
  remain distinguishable and must not overwrite them.
- Existing command-line verification is compiled into product projects. Moving
  it changes launch and packaging contracts and therefore belongs late.

## 3. Current structural baseline

The inspected tree contains 477 C# files, approximately 142,420 C# lines, and
49 XAML files. The relevant ownership concentrations are:

| Runtime owner | Physical files | Approximate lines | Current concern |
| --- | ---: | ---: | --- |
| `ToolWorkbenchViewModel` | 33 partial files | 15,436 | authoring, execution, validation, results, and Viewer state share one runtime object |
| Viewer `MainWindowViewModel` | 21 partial files | 7,298 | scene, recipe, teaching, inspection, and presentation state share one runtime object |
| `OpenVisionThreeDViewerControl` | 30 partial files | 15,781 | WPF/OpenGL plus execution policy and smoke share one control |
| Shell `MainWindow` | 2 partial files | over 4,800 | composition, dialogs, layout, smoke, and native pointer automation share one Window |

The code already has substantial MVVM routing: 328 XAML Command or
CommandParameter attributes were found. The remaining 68 XAML event attributes
must be judged by responsibility rather than removed mechanically. Pointer,
rendering, docking, Window, dialog, and PropertyGrid lifecycle operations remain
valid View adapters.

## 4. Target ownership

### 4.1 Model

- `OpenVisionLab.ThreeD.Core`: runtime-neutral contracts, identities, metrics,
  overlays, results, frames, units, and Run Record contracts.
- `OpenVisionLab.ThreeD.Data`: file loading and immutable loaded data models.
- `OpenVisionLab.ThreeD.Tools`: deterministic recipe adapters, orchestration,
  validation, and product-owned policy around Vision SDK tools.
- Shell and Viewer feature folders may own presentation-only records. They must
  not duplicate Core, Data, or Tools domain models.

There will be no generic `Models.dll` or unbounded `Models` dumping folder.

### 4.2 View and ViewModel

- ViewModels own bindable state, selection, validation messages, commands,
  command enablement, and presentation values.
- Views own XAML, Window/dialog invocation, AvalonDock, PropertyGrid commit,
  SharpGL, pointer capture, hit testing, focus application, and screenshot
  capture.
- Cross-view state belongs to a concrete session or coordinator, not matching
  copies in two Views.
- Root ViewModels remain compatibility facades only when existing bindings or
  hosts require them. New work binds to the appropriate child owner.

### 4.3 Command

`OpenVisionLab.ThreeD.Presentation` owns the shared `RelayCommand`. Shell and
Viewer consume that implementation. The existing public
`OpenVisionLab.ThreeD.Viewer.RelayCommand` remains a delegating compatibility
surface until a separately approved major Viewer API change.

Add `AsyncRelayCommand` only when an existing command has real asynchronous
lifetime, cancellation, and exception ownership. Do not create one command
class per button.

### 4.4 Converter

Converters are pure, stateless presentation transforms such as boolean to
visibility or semantic status to brush. A conversion that needs recipe state,
services, mutation, or execution belongs in a ViewModel presentation property.
Feature-specific converters stay beside their feature; only two-consumer
converters move to the shared Presentation project.

### 4.5 Behavior

Attached Behaviors are allowed for repeated WPF-only interactions such as a
focus request, `ScrollIntoView`, or responsive width classification. They must
not own recipe state or invoke Preview, Publish, Run, or Validation. No external
Behaviors package is added until the native attached-property implementation is
proved insufficient.

OpenGL input, camera gestures, AvalonDock, Window chrome, and file dialogs are
not moved into Behaviors merely to make code-behind empty.

### 4.6 Controllers and coordinators

Concrete controllers own real application boundaries that do not fit a
ViewModel without importing WPF:

- Shell request/dialog subscription lifecycle;
- recipe open/save/unsaved-decision orchestration;
- Studio layout load/save and Window placement;
- linked Viewer presentation and host movement;
- smoke Window, monitor, pointer, and screenshot automation.

Create an interface only when a second implementation, external boundary, or
focused test double is actually needed.

## 5. Target dependency direction

```text
Core <- Data <- Tools
  ^       ^       ^
  |       |       +---- Reporting <- Runner
  |       |                    ^
  |       +--------------------+---- Shell
  |
Presentation <- Viewer <- Shell
                   ^
                   +---- ThreeDStudio

Docking.Controls ---------^ Shell only
Logging / Localization / MessageDialogs -> owning UI consumers
```

`Presentation` is WPF presentation infrastructure with no domain dependency.
`Reporting` is runtime-neutral and may depend on Core/Data/Tools only where the
record composition contract requires it.

## 6. Original PL-0025 implementation order (historical)

This order records the original checkpoint and is not the current handoff.
For lower-cost continuation, use section 12 and execute only the current
`PL-0026` milestone.

1. Shared Presentation command boundary | Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`
2. Runtime-neutral Reporting and exact schema/output parity | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
3. Shell request, recipe-dialog, layout, and smoke ownership | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
4. Workbench and Viewer state-owner extraction | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
5. View Command/Converter/Behavior cleanup | Recommended model: `gpt-5.6-sol` | Reasoning effort: `medium`
6. Verification project and PropertyGrid model navigation cleanup | Recommended model: `gpt-5.6-sol` | Reasoning effort: `medium`
7. Documentation and vendor policy qualification | Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`

The product-owner unaided Wide/Compact R0 remains a separate acceptance
priority. Prerequisite: owner operation and observer record. Recommended model:
none. Reasoning effort: none.

## 7. DLL and package policy

No tracked standalone third-party DLL currently requires a `lib` folder.
Active binary inputs remain versioned `.nupkg` files with SHA-256, source
commit, license, and README records under `third_party`.

If a future dependency is available only as a loose DLL, use:

```text
lib/<vendor>/<product>/<version>/<tfm-or-runtime>/
  Product.dll
  LICENSE.txt
  NOTICE.txt
  SHA256SUMS.txt
  README.md
```

Do not extract a NuGet package into `lib`, copy DLLs from `bin`/`obj`, or place
Windows system DLLs there. The BinaryHost sample's wildcard reference continues
to consume a generated and manifest-verified Viewer bundle.

`third_party/LibraryNoah` has no live product PackageReference. Its historical
packages require a separate provenance/legal audit and owner-approved archive
or removal decision. Do not mix that cleanup into a behavior refactor.

## 8. Historical PL-0025 checkpoint status

The states below record the focused `PL-0025` checkpoint as it was qualified.
They do not describe current whole-repository completion. Section 12 and
`PL-0026` own the remaining work after the broader audit.

| Milestone | State | Current evidence |
| --- | --- | --- |
| M1 Shared Presentation command boundary | Complete | `Presentation` owns `RelayCommand`; Viewer retains a delegating compatibility surface; focused command checks and Release build pass. |
| M2 Runtime-neutral Reporting boundary | Complete | `Reporting` owns ordered schema `1.9` identity, graph composition, and JSON output; Shell/Runner output checks and Release build pass. |
| M3 Thin Shell composition boundary | Complete | `StudioLayoutController`, `ShellRequestCoordinator`, `ShellEvidenceDialogController`, and `RecipeFileDialogService` own their existing boundaries; `ShellWorkbenchLifecycleController` now owns recipe-manager lifetime, C3D load/cancel state, recipe open/save/unsaved policy, source identity binding, and lifecycle smoke hooks. Release build (0 warnings/0 errors), structure guard 41/41, Shell smoke command-line 41/41, Workbench docking 87/87, old-owner search, and `git diff --check` pass. |
| M4 Workbench and Viewer state-owner extraction | Complete | `ViewerWorkspaceSession` owns layout and auxiliary-slot transitions. Existing `ViewerDisplaySettingsViewModel` owns point size, render-density selection/summary, C3D/LAZ/mesh/comparison sample budgets, and display revision. `ViewerCameraSession` owns current camera/projection state and saved-Perspective snapshot lifetime. `ViewerSelectionSession` owns selection mode, entity, pick coordinate, summary, and overlay visibility. `ToolWorkbenchTeachingCaptureSession` owns transient teaching-capture lifetime, owning step, progress, Apply readiness, additional-reference mode, and the atomic `ToolRecipeGridRectangle` ROI draft. `ToolWorkbenchRecipeSession` owns recipe schema/name/path/dirty state plus authored, storage, and source-binding validation results; root ViewModels retain normalization, persistence, execution invalidation, notifications, and composition. `ToolWorkbenchSourceSession` owns loaded source identity, source-binding provenance, opened-source snapshot, and source identity correction state. Structure guard 49/49, Display/Camera/Selection ViewModel 111/111, Viewer Teaching Capture 25/25, Profile 8/8, Inspection Workspace selection 64/64, Recipe Teaching 51/51, Workbench docking 87/87, Nominal/Actual ViewModel 71/71, and Release build 0/0 pass. |
| M5 View/Converter/Behavior cleanup | Complete | Added native Shell `ScrollIntoViewOnSelectionChangedBehavior` and applied it to recipe and validation step lists; duplicate View `SelectionChanged` handlers removed. Latest structure guard pass 49/49, latest Release build pass (0 warnings/0 errors), and `git diff --check` pass. |
| M6-M7 | Complete | Verification and ownership documentation was finalized, including DLL/vendor policy and completion lock. C6 and C7 evidence is recorded in `.proofline/issues/PL-0025.json` and this plan references no duplicated queue ownership. |

Current local evidence is stored under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260819-pl0025-mvvm-refactor\reports`.
This checkpoint changes structure but no visible UI, so a true before/after UI
comparison is not applicable. A current-build Viewer EXE screenshot and quality
report were still captured for the camera-session checkpoint. The product-owner
Wide/Compact R0 remains external.

## 9. Refactor proof: checkpoint M1

### Current structure

- Current responsibility owner: public `OpenVisionLab.ThreeD.Viewer.RelayCommand`.
- Current call path: Shell ViewModels construct the Viewer-owned command.
- Current dependency direction: Shell depends on Viewer for both Viewer hosting
  and generic command infrastructure.
- Current state/data owner: each command stores execute/can-execute delegates in
  the Viewer assembly.

### Intended structure

- New responsibility owner: `OpenVisionLab.ThreeD.Presentation.Commands.RelayCommand`.
- New call path: Shell ViewModels construct the Presentation command directly;
  the Viewer public command delegates to the same implementation.
- New dependency direction: Shell and Viewer each depend on Presentation.
- New state/data owner: the Presentation command owns execute/can-execute and
  notification state; the Viewer type owns compatibility only.

### Required structural proof

1. Presentation is present in `.sln` and `.slnx` and has no project dependency.
2. Shell aliases `RelayCommand` to Presentation, not Viewer.
3. Viewer public `RelayCommand` retains its members but delegates storage and
   execution to Presentation.
4. The structure guard, relevant builds, command-focused checks, and diff
   hygiene pass.

## 10. Refactor proof: checkpoints M2 and M3

- `OpenVisionLab.ThreeD.Reporting` is runtime-neutral and is the only owner of
  shared ordered Run Record identity, schema `1.9` graph construction, and JSON
  serialization. Shell and Runner no longer construct that graph independently.
- `MainWindow` no longer owns Studio layout store/profile state, individual
  request-handler subscription fields, evidence/Run Record dialog creation, or
  recipe Save/Open dialog construction.
- The structure guard proves the new owners and absence of the named former
  ownership. Release builds and focused Shell/Runner checks prove compatibility.
- `ShellWorkbenchLifecycleController` owns the named recipe/source lifecycle
  state, unsaved-change decisions, recipe-manager lifetime, and lifecycle smoke
  hooks extracted in `PL-0025`. The broader audit later found other smoke
  orchestration still in `MainWindow`; section 12 supersedes the former broad
  thin-Shell conclusion.
- The structure guard proves the new controller is constructed and disposed,
  the former lifecycle fields are absent, and the moved Save/Open/load/smoke
  policy is no longer implemented in `MainWindow`.
- M4 has started with a concrete state-owner boundary rather than another
  partial ViewModel file: `ViewerWorkspaceSession` owns layout and auxiliary
  slot transitions, while `ToolWorkbenchViewModel` maps recipe/artifact
  candidates and synchronizes the independent inspection-selection session.
  The existing `ViewerDisplaySettingsViewModel` now owns point size, render
  density and its sample budgets, summary, and display revision. The Viewer
  root ViewModel retains the existing public properties as delegating binding
  compatibility only. Focused checks pass without changing the visible UI or
  executing inspection.
- `ViewerCameraSession` now owns the current yaw, pitch, distance, target,
  projection, orthographic height, and saved-Perspective snapshot. Scene
  actions call this session for snapshot save/restore lifetime, while the root
  ViewModel preserves its public camera properties and their existing binding
  notifications. The former root camera fields and `SavePerspectiveCamera`
  method are absent. The current Display/Camera verification passes 109/109,
  including Top-view save and Perspective restore, and the actual Viewer EXE
  passed screenshot quality on the selected `DISPLAY2` monitor.
- `ViewerSelectionSession` now owns selection mode, selected entity, pick
  coordinate, selection summary, and overlay visibility. The root ViewModel
  retains mode-to-summary and status policy because it depends on several
  feature-specific measurement summaries, and its public binding surface is
  unchanged. The five former root fields are absent. Current verification
  passes Display/Camera/Selection 111/111, Profile 8/8, and Teaching Capture
  25/25 without invoking inspection state.
- `ToolWorkbenchTeachingCaptureSession` now owns the transient capture's active
  state, owning recipe-step ID, captured/required point counts, Apply readiness,
  additional Level Surface reference mode, and the atomic
  `ToolRecipeGridRectangle` draft. `ToolWorkbenchViewModel` retains recipe and
  source-bound validation, Viewer event coordination, and property/command
  notification. The eleven former lifecycle/draft fields are absent. Current
  checks pass structure 47/47, Inspection Workspace 64/64, Recipe Teaching
  50/50, Workbench docking 87/87, and Release build 0/0.
- `ToolWorkbenchRecipeSession` now owns recipe schema version, name, path,
  dirty state, authored/storage validation, and source-binding validation
  results. `ToolWorkbenchViewModel` retains input normalization, Save/Open
  policy, ordered-Run invalidation, property/command notification, and source
  identity validation. The seven former root fields are absent. Current checks
  pass structure 48/48, Recipe Teaching 51/51 including save/reopen session
  state, Inspection Workspace 64/64, Workbench docking 87/87, and Release
  build 0 warnings/0 errors.
- M5 has started with a repeated WPF-only interaction boundary:
  `ScrollIntoViewOnSelectionChangedBehavior` owns ListBox selection scrolling,
  and both recipe/validation Views consume it without owning duplicate event
  handlers. The behavior cannot mutate recipe, validation, or execution state.

## 11. Durable tracking

- Historical checkpoint issue: `.proofline/issues/PL-0025.json`.
- Current follow-up issue: `.proofline/issues/PL-0026.json`.
- Current queue owner: `OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`.
- Short current handoff: `OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`.
- Current code map: `CODEBASE_STRUCTURE.md`.

Each completed checkpoint receives current evidence and an issue milestone
update. This plan owns approved refactor scope and proof conditions; it does not
replace the master backlog as the mutable project queue.

## 12. Post-closure audit correction and remaining work

The 2026-08-19 whole-repository audit found that the original completion claim
was broader than its structure guard. The current `49/49` guard and Release
build still pass, but they verify selected owners and dependency rules rather
than whole-type cohesion or every View-to-ViewModel transition.

### Current observed gaps

1. `OpenVisionThreeDViewerControl.Recipes.cs` still owns recipe open/save,
   document dispatch, recipe validation, direct rule evaluation, and automatic
   Preview after recipe load. Dialog display and rendering are valid View
   responsibilities; recipe workflow and execution are not.
2. `ToolWorkbenchViewModel` remains one 33-file partial type with 15,428 lines,
   101 public commands, 50 public events, direct Tool execution, and many
   cancellation/output/published state groups. The extracted sessions are real
   improvements but do not make the root an independent composition facade.
3. `MainWindow.xaml.cs` remains 3,837 lines and retains command-line smoke setup,
   smoke scenario execution, pointer capture, dialog capture, and verification
   helpers in addition to WPF composition.
4. `ResultsWorkspaceView` and `RecipePipelineReviewView` navigation ownership
   is corrected by PL-0026 M2: child ViewModels own section state, commands,
   and validation selection side effects. The Views retain only WPF layout,
   binding, and thin external request adapters.
5. Ten Tool Lab windows repeat step identity, selected-step activation, and
   Loaded/Activated lifecycle code; seven contain the same `ActivateLabStep`
   implementation.
6. Verification code remains compiled into product assemblies. This is not a
   runtime behavior defect, but it obscures production ownership and keeps
   smoke code coupled to large WPF types.

### Low-cost-model execution order

Run one milestone at a time. Do not ask a lower-cost model to refactor the full
Viewer or Workbench in one turn.

1. M1 audit/ledger/document correction | Complete in documentation only |
   Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`.
2. M2 move Results and Validation navigation state to child ViewModels and
   commands | Complete: focused checks, structure guard, Release build, and
   Wide/Compact current-build evidence recorded in PL-0026.
3. M3 move one coherent Shell smoke scenario group at a time from `MainWindow`
   into `Verification/Smoke`; complete for the Source Quality workspace smoke.
   Focused checks, structure guard, Shell and solution Release builds, and
   current-build Wide/Compact exact-source Source Quality screenshots pass.
4. M4 move Viewer recipe workflow in small slices: validation, load/apply,
   save, then rule/Preview orchestration. M4a (Height Deviation source loading
   and rule preparation), M4b (Height Deviation apply/state orchestration),
   M4c (Height Deviation recipe save ownership), and M4d (rule/Preview
   orchestration) are complete.
   Preserve file-dialog and rendering adapters plus public host compatibility |
   Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium` per slice.
5. M5 extract Workbench execution owners one tool family at a time. Each owner
   must own its CancellationTokenSource, running/stale/output/published state,
   Preview command, and focused verification; do not create another partial |
   Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium` per tool
   family.
6. M6 consolidate only the repeated Tool Lab step/lifecycle owner and then move
   verification code behind explicit verification project boundaries where the
   existing smoke entry contract allows it | Recommended model:
   `gpt-5.6-terra` | Reasoning effort: `medium`.
7. M7 expand the structure guard to reject the old direct calls and owners,
   run focused checks plus Release build and diff hygiene, and refresh fixed R0
   validation if the binary changed | Recommended model: `gpt-5.6-terra` |
   Reasoning effort: `medium`.

### PL-0026 M2 completion evidence - 2026-08-19

Current responsibility owners were the two Views' private section fields and
click handlers. The intended owners are
`ResultsWorkspaceViewModel` and `RecipePipelineReviewValidationViewModel` in
`ViewModels/Workbench/WorkspaceNavigationViewModels.cs`. The call path now runs
from XAML radio binding and the shared `CheckedCommandBehavior` to a child
ViewModel command; the View only applies WPF visibility/grid layout and exposes
thin compatibility request methods for existing Shell smoke/docking callers.
Validation filter reset and failure-sample selection moved with the state into
the Validation child ViewModel. The former View-owned enum/state mutations and
navigation click handlers are absent.

Evidence: `D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
20260819-pl0026-m2/` contains structure 49/49, Workbench Docking 87/87,
Validation Set 84/84, Release build 0 warnings/0 errors, `git diff --check`,
monitor topology, and current-build Wide/Compact closest-baseline and
post-change Results screenshots. The closest baseline is explicitly not a
true pre-edit capture because the refactor was already applied before the UI
evidence pass.

### PL-0026 M3 completion evidence - 2026-08-19

The selected coherent group was the Shell Source Quality workspace smoke. The
former owner was `MainWindow.xaml.cs` and its private
`RunSourceQualitySmokeAsync` method, which combined the readiness wait,
view-only navigation assertion, no-execution boundary checks, and report
serialization. The new owner is
`Verification/Smoke/ShellSourceQualitySmoke.cs`. `MainWindow` now retains only
the command-line invocation and failure callback wiring; the former method and
report construction are absent.

Evidence: `D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
20260819-pl0026-m3/` contains structure `51/51`, Source Quality ViewModel
verification `18/18`, Workbench Docking `87/87`, Validation Set `84/84`, Shell
and solution Release builds with `0` warnings / `0` errors, and
current-build exact-source Source Quality smoke reports/screenshots for Wide
`1920 x 1040` and Compact `1280 x 760`. Both reports record
`viewOnly=true|recipeChanged=false|inspectionRun=false`; screenshot quality was
accepted on attempt 1. The separate asynchronous loader timing path was not
used as the M3 acceptance gate because its existing same-source load report
recorded zero dispatcher ticks; the moved Source Quality path itself passed in
the actual EXE. `git diff --check` passed after the documentation update.

M4a, M4b, and M4c are complete for the Height Deviation load/apply/save path.
M4d is now the next bounded slice: move rule/Preview orchestration or select
the next recipe-family boundary while preserving file-dialog/rendering
adapters and public host compatibility. Recommended model: `gpt-5.6-terra`;
reasoning effort: `medium`.

### PL-0026 M4a checkpoint evidence - 2026-08-19

The former owner was `OpenVisionThreeDViewerControl.Recipes.cs` in
`ApplyHeightDeviationRecipe`: it resolved the recipe-relative source path,
loaded the C3D grid, and prepared the existing Height Deviation rule result
before updating the ViewModel. The new owner is
`Recipes/HeightDeviationRecipeLoadPlan.cs`, a non-WPF typed plan that retains
recipe identity, resolved source path, loaded grid, and controlled preview
result. The View now consumes that plan and retains only state application,
ROI/optional-step orchestration, status text, and rendering compatibility.

Evidence: `D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
20260819-pl0026-m4/` contains Viewer recipe load-plan verification `6/6`,
structure guard `52/52`, Workbench Docking `87/87`, Validation Set `84/84`,
Shell/solution Release builds with `0` warnings / `0` errors, and current-build
recipe-load EXE screenshots for Wide `1920 x 1040` and Compact `1280 x 760`.
Both screenshot quality reports were accepted on attempt 1. The verification
also proves a missing source fails during plan creation. `git diff --check`
passed. This is an M4a checkpoint, not completion of the broader M4 workflow.

### PL-0026 M4b checkpoint evidence - 2026-08-19

The former owner was the remaining body of `ApplyHeightDeviationRecipe` in
`OpenVisionThreeDViewerControl.Recipes.cs`: it directly sequenced ViewModel
state clearing, preview-result assignment, recipe identity/alignment, ROI
application, optional Plane Flatness/Volume/Cross-section state, and status
text. The new owner is the non-WPF
`Recipes/HeightDeviationRecipeApplyCoordinator.cs`. The View now assigns its
rendering sample and supplies only the existing sample-status, ROI, and
optional-preview callbacks.

Evidence: `D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
20260819-pl0026-m4b/` contains recipe load/apply verification `7/7`, structure
guard `53/53`, Workbench Docking `87/87`, Validation Set `84/84`, Shell/solution
Release builds with `0` warnings / `0` errors, and current-build Wide/Compact
recipe-apply EXE screenshots. Both screenshot quality reports were accepted on
attempt 1. `git diff --check` passed. This is an M4b checkpoint; M4c then
completed save ownership, while the broader M4 workflow remains open for M4d
rule/Preview or next recipe-family slices.

### PL-0026 M4c checkpoint evidence - 2026-08-19

The former owner was `SaveCurrentHeightDeviationRecipe` in
`OpenVisionThreeDViewerControl.Recipes.cs`: it constructed the recipe,
calculated the recipe-relative source mapping, persisted the document, and
updated the saved-state/status. The new owner is the non-WPF
`Recipes/HeightDeviationRecipeSaveCoordinator.cs`. The View retains only
WPF-dependent recipe validation, source/ROI input collection, the validation
message adapter, and the call to the coordinator.

Evidence: `D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
20260819-pl0026-m4c/` contains recipe workflow verification `12/12`, structure
guard `54/54`, Workbench Docking `87/87`, Validation Set `84/84`, Shell and
solution Release builds with `0` warnings / `0` errors, and current-build
recipe-save EXE screenshots for Wide `1920 x 1040` and Compact `1280 x 760`.
Both screenshot quality reports were accepted on attempt 1; both saved JSON
documents parsed successfully and were reloaded by the workflow verifier.
`git diff --check` passed. This is an M4c checkpoint; the broader M4 workflow
remains open for M4d rule/Preview or next recipe-family ownership.

### PL-0026 M5 Datum Plane Deviation checkpoint evidence - 2026-08-20

The former owner was
`ToolWorkbenchViewModel.DatumPlaneDeviationExecution.cs`: it held the
CancellationTokenSource, Preview output, published-output dictionary,
running/stale/published state, input resolution, direct Tool execution, and
Preview/Publish lifecycle. The new owner is
`ToolWorkbenchDatumPlaneDeviationExecutionOwner`. The root ViewModel now
retains public compatibility bindings plus explicit log, display, and property
notification callbacks. The former partial contains no cancellation state,
published dictionary, or direct `ToolRecipeDatumPlaneDeviationExecution` call.

Focused verification passed Datum Plane Deviation `12/12`, Remove Outlier
Pixels `14/14`, and Level Surface `17/17`. The focused run found that the two
earlier preparation owners' selected-tool delegates recursively called their
own compatibility properties; their composition wiring now compares the
selected Tool ID directly. The expanded structure guard passes `55/55`, and
the Release solution builds with zero warnings and errors. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
20260820-pl0026-m5-datum-owner/`. The refreshed fixed R0 hashes and both
Wide/Compact `-ValidateOnly` checks pass on `\\.\DISPLAY2`; the unaided owner
runs remain external. M5 remains open for the other direct execution families;
Re-grid Height Field is the next smallest bounded owner candidate. Recommended
model: `gpt-5.6-terra`; reasoning effort: `medium`.

### PL-0026 M5 Re-grid Height Field checkpoint evidence - 2026-08-20

The former owner was
`ToolWorkbenchViewModel.RegridHeightFieldExecution.cs`: it held the
CancellationTokenSource, Preview output, published-output dictionary,
running/stale/published state, A2 input resolution, coverage-gated Publish
policy, and direct `ToolRecipeRegridHeightFieldExecution` calls. The new owner
is `ToolWorkbenchRegridHeightFieldExecutionOwner`. The root ViewModel now
retains dependency composition, public compatibility bindings, publication
notification, property notifications, command refresh, and artifact
projection. The former partial contains no cancellation state, published
dictionary, or direct Re-grid execution/route-validation call.

Focused Workbench verification passes `13/13`, including no implicit A2
execution, explicit Preview, Tools-output identity, exact Preview Publish,
artifact state, draft-versus-Apply behavior, stale evidence, and published
registry invalidation. The expanded structure guard passes `56/56`; the
Release solution builds with zero warnings and errors; and `git diff --check`
passes. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-regrid-owner/`. The refreshed fixed R0 package passes both Wide and
Compact `-ValidateOnly` on `\\.\DISPLAY2`; the unaided owner runs remain
external. This is an M5 checkpoint, not M5 completion. Three-Point Plane is
the next smallest direct-execution owner with an existing focused Workbench
verification seam. Recommended model: `gpt-5.6-terra`; reasoning effort:
`medium`.

### PL-0026 M5 3-Point Plane checkpoint evidence - 2026-08-20

The former owner was
`ToolWorkbenchViewModel.ThreePointPlaneExecution.cs`: it held the
CancellationTokenSource, Preview output, published-output and stale-output
registries, running/stale/published state, ordered-pick presentation,
recipe-relative input preparation, downstream Datum invalidation, display
requests, and direct `ToolRecipeThreePointPlaneExecution` calls. The new owner
is `ToolWorkbenchThreePointPlaneExecutionOwner`. The root ViewModel now owns
only dependency composition and property/command projection, while the former
partial retains its public events, event-argument contract, and compatibility
delegates.

Focused 3-Point Plane Workbench verification passes `11/11`; the downstream
Datum Plane Deviation regression passes `12/12`; and the expanded structure
guard passes `57/57`. The Release solution builds with zero warnings and
errors, `git diff --check` passes, and refreshed Wide/Compact fixed-package
`-ValidateOnly` checks pass on `\\.\DISPLAY2` without launching the
application. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-three-point-plane-owner/`. This is an M5 checkpoint, not M5
completion. Two-Point Line is the next smallest remaining owner with an
existing focused Workbench verification seam. Recommended model:
`gpt-5.6-terra`; reasoning effort: `medium`.

### PL-0026 M5 Two-Point Line checkpoint evidence - 2026-08-20

The former owner was
`ToolWorkbenchViewModel.TwoPointLineExecution.cs`: it held the
CancellationTokenSource, Preview output, published-output and stale-output
registries, running/stale/published state, selection presentation,
recipe-relative input preparation, downstream Line Intersection invalidation,
display requests, and direct `ToolRecipeTwoPointLineExecution` calls. The new
owner is `ToolWorkbenchTwoPointLineExecutionOwner`. The root ViewModel now
owns dependency composition, line-output composition across the Two-Point and
Line Fit producers, and property/command projection, while the former partial
retains its public events, event-argument contract, and compatibility
delegates.

Focused Two-Point Line Workbench verification passes `16/16`; the downstream
Line Intersection regression passes `23/23`; and the expanded structure guard
passes `58/58`. The Release solution builds with zero warnings and errors,
`git diff --check` passes, and refreshed Wide/Compact fixed-package
`-ValidateOnly` checks pass on `\\.\DISPLAY2` without launching the
application. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-two-point-line-owner/`. This is an M5 checkpoint, not M5 completion.
Line Intersection is the next smallest remaining direct-execution owner with
an existing focused Workbench verification seam. Recommended model:
`gpt-5.6-terra`; reasoning effort: `medium`.

### PL-0026 M5 Line Intersection checkpoint evidence - 2026-08-20

The former owner was
`ToolWorkbenchViewModel.LineIntersectionExecution.cs`: it held the
CancellationTokenSource, Preview output, published-output registry,
running/stale/published state, published-line input readiness and summaries,
downstream Landmark readiness refresh, display requests, and direct
`ToolRecipeLineIntersectionExecution` calls. The new owner is
`ToolWorkbenchLineIntersectionExecutionOwner`. The root ViewModel now owns
dependency composition, public event compatibility, published-line
composition across Two-Point Line and Line Fit, and property/command
projection. The former partial retains only compatibility delegates and its
public event-argument contract.

Focused Line Intersection Workbench verification passes `23/23`; the upstream
Two-Point Line regression, including downstream corner stale invalidation,
passes `16/16`; and the expanded structure guard passes `59/59`. The Release
solution builds with zero warnings and errors, `git diff --check` passes, and
refreshed Wide/Compact fixed-package `-ValidateOnly` checks pass on
`\\.\DISPLAY2` without launching the application. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-line-intersection-owner/`. This is an M5 checkpoint, not M5
completion. Line Fit is the next bounded owner because the smaller remaining
XYZ Affine and Landmark direct-execution families do not have focused
Workbench verifiers while Line Fit does. Recommended model:
`gpt-5.6-terra`; reasoning effort: `medium`.

### PL-0026 M5 Line Fit checkpoint evidence - 2026-08-20

The former owner was `ToolWorkbenchViewModel.LineFitExecution.cs`: it held the
CancellationTokenSource, Preview output, published-output registry,
running/stale/published state, upstream EdgePointSet readiness, diagnostic
selection command and state, residual-plot transformation, downstream Line
Intersection invalidation, display requests, and direct
`ToolRecipeLineFitExecution` calls. The new owner is
`ToolWorkbenchLineFitExecutionOwner`. The root ViewModel now owns dependency
composition, smoke-only parameter configuration, public event compatibility,
and property/command projection. The former partial retains only those smoke
and compatibility surfaces.

Focused Line Fit Workbench verification passes `14/14`, including exact
Preview/Publish identity, presentation-only diagnostic selection, residual
plot population, stale clearing without rerun, and upstream Edge preservation.
The downstream Line Intersection regression passes `23/23`; the expanded
structure guard passes `60/60`; and the Release solution builds with zero
warnings and errors. `git diff --check` and refreshed Wide/Compact
fixed-package `-ValidateOnly` checks pass on `\\.\DISPLAY2` without launching
the application. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-line-fit-owner/`. This is an M5 checkpoint, not M5 completion. Height
Difference Edge is the next bounded owner because the smaller remaining XYZ
Affine and Landmark families do not have focused Workbench verifiers while
Height Difference Edge does. Recommended model: `gpt-5.6-terra`; reasoning
effort: `medium`.

### PL-0026 M5 Height Difference Edge checkpoint evidence - 2026-08-20

The former owner was
`ToolWorkbenchViewModel.HeightDifferenceEdgeExecution.cs`: it held the
CancellationTokenSource, Preview output, published-output registry,
running/stale/published state, Filter readiness and display-path resolution,
parameter presentation, downstream Line Fit invalidation, display requests,
and direct `ToolRecipeHeightDifferenceEdgeExecution` calls. The new owner is
`ToolWorkbenchHeightDifferenceEdgeExecutionOwner`. The root ViewModel now owns
dependency composition, smoke-only selection setup, public event
compatibility, and property/command projection. The former partial retains
only those smoke and compatibility surfaces.

Focused Height Difference Edge Workbench verification passes `11/11`,
including explicit upstream Filter Preview/Publish gating, exact
Preview/Publish identity, exact display input path, headless output-hash
parity, stale-without-rerun behavior, and ordered Run blocking. The downstream
Line Fit regression passes `14/14`; the expanded structure guard passes
`61/61`; and the Debug and Release solutions build with zero warnings and
errors. The refreshed Wide/Compact fixed-package `-ValidateOnly` checks pass
without launching the application. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-height-difference-edge-owner/`. This is an M5 checkpoint, not M5
completion. Height Measurement is next because it has an existing focused
Workbench verifier; the smaller remaining XYZ Affine and Landmark families
require a focused seam first. Recommended model: `gpt-5.6-terra`; reasoning
effort: `medium`.

### PL-0026 M5 Height Measurement checkpoint evidence - 2026-08-20

The former owner was `ToolWorkbenchViewModel.MeasurementExecution.cs`: it
held cancellation, Preview output, running/stale/published state, raw or
Published Re-grid input resolution, completeness presentation updates, and
direct `ToolRecipeHeightMeasurementExecution` calls. The new owner is
`ToolWorkbenchHeightMeasurementExecutionOwner`. The root ViewModel now owns
dependency composition and explicit presentation/property refresh callbacks;
the former partial retains the ordered dual-ROI teaching workflow and public
compatibility projection.

Final-source Debug and Release Height Measurement Workbench verification pass
`56/56`. Artifact Navigator passes `32/32` after its stale expectation was
corrected to preserve the product contract that downstream readiness changes
only after explicit upstream Publish, not Preview. The expanded structure
guard passes `62/62`, and Debug and Release solution builds complete with zero
warnings and errors. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-height-measurement-owner/`. This is an M5 checkpoint, not M5
completion. The next bounded slice is a focused verification seam for the XYZ
Affine Solve/Apply pipeline followed by its execution-owner extraction;
Landmark Correspondence follows before M7. Recommended model:
`gpt-5.6-terra`; reasoning effort: `medium`.

### PL-0026 M5 XYZ Affine Solve/Apply checkpoint evidence - 2026-08-20

The former owners were
`ToolWorkbenchViewModel.XYZAffineSolveExecution.cs` and
`ToolWorkbenchViewModel.XYZAffineApplyExecution.cs`: together they held two
cancellation tokens, Preview outputs, published registries,
running/stale/published state, routed input resolution, A1-to-A2 invalidation,
downstream Re-grid clearing, and direct `ToolRecipeXYZAffine*Execution` calls.
The new `ToolWorkbenchXyzAffineExecutionOwner` owns the cohesive A1 Solve ->
A2 Apply lifecycle. The root ViewModel supplies explicit Landmark, source,
notification, and Re-grid callbacks; the former partials contain compatibility
projection only.

Final-source Debug and Release XYZ Affine Workbench verification pass `15/15`,
including no implicit upstream execution, explicit Solve and Apply
Preview/Publish, Workbench/Tools hash parity, draft-only no-execution, and
explicit parameter-Apply invalidation of A1/A2 without rerun. Re-grid passes
`13/13`, Artifact Navigator passes `32/32`, the structure guard passes
`63/63`, and Debug and Release solution builds complete with zero warnings and
errors. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-xyz-affine-owner/`. This is an M5 checkpoint, not M5 completion.
Landmark Correspondence was selected next. A current direct-call search after
that checkpoint found one older M5 boundary still incomplete: the existing
Filter owner owns state and commands, but the root Filter partial still owns
the explicit Preview body and direct Tools call. M7 therefore remains after
that corrected Filter boundary. Recommended model: `gpt-5.6-terra`; reasoning
effort: `medium`.

### PL-0026 M5 Landmark Correspondence checkpoint evidence - 2026-08-20

The former owner was
`ToolWorkbenchViewModel.LandmarkCorrespondenceExecution.cs`: it held the
CancellationTokenSource, Preview output, published-output registry,
running/stale/published state, published-CornerAnchor input readiness,
downstream XYZ invalidation, display requests, and direct
`ToolRecipeLandmarkCorrespondenceExecution` calls. The new owner is
`ToolWorkbenchLandmarkCorrespondenceExecutionOwner`. The root ViewModel now
supplies source, recipe, selection, upstream publication, notification, and
downstream callbacks; the former partial retains compatibility projection and
the public display event contract only.

Final-source Debug and Release Landmark Correspondence Workbench verification
pass `13/13`, including four-Published-CornerAnchor gating, explicit Preview,
Workbench/Tools hash parity, exact Preview Publish, typed artifact state,
draft-only no-execution, and explicit row-update stale invalidation. Debug and
Release Line Intersection pass `23/23`, XYZ Affine pass `15/15`, Artifact
Navigator passes `32/32`, the structure guard passes `64/64`, and the Debug
and Release solution builds complete with zero warnings and errors. Refreshed
Wide/Compact fixed-package `-ValidateOnly` checks pass without launching the
application. Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-landmark-correspondence-owner/`.

This is a complete Landmark checkpoint, not M5 completion. The corrected
Filter boundary below supersedes its former Filter next-action sentence.

### PL-0026 M5 Filter execution checkpoint evidence - 2026-08-20

The former owner was `ToolWorkbenchViewModel.FilterExecution.cs`: although an
existing owner stored Filter state and global command objects, the partial
still mutated that state through setters and held the cancellation token,
explicit Preview body, direct `ToolRecipeFilterExecution.Execute` call,
Publish, and stale/clear downstream lifecycle. The existing
`ToolWorkbenchFilterExecutionOwner` now owns those responsibilities. The root
ViewModel supplies recipe/source readiness, logging/display, and downstream
Edge callbacks; its partial retains selected-step routing, Ordered Run, kernel
authoring, source-display composition, and read-only compatibility projection.

Final-source Debug and Release Height Difference Edge Workbench verification
pass `12/12`, including the new Filter-parameter change assertion that marks
both Filter and downstream Edge stale without execution. Artifact Navigator
passes `32/32`, Line Fit `14/14`, Line Intersection `23/23`, and the expanded
structure guard passes `65/65`. Debug and Release solution builds complete with
zero warnings and errors. The refreshed nine-input R0 hashes pass Wide and
Compact `-ValidateOnly` on `\\.\DISPLAY2` without launching the application.
Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-filter-execution-owner/`. No UI, visible text, layout, or theme changed,
so no before/after screenshot was required. No tracked or loose DLL was found,
so `lib/` remains intentionally absent.

This is a complete Filter checkpoint, not M5 completion. The M7 former-owner
search found the remaining Surface Match Experiment cancellation and candidate
Preview/Publish lifecycle in
`ToolWorkbenchViewModel.SurfaceMatchExperiment.cs`, plus Validation Set
cancellation and direct execution in `ToolWorkbenchViewModel.ValidationSet.cs`.
Extract Surface Match Experiment next using its existing parity verifier, then
Validation Set, before M7. Recommended model: `gpt-5.6-sol`; reasoning effort:
`medium`.

### PL-0026 M5 Surface Match Experiment checkpoint evidence - 2026-08-20

The former owner was `ToolWorkbenchViewModel.SurfaceMatchExperiment.cs`: it
held candidate cancellation, direct shared-executor Preview, exact no-rerun
Publish, Published/Candidate display selection, commands, status, and
stale/discard/load/clear lifecycle even though the existing
`SurfaceMatchExperimentSession` already owned Published/Candidate/Stale data.
That existing session now owns the complete candidate execution and state
lifecycle. The root supplies selected-step and pending-draft inputs, published
evidence application, display/log callbacks, binding projection, localization,
and the public display event; it no longer owns the moved token, mutable state,
or direct executor call.

Final-source Debug and Release Surface Match parity verification pass `23/23`.
The checks cover temporary candidate Preview, published and recipe
preservation, presentation-only display switching, exact Publish without a
rerun, stale restoration after parameter changes, discard behavior, and
save/reopen non-persistence without automatic execution. The expanded
structure guard passes `66/66`, and Debug and Release solution builds complete
with zero warnings and errors. Refreshed Wide and Compact fixed-package
`-ValidateOnly` checks pass on `\\.\DISPLAY2` without launching the
application. No visible UI, text, layout, or theme changed, so no screenshot
was required. Tracked and loose DLL counts are zero, so `lib/` remains absent.
Evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-surface-match-experiment-owner/`.

This is a complete Surface Match Experiment checkpoint, not M5 completion.
The remaining Workbench-root execution family is Validation Set cancellation
and direct execution in `ToolWorkbenchViewModel.ValidationSet.cs`. Extract it
before M7. Recommended model: `gpt-5.6-sol`; reasoning effort: `medium`.

### PL-0026 M5 Validation Set and M7 closure evidence - 2026-08-21

`ToolWorkbenchValidationSetExecutionOwner` is the final M5 concrete owner. It
owns the cancellation source, running lifetime, and direct normal,
development-only, and Held-out `ToolRecipeValidationSetExecution` calls. The
root ViewModel retains sample roles, threshold Review/Apply policy, evidence,
persistence, localization, and command/property projection. Former-owner
searches confirm that `ToolWorkbenchViewModel.ValidationSet.cs` no longer owns
the moved cancellation field, mutable running field, or direct execution call.
The retained root Ordered Run call is the deliberate full-recipe composition
boundary, not a tool-family ownership leak.

Final-source Debug and Release Validation Set verification passes `86/86`,
including owner cancellation and idle-state restoration. Workbench Docking
passes `87/87`, Inspection Workspace `64/64`, Recipe Manager/PropertyGrid
`52/52`, Run Log Retention `6/6`, and Shell command-line routing `41/41`.
Debug and Release solution builds complete with zero warnings and errors; the
expanded structure guard passes `67/67`; tracked and loose DLL counts are both
zero; and Wide/Compact refreshed fixed-package `-ValidateOnly` passes on
`\\.\DISPLAY2` without launching the application. No UI, text, layout, or
theme changed in this final slice, so no screenshot was required.

M1-M7 are complete and `PL-0026` is closed for its bounded contract. Evidence
is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/
pl-0026-validation-set-execution-owner/`. This does not claim that every large
partial type should be deleted, that owner R0 passed, or that hosted CI/release
qualification completed. No dependency-ready software slice is selected after
this closure. Product-owner unaided Wide/Compact R0 is the next acceptance
priority and requires owner operation, not model execution.

### Per-milestone completion rule

A milestone is complete only when the former owner no longer contains the moved
state, direct call, validation, or lifecycle implementation; the new concrete
owner has a focused verification seam; the structure guard rejects regression;
the Release solution builds with zero warnings and errors; and
`git diff --check` passes. A partial-file move or delegating wrapper without
state/call removal does not close a milestone.

Audit evidence:
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260819-final-architecture-audit\`.
No loose or tracked DLL exists, so `lib/` must remain absent until a future
unavoidable DLL-only dependency is accepted with license, provenance, and
SHA-256 records.
