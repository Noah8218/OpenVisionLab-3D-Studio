# OpenVisionLab 3D code-structure refactor checkpoint

Date: 2026-07-25

## Status

Complete for the bounded Shell Smoke, Workbench catalog, Shell Tool Lab
composition, Workbench PropertyGrid session, and Viewer recipe-file
and inspection-session responsibility splits, plus Workbench/Viewer teaching
coordination, asynchronous C3D-load Smoke, and new/open recipe lifecycle Smoke
scenarios.

This checkpoint is a structural refactor. It does not change inspection
algorithms, recipe semantics, Viewer rendering, teaching geometry, or the
explicit Preview/Publish/Run boundary.

## Responsibility changes

### Shell Smoke command line

Before:

- `MainWindow.EnableShellSmokeFromCommandLine` parsed every Smoke argument.
- The method also owned derived relationships such as Publish implying Preview
  and the conditions that attach the loaded Smoke handler.

After:

- `Verification/Smoke/ShellSmokeCommandLineOptions.cs` owns argument names,
  values, flag semantics, derived modes, compact sizing, and loaded-handler
  selection.
- `MainWindow` consumes the parsed values and continues to own the remaining
  Shell UI orchestration.
- `ShellSmokeCommandLineOptionsVerification` provides a direct `9/9` parser and
  derived-mode contract check.

### Smoke artifact handling

Before:

- `MainWindow` refreshed Tool Lab windows, retried WPF screenshot capture,
  removed rejected attempts, and wrote quality/teaching reports.

After:

- `Verification/Smoke/ShellSmokeArtifacts.cs` owns those artifact operations.
- The old private artifact helper implementations are absent from `MainWindow`.

### Teaching-selection Smoke scenario

Before:

- The complete generic teaching-selection Smoke scenario was a private
  `MainWindow` method.

After:

- `Verification/Smoke/ShellTeachingSelectionSmoke.cs` owns the scenario.
- The call path is now
  `MainWindow loaded handler -> ShellTeachingSelectionSmoke.RunAsync ->
  Workbench/Viewer public contracts`.
- The scenario retains the exact prior state checks and command path.

### Plane Flatness live-A3 Smoke scenario

Before:

- `MainWindow` owned the complete live A2 -> A3 Publish, real Viewer pointer
  teaching, measurement Preview/Publish, save, and reopen verification flow.

After:

- `Verification/Smoke/ShellPlaneFlatnessLiveA3Smoke.cs` owns that scenario and
  its pass/fail report.
- `MainWindow` supplies the existing Shell ViewModel, Viewer, report/save
  paths, and its selection-overlay synchronization callback.
- No production measurement or recipe behavior moved into the Smoke owner.

### Asynchronous C3D-load Smoke scenario

Before:

- `MainWindow`'s Loaded handler owned the asynchronous source-load timer,
  cancellation threshold, Dispatcher responsiveness check, pass/fail decision,
  and complete performance-report serialization.
- The handler therefore mixed source-load Smoke behavior with unrelated recipe,
  Tool Lab, screenshot, and Viewer Smoke orchestration.

After:

- `Verification/Smoke/ShellAsyncC3DLoadSmoke.cs` owns the complete
  asynchronous C3D load scenario and its report fields.
- `MainWindow` supplies only its existing UI-bound source-load operation,
  source-identity comparison, current binding-duration value, and final
  fail/exit policy.
- The call path is now `MainWindow Loaded handler ->
  ShellAsyncC3DLoadSmoke.RunAsync -> existing MainWindow source-load contract
  -> Viewer/Workbench public state`.
- No second source loader, cancellation owner, report schema, interface,
  event bus, or service container was introduced.

### New/open recipe lifecycle Smoke scenarios

Before:

- `MainWindow`'s Loaded handler owned both New/Open lifecycle state checks,
  document reads, pass/fail decisions, and report serialization.
- The New path also had to coordinate the unsaved-changes dialog; the Open
  path had to check Recipe Manager visibility after the existing open action.

