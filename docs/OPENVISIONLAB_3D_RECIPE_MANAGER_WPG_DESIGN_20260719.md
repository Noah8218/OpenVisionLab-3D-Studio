# OpenVisionLab 3D Recipe Manager + WPG Teaching Design

Date: 2026-07-19
Status: Approved and implemented locally; Recipe Manager v1 gate passed on 2026-07-19

## 1. Decision Summary

OpenVisionLab 3D Studio will use the existing `*.ov3d-teach.json` document as the recipe authoring format and open it into a recipe session that connects the recorded source, ordered inspection steps, teaching selections, parameters, and validation state.

The Recipe Manager will be a dockable lifecycle and status view. It will not become a second pipeline editor. The ordered Pipeline remains the owner of step order and entity routing, while the right-side Step Parameters view uses a typed WPG PropertyGrid to teach the selected algorithm's parameters.

The first implementation must preserve these rules:

- Opening a recipe may resolve and display its 3D source, but it never runs Preview, Run, or Publish.
- PropertyGrid edits are staged. `Apply parameters` is required before the recipe document changes.
- Applying parameters marks the recipe dirty and existing Preview/Publish evidence stale; it still does not run an algorithm.
- Saving is explicit and cannot silently omit an uncommitted PropertyGrid edit.
- Unsupported steps stay visible and round-trip unchanged. They are never dropped or guessed.
- WPG is a Shell/presentation dependency only. Core, Data, Tools, Viewer, and Runner do not reference WPG.
- The WPG build and theme used by 3D Studio are app-local and version-pinned. Existing WPG consumers are not updated or globally themed.

## 2. Work Contract

### Outcome

An operator can open a saved 3D teaching recipe, see whether its source and step graph are usable, select a step, teach its typed parameters through PropertyGrid, explicitly apply the edit, save/reopen the recipe, and then invoke the existing explicit Preview/Run workflow.

### Included in Recipe Manager v1

- New, Open, Save, Save As, and a bounded Recent list.
- Current recipe identity, full path, schema, dirty state, and source readiness.
- Relative source-path resolution against the recipe directory.
- Source length/hash/dimension verification where the schema contains that evidence.
- Ordered Pipeline selection and validation summary.
- A typed WPG editor for the already-supported `filter` and `height-difference-edge` steps.
- Explicit Apply/Discard behavior for the selected step editor.
- Partial-support state for imported or not-yet-adapted tools.
- Existing explicit Preview selected step, Run complete recipe, and Publish behavior.
- Save/reopen round-trip and no-auto-run evidence.

### Excluded from v1

- Recipe database, directory watcher, server library, tags, permissions, or cloud sync.
- Camera, PLC, robot, production-line, or sensor configuration.
- Automatic execution on open, selection, property change, visibility change, or save.
- A second Pipeline editor inside Recipe Manager.
- Automatic migration of legacy recipes.
- Generic reflection-driven execution or string-to-algorithm discovery.
- Line Fit algorithm implementation. Its approved design follows after Recipe Manager v1.
- Physical calibration, Gauge R&R, metrology-grade, or production-readiness claims.

### External prerequisite

The user approved this design and all Recipe Manager v1 decisions. The WPG Phase 0 compatibility gate and the local implementation/verification gates are now complete; see `docs/OPENVISIONLAB_3D_RECIPE_MANAGER_WPG_IMPLEMENTATION_20260719.md`.

## 3. Evidence Used

### WPG-CUSTOM

- Repository: `C:\Git\WPG-CUSTOM`
- Reviewed commit: `2050f36a144f8c4c6964ff5777ec21aa03e89877`
- License: Apache-2.0
- Existing project: non-SDK WPF library targeting .NET Framework 3.5.
- Existing assembly identity: `System.Windows.Controls.WpfPropertyGrid, Version=2010.11.10.0`.
- Existing Debug DLL SHA-256: `CB418BC1D20FF950AE559FCD8662BEBD519EAFAC3B6C016135B03C89573D1E8B`.
- The same exact DLL and hash are vendored by `C:\Git\OpenVisionLab_Dev`.
- The control supports `SelectedObject`, categorized/alphabetical layouts, search, common `System.ComponentModel` metadata, custom property ordering, numerical range, threshold, range, and metric-range editors.
- Current WPG templates define their colors inside the component dictionaries and mostly consume them with `StaticResource`. A host-level foreground/background assignment is not a complete theme contract.

### OpenVisionLab 2D reference

