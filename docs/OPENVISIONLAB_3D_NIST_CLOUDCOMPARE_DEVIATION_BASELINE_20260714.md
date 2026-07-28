# NIST Overhang X4 CloudCompare Deviation Baseline

Checked: 2026-07-14

## Decision

CloudCompare `2.13.2` now provides an independent measured-to-nominal cloud-to-mesh baseline for the local NIST AMMT `Overhang Part X4` pair. The baseline uses all `4,223,524` unique measured vertices produced by CloudCompare's STL import and the original `2,904`-triangle nominal mesh. Coordinates stay in the NIST part frame and units stay in millimetres.

No ICP, best-fit, center matching, scale adjustment, clipping, smoothing, or maximum-distance cap was applied. This is intentional: the NIST XCT method states that the measured volume was already aligned by a 3-2-1 datum construction and exported in the part coordinate system. A second implicit best-fit would change the inspection reference and could absorb real form or datum error.

This passes the external deviation-baseline prerequisite for the local candidate. The baseline by itself does not pass the OpenVisionLab nominal/actual product slice, load the measured mesh in the Viewer, certify metrology accuracy, or authorize redistribution of the source or derived data. The downstream fixed product slice later passed and is recorded separately in `docs/OPENVISIONLAB_3D_NIST_NOMINAL_ACTUAL_END_TO_END_20260714.md`.

## Source Frame

The NIST XCT paper records the source contract used here:

- nominal design: `9 mm x 5 mm x 5 mm`;
- primary datum: the EDM-separated surface, constraining two rotations and one translation;
- secondary datum: the part `-Y` face;
- tertiary datum: the part `-X` face;
- aligned primary/secondary normals: `-Z` and `-Y`;
- export: registered volume and STL surface relative to the constructed part coordinate system;
- XCT voxel size: `11.95 um` isotropic;
- STL meshing: one-voxel spatial resampling and `0.0012 mm` meshing tolerance.

Primary source: https://doi.org/10.6028/jres.125.031

## Tool And Inputs

```text
CloudCompare: 2.13.2 Kharkiv
CloudCompare.exe SHA-256: 4695ABC490711ABD824714157D5258D3C16646AB107A86422BFD6CDA17D7CAF1
Nominal STL SHA-256: D9FC086CA8C0BC3722709E5C03A39C5C1CF60553845FF62F5699780E1D3C1734
Measured STL SHA-256: 2108E1B17B2CCE59138C74E5DF4951D407F52A3649C257C3FE942DE874FACA00
Measured source archive SHA-256: D31DD4B72101CDE9EC7422A74B4E1B2C87C21C0E92E1EE9BA71A54E18904F583
Measured STL bytes: 428,004,884
Measured STL declared triangles: 8,560,096
Units: mm
Global shift: (0,0,0)
Transform: identity in the NIST part frame
```

CloudCompare merges duplicate STL vertices and faces during import. It reports `4,223,524` remaining vertices and `8,453,502` remaining faces, then `EXTRACT_VERTICES` creates the compared point cloud. The resulting point-only PLY is `50,682,545` bytes with SHA-256 `447CDC6E7703DFDE98431F0A1BA154802FEA02E476F2FC7D06AA09F022874B50`.

This derivative is valid for an independent per-vertex C2M baseline. It is not the product inspection representation: CloudCompare's import changed source topology, and per-vertex statistics are weighted by mesh tessellation rather than surface area. OpenVisionLab must preserve the original source identity and define its own inspection sampling contract explicitly.

## Nominal Mesh Sign Contract

An independent binary-STL topology probe found:

```text
Triangles: 2,904
Unique undirected edges: 4,356
Boundary edges: 0
Non-manifold edges: 0
Same-direction paired edges: 0
Degenerate triangles: 0
Stored normals aligned with winding: 2,904 / 2,904
Signed volume: +163.30136502372818 mm^3
```

The nominal mesh is therefore closed, consistently wound, and outward-oriented. CloudCompare signs C2M values with the reference triangle normal. Under this verified winding, positive values are on the outward-normal side and negative values are on the inward-normal side.

## Identity-Frame Results

Unsigned C2M over all `4,223,524` measured vertices:

| Metric | Result (mm) |
| --- | ---: |
| Minimum | `0.0000000427008` |
| Mean | `0.192040211` |
| Population standard deviation | `0.208181684` |
| RMS | `0.283229688` |
| Median | `0.109370261` |
| P90 | `0.416323572` |
| P95 | `0.576279637` |
| P99 | `0.950326611` |
| Maximum | `1.22322023` |