After:

- `Verification/Smoke/ShellRecipeLifecycleSmoke.cs` owns the New/Open
  scenario assertions and report fields.
- `MainWindow` supplies only existing UI-bound operations: show Recipe
  Manager, click the localized `Don't Save` button, open a recipe, inspect
  the manager visibility, and compare the current Viewer source.
- The call paths are `MainWindow Loaded handler ->
  ShellRecipeLifecycleSmoke.RunNewAsync -> existing New command/dialog path`
  and `MainWindow Loaded handler -> ShellRecipeLifecycleSmoke.RunOpen ->
  existing OpenWorkbenchRecipe path`.
- No duplicate recipe lifecycle, dialog automation framework, interface,
  event bus, or state owner was introduced.

### Tool Lab window lifetime

Before:

- `MainWindow` stored ten Tool Lab window fields and repeated step selection,
  construction, owner assignment, single-instance reuse, refresh, activation,
  and `Closed` cleanup for every tool.

After:

- `Views/Tooling/ToolLabWindowManager.cs` owns those ten window instances and
  the shared lifetime policy.
- `MainWindow` retains thin event handlers and read-only window access needed
  to route result displays and run existing screenshot Smoke checks.
- The concrete manager reuses existing windows directly; no speculative
  interface, service container, or factory layer was introduced.

### Workbench tool catalog

Before:

- `ToolWorkbenchViewModel` constructed all twenty tool definitions and the
  default reference-grid parameter list inside its constructor/class.

After:

- `ToolWorkbenchToolCatalog.cs` owns the ordered tool definitions and default
  reference-grid parameter seeds.
- `ToolWorkbenchViewModel` constructs its observable presentation collection
  from that catalog.
- The ViewModel summary now states its actual role: recipe-authoring facade and
  explicit teach-time Preview/Publish orchestration over Tools-owned adapters.

### Workbench PropertyGrid session

Before:

- `ToolWorkbenchViewModel.PropertyGrid.cs` owned the selected step draft
  object, selected step identity, pending/status state, typed draft creation,
  validation, and conversion back to recipe parameter values.
- The same file also physically contained all eighteen independent typed step
  PropertyGrid models and enums.

After:

- `ToolWorkbenchStepPropertySession.cs` owns the detached draft lifecycle,
  selected step identity, pending/status state, adapter coverage, validation,
  and typed recipe-value serialization.
- `ToolWorkbenchViewModel` remains the public WPF binding facade and owns the
  actual recipe collection mutation, Preview-stale transitions, dirty state,
  and recipe refresh.
- The call path is now `WPF -> ToolWorkbenchViewModel facade ->
  ToolWorkbenchStepPropertySession -> ToolWorkbenchViewModel recipe commit`.
- `ToolWorkbenchStepProperties.cs` owns the existing typed PropertyGrid
  models/enums as one stable responsibility group. No new interface, factory,
  service container, or one-class-per-file hierarchy was introduced.

### Viewer recipe file loading

Before:

- `OpenVisionThreeDViewerControl.Data.cs` read the `recipeType` JSON property
  and resolved recipe-relative source paths.
- `OpenVisionThreeDViewerControl.Recipes.cs` selected the recipe format, and
  each format-specific Apply method reopened and deserialized its own file
  before mutating Viewer inspection and presentation state.

After:

- `Recipes/ViewerRecipeFile.cs` owns the canonical recipe path, format
  recognition, typed document deserialization, and recipe-relative source-path
  resolution.
- `OpenVisionThreeDViewerControl` receives the already loaded typed document
  and remains responsible for applying it to Viewer inspection, presentation,
  and render state.
- The call path is now `Open/Smoke command -> ViewerRecipeFile Open/Load ->
  typed Viewer Apply -> explicit Preview where the existing recipe requires
  it`.
- Existing format-specific failure labels remain Viewer presentation policy.
  No interface, factory, service container, or renderer abstraction was added.

### Viewer inspection-session identity

Before:

