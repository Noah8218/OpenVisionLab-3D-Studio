# OpenVisionLab 3D Invalid-Cell Map Parity

Date: 2026-07-28

Backlog item: `B-09`

Status: Complete

## Outcome

Data now owns one immutable, coordinate-true `C3DInvalidCellMap` for the
native C3D grid.

The same map contract is consumed by:

- `C3DSourceQualityAnalyzer`, which publishes its identity through
  `SourceQualityReport`;
- `C3DHeightImageFrame`, which exposes the exact packed bytes and uses the
  map to choose valid versus missing display pixels.

The two paths no longer implement separate missing-cell scans or separate
SHA calculations.

This is a data and evidence closure. It does not add a visible Height Image
overlay, so no Workbench UI changed in this slice.

## Coordinate and byte contract

```text
cell index = row * width + column
bit value 1 = missing
packing = row-major, least-significant bit first
pixel X = column
pixel Y = row
no flip
no sampling
no interpolation
```

The exact encoding string remains:

```text
row-major-bitset;1=missing;lsb-first;identity=prefix+version+width+height+byteLength+bytes
```

The SHA-256 identity includes:

1. the stable identity prefix;
2. contract version `1.0`;
3. native width;
4. native height;
5. packed byte length;
6. the exact packed bytes.

Equal missing counts at different coordinates cannot share an identity.
Equal packed bits reshaped to different native dimensions cannot share an
identity.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `C3DInvalidCellMap` | Native width/height, missing count, packed bytes, coordinate lookup, contract identity, SHA-256 |
| `C3DSourceQualityAnalyzer` | Source statistics and report projection using `C3DInvalidCellMap.Identity` |
| `C3DHeightImageFrame` | Exact Height Image values/pixels plus the same typed invalid-cell map |
| `C3DInvalidCellMapVerification` | Synthetic coordinate, byte, identity, Source Quality, and Height Image parity |
| `C3DHeightImageVerification` | Native pixel mapping plus exact-source map/report parity |

`SourceQualityReport` remains schema `1.0`. It carries the compact mask
identity, while consumers that need the coordinate bytes use the typed Data
artifact. This avoids embedding a large Base64 mask in every report without
losing a verifiable linkage.

## Exact owner-source evidence

Source:

```text
3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D
```

| Field | Value |
| --- | ---: |
| Width | 1,466 |
| Height | 2,269 |
| Native cells / Height Image pixels | 1,075,200 |
| Valid cells | 908,436 |
| Missing cells | 166,764 |
| Packed mask bytes | 134,400 |
| Source SHA-256 | `5D3625B1A5A65EF8BEAB366FF7A007918D28FB614136414BBD30A441E85C8937` |
| Height Image pixel SHA-256 | `D6B402B870622F25C73C10C6D312DF1BB8EC837BC3EFC7A9B5BA8FB8EF432C4A` |
| Invalid-cell map SHA-256 | `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358` |
| Source Quality mask SHA-256 | `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358` |
| Parity | `True` |

## Verification

Commands:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release --disable-build-servers

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -- `
  --verify-c3d-invalid-cell-map `
  --report artifacts/current/20260728-invalid-cell-map-parity/invalid-cell-map-verification.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -- `
  --verify-source-quality-report `
  --report artifacts/current/20260728-invalid-cell-map-parity/source-quality-verification.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -- `
  --verify-c3d-height-image `
  --report artifacts/current/20260728-invalid-cell-map-parity/height-image-verification.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -- `
  --height-image-c3d 3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D `
  --entity-id source.c3d.height-map `
  --unit raw-height `
  --frame frame.c3d-grid-index `
  --report artifacts/current/20260728-invalid-cell-map-parity/exact-source-parity.txt
```

Current evidence:

| Gate | Result |
| --- | --- |
| Release build | `0` warnings / `0` errors |
| Invalid-cell map coordinate/byte/identity suite | `15/15` |
| SourceQualityReport regression | `13/13` |
| Height Image mapping/parity regression | `14/14` |
| Exact owner-source mask/image parity | Pass |
| Executable structure guard | `17/17` |

## Boundaries and next dependencies

This completion does not claim:

- a visible or selectable invalid/missing overlay in the Height Image
  (`C-11`);
- a Source Quality Workbench surface (`B-08`);
- manual/auto shared display range (`C-07`);
- shared Height Image and 3D hover/crosshair state as part of `B-09`; the
  separate `C-08` slice is now complete;
- Height Image ROI rendering or editing as part of `B-09`; `C-09/C-10` were
  completed later on 2026-07-28;
- physical calibration, traceability, uncertainty, GR&R, or certified
  metrology.

Next dependency-correct order:

1. `B-08 unified Source Quality workspace` | Completed 2026-07-28

2. `C-07 manual/auto display-range contract` | Completed 2026-07-28

3. `C-09/C-10 synchronized Height Image / 3D ROI editing` | Completed 2026-07-28

4. `C-11 visible invalid-cell overlay` | Recommended model: `gpt-5.6-terra` | Reasoning effort: medium

## Completion record

Status: Complete

Scope: `B-09` coordinate-true packed invalid-cell map, stable identity, and
Source Quality / Height Image byte and SHA parity.

Acceptance criteria:

- native missing-cell positions are exposed as stable row-major packed bytes
  -> pass, synthetic `15/15`;
- Source Quality and Height Image consume the same contract -> pass,
  Source Quality `13/13` and Height Image `14/14`;
- equal counts at different locators or dimensions cannot collide -> pass,
  deterministic fixtures;
- exact owner source preserves cell count, missing count, and SHA parity ->
  pass, `1,075,200` cells, `166,764` missing, identical
  `44EDC44D...C358`.

Verification: commands and results listed above.

Evidence:

- this document;
- `artifacts/current/20260728-invalid-cell-map-parity/`.

Boundary / next dependency: `C-11` still owns the visible invalid-cell overlay.
The next product slice is `B-08` Source Quality workspace. R0 owner replay,
physical calibration, and metrology remain external or unverified.
