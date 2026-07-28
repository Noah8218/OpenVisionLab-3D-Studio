# Inspection Workspace Selection Boundary

Date: 2026-07-27

Status: Complete

## Outcome

Inspection Workspace v3 implementation slice 1 is complete without changing
the current default XAML.

The Workbench now has:

- one non-WPF `InspectionWorkspaceSelectionSession`;
- one `SelectedToolWorkspaceViewModel` projection;
- root integration that keeps the existing recipe step, ROI teaching role,
  Viewer-selected ROI, PropertyGrid draft, artifact output, and Viewer slot
  focus synchronized;
- a focused executable verification command.

This is the state boundary required before recomposing the permanent
Workbench layout.

## Responsibility boundary

### `InspectionWorkspaceSelectionSession`

Owns only presentation selection identity:

```text
selected recipe step
  + selected input
  + active region role and region
  + selected output
  + focused Viewer slot
```

It:

- publishes one atomic change for a selected-tool synchronization;
- ignores casing-only identity changes;
- keeps Viewer slot focus when the recipe selection is cleared;
- allows input, region, output, and Viewer focus to change without mutating a
  recipe;
- contains no WPF, persistence, inspection execution, or numerical code.

The root Workbench is the only caller allowed to synchronize the step identity.
Region changes initiated through the session update the existing dual-ROI
teaching owner, so the new session and the established ROI commands cannot
silently disagree.

### `SelectedToolWorkspaceViewModel`

Projects the selected step into the future default configuration sections:

- Inputs: required contract, entity identity, state, frame, and unit;
- Parameters: the exact existing `ToolWorkbenchStepPropertySession` draft;
- Regions: Reference/Measurement or the selected tool's single selection;
- Outputs: current declared/Preview/Published artifact state and evidence;
- Help: selected tool purpose, required input, explicit authoring order, and
  unit boundary.

It does not copy parameters into a second editor. It does not own ROI geometry,
artifact execution, Viewer rendering, recipe mutation, or persistence.

### `ToolWorkbenchViewModel`

Remains the composition root and retains its existing binding facade while the
old XAML is still active. Its new integration partial only:

- derives the canonical identity snapshot from existing owners;
- translates a workspace region choice back to the existing dual-ROI role;
- refreshes the selected-tool read model after recipe, PropertyGrid, ROI
  capture, artifact, or Viewer output changes.

This is a transition bridge, not a claim that the root ViewModel no longer
contains legacy flat bindings.

## Behavioral invariants

- Selecting a step, input, region, output, or Viewer slot does not set recipe
  dirty state.
- Selection does not change a route.
- Selection does not invoke Preview, Publish, Run, or save.
- Parameter editing still uses the existing typed PropertyGrid draft and
  explicit Apply/Discard.
- Thickness still exposes two distinct recipe-owned GridRectangle roles.
- Reference ROI completion advances the active role to Measurement.
- Viewer ROI selection restores the owning step and exact ROI role.
- Viewer focus is session-only and survives recipe-selection clearing.
- Viewer default geometry remains Surface.

## Files

- `src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/InspectionWorkspaceSelectionSession.cs`
- `src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/SelectedToolWorkspaceViewModel.cs`
- `src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchViewModel.InspectionWorkspace.cs`
- `src/OpenVisionLab.ThreeD.Shell/Verification/Workbench/InspectionWorkspaceSelectionVerification.cs`
- `src/OpenVisionLab.ThreeD.Shell/Verification/ShellVerificationCommandRouter.cs`

## Verification

Commands run from `C:\Git\OpenVisionLab-3D-Studio`:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU" --no-restore

OpenVisionLab.ThreeD.Shell.exe `
  --verify-inspection-workspace-selection `
  artifacts/current/20260727-inspection-workspace-selection/inspection-workspace-selection-verification.txt

OpenVisionLab.ThreeD.Shell.exe `
  --verify-tool-recipe-teaching `
  artifacts/current/20260727-inspection-workspace-selection/tool-recipe-teaching-verification.txt

OpenVisionLab.ThreeD.Shell.exe `
  --verify-tool-height-measurement-workbench `
  artifacts/current/20260727-inspection-workspace-selection/height-measurement-verification.txt

OpenVisionLab.ThreeD.Shell.exe `
  --verify-workbench-docking `
  artifacts/current/20260727-inspection-workspace-selection/workbench-docking-verification.txt

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verify-code-structure.ps1
```

Results:

- Release build: `0` warnings, `0` errors;
- Inspection Workspace selection: `12/12`;
- Tool Recipe teaching: `28/28`;
- generic height measurement: `44/44`;
- Workbench docking: `29/29`;
- code structure guard: `15/15`.

No XAML, visible text, layout, or Viewer rendering changed in this slice, so a
before/after UI capture would be identical and is intentionally deferred to
the layout-composition slice.

## Next priority

1. Bind the current commands and the new selected-tool projection into the
   Inspection Workspace v3 default layout, with the Viewer remaining dominant
   and Surface remaining the default. | Recommended model: `gpt-5.6-sol` |
   Reasoning effort: `high`

The later Top orthographic, Fit ROI, compact ROI controls, outputs, Viewer
split/pop-out, and 4 x 2 repeat services remain separate gated slices.

## Completion record

Status: Complete

Scope: non-WPF workspace selection session, selected-tool Inputs/Parameters/
Regions/Outputs/Help projection, existing-owner synchronization, and focused
verification routing; default XAML and visible behavior excluded.

Acceptance criteria: atomic selection identity -> pass; existing Thickness
Reference/Measurement roles synchronized -> pass; existing PropertyGrid draft
reused -> pass; selected-tool inputs/regions/output projected -> pass;
selection-only changes leave recipe route, dirty state, and execution
unchanged -> pass.

Verification: Release build `0/0`; focused selection `12/12`; recipe teaching
`28/28`; height measurement `44/44`; docking `29/29`; structure `15/15`.

Evidence:
`artifacts/current/20260727-inspection-workspace-selection/inspection-workspace-selection-verification.txt`
and the regression reports in the same folder.

Boundary / next dependency: the current visible Workbench is intentionally
unchanged. The default XAML must now be recomposed against these owners before
the GoPxL-informed workflow can be evaluated visually. Physical datum,
calibration, units, uncertainty, and production tolerances remain external
prerequisites for certified thickness.
