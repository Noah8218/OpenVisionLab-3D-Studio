# OpenVisionLab 3D MVVM View Boundaries

Date: 2026-07-26

Status: Complete

## Scope

This checkpoint audits the WPF view layer and removes the remaining cases where a
View directly owned Workbench presentation state or changed recipe-edit state.
The visible workflow and the explicit Preview / Publish / Run contract are
unchanged.

## Ownership rule

| Responsibility | Owner |
|---|---|
| Selected review tab, selected recipe step, draft dirty/apply/discard state | ViewModel |
| User actions that change Workbench state | `ICommand` on the ViewModel |
| File-dialog intent | ViewModel request event |
| File-dialog creation and Window ownership | Shell View adapter |
| AvalonDock activation, pane height, `ScrollIntoView` | View |
| PropertyGrid binding flush and WPF validation | PropertyGrid View adapter |
| OpenGL rendering, pointer hit-testing, camera gestures | Viewer View adapter |
| Output Compare child-viewer materialization | Output Compare View adapter |
| Recipe rules, validation, execution, and evidence state | ViewModel / domain execution owner |

Code-behind is therefore not required to be empty. It is limited to operations
that need a WPF visual, input device, window, dialog, or rendering context.

## Structural changes

- `ToolWorkbenchViewModel.SelectedReviewTabIndex` now owns the active pipeline
  review tab. `RecipePipelineReviewView` uses a two-way binding.
- `SelectValidationSetSourcesCommand` raises a request handled by
  `MainWindow`; the nested review View no longer constructs a file dialog or
  writes validation sources.
- `RecipeStepPropertyGridHost.PropertyValueChangedCommand` forwards WPF
  PropertyGrid edits to `MarkSelectedStepParameterDraftDirtyCommand`.
- Apply and Discard use the existing ViewModel commands. The remaining Apply
  click adapter only flushes pending WPF bindings before executing the command.
- `SelectPipelineStepCommand` is the single recipe-step selection route used by
  Tool Lab activation.
- Two-point line, three-point plane, and datum-plane Tool Lab views no longer
  reset `SelectedPipelineStep` while refreshing their viewer content.
- Startup navigation writes the requested review tab to the Shell Workbench
  ViewModel before the nested View is loaded. This prevents late DataContext
  binding from resetting the requested tab.

## Structural proof

The current view tree has no remaining matches for these direct mutations:

```text
SelectValidationSetSources_Click
OnPropertyGridValueChanged
OnDiscardParametersClick
workbench.SelectPipelineStep(...)
workbench.MarkSelectedStepParameterDraftDirty(...)
workbench.DiscardSelectedStepParameterDraft(...)
workbench.TryApplySelectedStepParameterDraft(...)
```

`OutputCompareView`, `OpenVisionDockWorkspaceView`, the Viewer custom control,
and PropertyGrid binding commit code remain in the View layer deliberately.
Moving them into a ViewModel would introduce WPF/OpenGL dependencies into the
ViewModel rather than create an MVVM boundary.

## Verification

- Release solution build: `0` warnings, `0` errors.
- Workbench docking and review navigation: `28/28`.
- Recipe Manager / PropertyGrid / MVVM commands: `34/34`.
- Validation Set ordered replay: `24/24`.
- Current Release Shell screenshot:
  `artifacts/current/20260726-mvvm-view-boundaries/workbench-validation-set-after.png`.
- Screenshot quality:
  attempt `1`, acceptable `True`, black ratio `0.0005`, white ratio `0.5302`,
  luminance `0..255`.

The change was structural and intentionally preserved the visual design. A true
pre-edit screenshot was not captured before the refactor began, and rebuilding
the repository baseline would not reproduce the current dirty-worktree product
state safely. The current-source screenshot is therefore after evidence, not a
claimed before/after visual redesign.

## Acceptance record

Status: Complete

Scope: Workbench WPF presentation state and state-changing View gestures use
ViewModel properties, commands, and request events; WPF/OpenGL-only operations
remain View adapters.

Acceptance criteria:

- Review-tab state survives initial binding and command-line navigation: pass.
- Validation source selection is requested by a ViewModel command: pass.
- PropertyGrid dirty/apply/discard and Tool Lab selection route through
  ViewModel commands: pass.
- Removed responsibilities are absent from the former View owners: pass.
- Existing docking, PropertyGrid, and Validation Set behavior remains green:
  pass.

Verification: Release build; `--verify-workbench-docking`;
`--verify-recipe-manager-wpg`; `--verify-validation-set`; current Release Shell
screenshot smoke.

Evidence: `artifacts/current/20260726-mvvm-view-boundaries/`.

Boundary / next dependency: this proves the code ownership boundary and current
automated UI behavior. It does not prove the owner's unaided first-recipe
workflow or physical calibration/metrology accuracy.
