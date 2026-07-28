# Surface ROI usability correction - 2026-07-25

## Outcome

Owner review reopened the prior Surface ROI usability claim. The saved
`GridRectangle` was technically editable, but its mean-height outline could be
hidden by the C3D surface, its hit testing required a valid rendered C3D point,
and the numeric editor was below the useful part of Step Parameters.

The corrected interaction now provides:

- a depth-independent active ROI overlay with a translucent yellow fill,
  `4 px` outline, four `16 px` visible corner handles, and a center move marker;
- a thin blue authored-ROI outline while its yellow replacement candidate is
  active, so saved and transient geometry are visually distinct;
- fixed `18 px` screen-space corner hit targets;
- hover cursor and status feedback for move versus corner resize;
- X/Z footprint-plane pointer mapping at the C3D mean height, independent of
  valid or missing surface cells;
- interior drag to move and corner drag to resize, clamped to the exact bound
  source grid;
- a compact numeric editor placed first in Teaching Selections with
  `Start X (column)`, `Start Z (row)`, `Width (columns)`, and `Height (rows)`;
- all four numeric fields visible at `1280 x 760` without scrolling to a
  second field row.

The existing Viewer capture ribbon remains the single primary Apply/Cancel
location. The duplicate Apply button was removed from the numeric card.

## Preserved contract

`GridRectangle` remains an integer height-field footprint:

- `X = column`;
- `Z = row`;
- pointer mapping intersects the displayed X/Z footprint plane;
- values are clamped and validated against the exact source grid;
- editing remains transient until explicit Apply;
- Cancel retains authored geometry;
- Apply replaces the same selection identity;
- Preview, Publish, and Run are never invoked by selection, hover, move,
  resize, numeric edit, or Apply.

This does not reinterpret `GridRectangle` as an XYZ volume. A future
center/size/rotation volume remains a separate typed `OrientedBox3D`.

## Verification

Current Release evidence:

- solution build: pass, `0` warnings / `0` errors;
- Viewer display/camera report: pass, `96/96`;
- teaching-capture ViewModel: pass, `20/20`;
- generic height measurement Workbench: pass, `32/32`;
- Tool Recipe teaching: pass, `27/27`;
- docking workspace: pass, `28/28`;
- actual Windows pointer hover: move and resize modes pass;
- actual Windows pointer edit: move and corner resize pass with two routed
  down/move/up gesture sequences;
- the exercised C3D contains missing cells, while the interaction report
  confirms `surfacePointRequired=False` and `footprintPlane=XZ@mean`;
- camera, authored selection, Preview, and result references remain unchanged
  before Apply;
- explicit Apply preserves the selection identity and still leaves
  Preview/Publish/Run untouched;
- current Release screenshots at `1920 x 1040` and `1280 x 760` pass quality
  on attempt 1.

Evidence:

- `artifacts/current/20260725-surface-roi-usability-correction/before-surface-roi-1920x1040.png`
- `artifacts/current/20260725-surface-roi-usability-correction/after-surface-roi-1920x1040.png`
- `artifacts/current/20260725-surface-roi-usability-correction/after-surface-roi-1280x760.png`
- `artifacts/current/20260725-surface-roi-usability-correction/pointer-hover-move-resize-after.drag-pointer.txt`
- `artifacts/current/20260725-surface-roi-usability-correction/explicit-apply-after.txt`
- `artifacts/current/20260725-surface-roi-usability-correction/viewer-display-final.txt`
- `artifacts/current/20260725-surface-roi-usability-correction/teaching-capture-viewmodel-final.txt`
- `artifacts/current/20260725-surface-roi-usability-correction/height-measurement-workbench-final.txt`
- `artifacts/current/20260725-surface-roi-usability-correction/recipe-teaching-final.txt`
- `artifacts/current/20260725-surface-roi-usability-correction/docking-final.txt`

## Boundary

This closes software usability for the current C3D `GridRectangle` teaching
path. It does not prove arbitrary point-cloud or mesh volume editing, physical
calibration, two-surface physical thickness, metrology, or production-line
integration. Current Thickness remains one-ROI scalar height statistics.

## Completion record

Status: Complete

Scope: depth-independent ROI presentation, distinct saved/transient state,
fixed-pixel handles, hover feedback, footprint-plane move/resize, compact
numeric editing, and explicit Apply preservation.

Acceptance criteria:

- ROI edges and handles remain visible over the C3D surface: pass;
- pointer editing does not depend on valid C3D surface cells: pass;
- fixed-pixel hover, move, and corner resize work through actual Windows
  pointer input: pass;
- all four numeric fields are visible at `1280 x 760`: pass;
- authored recipe and execution state remain unchanged until explicit Apply:
  pass;
- explicit Apply preserves identity and does not execute inspection: pass.

Verification: Release build `0/0`; focused reports `96/96`, `20/20`, `32/32`,
`27/27`, and `28/28`; actual hover/move/resize and explicit Apply; current
wide/compact screenshot quality.

Evidence: this document and
`artifacts/current/20260725-surface-roi-usability-correction/`.

Boundary / next dependency: restart the unaided owner first-recipe replay on
this corrected Release EXE. Trusted real four-landmark data remains an
external prerequisite for physical alignment evidence.