- `MainWindowViewModel` owned five independent active Preview/Publish identity
  fields: Preview layer ID/name, source entity ID, and result entity ID/name.
- Ten inspection paths repeated direct assignments to those fields across
  `Inspection`, `Warpage`, `Scene`, and `Presentation` partial files.
- Preview layer creation, display context, deviation legend, and Publish all
  consumed the shared fields directly.

After:

- `ViewerInspectionSession.cs` owns the active inspection kind, all ten
  complete Preview/Publish identity mappings, and reset behavior.
- `MainWindowViewModel` keeps its existing public entity-ID constants as
  compatibility aliases, but their values now come from the session owner.
- Inspection result setters activate one typed `ViewerInspectionKind`;
  presentation and Publish read the current identity from the session.
- The call path is now `typed inspection Preview -> ViewerInspectionSession
  Activate -> layer/display/Publish consumers`.
- No OpenGL, WPF control, interface, factory, or service-container dependency
  was introduced.

### Workbench/Viewer teaching coordination

Before:

- `MainWindow` owned ten Workbench/Viewer teaching-event subscriptions,
  matching delegate fields, shutdown cleanup, capture-state translation,
  selected/applied ROI synchronization, GridRectangle draft routing, and
  display-height diagnostics.
- The root window therefore knew both the Workbench recipe-authoring state
  contract and the Viewer capture-state contract.

After:

- `Views/Workbench/WorkbenchViewerTeachingCoordinator.cs` owns the complete
  subscription lifetime and Workbench <-> Viewer teaching translation.
- Workbench remains the recipe/selection owner; Viewer remains the transient
  capture/render owner. The coordinator stores no duplicate recipe, capture,
  camera, Preview, Publish, or Run state.
- The call path is now `Workbench command/event -> coordinator -> Viewer
  public capture contract -> coordinator -> Workbench public authoring
  contract`.
- `MainWindow` owns one coordinator and retains only explicit
  `SyncAppliedSelections` calls after it changes the displayed source/result.
- One concrete coordinator and one bottom-pane callback were sufficient. No
  event bus, interface, factory, DI container, or new state store was added.

## Structural evidence

- `MainWindow.xaml.cs`: `3,861 -> 2,483` lines across the current workspace
  refactor checkpoints.
- `ToolWorkbenchViewModel.cs`: `2,849 -> 2,816` lines.
- `ToolWorkbenchViewModel.PropertyGrid.cs`: `2,050 -> 359` lines. The new
  independent state owner is `423` lines; the existing typed models are grouped
  in one `1,359`-line responsibility file.
- Direct Shell Smoke argument interpretation in
  `EnableShellSmokeFromCommandLine` was replaced by
  `ShellSmokeCommandLineOptions`.
- `RunToolTeachingSelectionSmokeAsync`,
  `RunPlaneFlatnessLiveA3PointerSmokeAsync`,
  `CaptureWindowWithRetryAsync`, `RefreshToolLabForCapture`,
  `WriteTeachingSelectionSmokeReport`, and screenshot-quality helper
  implementations no longer belong to `MainWindow`.
- `MainWindow` no longer constructs a Tool Lab window or subscribes a Tool Lab
  `Closed` handler; those operations belong to `ToolLabWindowManager`.
- The tool-definition literals and `CreateDefaultRegridParameters` no longer
  belong to `ToolWorkbenchViewModel`.
- The former draft state fields, selected-draft identity field, validation/
  serialization switch, and typed-model calls back into
  `ToolWorkbenchViewModel.GetParameter/GetUnmappedParameters` are absent.
- `ViewerRecipeFile.cs` is the only Viewer owner that calls the seven legacy
  recipe `Load` methods.
- `ReadRecipeType`, `ResolveRecipePath`, and direct recipe `Load` calls are
  absent from all `OpenVisionThreeDViewerControl` partial files.
- The former `activePreviewLayerId`, `activePreviewLayerName`,
  `activePreviewSourceEntityId`, `activeResultEntityId`,
  `activeResultEntityName`, and `ResetActivePreviewIdentity` owners are absent
  from `MainWindowViewModel`.