The useful operating pattern is:

```text
typed algorithm property object
    -> WPG SelectedObject
    -> operator edit
    -> CommitPendingEdit
    -> explicit mapper apply
    -> persisted recipe/XML
    -> explicit Preview or Run
```

The following 2D behavior is adopted:

- Algorithm parameter models drive PropertyGrid rows.
- PropertyGrid value changes mark an edit as pending; they do not auto-run inspection.
- `CommitPendingEdit` is called before applying or saving.
- A mapper owns property-object-to-recipe conversion.
- Selecting a step swaps `SelectedObject` synchronously.

The following 2D implementation details are not copied:

- The approximately 3,700-line bridge and its 2D-specific localization/visibility behavior.
- Application-wide theme dictionaries.
- Forcing the PropertyGrid to `Foreground=Black` and `Background=White`.
- XML-specific UI wording and 2D layer-routing concepts.
- A floating Recipe Manager panel. The 3D product keeps the established docking workspace.

### Current 3D Studio baseline

- `ToolRecipeDocument` schema `1.1` already records recipe identity, source identity, references, ordered steps, parameters, and structured selections.
- `ToolRecipeDocumentStore` already validates load/save and verifies current structured-selection source bindings before save.
- `ToolWorkbenchViewModel` already owns New/Open/Save, source loading, step order, validation, and dirty state.
- Filter and Height Difference Edge already have typed Preview/Publish adapters.
- The current generic parameter list is a string `Name/Value` editor and is the surface to replace for supported typed steps.
- The workbench already uses docked Project Explorer, 3D View, Properties, Pipeline/Validation, Session Log, and Height Profile views.

This means Recipe Manager v1 is an evolution of the existing workbench, not a new parallel recipe subsystem.

## 4. UI-First Layout

```text
+----------------------------------------------------------------------------------+
| Recipe: BatteryCell_3D   [Modified]   Source: Ready   Graph: 2/2 supported       |
+----------------------+--------------------------------------+--------------------+
| Recipe Manager       | 3D View                              | Step Parameters    |
| [New] [Open] [Save]  |                                      | Height Diff. Edge  |
| [Save As]            | source + taught overlays             | [search]           |
|                      |                                      | Basic              |
| Current recipe       |                                      |   Axis       [v]   |
| - file / schema      |                                      |   Polarity   [v]   |
| - source identity    |                                      |   Min delta  [ ]   |
| - readiness          |                                      |                    |
|                      |                                      | [Discard] [Apply]  |
| Recent recipes       |                                      | Unapplied changes  |
|                      |                                      |                    |
| tabs:                |                                      | read-only route    |
| Recipe | Toolbox |   |                                      | and output summary |
| Entities             |                                      |                    |
+----------------------+--------------------------------------+--------------------+
| Pipeline / Validation: 01 Filter -> 02 Height Edge    errors / warnings / stale |
+----------------------------------------------------------------------------------+
| Session Log / Height Profile (dockable tabs)                                      |
+----------------------------------------------------------------------------------+
```

### Dock ownership

| Dock view | Responsibility |
| --- | --- |
| Recipe Manager | File lifecycle, current recipe summary, source readiness, recent files |
| Toolbox | Add a supported tool definition to the Pipeline |
| Entities | Source, reference, selection, and derived entity inspection |
| 3D View | Source and explicit teaching/result overlays |
| Step Parameters | Selected-step WPG editor plus Apply/Discard |
| Pipeline / Validation | Step order, routing state, adapter support, errors, warnings |
| Session Log | File, teaching, validation, Preview, Run, and Publish events |
| Height Profile | Viewer-linked profile interaction |

Recipe Manager, Toolbox, and Entities may initially share one dock group as tabs. They remain separate views and can later be floated or moved using the existing docking host. No Recipe commands are placed on the Viewer itself.

### Recipe Manager summary content

The default view is compact:

1. Lifecycle commands: New, Open, Save, Save As.
2. Recipe name, file name/path, schema, and Modified/Saved state.
3. Source state: Ready, Missing, Identity mismatch, or Unsupported format.
4. Pipeline state: step count, supported adapter count, errors, and warnings.
5. Recent list: maximum ten explicitly opened or saved local files.

There is no folder scan or recipe database in v1. A missing recent path is shown as unavailable and can be removed from the list.

### Step Parameters content

The right panel has four stable areas:

