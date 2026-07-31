# OpenVisionLab 3D Authoring Panel Integrity and Side-collapse Closure

Date: 2026-07-31
Status: Complete

## Product boundary

OpenVisionLab 3D Studio remains a local deterministic rule-based 2.5D/3D
inspection workbench. This slice improves the authoring shell around the
existing C3D source, recipe, ROI, Preview, Publish, validation, and result
contracts. It does not add camera, PLC, robot, cloud, production-line, or
physical-metrology claims.

## Reported problem

The loaded Completeness step exposed three concrete usability failures:

1. `Selected Tool` rendered Source Quality and tool configuration content in
   the same coordinates. Controls, section titles, cards, and actions visibly
   overlapped.
2. The responsibility rail and compact utility area could truncate required
   text, including the Recipe Center/language area.
3. Supporting panes consumed Viewer width even when the operator wanted to
   inspect the surface at a larger scale. The initial authoring screen also
   did not state the shortest normal workflow.

The true current-build baseline was captured before editing:

- `artifacts/current/20260731-authoring-panel-integrity-and-collapse/before/wide-authoring-before.png`
- `artifacts/current/20260731-authoring-panel-integrity-and-collapse/before/compact-authoring-before.png`

## Root cause

`SelectedToolWorkspaceView` assigned its Source Quality child a local
`DataContext` and then bound the child's `Visibility` to
`IsSourceQualityWorkspaceVisible`. That property belongs to the parent
Workbench owner, not to the local Source Quality view model. The unresolved
binding fell back to the default visible state, so Source Quality and Selected
Tool were composed simultaneously.

The selected-tool sections also depended on an implicit third-party Expander
template whose animated measurement was not a reliable boundary for the dense
ROI content. This amplified the visible collision when both surfaces were
present.

## Implemented behavior

### Mutually exclusive Selected Tool surface

- Source Quality visibility now resolves explicitly through
  `SelectedToolRoot.DataContext`.
- A deterministic non-animated Expander template owns the Inputs, Parameters,
  ROI, Outputs, and Help measurement boundaries.
- The selected-tool surface uses one vertical ScrollViewer. Narrow layouts
  wrap secondary identifiers and retain all required task labels and actions.
- A reusable visible-text geometry diagnostic records every rendered
  `TextBlock` for loaded Wide and Compact evidence.

### Responsive labels without hidden meaning

- Wide responsibility rail width is `140` pixels so required route and utility
  labels remain readable.
- Compact remains a `60`-pixel icon rail. Navigation and utility commands use
  familiar icons with existing tooltips, accessible names, and AutomationIds.
- The language selector shows the complete display name in Wide and the
  non-truncated adaptive value `한` or `EN` in Compact.
- `AGENTS.md` now requires every UI change to check Wide and Compact for
  overlap, clipped required text/actions, out-of-pane controls, unreachable
  controls, and unintended scrollbars. The rule also requires relevant
  loaded/empty and expanded/collapsed evidence.

### Side-collapse with Viewer fixed

- Workbench and Advanced task/support panes expose AvalonDock side auto-hide.
- A multi-tab pane shows a compact chevron on its tab strip; a single tab keeps
  the standard pin/auto-hide affordance.
- Selected Tool, Tool Library/Recipe Chain, Results/evidence, linked view, and
  height-profile support surfaces can return their width to the Viewer.
- The dominant Viewer itself remains fixed and cannot be hidden. Calibration
  anchors retain their existing non-hideable safety contract.
- Collapse and restore are presentation-only. They do not mutate recipe,
  source, ROI, selected step, draft parameters, Preview, Publish, Run, or
  validation state.

### First visible action

Authoring now places one concise guide immediately below `Inspection
Configuration`:

`1 Input -> 2 Select tool -> 3 ROI -> 4 Preview`

This guide points to existing explicit actions; it does not execute or mutate
anything.

## Acceptance evidence

| Criterion | Evidence | Result |
|---|---|---|
| Loaded Wide has no overlapping Selected Tool controls | `after/wide-authoring-after.png` and `after/wide-selected-tool-layout.txt` | Pass |
| Loaded Compact has no overlapping controls or clipped required actions | `after/compact-selected-tool-after.png` and `after/compact-selected-tool-layout.txt` | Pass |
| Wide Recipe Center and responsibility labels remain readable | `after/wide-authoring-after.png` | Pass |
| Compact language value is adaptive rather than clipped | `after/compact-selected-tool-after.png` (`한`) | Pass |
| Selected Tool can side-collapse and Viewer reclaims the width | `after/wide-selected-tool-collapsed.png` | Pass |
| Collapse restores without changing composition | Workbench docking verification | Pass |
| Source Quality and Selected Tool cannot render together | Workbench docking verification | Pass |
| First normal workflow is visible | Wide capture and Workbench docking verification | Pass |
| Explicit recipe/ROI/Preview/Run contracts remain intact | Inspection Workspace and Validation Set verification | Pass |

