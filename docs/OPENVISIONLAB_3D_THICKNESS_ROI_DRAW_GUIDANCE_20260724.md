# Thickness ROI draw guidance and workflow layout - 2026-07-24

## Owner finding

During the restarted first-recipe replay, the owner reached the Thickness step
but stopped at stage 3:

- `Step Parameters` was separated from `Inspection Flow`, so the left-to-right
  workflow did not match the staged authoring model;
- selecting `Capture/Replace ROI` entered a valid `0/2` capture, but the Viewer
  did not make the required gesture discoverable;
- left-drag rotated the camera instead of drawing the expected ROI.

This was a real interaction conflict, not only missing help text.

## Decision

The normal wide Workbench order is now:

`Inspection Tools -> Inspection Flow -> Step Parameters -> 3D View`

All panes remain AvalonDock views and retain float/dock behavior. At compact
width, Inspection Tools and Inspection Flow remain tabbed in the first dock
group, followed by Step Parameters and the 3D View.

GridRectangle teaching supports both familiar entry methods:

1. left-click two opposite surface corners; or
2. left-drag diagonally across the intended ROI.

During GridRectangle capture, left-drag is reserved for ROI teaching and shows
a yellow translucent rubber-band rectangle. Middle/right drag retain camera
pan. The Viewer cursor changes to a crosshair, and a bilingual in-Viewer prompt
shows the exact next action for `0/2`, `1/2`, and `2/2`.

`Apply ROI / selection` remains explicit. Drawing a candidate does not modify
the authored recipe, invoke Preview/Publish/Run, or change the camera.

## UI density correction

The selected-step context and active capture ribbon now span all three Viewer
header columns. This prevents the Apply button from being clipped at
`1280 x 760` while preserving the full wide layout at `1920 x 1040`.

## Operator procedure

For a Thickness step:

1. select the Thickness node in Inspection Flow;
2. in Step Parameters, select `Capture ROI` or `Replace ROI`;
3. in the 3D View, either drag diagonally across the intended surface or
   left-click two opposite corners;
4. confirm the Viewer prompt changes to `ROI ready`;
5. select `Apply ROI / selection`;
6. edit tolerances and invoke Preview explicitly.

## Verification

- Release solution build: pass, `0` warnings / `0` errors.
- Docking workspace: pass, `28/28`, including the new
  `Inspection Flow -> Step Parameters -> 3D View` order.
- Generic height measurement Workbench: pass, `28/28`.
- Teaching capture ViewModel: pass, `18/18`.
- Actual Release EXE Windows-pointer drag at `1920 x 1040`: pass.
- Actual Release EXE Windows-pointer drag at `1280 x 760`: pass.
- Both pointer runs produced a transient `2/2`, `CanApply=true`
  GridRectangle candidate from one left-drag.
- Both pointer runs retained camera, authored selection count/schema/route,
  Preview status, and result entity identity.
- Both current EXE screenshots passed quality inspection on attempt 1; the
  compact Apply button is fully visible.

Evidence:

- `artifacts/current/20260724-thickness-roi-draw-guidance/before-layout-and-capture.png`
- `artifacts/current/20260724-thickness-roi-draw-guidance/after-layout-and-drag-ready.png`
- `artifacts/current/20260724-thickness-roi-draw-guidance/after-layout-and-drag-ready-1280.png`
- `artifacts/current/20260724-thickness-roi-draw-guidance/actual-exe-thickness-replace-drag-ready.drag-pointer.txt`
- `artifacts/current/20260724-thickness-roi-draw-guidance/actual-exe-thickness-replace-drag-ready-1280.drag-pointer.txt`
- `artifacts/current/20260724-thickness-roi-draw-guidance/release-build-final.txt`
- `artifacts/current/20260724-thickness-roi-draw-guidance/workbench-docking-final.txt`
- `artifacts/current/20260724-thickness-roi-draw-guidance/height-measurement-workbench-final.txt`
- `artifacts/current/20260724-thickness-roi-draw-guidance/teaching-capture-viewmodel-final.txt`

## Boundary

The current generic Thickness adapter still calculates scalar height
statistics within one recipe-owned GridRectangle. This checkpoint proves
discoverable ROI authoring and interaction boundaries; it does not prove a
calibrated two-surface physical-thickness algorithm or metrology.

## Completion record

Status: Complete

Scope: staged panel order, bilingual in-Viewer ROI guidance, two-click and
left-drag GridRectangle teaching, compact capture-ribbon density, and actual
pointer evidence.

Acceptance criteria:

- Step Parameters is adjacent to Inspection Flow: pass;
- the operator can see how to start and complete ROI drawing: pass;
- a real left-drag produces a ready ROI candidate: pass at both resolutions;
- drawing does not mutate the recipe or execute inspection: pass;
- compact and wide Apply controls are not clipped: pass.

Verification: Release build `0/0`, docking `28/28`, height measurement
Workbench `28/28`, capture ViewModel `18/18`, two current actual-EXE pointer
runs and screenshots pass.

Evidence: this document and
`artifacts/current/20260724-thickness-roi-draw-guidance/`.

Boundary / next dependency: the owner must restart the unaided first-recipe
replay on the updated EXE to close the external usability acceptance gate.
