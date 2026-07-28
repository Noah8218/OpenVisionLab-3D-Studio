# Viewer Workspace Composition

Date: 2026-07-27
Status: Complete

## Outcome

Inspection Workspace v3 implementation slice 6 is complete. The normal
Workbench now provides one compact Viewer layout toolbar with four explicit
presentation choices:

| Choice | Result |
| --- | --- |
| Single | Keep only the primary Viewer slot A |
| Split vertical | Show primary A and auxiliary B side by side |
| Split horizontal | Show primary A above auxiliary B |
| Pop out | Keep A in the Workbench and move B to one reusable window |

The auxiliary selector contains only real, existing C3D artifacts discovered
through the established Output Compare candidate source. A current Filter
Preview can therefore be compared with the authored source without fabricating
a surface for metric-only or feature-only outputs.

## Interaction contract

- A remains the existing normal Workbench Viewer and follows the current
  display request.
- B is a separate `OpenVisionThreeDViewerControl` instance. It has independent
  camera, projection, geometry style, color map, HUD, fit, and pointer state.
- Opening a second slot prefers an existing Output Compare B/A/C pin. If no
  pin is available, the first real C3D candidate is used.
- Selecting an auxiliary artifact synchronizes selected-output identity and
  focused Viewer-slot identity.
- Returning to Single focuses A but retains B's valid artifact pin for a later
  split or pop-out.
- Closing the pop-out window returns to Single. Reopening uses the same
  reusable window and auxiliary Viewer instance.
- Layout, focus, and pin changes are presentation-only. They do not edit the
  recipe and do not invoke Preview, Publish, Run, Validation Set, or Save.

At `1280 x 760`, side-by-side is the recommended comparison layout because it
keeps both full 3D regions visible. Stacked mode remains available and
resizable, but the available height naturally limits each Viewer. Pop-out is
the preferred detailed comparison mode when a second monitor or a larger
inspection surface is available.

## Ownership

`ViewerWorkspaceSession` is the non-WPF owner of:

- Single, side-by-side, stacked, and pop-out layout;
- the auxiliary real C3D artifact pin;
- focused Viewer slot A or B.

`ToolWorkbenchViewModel.ViewerWorkspace.cs` composes that session with current
real artifact candidates and exposes presentation commands. It owns no camera,
renderer, recipe calculation, or inspection execution.

`ViewerWorkspaceView` and `ViewerWorkspacePopoutWindow` are WPF/OpenGL View
adapters. They host or move the two existing Viewer controls and handle WPF
window lifecycle. Each Viewer retains its own existing ViewModel state.

The executable structure guard now verifies this owner and composition
boundary.

## Verification

Current Release evidence:

- solution build: `0` warnings / `0` errors;
- Inspection Workspace selection and Viewer session: `26/26`;
- Workbench docking and two-slot composition: `32/32`;
- Shell smoke command-line contract: `12/12`;
- Artifact Navigator, Output Compare, and real Filter Preview pin:
  `31/31`;
- generic Thickness Workbench: `45/45`;
- Tool Recipe teaching and save/reopen identity: `28/28`;
- Recipe Manager and typed PropertyGrid: `37/37`;
- teaching capture: `25/25`;
- Validation Set: `25/25`;
- Viewer display/projection: `103/103`;
- logging: `4/4`;
- keyboard readiness: `3/3`;
- code structure: `16/16`;
- exact supplied-source eight-Tab Runner replay: `8/8`;
- final wide split, compact split, main pop-out, auxiliary pop-out, and
  standalone Viewer screenshot quality: accepted on attempt 1.

The real C3D comparison regression explicitly creates a Filter Preview, pins
that actual derived C3D beside the source, transitions through side-by-side,
stacked, pop-out, and Single, and verifies that the downstream Edge tool was
not executed.

## Evidence

- `artifacts/current/20260727-viewer-workspace-composition/before-wide.png`
- `artifacts/current/20260727-viewer-workspace-composition/before-compact.png`
- `artifacts/current/20260727-viewer-workspace-composition/after-final-split-vertical-wide.png`
- `artifacts/current/20260727-viewer-workspace-composition/after-final-split-vertical-compact.png`
- `artifacts/current/20260727-viewer-workspace-composition/after-final-popout-main-wide.png`
- `artifacts/current/20260727-viewer-workspace-composition/after-final-popout-window.png`
- `artifacts/current/20260727-viewer-workspace-composition/`

## Completion record

Status: Complete

Scope: Single, side-by-side, stacked, and reusable pop-out Viewer composition;
real auxiliary C3D selection; independent Viewer instances; synchronized
focused-slot identity; responsive wide/compact presentation.

Acceptance criteria: four explicit layouts -> pass; real existing C3D in B ->
pass; independent Viewer hosts -> pass; Output Compare candidate reuse ->
pass; no recipe or implicit execution mutation -> pass; wide and `1280 x 760`
evidence -> pass; reusable pop-out lifecycle -> pass.

Verification: Release build `0/0`; focused session/composition/artifact checks
`26/26`, `32/32`, `12/12`, and `31/31`; existing product regressions
`45/45`, `28/28`, `37/37`, `25/25`, `25/25`, `103/103`, `4/4`, `3/3`,
and `16/16`; exact-source Runner `8/8`; all final screenshot quality reports
accepted on attempt 1.

Evidence: this document and
`artifacts/current/20260727-viewer-workspace-composition/`.

Boundary / next dependency: this completes only Viewer composition. Inspection
Workspace v3 is `6/8` bounded slices (`75%`) complete. The next implementation
slice is bounded Thickness `4 x 2` repeat authoring. Owner unaided exact-source
replay remains the final acceptance gate. Physical datum, calibration,
traceable units, uncertainty, and production tolerances remain unverified.
