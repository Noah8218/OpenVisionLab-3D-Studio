# Aligned Point Repeatability Contract

Updated: 2026-07-17

## Decision

The first per-point repeatability slice is a typed, render-independent Model/Tool contract plus a closed-schema Data loader and headless Runner report. It is intentionally not a Viewer feature and it does not infer registration, calibration, or physical units from C3D display data.

The contract is implemented by:

- `src/OpenVisionLab.ThreeD.Core/AlignedPointRepeatabilityContracts.cs`
- `src/OpenVisionLab.ThreeD.Data/AlignedPointRepeatabilityStudyLoader.cs`
- `src/OpenVisionLab.ThreeD.Tools/AlignedPointRepeatabilityRule.cs`
- `src/OpenVisionLab.ThreeD.Runner/AlignedPointRepeatabilityStudyExecution.cs`
- `src/OpenVisionLab.ThreeD.Runner/AlignedPointRepeatabilityGoldenVerification.cs`
- `src/OpenVisionLab.ThreeD.Runner/AlignedPointRepeatabilityStudyLoaderVerification.cs`

## Required Evidence

An `AlignedPointRepeatabilityInput` requires:

- stable study, measurement-definition, reference-ROI, unit, frame, alignment-reference, and correspondence-definition IDs;
- a unique finite aligned XYZ reference location for every correspondence ID;
- at least two acquisition runs with unique run IDs, unique source entity IDs, positive source byte lengths, and distinct 64-character SHA-256 values;
- capture time, matching unit/frame/alignment-reference, and non-empty alignment method/evidence IDs on every run; and
- one finite observation for every declared correspondence in every run. Missing or extra observations fail validation; partial coverage is not silently compared.

The closed-schema Study loader independently reads every source and separately hashed Mapping export. It hashes the exact Study and Mapping bytes that it parses, verifies source and Mapping byte lengths/SHA-256 values, rejects duplicate source or Mapping paths/hashes, and requires every Mapping to restate the run/source identity, unit, frame, alignment reference, correspondence definition, alignment method/evidence, and full correspondence set. The loader does not prove that a Mapping value was derived from its raw source; that requires a source-format-specific parser or trusted acquisition export in a later real-data gate.

## Calculation

For each declared correspondence, the rule calculates the mean, minimum, maximum, sample standard deviation (`n - 1`), six-sigma spread, and range from all runs. It reports each point in ordinal correspondence-ID order, preserving its declared aligned XYZ reference point for a future linked-Viewer projection.

The result passes only when every point is within both configured limits. It reports run count, correspondence count, maximum point sample standard deviation, maximum point range, and the number of failing correspondences. Validation failures are `Error`, not partial Pass/Fail results.

## Verification

Run the focused contract Golden from the repository root:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform="Any CPU"
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-aligned-point-repeatability --report artifacts\aligned_point_repeatability_20260717\aligned_point_repeatability_golden.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-aligned-point-repeatability-study --report artifacts\aligned_point_repeatability_study_20260717\aligned_point_repeatability_study_loader_golden.txt
```

For an actual Study document, run:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --aligned-point-repeatability-study <study.json> --report artifacts\aligned_point_repeatability\study_run.txt
```

The report preserves the parsed Study path/length/SHA-256 identity, source and Mapping path/length/SHA-256 evidence, acceptance policy, normal `ToolResult`/metric lines, and every per-point aligned coordinate and statistic. A valid inspection exits `0` for either Pass or Fail; an invalid file/evidence input exits `1` and writes an Error report; an input that reaches the Tool but is semantically invalid exits `4` with its Tool Error report.

Use `docs/OPENVISIONLAB_3D_ALIGNED_POINT_REPEATABILITY_STUDY_INTAKE_20260717.md` for the real-data folder, JSON, hashing, and evidence checklist.

Current local result: Model/Tool `33/33` and Study/Mapping Loader plus headless execution `20/20` pass. The Model suite covers exact threshold behavior, point ordering and immutable snapshots, null/invalid policies, source identity reuse, unit/frame/alignment mismatch, missing alignment evidence, duplicate/missing/extra correspondence coverage, non-finite input, and statistics overflow. The Loader/Runner suite covers closed schemas, Study/source/Mapping file identity from the parsed bytes, UTF-8 BOM Study/Mapping compatibility, duplicate path/hash rejection, mapping-to-study identity binding, alignment evidence, full correspondence coverage, provenance report emission, and altered-source Error reporting.

## Claim Boundary

This verifies only deterministic software behavior for synthetic aligned observations. It is not evidence of:

- real repeat acquisition or trusted derivation of mapping values from source bytes;
- alignment quality, registration accuracy, or correspondence extraction;
- calibrated sensor height, physical thickness, uncertainty, Gauge R&R, or metrology certification; or
- a Viewer overlay or linked 3D selection.

## Next Gate

Obtain distinct real aligned acquisitions with documented source unit/frame, alignment method/evidence, and a trusted source-to-correspondence export. Only after that evidence is accepted should the Calibration View -> ViewModel -> Model path expose 3D linked point selection. Physical calibration, uncertainty, Gauge R&R, and metrology claims remain separate gates.