1. Selected step header: icon/text tool name, step ID, support state.
2. WPG parameter editor.
3. `Discard` and `Apply parameters` actions plus pending/validation text.
4. Read-only input entity, output entity, Preview/Publish evidence summary.

Input/output routing remains a Pipeline concern. It is not mixed into the algorithm PropertyGrid.

## 5. Recipe Open and Activation Flow

```text
Open file
  -> preserve current session until the candidate is accepted
  -> read candidate bytes and parse supported schema
  -> structural recipe validation
  -> resolve relative source path from recipe directory
  -> verify source format, length, hash, grid size, frame, and selection bindings
  -> identify locally supported/unsupported step adapters
  -> create typed editor projections for supported steps
  -> replace current session
  -> display source in Viewer when source identity is ready
  -> mark all execution evidence as Not run / Stale
```

Opening never calls Preview, Run, or Publish.

### Failure behavior

- Invalid JSON or structurally invalid recipe: reject the candidate and keep the current session unchanged.
- Missing source: open the authored recipe in `Needs source relink`; preserve all steps and selections, disable Preview/Run.
- Source identity mismatch: open in `Source mismatch`; do not trust structured selections or run. Relink must verify before activation.
- Unsupported tool ID: open in `Partially supported`; keep the step read-only and round-trip all parameters unchanged. Full Run remains disabled.
- Newer unsupported schema: reject without rewriting.
- Legacy supported schema: open without silent migration. Migration is an explicit later Save As decision.

## 6. Recipe Session State

The Shell ViewModel owns one active session with orthogonal state:

| State axis | Values |
| --- | --- |
| Document | None, Loading, Ready, Invalid |
| Source | None, Ready, Missing, IdentityMismatch, Unsupported |
| Capability | FullySupported, PartiallySupported |
| Edit | Saved, RecipeDirty, ParameterDraftDirty |
| Execution | NotRun, PreviewRunning, PreviewReady, PreviewStale, Published |

These are presentation/session states, not additions to the serialized Core document.

New/Open/Close or step change while `ParameterDraftDirty` or `RecipeDirty` uses an explicit Apply/Discard/Cancel or Save/Discard/Cancel decision. No draft is silently lost and no invalid draft is silently serialized.

## 7. WPG Parameter Teaching Contract

### Ownership boundary

```text
Core:   immutable recipe contracts and structural validator
Data:   recipe bytes, JSON, path resolution, hashes, recent-path persistence
Tools:  typed algorithm input/output and numerical validation/execution
Shell:  recipe session, selected step, parameter draft, Apply/Discard commands
View:   WPG lifetime, focus, CommitPendingEdit bridge, file dialogs only
Runner: recipe replay without WPG or Shell references
```

### Typed edit models

WPG is bound to a typed edit object, never directly to `ToolRecipeDocument` and never to a dictionary.

Initial models:

- `FilterStepProperties`
  - Method enum
  - Kernel size with odd/min/max validation
  - Missing-value policy
  - Boundary policy
- `HeightDifferenceEdgeStepProperties`
  - Comparison axis enum
  - Polarity enum
  - Minimum delta with finite and greater-than-zero validation
  - Candidate, point, missing-value, and boundary policies

Properties use `Category`, `DisplayName`, `Description`, `PropertyOrder`, and numerical editor metadata. Serialized numeric values remain invariant-culture strings because schema `1.1` currently stores name/value pairs.

The initial implementation uses one explicit `switch` mapper for the two supported tool IDs. A plugin/factory hierarchy is not added until at least a third implemented tool proves a stable repeated contract.

### Apply semantics

1. Selecting a supported Pipeline step creates a detached typed edit object from its stored parameters.
2. WPG edits mutate only that detached object and set `ParameterDraftDirty`.
3. `Apply parameters` first calls `CommitPendingEdit`.
4. Typed validation runs. Invalid properties stay in the editor and the recipe is unchanged.
5. A successful mapper replaces only the selected step's parameter collection.
6. Recipe validation runs again.
7. The recipe becomes dirty and the selected/downstream execution evidence becomes stale.
8. Preview/Run does not start.

`Discard` recreates the edit object from the current recipe step. `Save` cannot bypass an unapplied draft; it asks Apply/Discard/Cancel.

### Unknown and future properties

When a known tool step contains a parameter not recognized by the current typed mapper, the parameter is retained in an `Unmapped parameters` read-only section and the step reports a compatibility warning. Applying known properties must merge them with the untouched unknown parameters rather than deleting them.

## 8. WPG Compatibility and Theme Isolation