- All Preview identity transitions now use `ViewerInspectionSession.Activate`
  or `Reset`; its direct verification covers ten unique Preview/result
  identities over the four existing Viewer source identities.
- All ten teaching-event subscribe/unsubscribe pairs and their handlers are
  absent from `MainWindow` and present in
  `WorkbenchViewerTeachingCoordinator`.
- `SetAppliedTeachingSelections` and `BeginC3DTeachingCapture` have one Shell
  production coordination owner. Verification-only subscribers remain
  intentionally separate.
- The asynchronous-load timer, pass/fail decision, and all
  `DispatcherTicksDuringLoad`/render-performance report serialization are
  absent from `MainWindow` and owned by `ShellAsyncC3DLoadSmoke`.
- New/Open lifecycle pass/fail decisions and all `DoNotSaveButtonClicked`,
  `OpenWorkbenchRecipeMilliseconds`, and `ViewerSourceReused` report
  serialization are absent from `MainWindow` and owned by
  `ShellRecipeLifecycleSmoke`.

Line-count reduction is supporting evidence only. The completion claim is
based on changed responsibility owners and call paths.

## Verification

- `dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Debug -p:Platform="Any CPU"`
  - Pass: `0` warnings, `0` errors.
- Shell Smoke command-line options verification
  - Pass: `9/9`.
  - Evidence:
    `artifacts/current/20260725-shell-composition-refactor/shell-smoke-command-line.txt`.
- Tool Recipe teaching verification
  - Pass: `27/27`, including the exact twenty-item catalog sequence.
  - Evidence:
    `artifacts/current/20260725-shell-composition-refactor/tool-recipe-teaching.txt`.
- Recipe Manager/WPG verification
  - Pass: `31/31`, including three direct
    `ToolWorkbenchStepPropertySession` ownership/validation/serialization
    checks.
  - Evidence:
    `artifacts/current/20260725-workbench-property-session-refactor/recipe-manager-wpg.txt`.
- Tool Recipe teaching verification
  - Pass: `27/27`.
  - Evidence:
    `artifacts/current/20260725-workbench-property-session-refactor/tool-recipe-teaching.txt`.
- Tool Recipe selection verification
  - Pass: `17/17`.
  - Evidence:
    `artifacts/current/20260725-workbench-property-session-refactor/tool-recipe-selections.txt`.
- Current Tool Recipe teaching regression
  - Pass: `27/27`.
  - Evidence:
    `artifacts/current/20260725-viewer-recipe-file-refactor/tool-recipe-teaching.txt`.
- Current typed Viewer recipe-file application
  - Pass: explicit `C3DThicknessRecipe` and fallback
    `HeightDeviationRecipe` both loaded through `ViewerRecipeFile`, resolved
    the same relative C3D source, completed with `smokeExitCode=0`, and
    retained their expected inspection Pass/Fail result.
  - Both current-source screenshots passed quality on attempt `1`.
  - Evidence:
    `artifacts/current/20260725-viewer-recipe-file-refactor/thickness-contracts.txt`,
    `height-deviation-contracts.txt`, `thickness.png`, and
    `height-deviation.png`.
- Viewer inspection-session verification
  - Pass: `5/5`, covering default identity, all ten complete mappings, unique
    Preview/result IDs, existing source IDs, and reset.
  - Evidence:
    `artifacts/current/20260725-viewer-inspection-session-refactor/inspection-session.txt`.
- Existing Viewer display/runtime regression
  - Pass: `96/96`.
  - Evidence:
    `artifacts/current/20260725-viewer-inspection-session-refactor/display-viewmodel.txt`.
- Current Thickness Preview and Publish integration
  - Pass: typed Preview retains `layer.preview.c3d-thickness`; explicit Publish
    creates `result.c3d-thickness` from `source.c3d-thickness` with all nine
    metrics and the ROI overlay; both runs exit `0`.
  - Current-source screenshots passed quality on attempt `1`.
  - Evidence:
    `artifacts/current/20260725-viewer-inspection-session-refactor/thickness-contracts.txt`,
    `thickness-published-contracts.txt`, `thickness.png`, and
    `thickness-published.png`.
