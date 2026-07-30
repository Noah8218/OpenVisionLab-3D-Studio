# OpenVisionLab 3D Setup / Teach Workspace Separation

Date: 2026-07-29

Status: Complete for IA-1

## Completed scope

IA-1 replaces the former all-in-one default Workbench with real operator-stage
navigation and two distinct compositions:

| Stage | Primary job | Visible default surfaces | Intentionally absent |
| --- | --- | --- | --- |
| Setup | compose the inspection | Tool Library, source card, full Recipe Chain | Viewer, Selected Tool editor, Validation Set, Run Record |
| Teach | teach one selected inspection step | compact step rail, dominant 3D/Height Image Viewer, Selected Tool regions and parameters | Tool Library, tool-add commands, Validation Set, Run Record |

The top shell now exposes `검사 구성`, `티칭`, `검증`, `결과`, and `교정`.
Advanced diagnostics moved under the existing Tool Labs menu instead of
remaining a permanent peer of the operator stages.

`Validate` and `Results` have explicit navigation identities and bounded
presentation-only compositions in this slice. Their complete IA extraction is
still IA-2 and IA-3; this document does not claim those later slices complete.

## Preserved operating contracts

- Setup, Teach, Validate, and Results share the same recipe, source, selected
  step, selections, Viewer session, and output state.
- Changing stages does not mutate the recipe and does not invoke Preview,
  Publish, Run, or Validation Set.
- An active ROI Review, unapplied PropertyGrid draft, running Preview, or
  running Validation Set blocks navigation until the operator resolves it.
- ROI Review/Apply/Cancel/Delete remains explicit.
- PropertyGrid Apply/Discard remains explicit.
- Preview, Publish, whole-recipe Run, Save, and reopen remain explicit.
- Viewer zoom, pan, layouts, Height Image, overlays, and source/result
  separation are preserved.

## Responsive behavior

- Wide Setup shows Tool Library and Recipe Chain side by side.
- Compact Setup puts those two surfaces in one tab group.
- Wide Teach shows step rail, dominant Viewer, and Selected Tool.
- Compact Teach keeps the Viewer dominant and tabs the step rail with Selected
  Tool so only one support surface competes with the Viewer.
- Setup and Teach do not attach the lower evidence workspace.

## Visual evidence

Closest reproducible current-Release baseline before the implementation:

- `artifacts/current/20260729-workspace-information-architecture/before-wide.png`
- `artifacts/current/20260729-workspace-information-architecture/before-compact.png`

Current Release after implementation:

- `artifacts/current/20260729-workspace-information-architecture/after-setup-wide.png`
- `artifacts/current/20260729-workspace-information-architecture/after-setup-compact.png`
- `artifacts/current/20260729-workspace-information-architecture/after-teach-wide.png`
- `artifacts/current/20260729-workspace-information-architecture/after-teach-compact.png`

All six capture-quality reports accepted attempt 1. Visual review confirms
that the former Tool Library + Recipe Chain + Selected Tool + Viewer
competition is removed from the default path. The before capture used the
closest reproducible loaded-source baseline because the earlier command-line
recipe option was not the teaching-recipe option; it is not presented as
recipe-equivalent data evidence.

## Verification

Current Release verification:

- solution build: `0` warnings, `0` errors;
- docking and stage composition: `43/43`;
- Shell workspace state and aliases: `75/75`;
- Inspection Workspace selection/ROI lifecycle: `63/63`;
- recipe teaching and save/reopen: `28/28`;
- height measurement Workbench: `54/54`;
- Recipe Manager and PropertyGrid: `37/37`;
- Validation Set ordered graph: `82/82`;
- Shell smoke command-line options: `24/24`;
- Run Record history: `10/10`;
- code structure: `17/17`.

Reusable reports are under:

`artifacts/current/20260729-workspace-information-architecture/`

## Completion record

```text
Status: Complete
Scope: IA-1 top-stage navigation plus real Setup and Teach workspace separation
Acceptance criteria: Setup contains only Tool Library/full Recipe Chain -> pass; Teach contains step rail/dominant Viewer/Selected Tool -> pass; Setup/Teach exclude Validation Set and Run Record -> pass; stage transitions preserve recipe/selection and never execute -> pass; active ROI draft blocks navigation -> pass; Wide/Compact current-Release captures -> pass
Verification: Release build 0/0; docking/stage 43/43; Shell state 75/75; Inspection Workspace 63/63; teaching 28/28; measurement 54/54; Recipe Manager/WPG 37/37; Validation Set 82/82; Shell options 24/24; Run Record 10/10; structure 17/17
Evidence: docs/OPENVISIONLAB_3D_SETUP_TEACH_WORKSPACE_SEPARATION_20260729.md and artifacts/current/20260729-workspace-information-architecture/
Boundary / next dependency: IA-2 must extract the dedicated Validate workflow; IA-3 must complete Results/Advanced extraction; IA-4 requires the owner's unaided Wide/Compact stage replay. A-01 therefore remains Partial and the 103 C / 18 P / 88 N / 9 E / 16 O inventory is unchanged.
```

## Next priority

1. `IA-2 / A-10 dedicated Validate stage` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`

SurfaceModel `J-01/J-03/J-04` remains the next functional product train after
the information-architecture sequence is complete.
