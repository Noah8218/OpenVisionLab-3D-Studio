# OpenVisionLab 3D Height Difference Edge Typed Adapter v1

Updated: 2026-07-19

Status: **Complete locally; remote CI not yet run**

## Completed scope

The owner-approved adjacent-height-difference rule is now one typed feature-
extraction slice after Filter:

```text
explicit Filter Preview -> explicit Filter Publish
  -> recipe-owned GridRectangle
  -> explicit Edge Preview -> temporary EdgePointSet
  -> explicit Edge Publish without recalculation
```

- Core owns immutable ordered points, diagnostics, provenance, and canonical
  SHA-256.
- Tools owns strict recipe parsing and the one adjacent-pair implementation.
- Workbench owns waiting/ready/running/preview/stale/published state.
- Viewer owns the band, direction, markers, selected-marker detail, and
  Preview/Published presentation only.
- Runner executes the same Filter and Edge adapters headlessly.

`minimumInputCount` remains `1` in the existing teaching document as the
required data-input count so an incomplete template can still be saved. Typed
readiness separately requires exactly two routed entities: the current
Published `FilteredHeightField` followed by one source-bound `GridRectangle`.

## Fixed v1 numerical contract

- numeric frame: `X=column`, `Y=raw height`, `Z=row`;
- `AcrossColumns` / `AcrossRows` and `Rising` / `Falling` / `Absolute`;
- explicit invariant finite `MinimumDelta > 0`, inclusive comparison;
- one `StrongestPerScanline` candidate, lowest start index on an exact tie;
- adjacent-pair arithmetic midpoint;
- `SkipPair`, `WithinSelection`, no filling or bridging; and
- at least two accepted output points.

The output is preprocessing evidence and never produces measurement OK/NG.

## Local evidence

```text
Build:                  0 warnings, 0 errors
Numerical/Runner:       13/13
Workbench state/parity: 10/10
Teaching:               16/16
Selection contracts:    17/17
```

The fixed synthetic Runner output hash is
`9D543B48DAB4C3448C7ABFBC8E07E48901F4BEF6F45FF4D9F5B0265CBA257C61`.
The Workbench verifier independently matches its fixed synthetic Preview hash
to the shared headless adapter and proves Publish reuses the exact object.

Current-source UI evidence:

- before: `artifacts/ui/20260719-height-difference-edge-v1/before.png`;
- after: `artifacts/ui/20260719-height-difference-edge-v1/after.png`;
- quality: `artifacts/ui/20260719-height-difference-edge-v1/after-quality.txt`
  (`accepted=True`, attempt 1, `1280 x 760`).

## Fixed-source smoke boundary

The UI wiring smoke uses a documented, in-memory-only band:

```text
Root SHA-256: 79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C
Filter SHA-256: 569436F1ED6DCB656862935A738FAB691D156BD7FBE1071962FB8DA290E400C6
Band: rows 285..419, columns 290..305
Axis / polarity / threshold: AcrossColumns / Rising / 100 raw-height
Output: 135 points
Output SHA-256: 94F44FC244DCED2409DEEE5AF07C0DF9E2AC108C7C3DBB985647ED0A6B8CFB2B
```

This proves wiring and deterministic calculation only. The values are never
saved into the shipped template and are not production teaching, accuracy,
physical units, calibration, or metrology evidence. The owner must still teach
and review four real bands, axes, polarities, and thresholds.

## Commands actually run

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Debug -p:Platform="Any CPU"
dotnet run --no-build --project src/OpenVisionLab.ThreeD.Runner -- --verify-c3d-edge --report artifacts/verification/20260719-height-difference-edge-v1/golden.txt
dotnet run --no-build --project src/OpenVisionLab.ThreeD.Shell -- --verify-tool-edge-workbench artifacts/verification/20260719-height-difference-edge-v1/workbench.txt
dotnet run --no-build --project src/OpenVisionLab.ThreeD.Shell -- --verify-tool-recipe-teaching artifacts/verification/20260719-height-difference-edge-v1/teaching.txt
dotnet run --no-build --project src/OpenVisionLab.ThreeD.Shell -- --verify-tool-recipe-selections artifacts/verification/20260719-height-difference-edge-v1/selections.txt
```

## Completion record

```text
Status: Complete
Scope: Approved Height Difference Edge v1 typed rule, strict recipe adapter,
       explicit Workbench lifecycle, Viewer overlay, Runner replay, template,
       CI gate, and current-source UI evidence.
Acceptance criteria:
  - Seven approved numerical/workflow decisions implemented: Pass.
  - Explicit upstream/Preview/Cancel/Publish/stale contract: Pass.
  - Synthetic numerical, identity, schema, Runner, and UI parity: Pass.
  - Shipped template invents no real bands or thresholds: Pass.
Verification: Build 0/0; Golden/Runner 13/13; Workbench 10/10; teaching
              16/16; selections 17/17; screenshot accepted on attempt 1.
Evidence: artifacts/verification/20260719-height-difference-edge-v1,
          artifacts/ui/20260719-height-difference-edge-v1, and this document.
Boundary / next dependency: Remote Windows CI is not run for this working tree.
  Real alignment adoption still requires four owner-taught bands. 3D Line Fit,
  intersection, correspondence, affine, physical calibration, uncertainty,
  and metrology remain separate gates.
```