- Generic height-measurement Workbench verification
  - Pass: `42/42` when run in isolation.
  - An earlier parallel invocation produced `41/42` because independent
    processes shared `OpenVisionLab-ALL.log`; no source failure was hidden.
  - Evidence:
    `artifacts/current/20260725-workbench-property-session-refactor/height-measurement-workbench-isolated.txt`.
- Moved Plane Flatness live-A3 scenario
  - Pass in one actual Shell session: Published synthetic A3, real Viewer
    pointer teaching, explicit measurement Preview/Publish, exact save/reopen,
    and unchanged pre-existing Viewer Preview/result references.
  - Evidence:
    `artifacts/current/20260725-shell-composition-refactor/live-a3-pointer.txt`
    and `live-a3-saved.ov3d-recipe.json`.
- Tool Lab lifetime and reuse
  - Pass in the actual current Debug Shell: Filter Tool Lab reused its single
    window instance, captured successfully, and exited `0`.
  - Screenshot quality accepted on attempt `1`.
  - Evidence:
    `artifacts/current/20260725-shell-composition-refactor/filter-tool-lab.png`
    and `filter-tool-lab-quality.txt`.
- Moved teaching-selection scenario
  - Pass using the valid legacy inactive contract; authored recipe and
    Preview/result references remained unchanged.
  - Evidence:
    `artifacts/current/20260725-smoke-responsibility-refactor/teaching-selection-inactive.txt`.
- Current Debug Shell actual execution
  - Pass: exit `0`; `1280 x 760` Korean capture accepted on attempt `1`.
  - Evidence:
    `artifacts/current/20260725-smoke-responsibility-refactor/after-shell.png`
    and `after-shell-quality.txt`.
- Current Workbench/Viewer teaching coordination
  - Release Shell project build: pass, `0` warnings / `0` errors.
  - Full Debug solution build: pass, `0` warnings / `0` errors.
  - Teaching-capture ViewModel: pass, `24/24`.
  - Tool Recipe teaching: pass, `27/27`.
  - Tool Recipe selections: pass, `17/17`.
  - Actual Release Shell replacement entry: pass, existing ROI seeded at
    `2/2`, authored recipe and execution references unchanged.
  - Actual Viewer left-drag plus explicit Apply: pass, same selection identity
    replaced, recipe became dirty, Preview/Publish/Run references unchanged,
    and camera unchanged.
  - Evidence:
    `artifacts/current/20260725-workbench-viewer-teaching-coordinator-refactor/`.
- Current asynchronous C3D-load Smoke ownership
  - Release Shell project build: pass, `0` warnings / `0` errors.
  - Full Debug solution build: pass, `0` warnings / `0` errors.
  - Shell Smoke command-line options: pass, `9/9`.
  - Actual Release complete load: pass; the `240 x 160` synthetic C3D became
    the current Viewer source, `DispatcherTicksDuringLoad=1`, state cleared,
    one render execution, and all existing report fields were written.
  - Actual Release cancellation: pass; a `15.6 MB` Thickness Coupon v1 target was
    cancelled at `1.0%`, the previous Thickness source remained current, and
    source-load state cleared.
  - Evidence:
    `artifacts/current/20260726-shell-async-c3d-load-smoke-refactor/`.
- Current New/Open recipe lifecycle Smoke ownership
  - Release Shell project build: pass, `0` warnings / `0` errors.
  - Full Debug solution build: pass, `0` warnings / `0` errors.
  - Shell Smoke command-line options: pass, `9/9`.
  - Actual Release New lifecycle: pass; the WPF `Don't Save` button was
    clicked, a zero-step/source-less schema `1.3` recipe was saved, the Viewer
    source cleared, and the recipe was clean.
  - Actual Release Open lifecycle: pass; the dual-ROI recipe opened cleanly,
    Recipe Manager was no longer visible, and the Viewer source was current.
  - Evidence:
    `artifacts/current/20260726-shell-recipe-lifecycle-smoke-refactor/`.