All after images are application-only captures from the current Release build
and passed screenshot quality on the first attempt.

## Verification

- `dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"`
  - Pass: `0` warnings, `0` errors.
- `OpenVisionLab.ThreeD.Shell.exe --verify-workbench-docking <report>`
  - Pass: `75/75`.
  - Includes Source Quality/Selected Tool mutual exclusion, Wide `140` /
    Compact `60` rail, side-collapse availability, collapse/restore
    round-trip, and first-action guide.
- `--verify-inspection-workspace-selection`
  - Pass: `63/63`.
- `--verify-validation-set`
  - Pass: `84/84`.
- `--verify-c3d-height-distribution`
  - Pass: `25/25`.
- `scripts/verify-code-structure.ps1`
  - Pass: `17/17`.
- `scripts/start-human-owner-r0.ps1 -Layout Wide -ValidateOnly`
  - Pass: fixed inputs and current Release validated; no application launched.
- `scripts/start-human-owner-r0.ps1 -Layout Compact -ValidateOnly`
  - Pass: fixed inputs and current Release validated; no application launched.

Reports:

- `artifacts/current/20260731-authoring-panel-integrity-and-collapse/final-workbench-docking.txt`
- `artifacts/current/20260731-authoring-panel-integrity-and-collapse/final-inspection-workspace.txt`
- `artifacts/current/20260731-authoring-panel-integrity-and-collapse/final-validation-set.txt`
- `artifacts/current/20260731-authoring-panel-integrity-and-collapse/final-height-distribution.txt`
- `artifacts/current/20260731-authoring-panel-integrity-and-collapse/final-code-structure.txt`

## Visual comparison

Before, the Selected Tool header, Source Quality summary, ROI controls, and
output cards occupied the same vertical coordinates. After, one selected-tool
surface owns the pane, its four task sections form a stable vertical sequence,
and remaining content is reached through one vertical scroll.

Wide keeps readable responsibility and Recipe Center labels. Compact uses
icons for the responsibility rail and the adaptive `한`/`EN` language value,
without ellipsis hiding required meaning. When Selected Tool is side-collapsed,
its narrow auto-hide tab remains discoverable and the Viewer expands into the
released width.

## Fixed R0 package

The authoring UI change supersedes every earlier unaided R0 attempt. The
launcher now pins these current binary hashes:

| Input | SHA-256 |
|---|---|
| Release EXE | `01B857854B4E34D62E0E2C99EC523FA5BF81CCB6A7AD14173DBE5868F76C8719` |
| Shell assembly | `8066123F3818C7D7B2B734B7BB265919A5D91DCFE0CCEBBD2B2A8D6AD9FA984B` |
| Docking assembly | `A271EDD087D6598D5BB37CD16242A8390BFCEE1F7CC39F56317963F09F76D523` |

Automated `ValidateOnly` proves package integrity only. It does not replace the
product owner's unaided Wide and Compact operation.

## Completion record

Status: Complete
Scope: Removed the Selected Tool overlap, protected required navigation and
language labels from clipping, added safe side-collapse for task/support panes,
and added one visible input-to-Preview guide.
Acceptance criteria: Wide no overlap/clipping -> Pass; Compact no
overlap/clipped required action -> Pass; side-collapse and Viewer width return
-> Pass; collapse/restore non-mutation -> Pass; reusable AGENTS layout gate ->
Pass.
Verification: Release `0/0`; Workbench docking `75/75`; Inspection Workspace
`63/63`; Validation Set `84/84`; Height distribution `25/25`; structure
`17/17`; current application-only Wide/Compact/collapsed capture quality Pass;
R0 Wide/Compact `ValidateOnly` Pass.
Evidence:
`artifacts/current/20260731-authoring-panel-integrity-and-collapse/`, this
document, `AGENTS.md`, and `scripts/start-human-owner-r0.ps1`.
Boundary / next dependency: `A-01` remains Partial until the human product
owner completes both current fixed-hash runs unaided. Only after that external
gate passes may `J-01/J-03/J-04 SurfaceModel` begin. Physical calibration and
metrology remain unverified.
