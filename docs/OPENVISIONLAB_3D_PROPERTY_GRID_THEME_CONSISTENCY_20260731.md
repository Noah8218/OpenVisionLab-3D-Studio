# OpenVisionLab 3D PropertyGrid Theme Consistency Closure

Date: 2026-07-31

Status: Complete

## Operator problem

The Surface Match parameter search, property-name cells, and numeric editors
rendered with a private light palette inside the graphite Selected Tool pane.
The abrupt white block made the parameter surface look like a separate product
and weakened the visual relationship between the selected step and its
configuration.

This repair applies the OpenVisionLab principle that one task surface should
have one coherent visual system.

## Completed scope

- Removed 22 light hard-coded PropertyGrid brushes from
  `RecipeStepPropertyGridHost`.
- Kept the third-party theme contract view-local, but aliased every package
  role to existing OpenVision semantic resources:
  - surfaces: `Panel`, `PanelAlternate`, `CommandBar`, `SelectedSurface`;
  - text: `Text`, `PrimaryText`, `MutedText`, `Disabled`;
  - editors: `Control`, `Divider`, `DisabledSurface`;
  - interaction: `Accent`, `Focus`.
- Added an explicit parameter-search style for normal, pointer hover, keyboard
  focus, read-only, disabled, and validation-error borders and text.
- Added stable automation IDs for the parameter search and inner PropertyGrid.
- Added a deterministic smoke option that focuses the search field without
  changing recipe, source, ROI, Preview, Publish, Run, or Validation state.
- Extended the existing Recipe Manager/WPG verifier to compare 13 local
  package roles with the owning product-theme brush colors.
- Added the reusable theme-integrity rule to the global Codex `AGENTS.md` so
  future controls must be reviewed in normal and interactive states and may
  not leak framework, operating-system, browser, or third-party defaults.

## Acceptance and evidence

| Criterion | Evidence | Result |
| --- | --- | --- |
| Parameter surface uses the OpenVision graphite system | Current Release Wide and Compact after captures | Pass |
| Search keyboard focus remains visible and product-consistent | Compact focused-search capture | Pass |
| Required labels, values, and commands do not overlap or clip | Visual review at `1920 x 1040` and `1280 x 760` | Pass |
| No unintended horizontal or nested scroll bar appears | Visual review of expanded Surface Match parameters | Pass |
| Package theme roles remain view-local and match product tokens | Recipe Manager/WPG verification `38/38` | Pass |
| Focus smoke route remains recognized and bounded | Shell smoke option verification `26/26` | Pass |
| Presentation change does not change inspection workflows | Workbench `76/76`, Inspection Workspace `63/63`, Validation Set `84/84` | Pass |

The screenshot-quality white-pixel signal also records the removed light-theme
leak:

| Layout | Before | After |
| --- | ---: | ---: |
| Wide `1920 x 1040` | `2.45%` | `0.03%` |
| Compact `1280 x 760` | `3.57%` | `0.09%` |

This ratio is supporting evidence only; visual inspection remains the layout
and theme acceptance gate.

## Verification

- Release solution build: Pass, `0` warnings / `0` errors.
- Recipe Manager and PropertyGrid: Pass, `38/38`.
- Shell smoke command-line options: Pass, `26/26`.
- Workbench docking/layout: Pass, `76/76`.
- Inspection Workspace: Pass, `63/63`.
- Validation Set: Pass, `84/84`.
- Height distribution: Pass, `25/25`.
- Code structure: Pass, `17/17`.
- Current Release capture quality: all final captures accepted on attempt `1`.

## Evidence locations

- true pre-change evidence:
  `artifacts/current/20260731-property-grid-theme-consistency/before/`;
- current Release after evidence:
  `artifacts/current/20260731-property-grid-theme-consistency/after/`;
- verification reports:
  `artifacts/current/20260731-property-grid-theme-consistency/verification/`.

## Boundary and next dependency

This closure proves the current dark product theme, the visible Surface Match
numeric editor state, and the parameter-search normal/focus states at the two
supported layouts. The global rule requires future control types and product
themes to add their smallest applicable state evidence, including popup and
validation-error rendering when those states are introduced or changed.

Human-owner unaided Wide/Compact R0 remains the external acceptance task for
`A-01`; automated evidence does not replace it. The next deterministic product
priority remains `K-02/K-03/K-06` identified model/scene 3D-edge artifacts and
separate surface/edge scores.

## Durable closure record

Status: Complete

Scope: Surface Match parameter search, PropertyGrid rows/editors, shared theme
aliases, focus smoke, and reusable global theme-integrity rule.

Acceptance criteria: graphite consistency -> Pass; keyboard focus -> Pass;
Wide/Compact overlap and required-text clipping -> Pass; no inspection side
effect -> Pass.

Verification: Release `0/0`; Recipe Manager/WPG `38/38`; smoke options `26/26`;
Workbench `76/76`; Inspection Workspace `63/63`; Validation Set `84/84`;
height distribution `25/25`; structure `17/17`; final screenshot quality Pass.

Evidence: `artifacts/current/20260731-property-grid-theme-consistency/`.

Boundary / next dependency: human-owner R0 remains external; next software
slice is `K-02/K-03/K-06`.
