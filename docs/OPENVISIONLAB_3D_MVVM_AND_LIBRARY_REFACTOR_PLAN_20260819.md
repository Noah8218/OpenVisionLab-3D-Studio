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
   and rule preparation), M4b (Height Deviation apply/state orchestration), and
   M4c (Height Deviation recipe save ownership) are complete. M4d is the next
   rule/Preview or next recipe-family boundary.
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
