# Cross-section Dimensions Generic ToolRecipe Completion

Date: 2026-07-22  
Status: Complete for deterministic display-frame software verification

## Outcome

Cross-section Dimensions is now an ordinary Measure node in the generic
Inspection Recipe Workbench:

```text
Published TransformedHeightField
  + one-row GridRectangle spanning at least two columns
  -> ExpectedWidth, WidthTolerance
  -> ExpectedHeightRange, HeightTolerance
  -> explicit Preview
  -> explicit Publish
  -> MeasurementResult
```

It is not a product mode, separate recipe family, or implicit inspection command.

## Numerical and ownership contract

- width = maximum finite A3 U-axis position - minimum position;
- H range = maximum finite A3 H value - minimum value;
- width and H range have independent expected values and tolerances;
- both decisions must pass for the tool result to pass;
- the declared A3 model unit is preserved.

Library-Noah owns the source-neutral range arithmetic and acceptance. Studio
owns exact Published A3 identity, one-row recipe policy, U/H adaptation,
metrics, overlays, hashes, WPG state, Preview/Publish, and Runner routing.

Pinned Library-Noah identity:

- commit: `e36d9c07baab967fd4252e7052345563f29872a3`
- package: `Lib.ThreeD 2.7.9`
- SHA-256: `B21A6266AFD470B7EE8A4C857496E53561F4D399F2460FEE2939AAE85AD0FF92`

The legacy raw-C3D Cross-section recipe remains compatible and delegates its
range arithmetic to the same Library-Noah implementation. Its historical
aligned-X/raw-height vocabulary is not reused by the generic A3 UI.

## UI and recipe contract

- Toolbox: `Measure / Cross-section Dimensions`.
- Step Parameters: common typed WPG adapter.
- Korean/English: distinct localized row-selection text.
- Selection: two Viewer grid picks resolving to exactly one row and at least
  two columns.
- Teaching and editing do not execute inspection.
- Preview and Publish remain explicit.
- Schema `1.3` preserves exact A3 owner, artifact/root hashes, grid, unit,
  frame, and row-segment identity through save/reopen.
- Ordered Runner calls the same Tools adapter used by Workbench Preview.

## Evidence

- before: `artifacts/verification/20260722-generic-cross-section/before.png`
- after: `artifacts/verification/20260722-generic-cross-section/after.png`
- live Shell: `after-shell.txt`
- ordered Runner: `ordered-runner.txt`
- typed WPG/Workbench: `workbench.txt`
- Library-Noah package/bridge: `package.txt`, `noah-bridge.txt`
- legacy golden: `cross-section-golden.txt`

The deterministic live fixture reports width `5`, H range `6.5`, six finite
samples, explicit Publish, and exact save/reopen identity.

## Completion record

```text
Status: Complete
Scope: Generic A3-owned one-row Cross-section Dimensions Measure node, Noah numerical ownership, typed WPG and Korean/English UI, explicit Preview/Publish, schema 1.3 save/reopen, and ordered Runner replay.
Acceptance criteria:
- exact Published A3 plus one-row GridRectangle with at least two columns -> pass
- A3 U width and H range with independent tolerances -> pass
- typed WPG and distinct Korean/English selection text -> pass
- explicit Preview/Publish and save/reopen -> pass
- direct adapter and ordered Runner output hash parity -> pass
- legacy raw-C3D Cross-section golden remains compatible -> pass
Verification:
- Library-Noah Release build -> 0 warnings, 0 errors
- Library-Noah smoke -> 54/54
- Studio Debug build -> 0 warnings, 0 errors
- Library-Noah package integrity and bridge -> pass, 7/7
- legacy Cross-section golden -> 9/9
- artifact-owned ordered Runner -> 18/18
- generic height measurement Workbench -> 27/27
- live Shell A3 Cross-section Preview/Publish/save/reopen -> PASS
- after screenshot quality -> accepted on first attempt
Evidence: artifacts/verification/20260722-generic-cross-section/
Boundary / next dependency: No automatic edge/feature detection, calibrated physical dimension, uncertainty, Gauge R&R, or metrology claim. The next priority is multi-step Run Record aggregation for the bounded ordered executor.
```
