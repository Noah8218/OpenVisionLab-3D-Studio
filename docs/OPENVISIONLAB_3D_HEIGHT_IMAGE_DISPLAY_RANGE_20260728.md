# OpenVisionLab 3D Height Image Display Range

Date: 2026-07-28

Backlog item: `C-07`

Status: Complete

## Outcome

The full-size Height Image now has first-class, view-only display controls:

- `Height`, `Grayscale`, and `Thermal` palettes;
- explicit `Auto range`;
- numeric minimum and maximum fields;
- explicit `Apply range`;
- a visible color legend with the active minimum and maximum;
- a compact wrapping layout that keeps each label attached to its input.

Auto range uses the current source's finite raw-height minimum and maximum.
Manual range changes only display normalization. It does not change source
values, native pixel coordinates, the invalid-cell map, recipe state,
Preview, Publish, Run, Validation Set, or Save.

## Operator workflow

1. Open the Height Image in a split, stacked, or pop-out auxiliary view.
2. Select `Height`, `Grayscale`, or `Thermal`.
3. Keep `Auto range` when the full valid source range is useful.
4. To inspect a narrower band, enter finite `Min` and `Max` values with
   `Min < Max`.
5. Select `Apply range`.
6. Read the active range in the toolbar and the color legend.
7. Select `Auto range` to restore the source minimum and maximum.

Example using the owner Thickness Coupon v1 source:

```text
Palette: Thermal
Min: 0
Max: 1200
Action: Apply range
Result: Manual range | 0 … 1200 raw-height
```

Invalid input is fail-closed:

- non-numeric or non-finite values are rejected;
- `Min >= Max` is rejected;
- the last valid rendered image remains visible;
- no recipe or inspection state changes.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `C3DHeightImageFrame` | Immutable native raw-height values, native coordinate mapping, invalid-cell map, default pixels |
| `C3DHeightImageDisplayFrame` | Immutable palette/range-specific BGRA pixels and pixel SHA-256 |
| `C3DPointMapPalette` | WPF-neutral Height, Grayscale, and Thermal byte colors |
| `HeightImageViewerViewModel` | Palette, auto/manual range draft, validation, explicit Apply/Auto commands, display identity |
| `HeightImageViewerView` | Bitmap presentation, compact controls, legend, fit/zoom/pan and pointer adapter |

The recipe Workbench remains the owner of authored steps, selections, and
execution. The Height Image display controls do not call those owners.

## Coordinate and validity invariants

Display range and palette changes preserve:

```text
pixel X = source column
pixel Y = source row
no flip
one source cell per pixel
invalid cells use the same C3DInvalidCellMap
raw-height hover reads the immutable source value
```

Clipping means color normalization only:

```text
normalized = clamp((rawHeight - displayMin) / (displayMax - displayMin), 0, 1)
```

It does not clamp or replace the stored raw height.

## Exact owner-source evidence

Source:

```text
3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D
```

Native source/display foundation:

| Field | Value |
| --- | ---: |
| Grid | `1280 x 840` |
| Native range | `-1179.4000244140625 … 2348.60009765625 raw-height` |
| Auto Height pixel SHA-256 | `6A6C12F7A729ABF49830F07CBB868FCCCB94C987584856128662109BA377B087` |
| Invalid-map SHA-256 | `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358` |

Applied display:

| Field | Value |
| --- | ---: |
| Palette | `Thermal` |
| Manual range | `0 … 1200 raw-height` |
| Display pixel SHA-256 | `49FE0B0009CDE14BEE44C40C99F7EC0A6571BBC3DCDF8EDA168943E418F531BF` |
| Recipe dirty | `False -> False` |
| Steps | `1 -> 1` |
| Selections | `1 -> 1` |
| Run log | `3 -> 3` |
| Preview running | `False -> False` |

Wide and compact actual-window smoke produced the same manual display SHA and
the same non-mutation boundary.

## Verification

Commands:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -- `
  --verify-c3d-height-image `
  --report artifacts/current/20260728-height-image-display-range/height-image-verification.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-inspection-workspace-selection `
  artifacts/current/20260728-height-image-display-range/inspection-workspace-verification.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-shell-smoke-command-line `
  artifacts/current/20260728-height-image-display-range/shell-smoke-options.txt