Unsigned threshold distribution:

| Distance | Vertices within distance |
| --- | ---: |
| `0.05 mm` | `1,408,044` (`33.3381%`) |
| `0.10 mm` | `1,993,117` (`47.1909%`) |
| `0.25 mm` | `2,814,592` (`66.6408%`) |
| `0.50 mm` | `3,958,971` (`93.7362%`) |
| `1.00 mm` | `4,196,843` (`99.3683%`) |

Signed C2M in the same frame:

| Metric | Result (mm) |
| --- | ---: |
| Minimum | `-0.454320908` |
| Mean | `0.0124131265` |
| Population standard deviation | `0.282957542` |
| RMS | `0.283229688` |
| Median | `-0.0105711985` |
| P90 | `0.390244019` |
| P95 | `0.576279637` |
| P99 | `0.950326611` |
| Maximum | `1.22322023` |

Signed counts are `1,799,044` positive (`42.5958%`) and `2,424,480` negative (`57.4042%`). No scalar is non-finite or exactly zero.

## Independent Verification

`scripts/verify-cloudcompare-c2m-ply.py` independently parses the binary PLY outputs and checks source/candidate point order, exact XYZ preservation, finite scalar values, population statistics, quantiles, thresholds, and CloudCompare-log agreement.

| Check | Result |
| --- | --- |
| Verifier self-test | Pass |
| Source-to-unsigned output XYZ | Exact; maximum error `0` |
| Source-to-signed output XYZ | Exact; maximum error `0` |
| Unsigned log mean/std agreement | Errors `2.1119e-7` / `3.1643e-7 mm` |
| Signed log mean/std agreement | Errors `2.6506e-8` / `4.5834e-7 mm` |
| `abs(signed)` versus unsigned | Maximum difference `2.38418579e-7 mm` |
| Finite scalar values | `4,223,524 / 4,223,524` in both runs |

Observed local execution evidence, not a performance contract:

| Stage | Exit | Elapsed | Peak working set |
| --- | ---: | ---: | ---: |
| Measured STL import and vertex extraction | 0 | 12,758 ms | 1,180,053,504 bytes |
| Unsigned C2M and save | 0 | 4,430 ms | 277,483,520 bytes |
| Signed C2M and save | 0 | 4,921 ms | 276,930,560 bytes |

Evidence is retained under ignored `artifacts/research_samples/nist_overhang_x4/cloudcompare_deviation_20260714`:

```text
measured_extract_vertices_full.log
measured_vertices_full.ply
identity_measured_to_nominal_c2m_unsigned.log
identity_measured_to_nominal_c2m_unsigned.ply
identity_measured_to_nominal_c2m_unsigned_verify.txt
identity_measured_to_nominal_c2m_signed.log
identity_measured_to_nominal_c2m_signed.ply
identity_measured_to_nominal_c2m_signed_verify.txt
```

Repeat the independent checks with:

```powershell
python scripts\verify-cloudcompare-c2m-ply.py --self-test
$root = 'artifacts\research_samples\nist_overhang_x4\cloudcompare_deviation_20260714'
python scripts\verify-cloudcompare-c2m-ply.py --source "$root\measured_vertices_full.ply" --candidate "$root\identity_measured_to_nominal_c2m_unsigned.ply" --units mm --threshold 0.05 --threshold 0.1 --threshold 0.25 --threshold 0.5 --threshold 1.0 --expected-mean 0.19204 --expected-std 0.208182 --stat-tolerance 0.000001 --require-nonnegative --report "$root\identity_measured_to_nominal_c2m_unsigned_verify.txt"
python scripts\verify-cloudcompare-c2m-ply.py --source "$root\measured_vertices_full.ply" --candidate "$root\identity_measured_to_nominal_c2m_signed.ply" --scalar scalar_C2M_signed_distances --units mm --expected-mean 0.0124131 --expected-std 0.282958 --stat-tolerance 0.000001 --report "$root\identity_measured_to_nominal_c2m_signed_verify.txt"
```

After producing a current Viewer contract, append the corresponding `--viewer-contract <path> --viewer-statistics signed|unsigned` options. The verifier then checks count, min, max, mean, population standard deviation, RMS, and unit against the independently parsed CloudCompare scalar PLY and appends `ViewerContractParity|Pass` to the report.

