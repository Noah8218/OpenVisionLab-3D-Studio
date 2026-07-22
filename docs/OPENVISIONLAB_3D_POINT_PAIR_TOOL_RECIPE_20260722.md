# Point Pair Dimensions Generic ToolRecipe Checkpoint

Updated: 2026-07-22

## Decision

`Point Pair Dimensions` is an ordinary reusable Measure node in the canonical
`Inspection Recipe` Workbench. It is not a workspace mode and it does not own a
separate recipe lifecycle.

```text
Published TransformedHeightField + recipe-owned PointSet(2)
  -> Point Pair Dimensions
  -> MeasurementResult
```

The two ordered points are selected in the visible A3 field. Each grid locator
is reconstructed as full XYZ from the exact published reference-grid profile:

```text
P = Origin + UAxis * ((column + 0.5) * PitchU)
           + VAxis * ((row + 0.5) * PitchV)
           + HAxis * Height(row, column)
```

The tool reports full-XYZ distance, width orthogonal to `HAxis`, signed
height-axis delta, signed elevation angle, XYZ deltas, and scalar height delta.
This removes the old assumption that world Y is always the height direction.

## Ownership

- Library-Noah `Lib.ThreeD 2.7.6` owns source-neutral point-pair arithmetic,
  height-axis normalization, tolerance decisions, and invalid-input rejection.
- Studio owns A3 identity verification, grid-to-XYZ reconstruction, typed WPG
  parameters, Viewer PointSet teaching/overlay, explicit Preview/Publish,
  recipe/result hashes, save/reopen, and Ordered Runner adaptation.
- The vendored package is built from Library-Noah commit
  `83507676ce4d7a21021def8751d77415f8a542da` and has SHA-256
  `A460C4B4C0706E033B76003EE374955B00121A71F1E7DE4FAC273F30F30D1AB1`.

## Accepted UI/UX

- The Measure catalog exposes `Point Pair Dimensions` with a typed input/output
  description.
- The Viewer captures exactly two distinct A3 cells and shows both endpoints
  plus their connecting line.
- WPG exposes expected distance, distance tolerance, expected planar width,
  width tolerance, expected elevation angle, and angle tolerance.
- Capturing or editing geometry never runs inspection. Preview and Publish are
  explicit commands.
- The same PointSet ID and exact A3 owner/hash/grid/unit/frame binding survive
  schema `1.3` save/reopen.

## Verification

- Library-Noah Release build: `0` warnings, `0` errors.
- Library-Noah smoke: `45/45`.
- Vendored package hash/metadata: Pass.
- Studio Debug solution build: `0` warnings, `0` errors.
- Library-Noah Studio bridge: `7/7`.
- Artifact-owned direct/Ordered Runner verification: `15/15`; Point Pair direct
  and ordered output hashes match.
- Live Shell: normal A3 Preview/Publish, real OS-pointer PointSet(2), Point Pair
  Preview/Publish, schema `1.3` save/reopen, and exact binding all Pass.
- Live deterministic result: distance `6.71491`, planar width `6.70820`,
  elevation `2.56065 degree`, scalar height delta `0.300001`.

Evidence:

- `artifacts/verification/20260722-generic-point-pair/`
- `artifacts/ui/20260722-generic-point-pair/before.png`
- `artifacts/verification/20260722-generic-point-pair/after.png`

## Claim Boundary

This closes deterministic software behavior for the synthetic display frame.
It does not establish physical scale, calibration, uncertainty, Gauge R&R,
licensed metrology parity, automatic edge finding, or production readiness.

## Completion Record

Status: Complete

Scope: Generic ToolRecipe Point Pair from A3 PointSet(2), WPG, explicit
Preview/Publish, Viewer overlay, save/reopen, and Ordered Runner parity.

Acceptance criteria: exact artifact-owned PointSet(2) -> Pass; Noah arithmetic
and rotated-axis behavior -> Pass; actual pointer teaching -> Pass; explicit
Preview/Publish -> Pass; save/reopen -> Pass; direct/Runner hash parity -> Pass.

Verification: commands and reports under
`artifacts/verification/20260722-generic-point-pair/`.

Evidence: current-build screenshot `after.png`, live Shell report, saved recipe,
package report, bridge report, and Runner report.

Boundary / next dependency: Gap/Flush is the next legacy typed slice to migrate.
Physical claims remain blocked by trusted calibration/reference evidence.
