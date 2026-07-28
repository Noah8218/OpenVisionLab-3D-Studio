# Inspection Workspace Default Composition

Date: 2026-07-27

Status: Complete

## Outcome

Inspection Workspace v3 implementation slice 2 is complete. The normal
Workbench now presents one coherent authoring path:

```text
compact command bar
  -> Tool Catalog
  -> Recipe Chain
  -> Selected Tool
  -> dominant 3D Viewer
```

This is a composition change over the existing recipe, PropertyGrid, ROI,
Viewer, output, persistence, and execution owners. It is not a Shell rewrite
and does not change the Thickness algorithm or saved `GridRectangle` contract.

## Default layout

The permanent five-stage journey strip is removed. The compact command bar
contains:

- recipe name;
- current C3D grid readiness;
- saved/dirty/validation state;
- selected tool;
- explicit Preview, Run all, and Save.

At wide width the four authoring panes use nominal star widths
`0.72 : 0.90 : 1.05 : 3.30`. The Viewer therefore owns about `55.3%` of the
available primary row before dock chrome. At widths below `1500`, Tool Catalog
and Recipe Chain remain tabs in one left pane and the nominal widths are
`1.00 : 1.15 : 3.00`.

The Viewer remains `Surface` by default and retains the existing near-top
inspection fit.

## Recipe Chain

`RecipeChainView` replaces the full entity tree as the normal Recipe Flow
content. It shows:

- current C3D source and readiness;
- the ordered inspection steps;
- selected-step state and identity;
- move up, move down, remove, and focused Tool Lab actions.

The previous `ToolboxEntityExplorerView` is preserved inside a collapsed
`Advanced recipe explorer`. Reference and entity editing therefore remain
available without dominating the normal path.

## Selected Tool

`SelectedToolWorkspaceView` is now the only normal selected-step
configuration surface. It binds the existing `SelectedToolWorkspaceViewModel`
projection and keeps:

- typed Inputs;
- the exact existing `RecipeStepPropertyGridHost` draft with explicit Apply
  and Discard;
- compact Reference and Measurement ROI rows;
- numeric X=column/Z=row rectangle values;
- selected output state/evidence;
- collapsed Help;
- the existing focused Tool Lab route for specialized evidence.

Inputs and Parameters start collapsed. ROI / Regions starts expanded so the
Reference and Measurement `Draw again` and `Delete ROI` actions remain visible
at `1280 x 760`. Opening a parameter section still provides the full
`210 px` PropertyGrid height.

The former `ToolInspectorView` remains available in the Advanced workspace. It
is not embedded in the new Selected Tool view, so the normal path has one
PropertyGrid rather than two competing parameter editors.

## Selection and execution invariants

The layout uses the already verified `InspectionWorkspaceSelectionSession`.
The new input, region, and output row commands update that presentation
selection only.

- selecting a row does not dirty or reroute the recipe;
- ROI Draw/Edit/Delete still delegates to the existing teaching commands;
- parameter changes still require Apply;
- Preview, Publish, Run, and Save remain explicit;
- source loading, ROI editing, output visibility, and docking do not invoke
  inspection;
- existing recipe and selection identities are unchanged.

## Current visual evidence

The exact current input and recipe used for both before and after captures:

- recipe SHA-256:
  `D0DEDF827985BFCC8EF5AC37777E22442222ECE6DE6101F02E475123D2C191A1`;
- C3D SHA-256:
  `5D3625B1A5A65EF8BEAB366FF7A007918D28FB614136414BBD30A441E85C8937`;
- selected step: `step.tab-thickness.01`;
- language: Korean.

Wide comparison:

- before: `artifacts/current/20260727-inspection-workspace-layout/before-wide.png`;
- after: `artifacts/current/20260727-inspection-workspace-layout/after-wide.png`.

Compact comparison:

- before:
  `artifacts/current/20260727-inspection-workspace-layout/before-compact.png`;
- after:
  `artifacts/current/20260727-inspection-workspace-layout/after-compact.png`.

Both after captures passed the current screenshot quality gate on attempt 1.
The visual comparison confirms:

- the permanent journey strip is gone;
- the existing-recipe Tool Catalog no longer shows the large first-use action
  card;
- the recipe steps scan as eight compact ordered rows;
- Reference and Measurement ROI actions are visible without opening a
  separate Tool Lab;
- the Selected Tool title and state occur once in the configuration pane;
- the Viewer is the dominant primary surface;
- wide and compact layouts retain the selected yellow ROI and Surface model.

## Verification

Commands were run from `C:\Git\OpenVisionLab-3D-Studio` against the current
Release build:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Shell.exe --verify-inspection-workspace-selection <report>
OpenVisionLab.ThreeD.Shell.exe --verify-workbench-docking <report>
OpenVisionLab.ThreeD.Shell.exe --verify-tool-recipe-teaching <report>
OpenVisionLab.ThreeD.Shell.exe --verify-tool-height-measurement-workbench <report>
OpenVisionLab.ThreeD.Shell.exe --verify-recipe-manager-wpg <report>
OpenVisionLab.ThreeD.Shell.exe --verify-teaching-capture-viewmodel <report>
OpenVisionLab.ThreeD.Shell.exe --verify-logging <report>
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-code-structure.ps1
```

Results:

- Release build: `0` warnings, `0` errors;
- Inspection Workspace selection: `12/12`;
- Workbench docking and composition: `31/31`;
- Tool Recipe teaching: `28/28`;
- generic height measurement: `44/44`;
- Recipe Manager / PropertyGrid: `37/37`;
- teaching capture: `24/24`;
- logging: `4/4`;
- code structure guard: `15/15`;
- wide screenshot quality: accepted, attempt 1;
- compact screenshot quality: accepted, attempt 1.

All reports and captures are under:

- `artifacts/current/20260727-inspection-workspace-layout/`

## Next priorities

Top/Fit ROI and the compact ROI Review lifecycle are complete in the newer
Viewer and ROI lifecycle checkpoints.

1. Add selected-output Show/Pin/Compare commands in the Selected Tool section.
   | Recommended model: `gpt-5.6-sol` | Reasoning effort: `medium`
2. Add Viewer slot split/pop-out composition.
   | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
3. Implement bounded Thickness `4 x 2` repeat authoring and exact-source
   replay. | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`

## Completion record

Status: Complete

Scope: Inspection Workspace v3 slice 2 default command bar, Recipe Chain,
Selected Tool composition, dominant Viewer sizing, compact responsive
presentation, preserved advanced explorer/Tool Lab access, and current
wide/compact UI evidence.

Acceptance criteria: permanent journey strip removed -> pass; one normal
selected-tool configuration surface -> pass; compact Reference/Measurement ROI
actions visible at `1280 x 760` -> pass; Viewer dominant at wide and compact
widths -> pass; existing advanced functionality retained -> pass; explicit
execution boundaries unchanged -> pass.

Verification: Release build `0/0`; selection `12/12`; docking/composition
`31/31`; teaching `28/28`; height measurement `44/44`; Recipe Manager/WPG
`37/37`; capture `24/24`; logging `4/4`; structure `15/15`; current wide and
compact screenshot quality accepted on attempt 1.

Evidence:
`artifacts/current/20260727-inspection-workspace-layout/`.

Boundary / next dependency: Top/Fit ROI and the ROI Review-state slice are
complete in newer checkpoints. Selected-output commands, Viewer slots, and
`4 x 2` repeat authoring remain separate implementation slices. The owner
unaided eight-Tab workflow,
physical datum, calibration, unit traceability, uncertainty, and production
tolerances remain unverified.