## OpenVisionLab Stream, Distance, And Parity Gate

The non-visual OpenVisionLab gate now passes without changing the Viewer limit or decimating inspection geometry:

- `BinaryStlInspectionReader` validates the binary length, streams every original triangle in source order, computes SHA-256 in the same pass, preserves stored normals/vertices/attributes for a visitor, and records full-source bounds without retaining the complete file.
- The original `428,004,884`-byte measured STL completed in `5.276 s` with a 25 ms sampled peak process working set of `22,405,120` bytes (`21.367 MiB`). This is local execution evidence, not a performance guarantee.
- The streamed source identity matches intake evidence exactly: SHA-256 `2108E1B17B2CCE59138C74E5DF4951D407F52A3649C257C3FE942DE874FACA00`, `8,560,096` declared/processed triangles, `25,680,288` expanded vertices, and bounds `(-0.0829502344,-0.0934725478,-0.138919488)..(8.98379898,5.00179577,4.79433393) mm`.
- Runner-only `BinaryPlyVertexReader` validates the exact ordered binary-little-endian PLY contract and preserves raw coordinate bits. It is a verification reader, not a new product format loader.
- `TriangleMeshDistanceIndex` provides a render-independent median-split BVH and exact point-to-triangle closest-point calculation. `FindClosest` keeps edge/vertex signs explicitly unresolved. `ResolveRobustSign` is a separate opt-in path that considers candidates within float epsilon, prefers a face-interior projection when numerically tied, and otherwise uses the most orthogonal tied boundary candidate.
- The robust behavior was independently implemented after reviewing the public CloudCompare/CCCoreLib behavioral contract at tag `v2024.04.10`, commit `cac3533c6b9b773d33122a393533e2a328241d5b`. No CCCoreLib source, binary, or product dependency was copied into this repository.
- The controlled stream/ordered-PLY/distance/robust-sign/error golden passes `17/17`, including source order/hash/bounds, corrupt/truncated inputs, direct edge/vertex unresolved state, explicit robust edge recovery, a real BVH split, and parity rejection when ordered XYZ changes.

Full OpenVisionLab comparison against both CloudCompare outputs over the same ordered `4,223,524` validation vertices passes:

| Check | Result |
| --- | --- |
| Raw-float XYZ order | `0 / 4,223,524` mismatches |
| Unsigned maximum absolute difference | `7.301871997600351e-7 mm` |
| Unsigned mean difference | `4.7576901307522235e-9 mm` |
| Unsigned population-std difference | `3.043473556507692e-9 mm` |
| Unsigned threshold counts | Exact match at `0.05`, `0.10`, `0.25`, `0.50`, and `1.00 mm` |
| Direct face-interior signed coverage | `4,145,609 / 4,223,524` (`98.155213514%`) |
| Direct unresolved population | `77,915`: `77,083` edge and `832` vertex |
| Robust recovered population | `77,915 / 77,915` |
| Signed coverage after robust selection | `4,223,524 / 4,223,524` (`100%`) |
| Signed sign mismatches | `0` |
| Signed maximum absolute difference | `7.116761328584964e-7 mm` |

The full parity run completed in `161.663 s`; calculation time was `161.315 s` and observed peak process working set was `33,529,856` bytes. The zero-warning/error solution build and current loader/Viewer/Shell regression matrix also pass (`128/128`). These are local measurements, not product performance guarantees.

This closes one fixed NIST identity-frame, vertex-query algorithm gate. It does not prove surface-area-weighted inspection, arbitrary mesh topology, transformed/aligned inputs, the original measured STL's full `25,680,288` expanded-vertex sampling, physical uncertainty, or certified metrology. The later fixed visible product workflow reuses this gate without broadening those claims.

Current ignored evidence:

```text
artifacts/mesh_deviation/mesh_deviation_golden_robust_20260714.txt
artifacts/mesh_deviation/nist_overhang_x4_stl_stream_20260714.txt
artifacts/mesh_deviation/nist_full_parity_robust_20260714.txt
artifacts/mesh_deviation/regression_robust_20260714/matrix_smoke_summary_after.txt
```

