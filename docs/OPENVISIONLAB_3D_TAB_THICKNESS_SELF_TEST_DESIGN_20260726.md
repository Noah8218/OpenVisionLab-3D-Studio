# Tab Thickness Self-Test Design

Date: 2026-07-26
Source: `3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D`

## Outcome

Provide one operator-testable inspection model for the supplied C3D:

1. open one saved recipe;
2. select Tab 1 through Tab 8 independently;
3. see and replace that Tab's Reference and Measurement ROI;
4. explicitly run the current source once;
5. review one Thickness result per Tab.

The implementation reuses the canonical schema `1.3` Thickness route:

```text
C3D HeightField + Reference GridRectangle + Measurement GridRectangle
    -> DualSurfaceThicknessRule
    -> MeasurementResult
```

It does not introduce a second thickness algorithm or reinterpret
`GridRectangle` as an XYZ volume.

## Source evidence

The exact source identity is:

| Field | Value |
|---|---|
| Grid | `1466 columns x 2269 rows` |
| Bytes | `13,305,424` |
| SHA-256 | `5D3625B1A5A65EF8BEAB366FF7A007918D28FB614136414BBD30A441E85C8937` |
| Valid samples | `908,436` |
| Unit | `raw-height` |
| Frame | `frame.c3d-grid-index` |

The top-height projection shows eight repeated Tab surfaces in two rows and
four columns. The design overlay is
`artifacts/current/20260726-tab-thickness-model/c3d-tab-roi-design.png`.

## Model boundary

The inspection model is an ordinary persisted tool recipe:

- eight independently named Thickness steps;
- two independently persisted selections per step;
- one independently named `MeasurementResult` output per step;
- one explicit ordered execution produces eight step records;
- recipe opening, ROI editing, parameter editing, and saving do not run the
  inspection.

Step labels must preserve instance identity (`Tab 1 Thickness`, etc.) when a
recipe is reopened. The underlying catalog tool remains `thickness`.

## Provisional ROI layout

Coordinates are the canonical C3D grid contract: X is column and Z is row.
Reference ROI is a narrow adjacent carrier strip at the same row span, which
reduces long-axis extrapolation. Measurement ROI excludes the Tab border and
cutout.

| Tab | Reference ROI `(row, column, rowCount, columnCount)` | Measurement ROI `(row, column, rowCount, columnCount)` |
|---|---|---|
| 1 | `(430, 515, 450, 20)` | `(430, 575, 450, 135)` |
| 2 | `(430, 744, 450, 20)` | `(430, 800, 450, 138)` |
| 3 | `(430, 972, 450, 20)` | `(430, 1028, 450, 137)` |
| 4 | `(430, 1198, 450, 20)` | `(430, 1255, 450, 135)` |
| 5 | `(1120, 515, 440, 20)` | `(1120, 575, 440, 135)` |
| 6 | `(1120, 744, 440, 20)` | `(1120, 800, 440, 138)` |
| 7 | `(1120, 972, 440, 20)` | `(1120, 1028, 440, 137)` |
| 8 | `(1120, 1198, 440, 20)` | `(1120, 1255, 440, 135)` |

The observed finite sample counts are about `8,800-9,000` for each Reference
ROI and `59,400-62,100` for each Measurement ROI. The model uses
`MinimumValidSampleCount=1000`.

## Acceptance policy for this self-test

The starter recipe uses broad software-connectivity limits:

```text
MinimumThickness = -100000
MaximumThickness = 100000
MinimumValidSampleCount = 1000
```

These limits prove data binding, plane fit, ROI ownership, ordered execution,
and result presentation. They are not production acceptance limits.

The owner must confirm the physical datum represented by each adjacent
Reference ROI before the values are called physical thickness. Calibration,
unit conversion, uncertainty, repeatability, and production OK/NG remain
outside this checkpoint.

## Operator workflow

```text
Open model recipe
  -> select Tab step
  -> review/edit its two ROI cards
  -> Apply explicitly
  -> save recipe
  -> add current C3D to Validation Set
  -> Run All explicitly
  -> review eight Tab rows and their metrics
```

