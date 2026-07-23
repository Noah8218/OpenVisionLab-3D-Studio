# Empty Recipe Lifecycle

Date: 2026-07-22
Status: Complete

## Owner decision

A recipe is a container that exists before its inspection chain. The operator
must be able to create, name, save, close, and reopen that container before
adding a 3D input or inspection step.

The contracts are separate:

| Contract | Allows no source path | Allows zero steps | Purpose |
| --- | --- | --- | --- |
| Draft persistence (`ValidateForStorage`) | Yes | Yes | Save/reopen editable work |
| Inspection readiness (`Validate`) | No | No | Preview/Run validation |

Save never fabricates a placeholder inspection step. A draft that already
contains references, selections, or steps must still pass their structural and
routing rules before persistence. Current source-identity and selection-binding
checks remain fail-closed where those entities exist.

## Implementation

- `ToolRecipeValidator.ValidateForStorage` owns the relaxed draft-persistence
  gate.
- `ToolRecipeValidator.Validate` retains the strict source-path and at-least-one-
  step requirements used by inspection adapters.
- `ToolRecipeDocumentStore.Save` and `Load` use the storage gate.
- Recipe Center Save/Save As use storage eligibility, not execution readiness.
- Empty relative source paths remain empty when a draft is reopened.
- The old inline action that claimed a first inspection step was required for
  Save was removed. File/structure Save failures still use the shared bilingual
  company dialog.
- A fresh Workbench and its automatically synchronized startup C3D source are
  clean. They do not trigger an unsaved-change prompt before the operator edits
  the recipe.
- Recipe Center `New` now follows one predictable transaction: resolve current
  changes -> choose the new file path -> create a named zero-step document ->
  save it immediately -> activate the main Workbench. Cancelling the new-file
  picker leaves the current recipe unchanged.
- The unsaved-change prompt uses action labels rather than generic answers:
  `저장 / Save`, `저장 안 함 / Don't Save`, and `취소 / Cancel`. `저장 안 함`
  continues the requested New/Open operation; it is not interpreted as Cancel.
- Recipe dialogs and file pickers are owned by the visible Recipe Center when
  that window initiated the command, preventing the prompt from appearing
  behind the active window.
- Opening a recipe closes Recipe Center and activates the Workbench. When the
  recipe points to the C3D already loaded by the Viewer, that source is reused
  instead of being synchronously decoded a second time.
- Recipe path and dirty-state changes notify both raw and localized state
  summaries, so a successful Save/Open immediately changes `미저장` to
  `저장됨` in Korean mode.

## Actual EXE evidence

The current Debug EXE was exercised through the corrected lifecycle, not by
constructing JSON directly:

1. The pre-fix Recipe Center capture shows the false initial state
   `수정 필요 1개 | 수정됨` before any operator edit.
2. The post-fix Recipe Center capture shows the same zero-step startup document
   as `수정 필요 1개 | 미저장`; the execution correction remains visible, but
   there is no false unsaved-change state.
3. The New lifecycle smoke deliberately makes the current recipe dirty, raises
   the real shared-dialog `저장 안 함` button, executes the production New
   command, and supplies only the native file-picker result through the smoke
   argument. The actual save path creates schema `1.3` JSON with zero steps,
   sets the exact current path, clears `IsDirty`, and activates Workbench.
4. A separate EXE invokes the exact `OpenWorkbenchRecipe` method used after the
   native Open picker. It opens the saved zero-step recipe, closes Recipe
   Center, activates Workbench, and reuses the already loaded C3D. The measured
   in-method elapsed time was `774 ms` on this run.

Artifacts in `artifacts/current/20260722-recipe-lifecycle-recheck/`:

- `before-recipe-center.png`
- `after-recipe-center-final.png`
- `actual-new-recipe-final4.ov3d-recipe.json`
- `actual-new-recipe-final4-report.txt`
- `actual-open-recipe-final4-report.txt`
- `after-new-open-workbench-final4.png`
- matching screenshot-quality reports

The smoke bypasses the Windows Save/Open picker UI only by injecting the chosen
path. It uses the real Shell New/Open handlers, shared message-dialog button,
ViewModel mutation, document store, Viewer source-reuse check, and Workbench
activation logic. A manual human click through the native pickers remains an
owner replay, not something claimed by this automation.

## Verification

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform="Any CPU" --no-restore`: pass, 0 warnings, 0 errors.
- Recipe Center/WPG: pass, `27/27`, including saveable-draft versus
  execution-readiness wording and the explicit localized
  `저장/저장 안 함/취소` button contract, `저장 안 함 -> No` result, and
  localized `저장됨` refresh after a successful zero-step save.
- Tool Recipe teaching: pass, `23/23`, including source-less zero-step
  save/reopen, clean automatic startup-source synchronization, and clean named
  New reset.
- Workbench docking: pass, `25/25`.
- Actual EXE New: pass; the real `저장 안 함` button was raised, the file
  exists, step count is `0`, `IsDirty=False`, and screenshot quality was
  accepted on attempt 1.
- Actual EXE Open: pass; exact path loaded, `IsDirty=False`, Recipe Center
  hidden, existing Viewer source reused, and the Workbench screenshot was
  accepted on attempt 1.

## Completion record

```text
Status: Complete
Scope: Empty recipe persistence plus predictable New/Don't Save/Open activation and same-source reuse; no algorithm, recipe schema, or Runner execution change.
Acceptance criteria: Initial startup clean; zero-step draft saveable; New asks for a path and immediately creates the file; Don't Save continues New; Open activates Workbench; no placeholder step; strict Preview/Run validation retained.
Verification: Build 0/0; Recipe Center/WPG 27/27; recipe teaching 23/23; docking 25/25; actual EXE New and Open pass; all current screenshots accepted on attempt 1.
Evidence: artifacts/current/20260722-recipe-lifecycle-recheck/ and this document.
Boundary / next dependency: This proves local Shell lifecycle logic and actual EXE paths with injected file-picker choices. It does not prove a manual native picker click, different-source C3D load performance, arbitrary graph execution, real four-landmark alignment, calibration, or metrology.
```
