# OpenVisionLab 3D Layout Redesign Conversation Handoff

Date: 2026-08-01

Status: Historical layout-stream handoff; superseded on 2026-08-03

## Superseding status - 2026-08-03

The product owner explicitly left this layout-only stream and approved
`J-12 Multiple-match result collection`, `K-09 Multiple-match issue
navigation`, `F-13 SurfaceModel symmetry declaration`, and `J-13
Symmetry-aware pose equivalence`, followed by `J-05 Model surface selection`.
All are now Complete. J-12 uses committed
Noah `4e301f481cac886f78425197314cd540b653473a`, vendored
`Lib.ThreeD 2.8.9`, stable/disjoint two-object evidence, and current Release
Wide/Compact UI evidence. K-09 adds non-wrapping selector-synchronized
Previous/Next review with Workbench `10/10` and current first/last-state
evidence. F-13 adds saved schema-1.1 none/discrete-axis declarations while
preserving exact schema-1.0 bytes. J-13 uses committed Noah
`f225fd2709de1dd1d0ecfe19b37315cb1f019ee4` through vendored
`Lib.ThreeD 2.8.10` for independent direct and declared cyclic pose
equivalence without changing J-12 execution. The current inventory is
superseded by J-07, which uses committed Noah
`7ed50ea37b3d7cb711c2afe698d209f9073e9217` through vendored
`Lib.ThreeD 2.8.12` for deterministic model key points over the completed J-05
active source-triangle selection. The current inventory is
`136 C / 17 P / 57 N / 9 E / 16 O`.

Use
`docs/OPENVISIONLAB_3D_MODEL_KEY_POINT_ARTIFACT_AND_DEBUG_OVERLAY_20260803.md`,
`docs/OPENVISIONLAB_3D_MODEL_SURFACE_SELECTION_20260803.md`,
`docs/OPENVISIONLAB_3D_SYMMETRY_AWARE_POSE_EQUIVALENCE_20260803.md`,
`docs/OPENVISIONLAB_3D_MULTIPLE_MATCH_ISSUE_NAVIGATION_20260803.md`,
`docs/OPENVISIONLAB_3D_MULTIPLE_SURFACE_MATCH_RESULT_COLLECTION_20260803.md`,
`docs/OPENVISIONLAB_3D_SURFACE_MODEL_SYMMETRY_DECLARATION_20260803.md`,
and `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md` as the active continuation
entry points. The next priority is `B-12 Acquisition/source provenance text
and limitation notes`. Recommended model: `gpt-5.6-sol`; reasoning effort:
high. `K-04` remains blocked until B-12 passes; L-13 is independently
dependency-ready.
The layout contracts in this document remain binding history and regression
requirements, but this file no longer selects the active work stream.

## Purpose

This document historically handed the layout and UI-design conversation to the next Codex
session without reopening completed work or drifting into the algorithm
backlog. The product owner explicitly requested that the next conversation
continue the layout stream first. `J-12 Multiple-match result collection`
remains a valid software backlog item, but it is deferred while this
layout-only continuation is active.

Future layout work must treat this file as the preserved layout-contract source
of truth, then use the linked closure documents for implementation and evidence
detail.

## Product and benchmark boundary

OpenVisionLab 3D Studio is a local, file-first, deterministic rule-based 3D
inspection workbench. The Viewer is the dominant teaching, measurement, and
evidence surface; the product is not only a model viewer.

GoPxL is benchmark evidence for abstract workflow principles:

- keep the selected Tool, its configuration, Viewer evidence, and result
  context linked;
- make the current task, status, and next safe action obvious;
- use progressive disclosure instead of presenting every control and message
  at once;
- allow support panes to collapse so the Viewer can reclaim space;
- preserve deterministic evidence and separate display state from inspection
  state.

Do not copy GoPxL theme, colors, panel proportions, docking topology, product
names, icon artwork, assets, screenshots, code, or industrial-platform scope.
Visual similarity is not an acceptance criterion. Every new change must name
the OpenVisionLab operator problem, the abstract principle being adapted, the
independent OpenVisionLab design, and current-build evidence that it works.

There is no approved full-layout redesign. Preserve the completed Workbench
v4 architecture and improve it only through bounded, evidence-backed slices.

## Completed layout and UI changes from this conversation