```

Actual-window range smoke adds:

```text
--smoke-viewer-layout vertical
--smoke-height-image-display-range
--smoke-height-image-palette Thermal
--smoke-height-image-range-min 0
--smoke-height-image-range-max 1200
--smoke-height-image-display-range-report <report>
```

Current evidence:

| Gate | Result |
| --- | --- |
| Release build | `0` warnings / `0` errors |
| Height Image mapping/palette/range | `21/21` |
| Inspection Workspace and non-execution | `36/36` |
| Shell smoke options | `18/18` |
| Exact-source wide manual-range smoke | Pass |
| Exact-source compact manual-range smoke | Pass |
| Wide screenshot quality | accepted on attempt 1 |
| Compact screenshot quality | accepted on attempt 1 |
| Invalid-cell map regression | `15/15` |
| SourceQualityReport regression | `13/13` |
| Source Quality workspace regression | `18/18` |
| Artifact Navigator regression | `31/31` |
| Docking/composition regression | `33/33` |
| Recipe teaching regression | `28/28` |
| Height distribution regression | `22/22` |
| Executable structure guard | `17/17` |

## UI evidence

Before: the current Release Height Image had Fit, 1:1, zoom, pan, and hover,
but no visible palette or numeric range controls.

- `artifacts/current/20260728-height-image-display-range/before-wide-auto-range.png`
- `artifacts/current/20260728-height-image-display-range/before-compact-auto-range.png`

After: the same source shows a Thermal manual range, explicit Auto/Apply
actions, active range text, and a matching legend.

- `artifacts/current/20260728-height-image-display-range/after-wide-thermal-manual-range.png`
- `artifacts/current/20260728-height-image-display-range/after-compact-thermal-manual-range.png`

Visual comparison:

- the operator can see which palette and numeric range are active;
- the image changes materially under the narrower range;
- Min/Max labels remain attached to their inputs at `1280 x 760`;
- the dominant 3D Viewer and existing Workbench composition remain intact.

## Boundaries and next dependencies

This completion does not claim:

- the same manual range is shared with the 3D Viewer (`C-13`);
- shared Height Image / 3D hover as part of `C-07`; that separate `C-08`
  slice is now complete;
- synchronized ROI display or editing as part of `C-07`; `C-09/C-10` were
  completed later on 2026-07-28;
- a visible invalid-cell overlay (`C-11`);
- persistence of view-only range settings in a recipe;
- physical calibration, traceability, uncertainty, GR&R, or metrology.

Next dependency-correct order:

1. `C-11 visible invalid-cell overlay` | Recommended model: `gpt-5.6-terra` | Reasoning effort: medium

2. `E-07/E-08 OrientedBox3D contract and numeric editing` | Recommended model: `gpt-5.6-sol` | Reasoning effort: high

## Completion record

Status: Complete

Scope: `C-07` Height Image palette and explicit auto/manual numeric display
range controls with compact UI, visible legend, deterministic pixels, and
view-only boundaries.

Acceptance criteria:

- Auto range restores current source minimum/maximum -> pass, `36/36`;
- finite manual Min/Max applies only after explicit Apply -> pass, `21/21`
  and exact-source smoke;
- invalid range fails closed and preserves the last rendered image -> pass,
  `36/36`;
- palette/range changes preserve raw values, coordinates, and invalid cells ->
  pass, `21/21`;
- recipe and inspection state remain unchanged -> pass, wide/compact boundary
  reports;
- Wide and Compact controls remain understandable -> pass, current Release
  screenshots and quality reports.

Verification: commands and results listed above.

Evidence:

- this document;
- `artifacts/current/20260728-height-image-display-range/`.

Boundary / next dependency: `C-08` is complete in
`OPENVISIONLAB_3D_SHARED_HEIGHT_CURSOR_20260728.md`; `C-09/C-10` synchronized
ROI display/editing was completed later on 2026-07-28. `C-11` is next. R0
owner replay, physical calibration, and metrology remain external or
unverified.
