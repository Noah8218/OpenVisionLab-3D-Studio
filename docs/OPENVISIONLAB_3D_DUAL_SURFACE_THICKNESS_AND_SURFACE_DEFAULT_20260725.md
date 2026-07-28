# Dual-surface Thickness and Surface Viewer default - 2026-07-25

> Owner use reopened this checkpoint's practical workflow evidence. The
> Delete, current Reference-only role, incomplete-draft save, and persistent
> diagnostics gaps are corrected and superseded by
> `OPENVISIONLAB_3D_OWNER_ROI_SAVE_DIAGNOSTICS_20260725.md`.

## Status

Status: Complete

Scope: Replace the generic one-ROI scalar-height `Thickness` adapter with an
ordered Reference ROI + Measurement ROI height-field measurement, preserve old
one-ROI recipes without silently executing the old meaning, and make `Surface`
the default C3D Viewer geometry.

## Product decision

`Thickness` now consumes:

1. one raw or Published transformed `HeightField`;
2. one recipe-owned `GridRectangle` for the reference surface;
3. one recipe-owned `GridRectangle` for the measurement surface.

The rule fits `height = a*u + b*v + c` to finite samples inside the Reference
ROI. Each finite Measurement ROI sample reports:

```text
signed thickness = measured H - fitted reference H at the same U/V
```

The reported unit is the declared source/reference height unit. The result is
an H-axis separation, not Euclidean plane-normal distance. Raw C3D uses source
grid indices for U/V plane prediction and retains the declared raw height unit.
A Published transformed height field uses its declared U/V pitches and H unit.

Acceptance checks every finite measurement separation against
`MinimumThickness` and `MaximumThickness`. Evidence retains mean, minimum,
maximum, range, RMS spread, reference-fit H RMS, both sample counts, limit
values, and below/above counts. Result overlays declare both ROIs, the fitted
reference plane, and the signed H-axis measurement direction.

## Legacy behavior

An existing one-ROI Thickness step is not deleted or reinterpreted. Its
existing selection remains the Measurement ROI. Storage/reopen remains
possible, but strict validation and Preview fail closed with an instruction to
teach the missing Reference ROI. Teaching that ROI upgrades the route to:

```text
HeightField -> Reference ROI -> existing Measurement ROI
```

No Preview, Publish, or Run occurs during this upgrade.

## UI behavior

- New Thickness steps require three ordered inputs.
- Step Parameters reuses the established two-ROI teaching cards:
  `1 Reference surface ROI`, then `2 Measurement ROI`.
- The active role owns capture, replacement, reuse, and the compact numeric
  X/Z footprint editor.
- The Viewer continues to render a `GridRectangle` as an X=column/Z=row
  height-field footprint. No `OrientedBox3D` or implicit vertical volume was
  introduced.
- A newly loaded C3D with surface topology now defaults to `Surface`.
  `Wireframe`, `Points`, and `Surface + Edges` remain explicit choices.

## Acceptance criteria and evidence

- Release build has zero warnings and zero errors.
- Display verification passes `96/96`; initial and reset C3D geometry is
  `Surface`.
- Generic height measurement verification passes `33/33`, including legacy
  one-ROI retention, ordered upgrade, known `5`-unit H-axis result, explicit
  Preview/Publish, numeric ROI edit, and save/reopen.
- Recipe teaching passes `27/27`.
- Docking passes `28/28`.
- Artifact-owned ordered Runner passes `18/18`.
- Synthetic Affine Inspection Plate passes `18/18` against independent
  dual-surface truth.
- Validation Set replays the updated graph, preserves all four Thickness
  overlays and twelve metrics on Fail, and leaves the authored source
  unchanged.
- Release Windows-pointer evidence passes hover, move, corner resize, and
  explicit same-ID Apply while camera, Preview, result collection, and the
  ordered two-ROI route remain unchanged.
- Current Release screenshots pass quality on attempt 1 at `1920 x 1040` and
  `1280 x 760`.

Evidence folder:

```text
artifacts/current/20260725-dual-surface-thickness/
```

Key evidence:

```text
display-release.txt
height-measurement-release.txt
recipe-teaching-release.txt
docking-release.txt
artifact-owned-roi-release.txt
synthetic-affine-release.txt
validation-set-release.txt
actual-pointer-dual-roi-apply-final.txt
actual-pointer-dual-roi-apply-final.drag-pointer.txt
before-single-roi-wireframe-1920x1040.png
after-dual-roi-surface-1920x1040.png
after-dual-roi-surface-1280x760.png
```

## Boundary / next dependency

This proves deterministic software behavior on the repository's fixed local
and synthetic data. It is not physical calibration, uncertainty, traceable
metrology, Gauge R&R, arbitrary sensor validation, or production-line proof.
The next product evidence gate remains the unaided owner first-recipe replay
on the updated Release EXE. Physical claims require a declared calibrated
height unit plus trusted reference and measurement samples.