| Area | Completed behavior | Durable evidence |
| --- | --- | --- |
| Global shell density | One `56 px` Job Bar owns product/recipe/source/status/window context. The former second stage row and full-width workspace command row were removed. | `OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_LAYOUT_CONTRACT_20260730.md` |
| Responsibility navigation | Stable Authoring, Validate, Results, Calibration, and Advanced rail. Wide shows readable icon+text; Compact uses purposeful icons with tooltips and accessible names. | Workbench v4 contract; authoring integrity closure |
| Unified Authoring | Setup/Teach are presented as one normal Authoring destination while existing lifecycle guards remain. Wide orders Recipe Chain/Tool Library -> Selected Tool -> dominant Viewer. Compact shows one support tab group beside the Viewer. | Workbench v4 contract |
| Viewer command density | Familiar presentation actions use compact icons with selected state, tooltip, accessible name, and AutomationId. Ambiguous or consequential actions retain text. | `OPENVISIONLAB_3D_VIEWER_COMMAND_BAR_SIMPLIFICATION_20260730.md` |
| Single-row Viewer | Loaded Single Viewer uses one shared row: geometry -> Height Image/layout -> projection/fit/overflow. Redundant source-ready, `Main`, status, and left text-HUD rows no longer consume canvas space. | `OPENVISIONLAB_3D_VIEWER_SINGLE_ROW_AND_HEIGHT_COLOR_RANGE_20260730.md` |
| Height color range | The right legend owns visible High/Low decrement, value, increment, and `AUTO` controls. The interval changes display normalization only; it does not mutate source values, recipes, measurements, or decisions. | Viewer single-row/Height range closure |
| Linked display range | Same-source 3D Viewer and full Height Image share exact manual/AUTO bounds in both directions. Source histograms and palettes remain independent, and recipe/execution state remains unchanged. | `OPENVISIONLAB_3D_LINKED_VIEW_DISPLAY_RANGE_CONSISTENCY_20260803.md` |
| Canvas text removal | Persistent measurement text on the left side of the 3D canvas was removed. The lower-left orientation gizmo remains; the right-side height legend remains. | Viewer single-row/Height range closure |
| Dock-tab discovery | Multi-item AvalonDock tabs moved to the top. Duplicate pane titles are suppressed only when the top strip owns navigation; single-item panes retain their title. | `OPENVISIONLAB_3D_VALIDATION_TOP_DOCK_TABS_20260730.md` |
| Validate/Results evidence layout | Validate and read-only Results place linked evidence beside the same dominant Viewer. Sample selection is display-only; Run and correction remain explicit. | `OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md` |
| Visual system | One OpenVision graphite semantic role system owns Shell, panels, controls, focus, selection, and semantic result colors. Scientific Viewer height colors remain independent. | Workbench v4 evidence/safe-layout closure |
| PropertyGrid theme | Search, property cells, numeric editors, focus, hover, disabled/read-only, and validation states use OpenVision semantic roles instead of a private light palette. | `OPENVISIONLAB_3D_PROPERTY_GRID_THEME_CONSISTENCY_20260731.md` |
| Overlap repair | Source Quality and Selected Tool content are mutually exclusive. Dense Inputs/Parameters/ROI/Outputs/Help sections use deterministic measurement and one vertical scroll owner. | `OPENVISIONLAB_3D_AUTHORING_PANEL_INTEGRITY_AND_SIDE_COLLAPSE_20260731.md` |
| Required-text integrity | Wide rail is `140 px`; Compact rail is `60 px`. Required navigation/actions are readable or adapt semantically; only secondary identifiers may trim with a full-value tooltip/detail route. | Authoring integrity closure and `AGENTS.md` UI gate |
| Side collapse | Task/support panes can auto-hide and return width to the Viewer. Selected Tool, Tool Library/Recipe Chain, evidence/results, linked view, and profile support surfaces participate. The Viewer and calibration safety anchors remain fixed. | Authoring integrity closure |
| First-use clarity | Empty Authoring exposes one primary `Open 3D input` action. Input-ready state directs the operator to select a Tool. Selected-Tool state directs `Set ROI -> Preview`. Duplicate waiting/input messages were removed. | `OPENVISIONLAB_3D_GOPXL_FIRST_USE_AUTHORING_CLARITY_20260731.md` |
| Safe persistence | Wide/Compact pane ratios and allowlisted presentation IDs are stored separately, validated, restored atomically, and reset explicitly. Recipe/source/ROI/command/result state is never persisted as layout. | Workbench v4 evidence/safe-layout closure |

These items are completed foundations. Do not rebuild them in a competitor's
form or reopen them without fresh evidence that an acceptance criterion has
regressed.

## Current supported layout composition

Wide `1920 x 1040`:

```text
┌──────────────────────────────── one Job Bar ────────────────────────────────┐
├──────────────┬────────────────────┬───────────────────────┬─────────────────┤
│ responsibility│ Tool Library /     │ Selected Tool         │ Viewer          │
│ rail 140 px   │ Recipe Chain       │ configuration + ROI   │ dominant        │
│               │ collapsible        │ collapsible           │ fixed           │
└──────────────┴────────────────────┴───────────────────────┴─────────────────┘
```

Compact `1280 x 760`:

```text
┌──────────────────────────────── one Job Bar ────────────────────────────────┐
├───────┬──────────────────────────────┬───────────────────────────────────────┤
│ rail  │ one active support tab group │ Viewer dominant                      │
│ 60 px │ Tools / Chain / Selected /   │                                       │
│ icons │ Outputs                      │                                       │
└───────┴──────────────────────────────┴───────────────────────────────────────┘
```

Validate and Results keep a bounded evidence pane beside the same Viewer.
Wide defaults to evidence `1.60*` and Viewer `2.70*`; Compact defaults to
evidence `1.05*` and Viewer `2.45*`. Safe user-adjusted ratios are remembered
independently by layout profile.

## Preserved interaction contracts

Every next layout change must preserve all of the following:

- Preview, Publish, Run, sample execution, and Validation remain explicit;
- selecting a step/sample, switching tabs, collapsing/restoring panes,
  changing visibility, or restoring layout never executes inspection;
- output creation does not change the input layer or active recipe step;
- PropertyGrid remains the algorithm-Tool editing surface;
- Viewer zoom, pan, drag, fit, ROI overlays, Height controls, comparison,
  split/stack/pop-out, docking, and native window behavior remain available;
- Results remains read-only and routes correction explicitly;
- collapse/restore never mutates recipe, source, ROI, selected Tool, draft
  parameters, Published/Candidate evidence, or validation state;
- icon-only controls require a localized tooltip, accessible name, stable
  AutomationId, and visible selected/focus state;
- new or changed controls must use the OpenVision semantic theme in normal,
  hover, focus, selected/checked, disabled/read-only, validation, and popup
  states where applicable.

## Current layout acceptance status

The completed software slices have current-build Wide and Compact evidence for
their owning changes. Workbench v4 `3/3`, Viewer simplification, single-row
Height controls, top dock tabs, Authoring overlap/clipping repair,
side-collapse, first-use clarity, safe layout persistence, PropertyGrid theme
consistency, and linked-view display-range consistency are recorded as
Complete in their closure documents.

This does not close human usability. Human-owner unaided Wide/Compact R0 is
still required for `A-01`, Workspace v3 `8/8`, and any release-usability claim.
Automated `-ValidateOnly`, screenshots, geometry probes, and smoke checks do
not replace that operation. Missing R0 evidence does not globally block
dependency-ready deterministic software work.

Historical product inventory at this layout handoff:

```text
130 Complete / 17 Partial / 63 Not started / 9 External / 16 Out of scope
```

## Remaining layout-only candidates

Do not assume that a complete shell redesign is the next task. Start with a
fresh current-build audit and choose one bounded operator problem. The current
candidate list is:

1. `A-11 Per-Tool state presentation matrix` — currently blocked by `A-09`
   and the human-owner `A-01` evidence. Do not spend implementation tokens on
   this as if the prerequisite were complete. Prerequisite first; no model
   tokens until it is available.
2. `M-18/M-19` — apply accessibility-name/tooltip and Korean/English capture
   coverage to each new visible control or state; these are per-change gates,
   not permission for decorative icon work. Recommended model:
   `gpt-5.6-terra`; reasoning effort: low.

There is no dependency-ready standalone layout implementation in this list.
The owner must either supply the `A-09/A-01` prerequisites or explicitly
return to the numerical stream. If the latter is approved, the next item is
`J-12 Multiple-match result collection` with `gpt-5.6-sol`, high reasoning.

`A-12 Global current-source quality state` and its fresh loaded/empty
Authoring, Validate, and Results audit are Complete. Preserve
`OPENVISIONLAB_3D_GLOBAL_CURRENT_SOURCE_QUALITY_STATE_20260803.md` and the
D-backed `20260803-a12-current-source-quality-layout` evidence.

`A-16 Advanced workspace semantic-theme parity` is Complete. Preserve
`OPENVISIONLAB_3D_ADVANCED_SEMANTIC_THEME_PARITY_20260803.md` and the D-backed
`20260803-a16-advanced-semantic-theme-parity` evidence. The closure covers
Wide/Compact English/Korean surfaces, generated input/tab states, and an
actual open ComboBox popup without changing inspection state.

