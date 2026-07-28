# OpenVisionLab 3D Visible Invalid-Cell Overlay

Date: 2026-07-28

Status: Complete for C-11 software scope

## Outcome

Height Image now presents the existing coordinate-true invalid-cell map as an
explicit, selectable visual layer.

- `결측 셀 표시` is on by default and can be switched off without changing
  the source, ROI, recipe, inspection state, or current output.
- Every missing native cell is shown in magenta.
- The visible legend shows the exact missing-cell count and percentage.
- Valid height pixels retain the selected palette and display range.
- The overlay uses the same immutable `C3DInvalidCellMap` identity consumed by
  Source Quality and Height Image. It does not calculate a second mask.

This closes master-backlog item `C-11`.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `C3DInvalidCellMap` | Immutable native row-major missing-cell bits, count, and SHA-256 |
| `C3DHeightImageFrame` | WPF-neutral visible/hidden overlay rendering on immutable display frames |
| `HeightImageViewerViewModel` | View-only visibility state, exact count, and legend presentation |
| `HeightImageViewerView` | Toggle, magenta swatch, localized help, and displayed bitmap |
| `C3DHeightImageVerification` | Pixel/color/count/SHA parity and source immutability |

The default raw Height Image frame remains unchanged. Only a derived display
frame receives the magenta pixels.

## Exact owner-source evidence

Source:

`3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D`

| Fact | Value |
| --- | --- |
| Native grid | `1280 x 840` |
| Total cells | `1,075,200` |
| Valid cells | `908,436` |
| Missing / visible overlay cells | `166,764` (`15.5%`) |
| Packed mask bytes | `134,400` |
| Mask SHA-256 | `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358` |
| Native Height Image pixel SHA-256 | `6A6C12F7A729ABF49830F07CBB868FCCCB94C987584856128662109BA377B087` |
| Visible-overlay display SHA-256 | `B0F467BF10BB5EF8CEE9F6CEB932B052416CF898AD5D907E5B5F6D3E0A1B4192` |

The overlay count and mask SHA match `SourceQualityReport`. Coordinate mapping
remains:

`pixelX=column;pixelY=row;no-flip;one-source-cell-per-pixel`

## Operator workflow

1. Open a C3D source and show Height Image in an auxiliary Viewer slot.
2. Leave `결측 셀 표시` enabled to review source coverage.
3. Read the magenta legend for the exact missing count and percentage.
4. Disable the toggle when uninterrupted height colors are more useful.
5. Continue ROI teaching. Overlay visibility never applies or changes an ROI.

## UI evidence

The pre-change baseline was copied before implementation from the immediately
preceding current-source Workspace v3 capture. It is not presented as a newly
rebuilt historical binary:

- `artifacts/current/20260728-invalid-cell-overlay/before-wide-dark-missing.png`;
- `artifacts/current/20260728-invalid-cell-overlay/before-compact-dark-missing.png`.

Current Release after evidence:

- `artifacts/current/20260728-invalid-cell-overlay/after-wide-visible-overlay.png`;
- `artifacts/current/20260728-invalid-cell-overlay/after-compact-visible-overlay.png`.

Both after captures pass the screenshot-quality gate on attempt 1. Wide and
Compact layouts retain the 3D reference, Height Image, toggle, swatch, count,
and ROI controls.

## Verification

| Gate | Result |
| --- | --- |
| Release build | `0 warnings / 0 errors` |
| Height Image mapping/range/overlay | `25/25` |
| Exact-source overlay probe | Pass |
| Inspection Workspace/non-execution | `53/53` |
| Invalid-cell map | `15/15` |
| SourceQualityReport | `13/13` |
| Workbench docking/composition | `33/33` |
| Recipe teaching | `28/28` |
| Artifact Navigator | `31/31` |
| Height measurement | `45/45` |
| Shell smoke options | `21/21` |
| Code structure | `17/17` |
| Wide/Compact screenshot quality | Pass on attempt 1 |

Reusable evidence is under:

`artifacts/current/20260728-invalid-cell-overlay/`

## Boundaries

This is a view-only missing-cell mask. It is not:

- interpolation, hole filling, filtering, or outlier removal;
- a confidence/SNR/intensity channel;
- a saved recipe parameter;
- a volumetric ROI;
- physical calibration, uncertainty, or metrology evidence.

`C-13` remains open for a shared 2D/3D display-range contract. The next
dependency-correct product slice is `E-07/E-08`, the typed `OrientedBox3D`
schema and numeric editor.

## Completion record

```text
Status: Complete
Scope: selectable native-coordinate missing-cell overlay, exact count/percentage legend, and visible Wide/Compact Height Image evidence
Acceptance criteria: every displayed overlay pixel maps to one existing missing native cell -> pass; visible overlay count equals SourceQualityReport -> pass; overlay mask SHA equals the shared invalid-cell map -> pass; toggle is view-only and deterministic -> pass; Wide/Compact controls remain usable -> pass
Verification: Release build 0/0; Height Image 25/25; exact-source probe pass; Workspace 53/53; invalid map 15/15; SourceQualityReport 13/13; docking 33/33; teaching 28/28; Artifact Navigator 31/31; height measurement 45/45; shell options 21/21; structure 17/17; screenshot quality pass
Evidence: docs/OPENVISIONLAB_3D_VISIBLE_INVALID_CELL_OVERLAY_20260728.md and artifacts/current/20260728-invalid-cell-overlay/
Boundary / next dependency: no interpolation, source correction, saved recipe mutation, confidence channel, calibration, or metrology claim; E-07/E-08 OrientedBox3D is next
```
