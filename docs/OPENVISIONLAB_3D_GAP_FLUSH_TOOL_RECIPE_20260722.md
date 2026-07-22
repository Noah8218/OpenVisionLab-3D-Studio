# Gap/Flush Generic ToolRecipe Completion

Date: 2026-07-22  
Status: Complete for deterministic display-frame software verification

## Outcome

Gap/Flush is now an ordinary Measure node in the generic Inspection Recipe Workbench:

```text
Published TransformedHeightField
  + first GridRectangle
  + second GridRectangle
  -> ExpectedGap, GapTolerance, ExpectedFlush, FlushTolerance
  -> explicit Preview
  -> explicit Publish
  -> MeasurementResult
```

It is not a workspace mode, separate recipe family, or implicit inspection command.

## Signed measurement contract

ROI order is authored and significant:

- signed gap = second ROI minimum U - first ROI maximum U;
- signed flush = second ROI mean H - first ROI mean H;
- a negative gap preserves overlap rather than taking an absolute value;
- both ROIs require finite samples and exact ownership by the Published A3 artifact.

The Studio derives U bounds and finite H statistics from the verified A3 grid/profile. Library-Noah performs caller-neutral validation, signed arithmetic, deltas, and independent tolerance acceptance.

## Ownership

- Library-Noah `Lib.ThreeD 2.7.7` owns the pure `GapFlushInspectionTool` arithmetic.
- Studio Core owns typed recipe/entity contracts.
- Studio Tools owns A3 binding verification, grid-to-statistics adaptation, metrics, overlays, evidence, and ordered execution.
- The Workbench ViewModel owns two-role ROI teaching, WPG draft state, and explicit Preview/Publish commands.
- Runner reuses the same Tools adapter; it does not duplicate the algorithm.

Pinned Library-Noah identity:

- commit: `6aba3d5b37e9d10f2d90977e483956b6d57e2aaf`
- package: `Lib.ThreeD 2.7.7`
- SHA-256: `B2909B939EEEF1000F22BDBED96D7A3AC1F67E2F6068AEC2F658ED1FF10E4708`

## UI contract

- The selected step exposes visible `INPUT -> parameters -> OUTPUT` routing.
- The first and second ROI are labelled separately in Korean and English.
- Capture/reuse follows the authored first-then-second order.
- ROI changes do not execute inspection.
- Preview and Publish remain explicit.
- Save/reopen retains exact A3 owner, hashes, grid, unit, frame, and ROI order.

Current-build visual evidence:

- before: `artifacts/verification/20260722-generic-gap-flush/before.png`
- after: `artifacts/verification/20260722-generic-gap-flush/after.png`

## Completion record

```text
Status: Complete
Scope: Generic A3-owned two-ROI Gap/Flush Measure node, Noah arithmetic ownership, WPG teaching, explicit Preview/Publish, schema 1.3 save/reopen, and ordered Runner replay.
Acceptance criteria:
- exact Published A3 plus two ordered GridRectangle inputs -> pass
- signed separation/overlap and signed flush arithmetic -> pass
- two-role ROI teaching and visible typed adapter -> pass
- explicit Preview/Publish and save/reopen -> pass
- direct adapter and ordered Runner numerical/hash parity -> pass
- invalid/empty input fails closed -> pass
Verification:
- Library-Noah build -> 0 warnings, 0 errors
- Library-Noah smoke -> 48/48
- Studio Debug build -> 0 warnings, 0 errors
- Library-Noah package integrity and bridge -> pass, 7/7
- legacy Gap/Flush golden -> 8/8
- artifact-owned ordered Runner -> 16/16
- generic height measurement Workbench -> 23/23
- Tool Recipe teaching -> 18/18
- Tool Recipe selection contract -> 17/17
- live Shell A3 Gap/Flush Preview/Publish/save/reopen -> PASS
- 1920 x 1080 screenshot quality -> accepted
Evidence: artifacts/verification/20260722-generic-gap-flush/
Boundary / next dependency: No automatic seam/edge detection, physical-unit calibration, uncertainty, Gauge R&R, or metrology claim. The next generic legacy-slice migration is Volume.
```

## Reusable next-tool checklist

1. Define the typed artifact and selection inputs before parameters.
2. Keep source-neutral arithmetic in Library-Noah when it is reusable.
3. Keep identity, grid mapping, UI state, overlays, and Runner adaptation in Studio.
4. Preserve selection roles and order through schema save/reopen.
5. Prove direct-adapter and ordered Runner parity.
6. Capture current-build UI evidence and state the physical/metrology boundary.
