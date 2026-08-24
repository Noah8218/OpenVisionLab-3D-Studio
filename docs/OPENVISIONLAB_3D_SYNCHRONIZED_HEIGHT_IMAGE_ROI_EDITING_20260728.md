# Synchronized Height Image / 3D ROI Editing

Date: 2026-07-28

Backlog: `C-09`, `C-10`

Status: Complete for the documented software scope

Follow-up: `PL-0041` later added a `67/67` headless cross-view selection
atomicity suite to the same existing Inspection Workspace verifier. See
`OPENVISIONLAB_3D_CROSS_VIEW_SELECTION_ATOMICITY_CLOSURE_20260823.md`. The
original C-09/C-10 runtime evidence below remains historical and unchanged.

## Outcome

The full-size Height Image and main 3D Viewer now display and edit the same
recipe-owned `GridRectangle` ROI.

- `pixel X = column`;
- `pixel Y = row`;
- Reference ROI is cyan;
- Measurement ROI is orange;
- both views use the same selection ID and native-grid rectangle;
- the active Height Image candidate exposes four resize handles and one move
  handle;
- drawing, moving, and resizing enter the existing `Review` state;
- `Apply ROI` / `Enter` is the only action that changes authored geometry;
- `Cancel` / `Esc` restores the prior `Missing` or `Applied` state;
- `Delete ROI` / `Delete` removes only the selected applied ROI;
- no Height Image ROI action invokes Preview, Publish, Run, Validation Set,
  or measurement execution.

This closes the linked teaching gap visible in the prior build: applied ROIs
were present only in the 3D Viewer while the Height Image contained no ROI
evidence.

## Ownership

| Responsibility | Owner |
| --- | --- |
| Recipe selection identity, role routing, lifecycle, Apply, Cancel, Delete | `ToolWorkbenchViewModel` |
| Height Image ROI projection and WPF-neutral pointer gesture state | `HeightImageRoiWorkspaceViewModel` |
| 2D raster, fixed-screen handles, pointer and keyboard adaptation | `HeightImageViewerView` |
| Existing 3D transient/applied ROI | `OpenVisionThreeDViewerControl` |
| Workbench-to-3D candidate synchronization | `WorkbenchViewerTeachingCoordinator` |

The Height Image does not own a second recipe model. Its candidate event is
validated against the active source binding and routed through the existing
Workbench draft event, so the 3D Viewer receives the same
`ToolRecipeGridRectangle`.

## Operator workflow

1. Select a Thickness step.
2. Click an applied cyan/orange ROI in either linked view to select its role.
3. Use `ROI 그리기` or `ROI 편집` in Selected Tool.
4. Drag in the Height Image:
   - empty candidate: draw;
   - inside the candidate: move;
   - corner handle: resize.
5. Review the same candidate in Height Image and 3D.
6. Use `Apply ROI` / `Enter`, or `Cancel` / `Esc`.
7. With no active capture, use `Delete ROI` / `Delete` to remove the selected
   applied ROI.

Fit, zoom, pan, palette, range, and linked hover remain presentation-only.
Only explicit ROI Apply/Delete changes the recipe.

## Exact-source evidence

Source:

`3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D`

Recipe:

`3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json`

Actual Windows pointer editing of Tab 1 Measurement ROI produced:

```text
before: row 430, column 575, rowCount 450, columnCount 135
review: row 430, column 575, rowCount 542, columnCount 195
selection ID: selection.tab-01.measurement-roi
```

The Height Image and 3D candidate rectangles were equal. Before Apply:

- dirty state remained `False`;
- steps remained `8`;
- selections remained `16`;
- route remained unchanged;
- applied geometry remained unchanged;
- current inspection output remained the same reference;
- the 3D camera remained unchanged.

Apply preserved the selection ID, saved the changed geometry, and reopened it
with both linked ROI overlays intact.

## Verification

| Check | Result |
| --- | --- |
| Release build | `0 warnings / 0 errors` |
| Inspection Workspace and ROI lifecycle | `50/50` |
| Shell smoke options | `21/21` |
| Actual Windows pointer Review | Pass |
| Actual Windows pointer Apply + save/reopen | Pass |
| Wide Review screenshot quality | accepted on attempt 1 |
| Compact Review screenshot quality | accepted on attempt 1 |
| Viewer display | `103/103` |
| Height Image | `21/21` |
| Invalid-cell map | `15/15` |
| SourceQualityReport | `13/13` |
| Source Quality workspace | `18/18` |
| Artifact Navigator | `31/31` |
| Docking/composition | `33/33` |
| Height measurement workbench | `45/45` |
| Recipe teaching | `28/28` |
| Code structure | `17/17` |

Evidence:

- `artifacts/current/20260728-height-image-roi-editing/before-wide-3d-only-roi.png`;
- `artifacts/current/20260728-height-image-roi-editing/after-wide-review-pointer.png`;
- `artifacts/current/20260728-height-image-roi-editing/after-compact-review-pointer.png`;
- `artifacts/current/20260728-height-image-roi-editing/actual-pointer-review.txt`;
- `artifacts/current/20260728-height-image-roi-editing/actual-pointer-apply-save-reopen.txt`;
- `artifacts/current/20260728-height-image-roi-editing/inspection-workspace-verification.txt`;
- `artifacts/current/20260728-height-image-roi-editing/`.

## Boundaries

- `GridRectangle` remains an X/Z native-grid footprint, not an XYZ volume.
- The existing display-only ROI overlay Y position remains view-only.
- This does not implement `OrientedBox3D`.
- This does not implement the visible invalid/missing-cell overlay (`C-11`).
- Physical calibration, traceability, uncertainty, GR&R, and certified
  thickness metrology remain unverified.
- R0 remains the owner's unaided current Release replay.

## Completion record

```text
Status: Complete
Scope: C-09 shared linked ROI identity/geometry/colors and C-10 Height Image draw/move/resize/delete/review/apply/cancel
Acceptance criteria: same ID/geometry in 2D and 3D -> pass; actual pointer Review -> pass; Apply/Cancel/Delete lifecycle -> pass; non-execution before Apply -> pass; save/reopen -> pass; wide/compact UI -> pass
Verification: Release build 0/0; Workspace 50/50; smoke options 21/21; display 103/103; Height Image 21/21; invalid map 15/15; SourceQualityReport 13/13; Source Quality 18/18; Artifact Navigator 31/31; docking 33/33; height measurement 45/45; recipe teaching 28/28; structure 17/17
Evidence: docs/OPENVISIONLAB_3D_SYNCHRONIZED_HEIGHT_IMAGE_ROI_EDITING_20260728.md and artifacts/current/20260728-height-image-roi-editing/
Boundary / next dependency: C-11 visible invalid-cell overlay is next; OrientedBox3D remains a later separate typed ROI contract; R0 and physical metrology remain external/unverified
```
