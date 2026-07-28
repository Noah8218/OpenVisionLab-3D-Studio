# Volume Generic ToolRecipe Completion

Date: 2026-07-22  
Status: Complete for deterministic display-frame software verification

## Outcome

Volume is now an ordinary Measure node in the generic Inspection Recipe Workbench:

```text
Published TransformedHeightField
  + reference GridRectangle
  + measurement GridRectangle
  -> ExpectedNetVolume, VolumeTolerance
  -> explicit Preview
  -> explicit Publish
  -> MeasurementResult
```

It is not a product mode, a separate recipe family, or an implicit inspection command.

## Numerical contract

The reference ROI fits `H = aU + bV + c` in the Published A3 reference-grid
axes. Each finite measurement cell contributes its signed H residual multiplied
by `PitchU * PitchV`:

- above volume = sum of positive contributions;
- below volume = sum of the absolute negative contributions;
- signed net volume = above volume - below volume;
- acceptance = absolute difference from `ExpectedNetVolume` no greater than
  `VolumeTolerance`.

The output unit is the declared reference-grid model unit cubed. This is not a
physical-volume claim until unit scale, calibration, uncertainty, and traceable
comparison evidence exist.

## Ownership

- Library-Noah `Lib.ThreeD 2.7.8` owns least-squares reference-plane fitting,
  signed integration, validation, and tolerance acceptance.
- Studio Core owns typed recipe/entity contracts.
- Studio Tools owns exact A3 identity, U/V/H adaptation, cell-area derivation,
  metrics, overlays, hashes, and ordered execution.
- The Workbench ViewModel owns reference/measurement ROI teaching, WPG draft
  state, Korean/English text, and explicit Preview/Publish.
- Runner calls the same Tools adapter and does not duplicate the algorithm.

Pinned Library-Noah identity:

- commit: `d1dff41ca0ce940492930267aa0ae7430e73e437`
- package: `Lib.ThreeD 2.7.8`
- SHA-256: `D7C0BD0ED60249870BD8B0A6DAC7D69A7A608FD23347E5440DF8ED30C3A90F2F`

## UI contract

- The selected node exposes `INPUT -> parameters -> OUTPUT` routing.
- Reference and measurement ROI roles remain ordered and separately visible.
- WPG exposes expected signed net volume and volume tolerance.
- The teaching title/detail is distinct in Korean and English.
- Teaching or parameter edits never execute the algorithm.
- Preview and Publish remain explicit.
- Schema `1.3` save/reopen retains exact A3 owner, source hash, artifact hash,
  grid, unit, frame, and both ROI identities.

Fresh visual evidence:

- before, current pre-change build at 1920 x 1080:
  `artifacts/verification/20260722-generic-volume/before.png`
- after, current changed build at 1280 x 760:
  `artifacts/verification/20260722-generic-volume/after.png`

The after capture intentionally uses the live pointer-smoke layout size. Visual
comparison confirms the former Step 06 Gap/Flush endpoint becomes a distinct
Step 07 Volume node with typed routing and the same docked Workbench structure;
no separate Volume workspace was introduced.

## Completion record

```text
Status: Complete
Scope: Generic A3-owned reference/measurement-ROI Volume Measure node, Noah numerical ownership, typed WPG and Korean/English UI, explicit Preview/Publish, schema 1.3 save/reopen, and ordered Runner replay.
Acceptance criteria:
- exact Published A3 plus ordered reference/measurement GridRectangles -> pass
- U/V/H reference-plane fit and signed cell-area integration -> pass
- typed WPG and distinct Korean/English teaching text -> pass
- explicit Preview/Publish and save/reopen -> pass
- direct adapter and ordered Runner numerical/hash parity -> pass
- legacy Volume behavior remains compatible -> pass
- invalid/empty input fails closed -> pass
Verification:
- Library-Noah Release build -> 0 warnings, 0 errors
- Library-Noah smoke -> 51/51
- Studio Debug build -> 0 warnings, 0 errors
- Library-Noah package integrity and bridge -> pass, 7/7
- legacy Volume golden -> 9/9
- artifact-owned ordered Runner -> 17/17
- generic height measurement Workbench -> 25/25
- live Shell A3 Volume Preview/Publish/save/reopen -> PASS
- after screenshot quality -> accepted on first attempt
Evidence: artifacts/verification/20260722-generic-volume/
Boundary / next dependency: No automatic feature detection, calibrated physical volume, uncertainty, Gauge R&R, or metrology claim. The next generic legacy-slice migration is Cross-section Dimensions, followed by multi-step Run Record aggregation.
```

## Reusable next-tool checklist

1. Define typed artifact and selection inputs before parameters.
2. Put reusable source-neutral arithmetic in Library-Noah.
3. Keep identity, grid mapping, UI state, overlays, and Runner adaptation in Studio.
4. Preserve selection roles through save/reopen.
5. Prove legacy compatibility plus direct/ordered Runner parity.
6. Capture current-build UI evidence and state the physical/metrology boundary.