### Why the legacy project is not referenced directly

The current WPG project targets .NET Framework 3.5, uses a strong-named 2010 assembly identity, and has assembly-name-specific pack URIs. A `ProjectReference` from the .NET 10 Studio solution or an absolute reference to `C:\Git\WPG-CUSTOM` would make the 3D build machine-dependent and would couple it to other applications' WPG work.

### Approved architecture recommendation

Add a companion project under WPG-CUSTOM without modifying the legacy project or its output:

```text
C:\Git\WPG-CUSTOM
  Main\WpfPropertyGrid                 # existing .NET 3.5 project, unchanged
  Main\WpfPropertyGrid.ThreeD          # new SDK-style companion
```

Companion contract:

- Target: `net10.0-windows` with WPF enabled.
- Unique PackageId and AssemblyName: `OpenVisionLab.WpfPropertyGrid.ThreeD`.
- Source basis pinned to WPG-CUSTOM commit `2050f36...`.
- Public namespaces may remain compatible, but all pack URIs use the unique assembly name.
- The legacy assembly, strong-name key, version, and theme files remain untouched.
- Theme templates replace internal hard-coded color lookups with package-scoped semantic keys such as `Ovl3D.Wpg.SurfaceBrush`, `Ovl3D.Wpg.TextBrush`, and `Ovl3D.Wpg.AccentBrush`.
- The package includes Apache-2.0 license/notice and source revision metadata.

The produced `.nupkg` and `.sha256` are vendored under:

```text
third_party/WpgCustom/
```

Studio references an exact package version from the repository-local NuGet source. It does not reference the sibling WPG repository at build time and it does not install or update WPG globally.

### 3D theme scope

- `RecipeStepParameterInspectorView.Resources` maps only `Ovl3D.Wpg.*` semantic keys to the current `ThreeD.*` brushes.
- WPG dictionaries are not merged into `App.xaml`.
- No unkeyed application-wide `TextBox`, `ComboBox`, `CheckBox`, `Expander`, or `ScrollViewer` style is introduced.
- Icon-only actions require a tooltip and accessible name; Apply/Discard retain text.
- The initial palette maps panel/text/divider/focus/accent to the current white/gray/blue-focus/teal-accent 3D theme.
- A theme check must prove that opening the Recipe Manager does not change controls in Viewer, Calibration Center, Session Log, or other applications.

## 9. File and ViewModel Changes Proposed for Implementation

The exact names may be adjusted to existing conventions, but ownership must remain:

```text
src/OpenVisionLab.ThreeD.Data/Recipes/
  RecipeRecentFileStore.cs                 # bounded path list only
  ToolRecipeDocumentStore.cs               # add candidate identity/atomic save as needed

src/OpenVisionLab.ThreeD.Shell/ViewModels/RecipeManager/
  RecipeManagerViewModel.cs                # lifecycle/status/recent commands
  RecipeSessionViewModel.cs                # active document/source/capability/edit state

src/OpenVisionLab.ThreeD.Shell/PropertyGrid/
  RecipeStepPropertyGridHost.cs            # thin WPG lifecycle bridge
  RecipeStepPropertyMapper.cs              # explicit two-tool mapping
  FilterStepProperties.cs
  HeightDifferenceEdgeStepProperties.cs

src/OpenVisionLab.ThreeD.Shell/Views/RecipeManager/
  RecipeManagerView.xaml(.cs)
  RecipeStepParameterInspectorView.xaml(.cs)
```

`ToolWorkbenchViewModel` remains the composition owner during v1. Recipe Manager state can be extracted only where the lifecycle responsibility is independently testable; this work does not justify a broad workbench rewrite.

## 10. Save Contract

- Save validates the committed recipe document and current source/selection bindings.
- Save writes UTF-8 without BOM, matching the current store.
- Save is atomic: write a temporary sibling file, flush/close, then replace/move into the destination.
- Save As may choose a new directory; relative source paths are normalized deliberately and never guessed.
- Recipe hash/evidence is computed from the final saved bytes.
- A failed save leaves the previous file and in-memory session intact.
- Successful save clears only `RecipeDirty`; it does not clear execution-stale state or imply that inspection ran.

## 11. Verification and Acceptance Gates

### Phase 0: WPG package