`C-13 Linked-view display-range consistency` is Complete. The 3D Viewer and
full Height Image now share exact manual/AUTO bounds only when their source
content SHA matches. Each view retains its palette and the 3D source histogram
remains full-source. Preserve
`OPENVISIONLAB_3D_LINKED_VIEW_DISPLAY_RANGE_CONSISTENCY_20260803.md` and the
D-backed `20260803-c13-linked-view-display-range-consistency` evidence.

`J-12` and other numerical/algorithm work are deliberately not part of the
next layout-only conversation unless the product owner explicitly changes the
priority again.

## Next-chat operating procedure

1. Work in `C:\Git\OpenVisionLab-3D-Studio`.
2. Run `git status --short` and `git log --oneline -5` first.
3. Read `AGENTS.md`, this document, the next-session handoff, and the linked
   layout closure documents before selecting a change.
4. Preserve all existing uncommitted Studio work and the user-owned untracked
   `3D/SSD-Black/`, `3D/TLB/`, `3D/fccsp/`, and `3D/새 폴더/` directories.
5. Build the current Release before UI evidence. Capture a genuine current
   baseline before editing at Wide `1920 x 1040` and Compact `1280 x 760`.
6. Test on the dynamically selected leftmost monitor and store test evidence
   physically under
   `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\`.
7. State the concrete operator problem, included/excluded scope, acceptance
   criteria, and evidence matrix before implementation.
8. Prefer the smallest independent OpenVisionLab layout change that solves the
   evidence-backed problem. Do not use resemblance to GoPxL as the gate.
9. Inspect normal, loaded/empty, expanded/collapsed, focus/hover, disabled or
   read-only, validation, popup, and localized states that the change can
   affect.
10. A layout task is not Complete until fresh before/after captures at both
    supported sizes show no unexplained overlap, clipped required text/action,
    out-of-pane or unreachable controls, or unintended horizontal/nested
    scrollbars, and focused non-mutation checks pass.
11. Refresh the fixed R0 hashes and rerun both `-ValidateOnly` modes when the
    UI slice changes the R0 binary set.
12. Do not commit or push unless the product owner explicitly requests it.

## Required reading for the next layout session

Read in this order:

1. `AGENTS.md`;
2. `docs/OPENVISIONLAB_3D_LAYOUT_REDESIGN_CONVERSATION_HANDOFF_20260801.md`;
3. `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`;
4. `docs/OPENVISIONLAB_3D_GOPXL_BENCHMARK_APPROVED_DIRECTION_20260731.md`;
5. `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_LAYOUT_CONTRACT_20260730.md`;
6. `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md`;
7. `docs/OPENVISIONLAB_3D_AUTHORING_PANEL_INTEGRITY_AND_SIDE_COLLAPSE_20260731.md`;
8. `docs/OPENVISIONLAB_3D_GOPXL_FIRST_USE_AUTHORING_CLARITY_20260731.md`;
9. `docs/OPENVISIONLAB_3D_VIEWER_COMMAND_BAR_SIMPLIFICATION_20260730.md`;
10. `docs/OPENVISIONLAB_3D_VIEWER_SINGLE_ROW_AND_HEIGHT_COLOR_RANGE_20260730.md`;
11. `docs/OPENVISIONLAB_3D_VALIDATION_TOP_DOCK_TABS_20260730.md`;
12. `docs/OPENVISIONLAB_3D_PROPERTY_GRID_THEME_CONSISTENCY_20260731.md`;
13. `docs/OPENVISIONLAB_3D_ADVANCED_SEMANTIC_THEME_PARITY_20260803.md`.

## Completion record

Status: Complete

Scope: Consolidate the completed layout-design changes, non-copy boundary,
current Wide/Compact composition, preserved interaction contracts, remaining
layout candidates, evidence gate, and next-chat startup procedure.

Acceptance criteria: The next session has one layout-only entry point; all
completed work links to its durable evidence; `J-12` is retained but explicitly
deferred; human R0 and blocked layout work are not misrepresented as complete;
GoPxL remains a principle benchmark rather than a visual template.

Verification: Cross-checked against the approved benchmark direction,
Workbench v4 layout and safe-persistence closures, Viewer simplification and
Height-range closures, top dock-tab closure, Authoring integrity/side-collapse
closure, first-use clarity closure, PropertyGrid theme closure, and the current
master backlog statuses.

Evidence: This document and the linked current layout closure documents.

Boundary / next dependency: This documentation handoff changes no UI or
inspection behavior and therefore requires no new UI capture. The next session
must obtain fresh current-build Wide/Compact baseline evidence before making
another visible change. Human-owner R0 remains external.