An exploratory invocation attempted current dual-ROI Thickness replacement
without first publishing its transformed A3 source. The existing command
correctly rejected that invalid prerequisite. It was not used as refactor pass
evidence, and the moved scenario's command behavior was left unchanged.

A separate exploratory coordinator invocation intentionally omitted the
teaching recipe and failed closed because no pipeline step was selected. It is
retained as `teaching-selection-capturing.txt` and is not pass evidence. The
same current executable then passed both valid dual-ROI replacement scenarios
after the recipe and step prerequisites were supplied.

## Boundary and next checkpoint

This does not complete the full architecture cleanup:

- `MainWindow` still owns composed screenshot/Tool Lab Loaded-handler UI
  orchestration and non-teaching Workbench/Viewer display coordination.
- `ToolWorkbenchViewModel` remains a large facade with execution-session state
  distributed across partial files.
- The Viewer still combines rendering, inspection calculation, typed recipe
  application, and Smoke responsibilities. Recipe-file loading and active
  Preview/Publish identity state no longer belong to the WPF control or the
  root ViewModel fields.

Next checkpoint: do not split the remaining screenshot/Tool Lab handler glue
unless a new independent scenario emerges without a larger callback surface.
The product evidence priority is the owner's unaided first-recipe replay. Do
not move inspection algorithms or introduce a general event bus/service
container.

## Closure record

Status: Complete

Scope: Shell Smoke option/artifact/scenario ownership, Workbench tool catalog,
Plane Flatness live-A3 Smoke ownership, Tool Lab window lifetime, and Workbench
PropertyGrid draft-session ownership, plus Viewer recipe-file loading
and active inspection-session identity ownership, plus Workbench/Viewer
teaching-event lifecycle and state translation, plus asynchronous C3D-load
Smoke ownership/reporting, plus New/Open recipe lifecycle Smoke
ownership/reporting.

Acceptance criteria: old private owners absent -> pass by source search; public
Workbench/Viewer execution boundary preserved -> pass by static and actual
Shell checks; current source builds -> pass with `0` warnings and `0` errors.

Verification: current Debug solution build `0/0`; Shell Smoke options `9/9`;
Tool Recipe teaching `27/27`; Recipe selections `17/17`; Recipe Manager/WPG
`31/31`; height-measurement Workbench `42/42`; live-A3 pointer/save/reopen
PASS; Filter Tool Lab single-instance capture and quality PASS; current
Thickness and HeightDeviation Viewer recipe-file Smoke exit `0` with
screenshot quality PASS; Viewer inspection session `5/5`; Viewer display
`96/96`; actual Thickness Preview/Publish identity and screenshot quality
PASS; current teaching capture `24/24`; actual dual-ROI replacement entry and
left-drag/Apply Shell scenarios exit `0`; current asynchronous C3D-load
complete and cancellation Shell scenarios exit `0`; current New/Open recipe
lifecycle Shell scenarios exit `0`.

Evidence: `artifacts/current/20260725-smoke-responsibility-refactor/` and
`artifacts/current/20260725-shell-composition-refactor/` and
`artifacts/current/20260725-workbench-property-session-refactor/` and
`artifacts/current/20260725-viewer-recipe-file-refactor/` and
`artifacts/current/20260725-viewer-inspection-session-refactor/` and
`artifacts/current/20260725-workbench-viewer-teaching-coordinator-refactor/`
and `artifacts/current/20260726-shell-async-c3d-load-smoke-refactor/` and
`artifacts/current/20260726-shell-recipe-lifecycle-smoke-refactor/`.

Boundary / next dependency: this proves the bounded structural split and
existing software behavior only. It does not prove the unaided owner
first-recipe replay, physical calibration, uncertainty, or metrology.
