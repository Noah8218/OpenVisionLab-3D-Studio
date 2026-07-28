# ROI Overlay Y-position clarity - 2026-07-26

## Context

Owner review correctly identified that changing the control formerly labelled
`ROI 표시 높이` did not make the ROI visibly taller. The implementation moved
one flat X/Z overlay plane along Y, while the wording and vertical handle
suggested a size or volume change.

`GridRectangle` still stores only `Row`, `Column`, `RowCount`, and
`ColumnCount`. In the C3D contract, X is column, Y is height, and Z is row.
There is no persisted Y extent.

## Correction

- The Viewer now calls the control `ROI 오버레이 Y 위치 · 보기 전용`.
- The hint states that only the plane moves and that ROI size and measurement
  remain unchanged.
- The numeric summary reads `surface -> overlay | ΔY`.
- Button, reset, tooltip, accessibility, Viewer-status, and structured-log
  wording use overlay-position semantics.
- When the offset is non-zero, the selected yellow overlay remains the
  movable plane, a cyan outline shows the local surface position, and broken
  guide segments connect corresponding corners.
- The guides do not draw filled vertical walls. They communicate translation,
  not a persisted prism or measurement volume.

The internal display-offset property names remain unchanged because they are
implementation details and do not alter the recipe contract.

## Preserved behavior

- The offset is session-only and per selection.
- It is cleared when the source changes.
- It does not change `GridRectangle`, recipe JSON, Preview, Publish, Run,
  Validation Set, or Thickness results.
- `MinimumThickness` and `MaximumThickness` remain acceptance limits, not ROI
  vertical bounds.
- A real Y-range ROI still requires a separate typed contract such as
  `GridPrism` or `OrientedBox3D`, persisted bounds, rendering, and explicit
  filtering/measurement semantics.

## Verification

- Release build:
  `dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"`
  -> `0` warnings, `0` errors.
- Teaching capture ViewModel -> `24/24`.
- Height measurement Workbench -> `44/44`.
- Tool Recipe teaching -> `27/27`.
- Docking/navigation/shortcuts -> `29/29`.
- Logging integration, isolated rerun -> `4/4`.
- Code-structure guard -> `15/15`.
- Current Release actual Windows capture:
  ten `+` invocations produced `ΔY +588.531`; an empty-space orbit drag exposed
  the yellow overlay, cyan surface reference, and broken translation guides.
- Visual comparison -> accepted at `1920 x 1040`; compact current-source
  screenshot quality accepted at `1280 x 760`.

Evidence:

- `artifacts/current/20260726-roi-overlay-y-position/before.png`
- `artifacts/current/20260726-roi-overlay-y-position/after-final.png`
- `artifacts/current/20260726-roi-overlay-y-position/after-compact.png`
- `artifacts/current/20260726-roi-overlay-y-position/`

## Completion record

Status: Complete

Scope: Correct the meaning and visual communication of the existing
view-only Surface ROI Y-position offset.

Acceptance criteria:

- Control no longer claims to change ROI height -> pass, current localized
  Viewer capture.
- Original and moved plane positions are distinguishable -> pass, current
  actual-window oblique capture.
- Visuals do not imply a persisted solid volume -> pass, outline and broken
  translation guides only.
- Recipe and inspection boundaries remain unchanged -> pass, focused
  verification and unchanged typed recipe model.

Verification: Release build `0/0`; focused checks `24/24`, `44/44`, `27/27`,
`29/29`, `4/4`, and structure `15/15`; actual Windows before/after comparison.

Evidence:
`artifacts/current/20260726-roi-overlay-y-position/`.

Boundary / next dependency: this is overlay-position clarity, not an actual
Y-range ROI, physical calibration, uncertainty, or metrology evidence. The
next product gate remains the owner's unaided first-recipe replay.
