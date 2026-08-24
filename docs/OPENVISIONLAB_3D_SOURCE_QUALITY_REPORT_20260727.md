# OpenVisionLab 3D SourceQualityReport

Date: 2026-07-27

Backlog item: `B-07`

Status: Complete

Follow-up: `PL-0040` later extended the same Runner verifier from this
historical B-07 baseline to `18/18` signed finite, missing-value, and malformed
C3D topology cases. `PL-0046/B-10` then moved reusable grid-diagnostic
calculation into the committed and vendored SDK `GridDiagnosticsTool`, raised
the current report schema to `1.1`, and extended the same verifier to `22/22`.
Current reports require ordered Topology, Locator Monotonicity, Duplicate
Locator, and Coordinate Finiteness evidence with fail-closed payload
validation. Legacy schema `1.0` omits diagnostics and retains exact JSON
SHA-256 `E2176611372E01F26A8208A9C7C09154209A8DB50BA4774A1F4DA6670B9F82A2`.
See `OPENVISIONLAB_3D_SOURCE_QUALITY_EDGE_FIXTURE_CLOSURE_20260823.md` and
`OPENVISIONLAB_3D_DETERMINISTIC_MALFORMED_SOURCE_DIAGNOSTICS_CLOSURE_20260823.md`.
The original B-07 evidence below is intentionally preserved as recorded.

## Outcome

The first Source Trust contract is now implemented without WPF or Viewer
dependencies.

`SourceQualityReport` schema `1.0` records:

- source entity, format, path, byte length, source SHA-256, and root-source
  SHA-256;
- grid width, height, cell count, sample count, valid count, missing count,
  and exact ratios;
- raw-height minimum, maximum, mean, and deterministic histogram;
- declared unit, frame, coordinate convention, provenance, and derived state;
- a coordinate-preserving invalid-cell mask identity;
- explicit availability for Height, Intensity, Color, Depth, Normal,
  Confidence, and SNR.

For the supported C3D height-grid layout, only raw Height is reported as
available. The other channels are explicitly `Unavailable` with reasons.
Normals, confidence, acquisition metadata, calibration, and physical units
are not inferred.

## Ownership

| Responsibility | Owner |
| --- | --- |
| Serializable WPF-neutral schema | `OpenVisionLab.ThreeD.Core` |
| Full-resolution C3D analysis and mask identity | `OpenVisionLab.ThreeD.Data` |
| Headless JSON generation and focused verification | `OpenVisionLab.ThreeD.Runner` |
| Source Quality UI | Not implemented in this slice (`B-08`) |
| Coordinate-true invalid-mask image/overlay | Not implemented in this slice (`B-09`) |

The new code is:

- `src/OpenVisionLab.ThreeD.Core/Contracts/Data/SourceQualityReport.cs`;
- `src/OpenVisionLab.ThreeD.Data/HeightMaps/C3DSourceQualityAnalyzer.cs`;
- `src/OpenVisionLab.ThreeD.Runner/Application/SourceQualityReportExecution.cs`;
- `src/OpenVisionLab.ThreeD.Runner/Verification/Data/SourceQualityReportVerification.cs`.

`C3DHeightFieldSnapshot.LoadIdentified` owns one local source read, format
validation, byte identity, and immutable full-resolution sample creation.
Recipe execution continues to use the stricter existing `LoadVerified` path.

## Missing-cell and mask contract

C3D zero or non-finite float32 cells are missing. The mask is:

```text
row-major bitset
1 = missing
least-significant bit first within each byte
identity = SHA-256(prefix + version + width + height + byte length + bytes)
```

Including dimensions and encoding version prevents an equal byte sequence
from silently representing a different grid. The report currently exposes
the identity and encoding, not the mask bytes or an image. `B-09` must expose
and verify those coordinate-true bytes against the future Height Image.

## Headless usage

Focused contract verification:

```powershell
dotnet run --no-build `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj `
  -c Release -- `
  --verify-source-quality-report `
  --report artifacts/current/20260727-source-quality-report/verification.txt
```

Identified C3D report:

```powershell
dotnet run --no-build `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj `
  -c Release -- `
  --source-quality-c3d 3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D `
  --entity-id source.thickness-coupon-v1 `
  --unit raw-height `
  --frame frame.c3d-grid-index `
  --report artifacts/current/20260727-source-quality-report/synthetic-thickness-coupon-v1.source-quality.json
```

The command is analysis-only. It does not modify a recipe and does not invoke
Preview, Publish, Run, or Validation Set.

## Verification evidence

Release build:

```text
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"
0 warnings, 0 errors
```

Focused synthetic fixture:

```text
SourceQualityReport verification: Pass (12/12)
grid: 4 x 3
cells/samples: 12
valid: 10
missing: 2
histogram: 2,2,3,3
invalid mask SHA-256:
E55705189A5D08B23D9037386E93CAA3C6A723A3E29A83A993AEAD9908A1D68B
```

The 12 checks cover schema/source identity, exact grid/counts/ratios,
statistics, histogram, coordinate-true mask identity, frame/unit/provenance,
available-only channel reporting, explicit unsupported channels, JSON
round-trip, all-missing JSON behavior, locator-sensitive mask identity, and
invalid-bin rejection.

Exact owner source:

| Field | Evidence |
| --- | ---: |
| Source | `thickness-coupon-v1.C3D` |
| Grid | `1280 x 840` |
| Cells/samples | `1,075,200` |
| Valid | `908,436` |
| Missing | `166,764` |
| Valid ratio | `0.454132963599184` |
| Minimum raw height | `-1179.4000244140625` |
| Maximum raw height | `2348.60009765625` |
| Mean raw height | `664.5656229231487` |
| Invalid mask SHA-256 | `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358` |

Evidence directory:

- `artifacts/current/20260727-source-quality-report/verification.txt`;
- `artifacts/current/20260727-source-quality-report/source-quality-report.json`;
- `artifacts/current/20260727-source-quality-report/synthetic-thickness-coupon-v1.source-quality.json`.

## Closure record

Status: Complete

Scope: `B-07` WPF-neutral `SourceQualityReport` schema, deterministic C3D
analyzer, invalid-cell mask identity, explicit channel availability, JSON
generation, and headless verification.

Acceptance criteria:

- WPF-neutral Core contract -> pass;
- exact sample/valid/missing counts and ratios -> pass;
- deterministic statistics and distribution -> pass;
- locator-sensitive invalid-mask identity -> pass;
- JSON serialization/deserialization -> pass;
- unsupported normals/confidence/acquisition channels never fabricated ->
  pass;
- exact owner C3D produces a report -> pass.

Verification: Release build `0/0`; focused contract `12/12`; exact owner C3D
report generated successfully.

Evidence: this document and
`artifacts/current/20260727-source-quality-report/`.

Boundary / next dependency: This does not implement the full-size Height
Image (`C-06`), invalid-cell image/overlay parity (`B-09`), Source Quality
workspace (`B-08`), cross-format channel catalog (`B-11`), saved operator
acquisition notes (`B-12`), physical calibration, or metrology. R0 owner
unaided replay remains externally unverified.
