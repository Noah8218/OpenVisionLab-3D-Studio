# Grid ROI Coordinate Confidence Closure

Date: 2026-08-17
Status: Complete
Issue: `PL-0017`

## Outcome

GridRectangle teaching now enters the existing Top orthographic Viewer as soon
as capture begins and keeps the exact draft coordinates beside the Apply and
Cancel actions. Before Apply, the operator can see start column, start row,
column count, and row count without opening a deep Selected Tool section.

The normal contract is unchanged: drawing and adjustment create only a draft;
Enter or **Apply ROI / selection** commits it, Esc or **Cancel** discards it,
and neither path invokes Preview, Publish, Run, or Validation.

## Operator Problem And Product Boundary

The actual ten-recipe study found one wrong-region attempt because a
Perspective screen rectangle did not make the resulting grid row and column
obvious. Wide exposed the values only in a deep tool panel, while Compact hid
that panel during teaching.

The retained workflow principle is task-context clarity: keep
the current teaching state, exact spatial identity, and next action together.
The implementation remains an independent OpenVisionLab design using the
existing Viewer Top command, draft coordinate owner, semantic theme resources,
and explicit teaching lifecycle.

Camera, lighting, PLC, industrial I/O, robot, cloud, accounts, deployment, and
production-line control remain excluded. Synthetic raw-height evidence is not
calibrated metrology, Gauge R&R, or production approval.

## Implemented Contract

- GridRectangle capture switches from the current Perspective camera to the
  existing Top orthographic fit; other teaching selection kinds are unchanged.
- The capture ribbon shows `X=column, Z=row` and the exact start X, start Z,
  X length, and Z length from the current draft.
- The ribbon uses shared graphite semantic brushes and wraps within Wide and
  Compact instead of obscuring the Viewer or Apply/Cancel actions.
- Existing orbit, pan, zoom, picking, move, resize, display-height adjustment,
  Undo, Enter Apply, Esc Cancel, and explicit no-execution behavior remain.
- No new algorithm, recipe field, persistence setting, or numerical ownership
  was added; the Studio continues to consume the existing typed GridRectangle.

## Actual EXE Evidence

The current Release EXE was built first and placed on the dynamically selected
leftmost monitor. Both review layouts passed screenshot quality on attempt 1.

| Workflow | Start | Actual input | Candidate result | Outcome |
| --- | --- | --- | --- | --- |
| Wide `1920 x 1040`, English | Perspective | replacement ROI pointer capture | row `298`, column `175`, rows `58`, columns `22` | Top and exact ribbon visible; draft and execution unchanged |
| Compact `1280 x 760`, Korean | Perspective | replacement ROI pointer capture | row `298`, column `174`, rows `58`, columns `23` | Top and exact ribbon visible; no clipping or default-light leak |
| Reference target | Perspective | one actual drag | target coverage `0.9756` | explicit Apply restored the stable route |
| Measurement target | Perspective | one actual drag | target coverage `1.0000` | explicit Apply restored the stable route |

The two target workflows used exactly one pointer down and one pointer up each,
required no corrective redraw, and left Preview `NotRun` with zero result
entities. A separate actual-pointer workflow also passed orbit, pan, wheel
zoom, context menu and bindings, Undo/repick, Esc Cancel, a second capture,
explicit Apply, ROI move/resize/display-height adjustment, and camera/execution
boundaries.

A direct current-build operator review opened the saved `10-local-defect`
recipe from Perspective, navigated to the selected Thickness ROI, started
**Edit ROI**, observed Top orthographic plus exact coordinates, and cancelled
with Esc. The recipe and execution state remained unchanged.

## Verification

- Release solution build: `0` warnings, `0` errors.
- Height Measurement Workbench: `56/56`.
- Tool Recipe teaching: `50/50`.
- Inspection Workspace selection: `64/64`.
- Workbench docking/theme/state: `87/87`.
- Teaching capture ViewModel: `25/25`.
- Shell smoke command-line options: `40/40`.
- Code structure and Vision SDK ownership guard: `29/29`.
- Wide and Compact actual Release EXE coordinate-confidence smokes: pass;
  selected-monitor intersection true.
- Reference and Measurement target actual-pointer teaching: pass with one
  drag each, stable route restoration, and no Preview or Run.

Durable evidence:

- `.proofline/issues/PL-0017.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0017-grid-roi-coordinate-confidence\before\`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0017-grid-roi-coordinate-confidence\after\`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0017-grid-roi-coordinate-confidence\logs\`.

## Maturity And Remaining Priority

The evidence-bounded operator authoring-readiness assessment moves from
`8.5/10` to `8.6/10`. This is a qualitative workflow judgment: exact
coordinate feedback and a deterministic teaching view remove the observed
wrong-region retry, but product-owner unaided Wide/Compact R0 remains open.
It is not telemetry, certified usability, release acceptance, production
approval, or physical-metrology evidence. Capability inventory is unchanged.

1. Product-owner unaided Wide and Compact R0 | Prerequisite: owner operation
   and observer record | Recommended model: none | Reasoning effort: none.
2. Large-C3D memory/performance target | Prerequisite: representative maximum
   C3D plus accepted process-memory and load-time limits | Recommended model:
   none until available | Reasoning effort: none.

No dependency-ready software slice is selected after `PL-0017`.

## Closure Record

```text
Status: Complete
Scope: GridRectangle Top-view teaching entry, always-visible exact draft coordinates, Wide/Compact layout, actual-pointer reference/measurement teaching, and unchanged explicit Apply/Cancel/no-execution behavior
Acceptance criteria: C1 exact row/column/counts before Apply -> pass; C2 Wide/Compact readable and bounded -> pass; C3 navigation, adjustment, Enter/Esc and no-execution contracts -> pass; C4 reference and measurement targets from Perspective with no corrective redraw -> pass
Verification: Release 0/0; height measurement 56/56; Tool Recipe teaching 50/50; workspace selection 64/64; docking 87/87; teaching capture 25/25; Shell options 40/40; code structure 29/29; final actual Release Wide/Compact and dual-target pointer smokes pass; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_GRID_ROI_COORDINATE_CONFIDENCE_20260817.md; .proofline/issues/PL-0017.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260817-pl0017-grid-roi-coordinate-confidence/
Boundary / next dependency: synthetic raw-height is not physical metrology; product-owner unaided Wide/Compact R0 remains external; no dependency-ready software slice is selected; large-C3D work requires a representative maximum input and accepted budgets
```