- Companion project builds on .NET 10 with zero errors and warnings.
- Exact package hash, source commit, assembly identity, license, and required entries are verified before Studio restore/build.
- The legacy WPG .NET 3.5 project files and existing DLL remain unchanged.
- A focused host verifies bool, enum, double, range/threshold, search, category ordering, SelectedObject swap, invalid value behavior, and `CommitPendingEdit`.
- Resource inspection verifies package-scoped keys and no application-wide implicit style leakage.

### Phase 1: Recipe lifecycle

- New/Open/Save/Save As and bounded Recent list pass.
- Opening a valid recipe displays the exact source but does not change Preview/Run/Publish counts.
- Invalid candidate open leaves the prior session intact.
- Missing and identity-mismatched sources enter the correct repair state.
- Unknown steps and parameters survive open/save/reopen byte-semantically.
- Dirty prompts cover New/Open/Close and unapplied parameter drafts.

### Phase 2: WPG teaching

- Filter and Height Difference Edge typed rows show correct values from an existing recipe.
- Invalid edits cannot alter the recipe.
- Apply changes only the selected step, marks recipe dirty, and makes affected evidence stale without running.
- Discard restores the last committed recipe values.
- Save/reopen preserves enum and invariant numerical values.
- Runner and non-Shell projects have no WPG dependency.

### UI evidence

Fresh current-build before/after captures are required at `1920x1080` and the established `1280x760` compact evidence size for:

- no recipe / empty state;
- valid recipe with Filter selected;
- Height Difference Edge with an invalid Minimum Delta;
- missing or identity-mismatched source;
- partially supported imported step;
- WPG panel docked, floated, moved, and restored.

The review checks clipped text/icons, editor text visibility, combo visibility, Apply/Discard clarity, status color meaning, focus indication, and whether unrelated views changed theme.

### Required solution checks

At minimum:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Debug
dotnet run --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -c Debug -- <recipe-manager-verifier>
```

The existing Viewer, Shell, typed Filter/Edge, recipe selection binding, Runner, and screenshot-quality gates must remain green.

## 12. Implementation Sequence

1. WPG .NET 10 companion package and isolated theme proof.
2. Recipe Manager dock view and active-session lifecycle using the existing recipe store.
3. Step Parameters WPG host with Apply/Discard and Filter mapping.
4. Height Difference Edge mapping, unknown-parameter preservation, and partial-support UI.
5. Full save/reopen/no-auto-run/UI regression evidence.
6. After Recipe Manager v1 passes, resume the separately designed 3D Line Fit typed adapter.

No new algorithm is implemented during steps 1-5.

## 13. Approved Checkpoint

Recommended approval is for the complete design above, specifically:

1. Recipe Manager remains a compact docked lifecycle/status view, not a second Pipeline editor.
2. Opening a valid recipe loads its recorded 3D source but never auto-runs inspection.
3. WPG edits are staged and require `Apply parameters`; Preview/Run remain explicit.
4. Input/output routing stays outside the algorithm PropertyGrid.
5. Recipe Manager v1 uses Recent files only, without a database or directory scanner.
6. A separate .NET 10 WPG companion package is built from the pinned WPG-CUSTOM source while the legacy project remains unchanged.
7. WPG theme resources are scoped to the Step Parameters view and cannot affect other programs or unrelated Studio views.
8. Filter and Height Difference Edge are the first typed PropertyGrid editors; Line Fit follows after this gate passes.

## 14. Closure Record

```text
Status: Complete
Scope: Recipe Manager v1 plus WPG-based Filter and Height Difference Edge parameter teaching
Acceptance criteria: New/Open/Save/Save As/Recent, source identity repair states, staged Apply/Discard, atomic save, typed WPG mapping, unknown/unsupported preservation, no auto-run, local theme isolation, docking, and current-build UI evidence -> passed
Verification: zero-warning/error Debug solution build; WPG package integrity passed; Recipe Manager/WPG 17/17; teaching 16/16; selections 17/17; docking 15/15; Filter 13/13; Edge 13/13; all sequential verification processes exited with remainingProcessCount=0; current-build screenshots passed quality checks
Evidence: docs/OPENVISIONLAB_3D_RECIPE_MANAGER_WPG_IMPLEMENTATION_20260719.md; artifacts/verification/20260719-recipe-manager-wpg-v1; artifacts/ui/20260719-recipe-manager-wpg-v1
Boundary / next dependency: WPG adapters are intentionally limited to Filter and Height Difference Edge. Full recipe Run remains blocked by unimplemented downstream adapters. The next approved implementation candidate is 3D Line Fit after its nine design decisions are confirmed.
```
