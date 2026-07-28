# OpenVisionLab 3D Shared Height Cursor

Date: 2026-07-28

Status: Complete for backlog item `C-08`

## Outcome

The full-size Height Image and the main 3D Viewer now share one view-only
native-grid cursor.

- Height Image pointer movement publishes the exact source
  `column / row / raw-height` cell.
- The main 3D Viewer renders a yellow/cyan marker at that same valid surface
  point.
- 3D Viewer pointer movement publishes the picked C3D point's native
  `column / row / raw-height`.
- The Height Image renders a full-width/full-height crosshair and marker at
  that same native pixel.
- Both views show the cursor origin (`2D` or `3D`) and the same coordinate
  summary.
- A Height Image missing cell remains an explicit missing value. It uses an
  orange crosshair and never fabricates a height or a 3D surface marker.

The exact mapping remains:

```text
Height Image pixel X = C3D column
Height Image pixel Y = C3D row
3D cursor position  = CreateC3DGridDisplayPosition(row, column, rawHeight)
```

There is no flip, resampling, inferred coordinate, or recipe selection
identity involved in this slice.

## Operator workflow

### Height Image to 3D

1. Open a C3D source.
2. Select side-by-side, stacked, or pop-out Height Image layout.
3. Move the pointer over the Height Image.
4. Read the `2D에서 | column ... | row ... | H ...` summary.
5. Confirm the same cell is marked on the 3D surface.

### 3D to Height Image

1. Move the pointer over a visible C3D surface point.
2. Read the `3D에서 | column ... | row ... | H ...` summary.
3. Confirm the Height Image crosshair is centered on the same native
   column/row.

Leaving one view clears the cursor only when that view still owns the current
cursor. A stale `MouseLeave` from Height Image cannot erase a newer 3D cursor,
and vice versa.

## Ownership

### Shell presentation session

`SharedHeightCursorSession` is the single WPF-neutral owner of:

- source content SHA-256;
- origin (`HeightImage` or `ThreeDViewer`);
- row;
- column;
- raw height;
- valid/missing state;
- monotonic revision.

It contains no OpenGL, WPF, recipe, execution, or persistence state.

### Height Image adapter

`HeightImageViewerViewModel`:

- publishes exact `C3DHeightImageFrame.TryGetCell` results;
- accepts only cursors whose source SHA and dimensions match the loaded frame;
- exposes crosshair coordinates and localized summary state;
- preserves `NaN` for a missing raw height.

`HeightImageViewerView` converts only native row/column into Canvas position.
Zoom, fit, pan, and palette/range do not alter the coordinate.

### 3D Viewer adapter

`OpenVisionThreeDViewerControl`:

- samples idle pointer movement at no more than one query per `24 ms` or
  meaningful `2 px` movement;
- reuses the established C3D pick path and publishes the picked point's
  existing native row/column/raw value;
- validates source SHA and dimensions before accepting a linked cursor;
- renders the linked marker in the current 3D projection;
- leaves camera, geometry, render style, and interaction LOD unchanged.

The SharpGL host already renders at 60 FPS, so cursor updates do not invoke a
synchronous render or enter the camera interaction scheduler.

## Exact-source evidence

Source:

`3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D`

Identity and test point:

| Field | Value |
| --- | --- |
| Source SHA-256 | `D879FC9E40678762214E8C3FBEA01F5C9A309701DAAEAD448067E563C5B502F8` |
| Grid | `1280 x 840` |
| Test column | `593` |
| Test row | `800` |
| Raw height | `633.4000244140625 raw-height` |
| Missing-cell test | `column 0 / row 0` |

Wide and compact actual-window smoke prove:

- Height Image -> shared session -> 3D marker;
- 3D Viewer -> shared session -> Height Image crosshair;
- explicit missing-cell propagation;
- identical source identity;
- unchanged camera;
- unchanged recipe dirty state, step count, selection count, Run log,
  Preview state, and current output.

## Verification

Current Release results:

