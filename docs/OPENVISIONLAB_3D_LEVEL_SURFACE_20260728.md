# OpenVisionLab 3D Level Surface

Date: 2026-07-28

Status: Complete for the documented software-preparation scope

Backlog items: `D-05`, `D-06`

## Completed scope

OpenVisionLab 3D Studio now owns a typed, deterministic `Level Surface`
preparation step. The operator routes one or more explicit
`GridRectangle` reference ROIs, applies the typed parameter draft, and
explicitly selects Preview.

The rule fits one least-squares raw-height plane to the unique finite source
cells covered by the reference ROIs:

```text
fittedHeight(X, Z) = slopeX * X + slopeZ * Z + intercept
targetHeight = mean raw height of the unique finite reference cells
derivedHeight = sourceHeight - fittedHeight(X, Z) + targetHeight
```

Overlapping reference cells are counted once. Missing source cells remain
missing. The source width, height, X/Z grid, frame, unit, and source bytes are
preserved. The result is a separate derived C3D.

This operation is an explicit raw-height detrend/shear while retaining the
native row/column grid. It is not a hidden source mutation, rigid XYZ pose
solve, resampling operation, or physical calibration.

## Typed output contract

`C3DLevelingTransform` records:

- source entity, root source SHA-256, frame, unit, width, and height;
- fit, leveling, missing-value, and grid policies;
- fitted X and Z slopes, intercept, and target height;
- reference sample count, residual RMS, and residual peak-to-valley;
- every routed reference ROI and its valid sample count;
- the equivalent 3 x 4 height-detrend matrix;
- deterministic transform SHA-256 and provenance.

The Workbench publishes both `LeveledHeightField` and
`LevelingTransform` evidence. Show, Pin, and Compare use the exact derived
C3D. The production Runner writes the same derived C3D and a JSON report with
the typed transform.

## Operator workflow

1. Add `Level Surface` from Prepare.
2. Draw the first stable reference ROI.
3. Use `Add reference ROI` when another separated datum region should
   contribute to the same plane.
4. Edit the typed minimum-valid-sample and maximum-reference-RMS gates.
5. Apply the parameter draft. This changes only the recipe.
6. Select Preview. Review input slopes, reference RMS/P2V, output slopes,
   transform identity, source/grid/missing preservation, and the exact
   derived C3D.
7. Select Publish to accept the current Preview without rerunning.
8. Save and reopen the recipe or replay it in Runner.

An RMS value above the authored maximum fails closed: the typed fit evidence
remains available, but no derived height field is produced.

## Known-fixture evidence

The focused fixture is a `16 x 12` identified height field containing a known
tilted plane, small deterministic residuals, two separated reference ROIs,
and one missing cell.

| Evidence | Value |
| --- | --- |
| Source valid / missing | `191 / 1` |
| Input reference slope X | `0.7999617440` raw-height/column |
| Input reference slope Z | `-0.3999192374` raw-height/row |
| Reference samples | `96` unique finite cells |
| Reference RMS | `0.0141768999` raw-height |
| Output reference slope X | `-4.2253E-08` raw-height/column |
| Output reference slope Z | `1.61666E-07` raw-height/row |
| Source SHA-256 | `D08AA4FE4377C0CC2A6A43210E98EC8A5E8815374311BA33D1CC40C1861EED52` |
| Output SHA-256 | `5BE202FAF610A7291CFD753837B2469A1C10A9F324A8216C4AB0D7CF8CE2A419` |
| Transform SHA-256 | `F2E47D4BC0C3CEB7746A5453501430D27D2016726D2F480920656580AA2BA265` |

The production Runner artifacts are:

- `artifacts/current/20260728-level-surface/runner-output.c3d`;
- `artifacts/current/20260728-level-surface/runner-report.json`.

## Verification

| Gate | Result |
| --- | --- |
| Release solution build | `0 warnings / 0 errors` |
| Deterministic fit, transform, failure gate, and recipe parity | `9/9` |
| Workbench typed draft, multi-ROI authoring, Preview/Publish, save/reopen, Viewer/Runner parity | `17/17` |
| Inspection Workspace regression | `63/63` |
| Shell smoke options | `24/24` |
| Tool recipe teaching regression | `28/28` |
| Artifact Navigator / Output Compare regression | `31/31` |
| Code structure | `17/17` |
| Wide current-source screenshot quality | Pass |
| Compact current-source screenshot quality | Pass |

Evidence folder:

`artifacts/current/20260728-level-surface/`

Current UI captures:

- `after-wide-current.png`;
- `after-compact-current.png`.

A true pre-change D-05 screenshot was not captured before implementation.
The closest reproducible prior Workbench baseline is
`artifacts/current/20260728-remove-outlier-pixels/after-wide-current.png`;
it is historical D-04 evidence, not represented as a true before capture.

## Boundaries

The contract uses row/column grid indices and `raw-height`. It does not prove
calibrated physical units, a rigid pose correction, re-gridded XYZ geometry,
traceability, uncertainty, GR&R, or certified metrology. Those require
separate source calibration and acceptance evidence.

## Completion record

```text
Status: Complete
Scope: one-or-more explicit reference ROI plane fit, typed deterministic leveling transform, source-preserving derived C3D, explicit Workbench Preview/Publish, save/reopen, Viewer/Output Compare, and production Runner parity
Acceptance criteria: tilted fixture -> input slopes recovered and output slopes near zero; multiple reference ROIs -> 96 unique finite samples and persisted routing; residual gate -> fail closed without output; source contract -> grid/missing/source identity preserved; transform -> typed deterministic hash; Workbench/Runner -> identical derived C3D and transform
Verification: Release build 0/0; golden 9/9; Workbench 17/17; Inspection Workspace 63/63; shell options 24/24; teaching 28/28; Artifact Navigator 31/31; structure 17/17; Wide/Compact capture quality pass
Evidence: docs/OPENVISIONLAB_3D_LEVEL_SURFACE_20260728.md and artifacts/current/20260728-level-surface/
Boundary / next dependency: raw-height grid-preserving detrend only; physical calibration, rigid pose/re-grid, and metrology are not claimed; I-04/I-05 labeled sample evidence is next
```
