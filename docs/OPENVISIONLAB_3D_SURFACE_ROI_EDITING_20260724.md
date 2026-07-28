# Surface ROI selection and editing - 2026-07-24

> Superseded for usability evidence by
> `OPENVISIONLAB_3D_SURFACE_ROI_USABILITY_CORRECTION_20260725.md`. Owner review
> showed that the original mean-plane outline, surface-point hit testing, and
> below-fold numeric layout did not satisfy the practical visibility and
> manipulation criterion.

## Outcome

The existing height-field `GridRectangle` now has a complete selected/edit
interaction in the Workbench:

- selecting a visible Surface ROI outline selects its owning Inspection Flow
  step;
- the selected ROI is yellow, has four corner handles, and exposes a bilingual
  Viewer label;
- `Replace ROI` opens the authored rectangle immediately as a ready `2/2`
  transient candidate;
- dragging inside the candidate moves it, while dragging a corner resizes it;
- Step Parameters exposes row, column, row count, and column count with
  source-grid validation;
- Viewer pointer edits and numeric edits stay synchronized;
- Apply replaces the same selection identity once and remains explicit.

Selection and transient editing do not dirty the recipe or invoke Preview,
Publish, or Run. Cancel restores the authored geometry. Apply mutates only the
authored selection and invalidates no execution boundary by itself.

## Coordinate and type contract

`GridRectangle` remains a height-field surface footprint:

- `X = column`;
- `Z = row`;
- row/column counts are integer source-grid extents;
- the rectangle must stay inside the exact bound C3D grid;
- persisted selection ID, source SHA-256, grid dimensions, frame, and owner
  binding remain unchanged during replacement.

This is not an XYZ volume. A future center/size/rotation volume must use a
separate typed `OrientedBox3D` contract and must not reinterpret saved
`GridRectangle` recipes.

## Interaction boundary

The active candidate is intentionally separate from the authored selection.
The Viewer raises candidate changes to Step Parameters, and valid numeric edits
update the same transient Viewer candidate. The recipe changes only through
the existing Apply command.

GridRectangle editing does not synchronously render once per `MouseMove`.
Pointer changes use the existing scheduled interaction render path, and the
captured MouseUp commits the final candidate pick. Double-click Fit is disabled
while teaching capture is active so it cannot steal an ROI edit gesture.

## Verification

Current Release evidence:

- solution build: pass, `0` warnings / `0` errors;
- teaching-capture ViewModel: pass, `20/20`;
- generic height measurement Workbench: pass, `32/32`;
- Tool Recipe teaching: pass, `27/27`;
- docking workspace: pass, `28/28`;
- Recipe Center/WPG: pass, `28/28`;
- artifact/navigator: pass, `24/24`;
- actual Windows-pointer replacement drag: pass, ready `2/2`, camera and
  authored/execution state unchanged;
- actual Windows-pointer drag plus explicit Apply: pass, same selection ID,
  changed rectangle, no Preview/Publish/Run;
- current Release UI captures at `1920 x 1040` and `1280 x 760`: screenshot
  quality accepted on attempt 1.

Evidence:

- `artifacts/current/20260724-surface-roi-editing/before-surface-roi.png`
- `artifacts/current/20260724-surface-roi-editing/after-surface-roi.png`
- `artifacts/current/20260724-surface-roi-editing/after-surface-roi-1280x760.png`
- `artifacts/current/20260724-surface-roi-editing/pointer-edit-state.txt`
- `artifacts/current/20260724-surface-roi-editing/explicit-apply-state.txt`
- `artifacts/current/20260724-surface-roi-editing/teaching-capture-viewmodel-final.txt`
- `artifacts/current/20260724-surface-roi-editing/height-measurement-workbench-final.txt`
- `artifacts/current/20260724-surface-roi-editing/recipe-teaching-final.txt`
- `artifacts/current/20260724-surface-roi-editing/docking-final.txt`
- `artifacts/current/20260724-surface-roi-editing/recipe-manager-wpg-final.txt`
- `artifacts/current/20260724-surface-roi-editing/artifact-navigator-final.txt`

## Boundary

This evidence covers the current C3D `GridRectangle` teaching path. It does not
prove an arbitrary point-cloud/mesh volume editor, physical calibration,
two-surface physical thickness, metrology, or production-line integration.
The unaided owner first-recipe replay remains the next product evidence gate.

## Completion record

Status: Complete

Scope: selected Surface ROI presentation, Viewer-to-Inspection-Flow selection,
seeded replacement, move, corner resize, numeric source-grid editing,
synchronized transient state, and explicit same-identity Apply.

Acceptance criteria:

- selected ROI and owning step are synchronized: pass;
- existing ROI opens as a ready editable candidate: pass;
- Viewer move/resize and numeric editing update only transient geometry: pass;
- invalid source-grid values block Apply: pass;
- explicit Apply preserves selection identity and does not execute inspection:
  pass;
- current wide and compact UI captures are usable: pass.

Verification: Release build `0/0`; focused checks `20/20`, `32/32`, `27/27`,
`28/28`, `28/28`, and `24/24`; actual Windows-pointer candidate and explicit
Apply smokes; current screenshot quality at both target sizes.

Evidence: this document and
`artifacts/current/20260724-surface-roi-editing/`.

Boundary / next dependency: restart the unaided owner first-recipe replay on
the current Release EXE. Trusted real four-landmark data remains an external
prerequisite for physical alignment evidence.
