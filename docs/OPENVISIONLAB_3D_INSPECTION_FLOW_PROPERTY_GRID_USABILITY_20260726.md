# Inspection Flow and PropertyGrid usability - 2026-07-26

## Status

Status: Complete

## Owner finding

The selected Filter PropertyGrid compressed its search row and four parameter
rows into a fixed `150 px` host, making the last row look clipped or
overlapped. Inspection Flow showed the selected route but did not expose its
available actions. Reorder and delete were discoverable only inside the
collapsed English `Advanced identity & order` section in Step Parameters.

## Correction

- The typed PropertyGrid host is `210 px`, so the Filter search row and all
  four parameter rows remain visually separated.
- The selected inspection-step card now explains that settings, ordering, and
  removal are available.
- `Open detailed settings`, `Up`, `Down`, and `Remove step` use the existing
  Workbench ViewModel commands directly in Inspection Flow.
- The duplicate move/remove controls were removed from the advanced identity
  section; that section now owns only the technical Step ID.
- The current navigator item receives a visible selected surface and accent
  border.
- Reorder and removal mutate the recipe draft only. They do not invoke
  Preview, Run, or Publish.

## Verification

- Release build: `0` warnings, `0` errors.
- Recipe Manager/WPG: `37/37`, including selected-step reorder/removal with no
  Filter or Height Difference Edge Preview.
- Tool recipe teaching: `27/27`.
- Workbench docking: `28/28`.
- Current Release actual-window comparison confirms that all Filter parameter
  rows are separated and the four selected-step actions are visible.

## Evidence

- Owner-marked before:
  `artifacts/current/20260726-inspection-flow-property-grid-usability/before-owner-markup.png`
- Current Release after:
  `artifacts/current/20260726-inspection-flow-property-grid-usability/after.png`
- Verification reports:
  `artifacts/current/20260726-inspection-flow-property-grid-usability/`

## Completion record

Status: Complete

Scope: remove the selected-step PropertyGrid clipping and make Inspection Flow
step actions understandable and directly available.

Acceptance criteria: all Filter PropertyGrid rows are visually separated;
selected-step settings/reorder/delete actions are visible in Inspection Flow;
the current step is visually identifiable; reorder/delete remain recipe-only.

Verification: Release build `0/0`; Recipe Manager/WPG `37/37`; recipe teaching
`27/27`; docking `28/28`; actual current Release before/after comparison.

Evidence:
`artifacts/current/20260726-inspection-flow-property-grid-usability/`.

Boundary / next dependency: this closes the reported layout and action
discoverability defects. The owner must continue the unaided first-recipe
replay to validate the full workflow. Physical calibration and metrology
remain unverified.