Repeat the checks with:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-mesh-deviation --report artifacts\mesh_deviation\mesh_deviation_golden_robust_20260714.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --stl-stream-probe "artifacts\research_samples\nist_overhang_x4\OverhangPartX4 Part1 Surface_cleaned.stl" --unit mm --report artifacts\mesh_deviation\nist_overhang_x4_stl_stream_20260714.txt
$root = 'artifacts\research_samples\nist_overhang_x4\cloudcompare_deviation_20260714'
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --mesh-deviation-parity "$root\measured_vertices_full.ply" --nominal-stl "artifacts\research_samples\nist_overhang_x4\OverhangPart_9x5x5mm.STL" --cloudcompare-unsigned "$root\identity_measured_to_nominal_c2m_unsigned.ply" --cloudcompare-signed "$root\identity_measured_to_nominal_c2m_signed.ply" --unit mm --report artifacts\mesh_deviation\nist_full_parity_robust_20260714.txt
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1 -Configuration Debug -ArtifactDir artifacts\mesh_deviation\regression_robust_20260714 -SkipBuild
```

## Preview Gate

The fixed identity-frame Preview gate passed locally on 2026-07-14. Core owns typed file identities, source/execution fingerprints, tolerances, statistics, display samples, and results; Data owns the ordered binary-PLY reader; Tools owns the full-query executor and the existing BVH distance index; and the Viewer ViewModel owns command and presentation state. The executor verifier passes `12/12`, and the ViewModel verifier passes `56` checks including nominal/source-layer visibility synchronization and comparison-unit camera status.

Both standalone Viewer and the docked Shell execute all `4,223,524` validation points asynchronously. With lower/upper tolerances `-0.3` / `0.3 mm`, the fixed query reports `548,207` below, `2,990,143` within, `685,174` above, and overall `Fail`. Direct triangle-interior signing resolves `4,145,609` points and robust recovery resolves the remaining `77,915`. The signed display uses `59,487` samples at stride `71`; this sample never replaces the full-query metric input. The maximum difference between the precise Viewer contract statistics and the independently parsed CloudCompare PLY statistics is `1.3381639552001445e-7 mm`, below the declared `1e-6 mm` tolerance.

Current-source before/final Viewer and Shell images, quality reports, contracts, and deterministic reports are under `artifacts/nominal_actual_execution_20260714`. `viewer_cloudcompare_signed_parity_final.txt` and `viewer_cloudcompare_unsigned_parity_final.txt` are the repeatable Viewer-contract parity reports produced by `scripts/verify-cloudcompare-c2m-ply.py --viewer-contract`. The final narrow Shell host keeps the signed legend inside the Viewer HUD without overlap, and hiding `Nominal` also hides the corresponding root source layer. The isolated post-change matrix at `matrix_final/matrix_smoke_summary_after.txt` passes `128/128`, and `mesh_deviation_golden_final.txt` remains `17/17`.

## Downstream Product Gate

The same fixed slice now publishes Preview as a separate result entity/layer, persists actual/nominal/query identities and hashes plus units/frame/alignment/tolerances in a typed recipe, reopens it, and replays it through the headless Runner with matching status and statistics. Current evidence is under `artifacts/nominal_actual_publish_20260714`; executor/recipe/result verification passes `17/17`, ViewModel verification passes `60` checks, and Runner reports `ViewerRunnerComparison|Matched`.

The remaining gate is cross-sample and alignment generalization. Require a second independent measured/nominal pair and known non-identity transform truth before changing the fixed identity-frame contract. Do not broaden to CAD/GD&T from this single pair.

Do not promote these ignored files to the public sample matrix until redistribution approval, practical CI size, deterministic derivative policy, and current Viewer/Runner evidence all pass.

## References

- NIST dataset: https://doi.org/10.18434/mds2-2291
- NIST XCT method and 3-2-1 registration: https://doi.org/10.6028/jres.125.031
- CloudCompare command-line mode: https://www.cloudcompare.org/doc/wiki/index.php/Command_line_mode
- CloudCompare cloud-to-mesh distance: https://www.cloudcompare.org/doc/wiki/index.php?title=Cloud-to-Mesh_Distance
- CloudCompare signed-C2M robust edge-case history: https://github.com/CloudCompare/CloudCompare/blob/master/CHANGELOG.md
- CCCoreLib `v2024.04.10` robust candidate behavior: https://github.com/CloudCompare/CCCoreLib/blob/v2024.04.10/src/DistanceComputationTools.cpp
- Geometric Tools point-to-triangle distance derivation: https://www.geometrictools.com/Documentation/DistancePoint3Triangle3.pdf