To make the last portion self-testable without selecting the same file again,
Validation Set provides an `Add current input` command. It adds the current
recipe source as a pending sample only; it does not execute anything.

## Acceptance criteria

1. The supplied source opens through one saved eight-Tab recipe.
2. Reopening preserves all eight instance labels, 16 ROI identities, and
   coordinates.
3. Selecting a Tab selects only that step's ROI pair in the Viewer and editor.
4. `Add current input` stages the current C3D without changing the recipe or
   Viewer.
5. Explicit `Run All` returns eight real Thickness step results from
   `DualSurfaceThicknessRule`.
6. Save/reopen and current-source execution pass automated verification.
7. Current-build before/after screenshots show the operator path.

## Excluded scope

- automatic Tab detection for arbitrary parts;
- an oriented XYZ ROI volume;
- automatic Preview, Publish, or Run after ROI editing;
- camera, PLC, robot, or production-line integration;
- physical calibration, millimetres, uncertainty, GR&R, or certified
  metrology.

## Verified result

The generated model is:

`3D/SyntheticValidation/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json`

Actual ordered Runner output:

| Tab | Mean separation (`raw-height`) | Reference samples | Measurement samples | Self-test status |
|---|---:|---:|---:|---|
| 1 | -6.541 | 9,000 | 60,750 | Pass |
| 2 | 27.905 | 9,000 | 62,100 | Pass |
| 3 | 4.808 | 9,000 | 61,650 | Pass |
| 4 | 20.012 | 9,000 | 60,750 | Pass |
| 5 | -156.033 | 8,800 | 59,400 | Pass |
| 6 | -241.993 | 8,800 | 60,720 | Pass |
| 7 | 6.676 | 8,800 | 60,280 | Pass |
| 8 | -54.477 | 8,800 | 59,400 | Pass |

The Pass state is relative only to the broad self-test limits. It is not a
production disposition.

Verification completed:

- Release solution build: `0 warnings / 0 errors`;
- code structure: `15/15`;
- Tool Recipe teaching and instance-name persistence: `28/28`;
- Validation Set and current-input staging: `25/25`;
- generic height-measurement Workbench: `44/44`;
- Recipe Manager/WPG: `37/37`;
- docking/shortcut regression: `29/29`;
- actual ordered Runner on the supplied C3D: `8/8`;
- current-window screenshot quality: accepted on attempt 1.

Evidence:

- `artifacts/current/20260726-tab-thickness-model/before-single-thickness.png`
- `artifacts/current/20260726-tab-thickness-model/after-tab-01-authoring.png`
- `artifacts/current/20260726-tab-thickness-model/after-tab-validation.png`
- `artifacts/current/20260726-tab-thickness-model/tab-model-run.txt`
- `artifacts/current/20260726-tab-thickness-model/tab-model-run.json`
- `artifacts/current/20260726-tab-thickness-model/tab-model-run.html`
- `artifacts/current/20260726-tab-thickness-model/tab-model-run.csv`

## Completion record

Status: Complete

Scope: Exact-source eight-Tab recipe generation, independently editable ROI
pairs, persisted inspection-instance labels, explicit current-input staging,
and eight-step Thickness execution/result presentation.

Acceptance criteria: eight-step model -> pass; 16 ROI save/reopen -> pass;
per-Tab selection and Viewer presentation -> pass; current-input staging
without execution -> pass; explicit eight-step execution -> pass; current
before/after evidence -> pass.

Verification: Release build `0/0`; structure `15/15`; focused checks `28/28`,
`25/25`, `44/44`, `37/37`, and `29/29`; actual Runner `8/8`; screenshot
quality accepted.

Evidence: this document, the generated recipe, the generator script, and
`artifacts/current/20260726-tab-thickness-model/`.

Boundary / next dependency: the owner must confirm that each left adjacent
carrier strip is the intended physical reference datum and provide calibrated
units and production tolerances before this self-test model can become a
physical production inspection.