| Gate | Result |
| --- | --- |
| Release build | `0` warnings / `0` errors |
| Inspection Workspace shared-cursor contract | `42/42` |
| Shell smoke option contract | `20/20` |
| Exact-source wide bidirectional smoke | Pass |
| Exact-source compact bidirectional smoke | Pass |
| Wide screenshot quality | accepted on attempt 1 |
| Compact screenshot quality | accepted on attempt 1 |
| Actual Windows C3D pointer/menu regression | Pass |
| Viewer display regression | `103/103` |
| Height Image regression | `21/21` |
| Invalid-cell map regression | `15/15` |
| SourceQualityReport regression | `13/13` |
| Source Quality workspace regression | `18/18` |
| Artifact Navigator regression | `31/31` |
| Docking/composition regression | `33/33` |
| Recipe teaching regression | `28/28` |
| Executable structure guard | `17/17` |

A cube-only diagnostic invocation passed routed events, latency, orbit, pan,
zoom, and menus but could not satisfy the regression's C3D-specific
double-click status precondition. The acceptance run therefore explicitly
used `--smoke-c3d thickness` and passed the complete pointer/menu suite on its
first correctly configured attempt.

## UI evidence

Closest current-source baseline before implementation:

- `artifacts/current/20260728-shared-height-hover/before-wide-independent-hover.png`
- `artifacts/current/20260728-shared-height-hover/before-wide-quality.txt`

The baseline has two independent views and no shared crosshair, 3D marker, or
common coordinate summary.

After implementation:

- `artifacts/current/20260728-shared-height-hover/after-wide-linked-hover.png`
- `artifacts/current/20260728-shared-height-hover/after-compact-linked-hover.png`
- `artifacts/current/20260728-shared-height-hover/after-wide-hover-smoke.txt`
- `artifacts/current/20260728-shared-height-hover/after-compact-hover-smoke.txt`
- `artifacts/current/20260728-shared-height-hover/viewer-pointer-regression.txt`

The wide capture visibly shows the same `column 593 / row 800 / H 633.4`
coordinate as a 3D surface marker, a Height Image crosshair, and a shared
summary. The compact capture preserves the same state without hiding the
layout actions or image controls.

## Boundaries

This completion does not claim:

- shared selected ROI identity or role colors (`C-09`);
- Height Image ROI draw/move/resize/delete/review/apply/cancel (`C-10`);
- a visible invalid-cell mask overlay (`C-11`);
- one shared numeric display range across 2D and 3D (`C-13`);
- hover over a 3D missing cell, because a missing cell has no rendered surface
  point;
- persistent cursor state in a recipe;
- physical calibration, traceability, uncertainty, GR&R, or metrology.

The 3D pointer selects an exact point from the established visible C3D pick
proxy. It does not reconstruct an unrendered native cell between displayed
points.

## Next dependency

1. `C-09/C-10 synchronized Height Image / 3D ROI display and editing` |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: high

2. `C-11 visible invalid-cell overlay` |
   Recommended model: `gpt-5.6-terra` | Reasoning effort: medium

3. `C-13 shared 2D/3D display range` |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: medium

## Completion record

Status: Complete

Scope: `C-08` bidirectional, coordinate-true, view-only shared cursor between
the full-size Height Image and the main 3D Viewer.

Acceptance criteria:

- Height Image publishes exact native cell and 3D marker -> pass, exact-source
  smoke;
- 3D Viewer publishes exact C3D point and Height Image crosshair -> pass,
  exact-source smoke;
- missing cell remains missing -> pass, wide/compact reports;
- source mismatch and stale leave fail closed -> pass, `42/42`;
- recipe, execution, and camera state remain unchanged -> pass,
  wide/compact boundary reports;
- wide and compact UI remain usable -> pass, current Release captures and
  quality reports.

Verification: commands and results listed above.

Evidence:

- this document;
- `artifacts/current/20260728-shared-height-hover/`.

Boundary / next dependency: `C-09/C-10` synchronized ROI display/editing was
completed later on 2026-07-28. `C-11` visible invalid-cell overlay is next.
R0 owner replay, physical calibration, and metrology remain external or
unverified.
