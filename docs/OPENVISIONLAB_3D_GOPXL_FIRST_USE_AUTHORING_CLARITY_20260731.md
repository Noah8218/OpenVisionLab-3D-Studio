# OpenVisionLab 3D GoPxL-inspired First-use Authoring Clarity

Date: 2026-07-31

Status: Complete

## Outcome

Empty Authoring now presents one primary action in the dominant Viewer:
`Open 3D input`. The surrounding panes describe only the current workflow
state and no longer repeat several competing input or waiting messages.

This applies the useful GoPxL pattern of keeping the active task obvious while
leaving detailed configuration available only after its prerequisite exists.
It does not copy GoPxL visual assets or expand OpenVisionLab into a sensor or
industrial-control platform.

## Scope

Included:

- current-action guidance for empty, input-ready, and selected-tool states;
- removal of duplicate empty-state source and no-step messages;
- preservation of Source Quality after a valid source exists;
- Wide `1920 x 1040` and Compact `1280 x 760` empty/input-ready layout review;
- fixed-hash Human-owner R0 launcher refresh for the current Release DLL.

Excluded:

- automatic tool selection or recipe-step creation;
- automatic ROI capture, Preview, Publish, Run, or Validation;
- automatic changes to the user's dock/side-collapse preference;
- Human-owner R0 acceptance;
- SurfaceModel, calibration, metrology, camera, PLC, robot, or cloud work.

## UI contract

### Empty input

- Recipe Chain shows `1 · Open 3D input`.
- The source summary card is hidden because no source evidence exists yet.
- The Selected Tool waiting card is hidden.
- The Viewer command/context ribbon is hidden.
- The Viewer-centered `Open 3D input` button is the only primary input action.

### Input ready, no selected tool

- Recipe Chain shows `2 · Select inspection tool`.
- The source card and its Source Quality/relink actions are available.
- Source Quality can occupy Selected Tool without overlapping tool content.
- One Viewer context ribbon explains that a tool must be selected.

### Selected tool

- Recipe Chain shows `3 · Set ROI -> 4 · Preview`.
- Existing Selected Tool authoring, ROI, output, Preview, Publish, Cancel, and
  Save behavior remains unchanged.

The state projection reads `IsSourceReadyForRecipe` and
`HasSelectedPipelineStep`; it does not execute commands or alter recipe data.

## Acceptance criteria and evidence

| Criterion | Evidence | Result |
| --- | --- | --- |
| Empty state has one primary input action | Workbench docking check plus empty Wide/Compact captures | Pass |
| Recipe Chain exposes exactly one current step | Workbench docking `76/76` | Pass |
| Input-ready state advances to tool selection | Wide/Compact input-ready captures | Pass |
| No overlap or required-text clipping | Visual review at both supported sizes | Pass |
| No Preview/Run/recipe mutation introduced | Existing command contracts plus Inspection/Validation regressions | Pass |
| Current R0 launcher uses the current Release | Wide/Compact `-ValidateOnly` | Pass |

## Current-build visual evidence

True before captures were taken from the current Release immediately before
the edit:

- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/before/wide-empty-authoring-before.png`
- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/before/compact-empty-authoring-before.png`

Final after captures:

- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/after/wide-empty-authoring-after.png`
- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/after/compact-empty-authoring-after.png`
- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/after/wide-input-ready-select-tool-after.png`
- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/after/compact-input-ready-select-tool-after.png`
- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/after/wide-selected-tool-after.png`
- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/after/compact-selected-tool-after.png`

All six after captures passed screenshot quality on the first attempt.
Visual comparison found no overlapping controls, clipped required labels or
actions, controls outside their panes, unintended horizontal/nested scroll
bars, or unreachable primary actions. The empty Wide Selected Tool dock
remains present but intentionally contains no misleading waiting content; its
existing side-collapse control preserves the user's presentation preference.

## Verification

- `dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"`
  - Pass: `0` warnings, `0` errors.
- `OpenVisionLab.ThreeD.Shell.exe --verify-workbench-docking <report>`
  - Pass: `76/76`.
- `OpenVisionLab.ThreeD.Shell.exe --verify-inspection-workspace-selection <report>`
  - Pass: `63/63`.
- `OpenVisionLab.ThreeD.Shell.exe --verify-validation-set <report>`
  - Pass: `84/84`.
- `powershell -ExecutionPolicy Bypass -File scripts/verify-code-structure.ps1`
  - Pass: `17/17`.
- `scripts/start-human-owner-r0.ps1 -Layout Wide -ValidateOnly`
  - Pass: current fixed inputs and current Release validated; no app launch.
- `scripts/start-human-owner-r0.ps1 -Layout Compact -ValidateOnly`
  - Pass: current fixed inputs and current Release validated; no app launch.

Reports and captures are under:

- `artifacts/current/20260731-gopxl-first-use-authoring-clarity/`.

## Durable closure

Status: Complete

Scope: GoPxL-inspired first-use Authoring current-action hierarchy and
duplicate empty-state cleanup.

Acceptance criteria: one empty primary action -> pass; one current-step guide
-> pass; input-ready transition -> pass; Wide/Compact layout integrity ->
pass; explicit execution contracts -> pass.

Verification: Release `0/0`; Workbench docking `76/76`; Inspection Workspace
`63/63`; Validation Set `84/84`; structure `17/17`; six final captures
accepted on first attempt; Wide/Compact R0 launcher validation passed.

Evidence:
`artifacts/current/20260731-gopxl-first-use-authoring-clarity/`.

Boundary / next dependency: this is automated software and visual evidence,
not Human-owner unaided acceptance. `A-01` remains Partial until the owner
completes Wide/Compact R0. SurfaceModel remains gated behind that evidence.
