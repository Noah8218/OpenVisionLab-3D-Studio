# OpenVisionLab 3D OrientedBox3D Viewer Handles

Date: 2026-07-28

Status: Complete for E-09 software scope

## Outcome

The persisted `OrientedBox3D` volume is now visible and pointer-editable in
the normal 3D Viewer.

- the typed center, right-handed axes, and half-extents render as a
  translucent oriented cuboid;
- a center handle moves the candidate;
- red, green, and blue handles resize local X, Y, and Z half-extents;
- the green Y handle changes persisted volume height, not the view-only
  `GridRectangle` overlay position;
- a magenta ring and handle rotate the box around local Y;
- handle size remains stable in screen pixels;
- axes that project onto the center in Top or side views receive distinct
  screen-space fallback positions;
- numeric edits and Viewer gestures update the same transient Review draft;
- the global Review bar is the sole visible Apply/Cancel action surface.

This closes master-backlog item `E-09`.

## Operator workflow

1. Load an identified C3D recipe containing an `OrientedBox3D`, or choose
   `New box`.
2. Select the box in Selected Tool -> Regions or click its Viewer outline.
3. Drag the white center to move the box.
4. Drag red, green, or blue handles to change local X, Y, or Z size.
5. Drag the magenta `Rotate Y` handle to rotate the local X/Z axes.
6. Confirm the synchronized numeric center, axes, and half-extents.
7. Press Enter or use the global Review bar to Apply; press Esc to Cancel.
8. Save only after the authored geometry is accepted.

Pointer movement does not mutate the recipe, Preview, Publish, Run, current
output, or camera. Only explicit Apply changes the recipe.

## Interaction contract

| Visible control | Draft change | Persistent change |
| --- | --- | --- |
| White center | Center XYZ | On explicit Apply |
| Red handles | Local X half-extent | On explicit Apply |
| Green handles | Local Y half-extent / volume height | On explicit Apply |
| Blue handles | Local Z half-extent | On explicit Apply |
| Magenta ring/handle | Local-Y rotation of X/Z axes | On explicit Apply |
| Numeric fields | Same center/axes/half-extents draft | On explicit Apply |
| Esc / Cancel | Discard current draft | None |
| Enter / Apply | Validate and preserve selection ID | Recipe only |

All box coordinates remain in the declared source frame. For the current C3D
sample this is `frame.c3d-grid-index` with `raw-height`; it is not calibrated
physical metrology.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `OrientedBox3DEditorViewModel` | Valid numeric draft and Viewer-to-numeric synchronization |
| `ToolWorkbenchViewModel.OrientedBox3D` | Explicit Apply/Cancel, same-ID recipe mutation, and non-execution boundary |
| `WorkbenchViewerTeachingCoordinator` | Draft and selection synchronization between Workbench and Viewer |
| `OpenVisionThreeDViewerControl.OrientedBox3D` | Projection, rendering, screen-space handles, hit testing, and pointer gestures |
| `OpenVisionThreeDViewerControl.OrientedBox3DSmoke` | Real Windows pointer evidence in Perspective, Top, and side views |

The Viewer remains a View adapter. It does not own persisted recipe geometry.

## Projection behavior

The cuboid outline and rotation ring are drawn in 3D source geometry. Pointer
handles are projected into the Viewer and rendered as fixed-screen-size WPF
overlays.

When an axis endpoint projects at least 28 pixels from the center, the handle
uses its true projected position. When it collapses toward the center, such
as Y height in Top view or a view-aligned axis in side view, a deterministic
screen-space fallback separates the handle while preserving the same local
axis edit. This avoids invisible or overlapping controls without changing the
stored geometry.

## Evidence

Source and recipe:

- `3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D`;
- `3D/Samples/ThicknessCouponV1/oriented-box-demo.ov3d-recipe.json`;
- selection `selection.oriented-box.01`.

Fresh baseline:

- `artifacts/current/20260728-oriented-box-viewer-handles/before-wide-numeric-only.png`.

Current Release UI:

- `artifacts/current/20260728-oriented-box-viewer-handles/after-wide-current-source.png`;
- `artifacts/current/20260728-oriented-box-viewer-handles/after-compact-current-source.png`;
- `artifacts/current/20260728-oriented-box-viewer-handles/after-side-pointer.png`.

Actual-pointer report:

- `artifacts/current/20260728-oriented-box-viewer-handles/actual-pointer-projections.txt`.

The actual-pointer report proves:

- Perspective move, X/Y/Z resize, and local-Y rotation;
- Top projection handle accessibility and Y height resize;
- side projection handle accessibility and collapsed-axis X resize;
- seven routed Windows pointer down/move/up gestures;
- one unchanged authored recipe until Apply;
- unchanged Preview/result evidence;
- unchanged camera during every individual edit gesture;
- preserved selection identity.

## Verification

| Gate | Result |
| --- | --- |
| Release build | `0 warnings / 0 errors` |
| Actual Windows pointer, Perspective/Top/side | Pass, `7` gestures |
| Inspection Workspace selection/Review boundary | `63/63` |
| Shell smoke options | `22/22` |
| Recipe teaching regression | `28/28` |
| Height measurement regression | `46/46` |
| Docking/composition regression | `33/33` |
| Display ViewModel regression | `103/103` |
| Code structure | `17/17` |
| Wide pointer-edited screenshot quality | Pass on attempt 1 |
| Compact screenshot quality | Pass on attempt 1 |
| Side pointer-edited screenshot quality | Pass on attempt 1 |

Focused command:

```powershell
OpenVisionLab.ThreeD.Shell.exe `
  --tool-teaching-recipe <oriented-box-recipe> `
  --tool-teaching-step step.oriented-box-authoring.01 `
  --smoke-oriented-box-pointer-report <report-path>
```

## Boundaries

This slice does not add:

- a downstream inspection or crop tool that consumes `OrientedBox3D`;
- free rotation around local X or Z; the first persisted rotation gesture is
  local Y;
- linked Height Image volume manipulation;
- calibrated length units, uncertainty, GR&R, or metrology evidence.

`D-04 Remove Outlier Pixels` is the next dependency-correct implementation
item.

## Completion record

```text
Status: Complete
Scope: rendered persisted OrientedBox3D, synchronized numeric/Viewer Review draft, move/X-Y-Z resize/local-Y rotate handles, projection fallbacks, global Apply/Cancel, and actual Perspective/Top/side pointer evidence
Acceptance criteria: visible 3D volume and fixed-size handles -> pass; Top/side/perspective handles accessible -> pass; actual move/resize/height/rotate pointer gestures -> pass; same ID and no recipe/execution mutation before Apply -> pass; Wide/Compact UI remains usable -> pass
Verification: Release build 0/0; actual Windows pointer 7 gestures; Workspace 63/63; shell options 22/22; teaching 28/28; height measurement 46/46; docking 33/33; display 103/103; structure 17/17; screenshot quality pass
Evidence: docs/OPENVISIONLAB_3D_ORIENTED_BOX_VIEWER_HANDLES_20260728.md and artifacts/current/20260728-oriented-box-viewer-handles/
Boundary / next dependency: no downstream OrientedBox3D consumer, free X/Z rotation, Height Image volume editing, calibration, or metrology claim; D-04 Remove Outlier Pixels is next
```
