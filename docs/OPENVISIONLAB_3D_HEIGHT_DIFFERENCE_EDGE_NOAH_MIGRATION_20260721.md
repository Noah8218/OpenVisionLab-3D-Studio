# Height Difference Edge Library-Noah Migration

Updated: 2026-07-21

Status: **Complete for deterministic software structure and regression evidence;
not a physical feature or metrology claim.**

## Outcome and boundary

Move only the deterministic adjacent-pair scan and strongest-per-scanline
selection from Studio to `Lib.ThreeD`. Studio remains the owner of C3D source
identity, raw-height/derived-field eligibility, recipe entity IDs, typed
`C3DHeightDifferenceEdgePointSet`, provenance, metrics/overlays, Preview /
Publish lifecycle, Viewer display, and Runner orchestration.

The new Noah contract is source-neutral: grid dimensions, row-major scalar
values, a rectangular selection, axis, polarity, and minimum delta in; ordered
selected pairs and diagnostics out. Non-finite values mean a pair is skipped.
It imports no Studio, C3D, recipe, UI, calibration, tolerance, or acceptance
types.

## Preserved v1 numerical contract

- `AcrossColumns` / `AcrossRows` and `Rising` / `Falling` / `Absolute`;
- finite `MinimumDelta > 0`, inclusive polarity comparison;
- adjacent pairs only, bounded by the taught selection;
- non-finite pair member -> skip pair, never fill or bridge;
- one strongest magnitude candidate per scanline, first start index wins an
  exact tie;
- midpoint and diagnostics retain the current C3D adapter values; and
- at least two accepted scanlines are required.

## Acceptance evidence

1. Noah smoke proves a winning edge, exact-tie ordering, missing-pair
   skipping, and support failure: **Pass (`37/37`)**.
2. Studio Edge Runner golden remains `13/13` with the packaged Noah DLL:
   **Pass**.
3. The package verifier reports `Lib.ThreeD 2.7.3`, source commit
   `b6e3275a223e6dd017dfb85bd1f47c8a9b0b69c8`, and SHA-256
   `EC635EDAA6DF1012AB73A7E03A104FD7F48300080A951A9E4357D415E9E242EC`:
   **Pass**.
4. Structural searches prove the Studio rule no longer contains the nested
   scan/candidate/polarity implementation and calls the Noah tool; `Lib.ThreeD`
   has no Studio dependency: **Pass**.
5. Full Studio build and current Library-Noah build pass without warnings or
   errors: **Pass (`0/0`, `0/0`)**.

## Excluded

This is a behavior-preserving ownership migration. It does not add automatic
edge discovery, fitting, calibration, physical units, a new recipe format, or
UI changes. No screenshot is required because no visible UI behavior is
changed.
