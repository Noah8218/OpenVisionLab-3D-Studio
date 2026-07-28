# Commercial ROI workflow and review-mode correction - 2026-07-26

## Status

Status: Complete

## Scope

Review official commercial-tool documentation for ROI authoring, compare the
observed interaction contracts with OpenVisionLab 3D Studio, and correct the
bounded gaps that prevented the owner from understanding when drawing had
finished.

This checkpoint also records the bounded inspection shortcuts added in the
same owner-feedback cycle.

## Commercial workflow evidence

Official sources checked on 2026-07-26:

- [Cognex In-Sight Interactive Graphics Mode](https://docs.cognex.com/is2d_2220/web/EN/InSight_EZ/Content/Topics/Spreadsheet/InteractiveGraphicsMode.htm)
  enters a distinct graphics-edit mode, exposes ROI position/size/rotation
  handles, accepts with Enter, and cancels with Esc.
- [Cognex VisionView manual](https://www.cognex.com/support/downloads/ns/38/125/211/VisionView1.4EN1.pdf)
  separates ROI-handle editing from Pan/Zoom mode and provides numeric entry
  for the active handle.
- [Autodesk Inventor point-cloud Box Crop](https://help.autodesk.com/cloudhelp/2014/ENU/Inventor/files/GUID-5DBD057F-0046-4051-9586-01884BC815E0.htm)
  creates a box after two diagonal corners, then exposes manipulators and
  direct numeric Z entry before an explicit OK.
- [MVTec HALCON drawing operators](https://www.mvtec.com/doc/halcon/1805/en/toc_graphics_drawing.html)
  distinguish blocking drawing operators from non-blocking interactive
  drawing objects and provide separate move/edit operations.
- [Artec Studio 17 User Guide](https://docs.artec3d.com/as/17/en/_downloads/8340e493dcebf118df8743b2f781f936/Manual-17-EN.pdf)
  exposes explicit 2D/3D/rectangular/cutoff-plane selection modes, modifier
  guidance, selection clearing, and Apply.

The common contract is:

1. Enter a visible ROI-authoring mode.
2. Draw the initial footprint.
3. End drawing and enter a review/edit state.
4. Adjust with visible handles and, where useful, numeric input.
5. Apply/OK explicitly or cancel explicitly.
6. Keep view navigation distinguishable from ROI geometry editing.

## Current-structure comparison

| Commercial contract | Prior OpenVisionLab state | Correction |
| --- | --- | --- |
| Visible authoring entry | Reference/Measurement actions looked like disabled generic buttons | Both cards use primary `ROI 그리기` / `ROI 다시 그리기` actions |
| Drawing ends after the required geometry | `2/2` remained visually similar to capture mode and the crosshair persisted | `2/2` now changes to arrow/review behavior and rejects additional capture |
| Explicit review phase | Ready text only said the candidate could be applied | Ribbon now states `그리기 완료 · 검토 모드` and explains that no additional ROI will be drawn |
| Handle and numeric editing | Existing corner/center/Y handle and X/Z numeric editor | Preserved; no duplicate editor was introduced |
| Apply/cancel shortcuts | Esc canceled, but no standard keyboard accept | Enter applies only while the capture command can execute; Esc still cancels |
| Apply is the primary action | Apply looked like the two secondary actions | Apply uses the primary button treatment |
| Navigation during review | Active capture continued to own empty-space left gestures | Ready review mode lets empty-space left drag return to normal Viewer orbit while ROI handles retain edit ownership |
| Height-axis meaning | `ROI 표시 높이` could be mistaken for Z or measurement geometry | Viewer labels it `ROI 표시 높이 (Y축 · Z=행)` and preserves its view-only boundary |

## Coordinate and measurement boundary

The current C3D contract remains:

- X = column
- Y = height
- Z = row

`GridRectangle` therefore remains an X/Z height-field footprint. Its yellow
Y handle changes only the display plane used to see and edit the selected ROI.
It does not alter recipe geometry, Reference/Measurement samples, Preview,
Publish, Run, or Validation Set.

Thickness still derives signed H-axis separation from a Reference surface fit
and finite samples in the Measurement ROI. A persisted XYZ volume would be a
different typed entity such as `OrientedBox3D`; it was not fabricated by this
usability correction.

## Bounded shortcut set

- `Ctrl+N`: new recipe
- `Ctrl+O`: open recipe
- `Ctrl+S`: save current recipe
- `Ctrl+Shift+S`: save as
- `Ctrl+Shift+O`: load 3D map
- `F5`: Preview selected step
- `Ctrl+F5`: Run recipe
- `Enter`: apply a ready ROI/selection candidate
- `Esc`: cancel active ROI/selection capture

Preview and Run remain explicit. Save does not invoke either action.

## Verification

- Release build:
  `dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"`
  -> `0` warnings, `0` errors.
- Recipe Manager/WPG: `37/37`.
- Height measurement Workbench: `44/44`, including reopening a saved
  Reference-only current-schema Thickness route and explicit ready review
  state.
- Docking/navigation/shortcut bindings: `29/29`.
- Teaching capture ViewModel: `24/24`.
- Tool Recipe teaching: `27/27`.
- Current Release actual Windows pointer:
  diagonal GridRectangle drag -> ready `2/2`; explicit Apply retained;
  authored recipe, Preview, and result references unchanged.
- Stabilized Windows pointer injection repeated the same ready-state check
  `3/3`.
- Current screenshot quality accepted on attempt 1.

Evidence:

- `artifacts/current/20260726-shortcuts-and-roi-capture/`
- `after-measurement-roi-action-final.png`
- `after-roi-candidate-review-stable-final.png`
- `roi-candidate-review-stable-final.txt`
- `roi-pointer-stability-1.txt` through `roi-pointer-stability-3.txt`

## Completion record

Status: Complete

Scope: Commercial ROI-authoring review, bounded matching corrections,
inspection shortcuts, and current Release evidence.

Acceptance criteria:

- Official commercial workflows checked -> pass, five official product/vendor
  sources recorded.
- Current workflow compared contract-by-contract -> pass, comparison table
  above.
- Drawing stops at `2/2` and visibly becomes review -> pass, automated state
  check plus actual-pointer screenshot.
- Apply/cancel and save shortcuts remain explicit -> pass, command and binding
  verification.
- Existing recipe/execution boundary preserved -> pass, actual-pointer report.

Verification: Release build `0/0`; focused checks
`37/37`, `44/44`, `29/29`, `24/24`, and `27/27`; actual pointer ready-state
stability `3/3`.

Evidence:
`artifacts/current/20260726-shortcuts-and-roi-capture/`.

Boundary / next dependency: this closes the identified interaction defects,
not the owner's full unaided first-recipe acceptance gate. It does not prove
physical calibration, uncertainty, metrology, or a persisted XYZ volume ROI.
