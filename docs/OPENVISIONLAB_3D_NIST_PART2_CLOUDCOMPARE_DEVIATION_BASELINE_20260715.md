# NIST Overhang X4 Part 2 CloudCompare Deviation Baseline

Checked: 2026-07-15

## Decision

NIST AMMT `Overhang Part X4` Part 2 now passes the independent CloudCompare signed/unsigned cloud-to-mesh baseline and the render-independent OpenVisionLab full-query algorithm-parity gate in the NIST-provided identity part frame.

This is a second physical-instance result for the same nominal design and XCT workflow. The visible Viewer/Runner product slice subsequently passed and is recorded separately in `OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md`. Neither result proves arbitrary geometry, registration recovery, uncertainty, redistribution approval, or metrology certification, so Viewer Reliability Phase 2 remains open.

## Tool Identity

The official CloudCompare archive was downloaded from `https://www.cloudcompare.org/release/`. Its published MD5 and the extracted executable identity match the Part 1 baseline exactly.

```text
CloudCompare: 2.13.2 Kharkiv
Official archive: CloudCompare_v2.13.2_bin_x64.7z
Archive bytes: 75,979,509
Archive MD5: 55F90E46BD9565907E50B49A2F5F9490
Archive SHA-256: B5C534443EB58A80B54CEF23C525D1C92534400E76FE068BB45AD13CB0A1FF29
CloudCompare.exe SHA-256: 4695ABC490711ABD824714157D5258D3C16646AB107A86422BFD6CDA17D7CAF1
```

The executable SHA-256 is the same as the Part 1 evidence. No newer beta, installer build, or alternate external implementation was substituted.

## Inputs And Frame

```text
Nominal STL SHA-256: D9FC086CA8C0BC3722709E5C03A39C5C1CF60553845FF62F5699780E1D3C1734
Part 2 archive bytes: 197,482,785
Part 2 archive SHA-256: BDA2BC07B0F2E2920E3F5AE378849319D75B22F36AE078FCAF6ED5CB12AC96F9
Part 2 measured STL bytes: 402,032,984
Part 2 measured STL SHA-256: 0F74D3A949488C161DAC71681420A171B1EDA3E478ED24D492D33AA6C9F7F032
Part 2 declared triangles: 8,040,658
Units: mm
Global shift: (0,0,0)
Transform: identity in the NIST 3-2-1 part frame
Alignment added by this workflow: none
```

No ICP, best-fit, center matching, scale adjustment, clipping, smoothing, or maximum-distance cap was applied. The same nominal mesh and sign contract used for Part 1 were retained.

CloudCompare imported `24,121,974` expanded STL vertices and `8,040,658` faces, then merged duplicates to `3,965,430` vertices and `7,940,086` faces. `EXTRACT_VERTICES` produced the ordered validation query:

```text
measured_vertices_full.ply bytes: 47,585,417
measured_vertices_full.ply SHA-256: F4831F96B3709DC69AD46F28CA22DE8EB6FF6D751FC693B80196F7B22B5C19F1
vertices: 3,965,430
bounds: (-0.081858255,-0.114424519,-0.150348008)..(8.97986984,5.03950977,4.82653236) mm
```

This de-duplicated vertex cloud is an independent validation derivative, not the product inspection representation or a surface-area-weighted sample.

## CloudCompare Results

Unsigned C2M:

| Metric | Result (mm) |
| --- | ---: |
| Minimum | `0.0000000587253` |
| Mean | `0.194395138` |
| Population standard deviation | `0.211636619` |
| RMS | `0.287366540` |
| Median | `0.104899168` |
| P90 | `0.440426350` |
| P95 | `0.585342747` |
| P99 | `0.931990842` |
| Maximum | `1.22140050` |

Unsigned threshold distribution:

| Distance | Vertices within distance |
| --- | ---: |
| `0.05 mm` | `1,320,162` (`33.291774158%`) |
| `0.10 mm` | `1,914,011` (`48.267426231%`) |
| `0.25 mm` | `2,662,047` (`67.131357759%`) |
| `0.50 mm` | `3,680,205` (`92.807211324%`) |
| `1.00 mm` | `3,945,604` (`99.500029001%`) |

Signed C2M:

| Metric | Result (mm) |
| --- | ---: |
| Minimum | `-0.474883080` |
| Mean | `0.0181653253` |
| Population standard deviation | `0.286791822` |
| RMS | `0.287366540` |
| Median | `-0.00920910854` |
| P90 | `0.428615850` |
| P95 | `0.585342747` |
| P99 | `0.931990842` |
| Maximum | `1.22140050` |

Signed counts are `1,751,061` positive and `2,214,369` negative, with no zero or non-finite scalar. A second complete signed CloudCompare run produced the same PLY SHA-256 `4FA0A9D4CF31AD14C6274385F715B217FA1E242562BC500B737ECF875EABEBB0`.

## Independent PLY Verification

`scripts/verify-cloudcompare-c2m-ply.py` independently parses each binary PLY and verifies exact ordered XYZ, finite scalar values, statistics, quantiles, thresholds, and CloudCompare log agreement.

| Check | Result |
| --- | --- |
| Verifier self-test | Pass |
| Source-to-unsigned XYZ | Exact; maximum error `0` |
| Source-to-signed XYZ | Exact; maximum error `0` |
| Unsigned log mean/std error | `1.38108108e-7` / `3.812733e-7 mm` |
| Signed log mean/std error | `2.53227423e-8` / `1.78185575e-7 mm` |
| `abs(signed)` versus unsigned | Maximum difference `1.78813934326e-7 mm` |
| Finite values | `3,965,430 / 3,965,430` in both runs |

## OpenVisionLab Algorithm Parity

The current Runner uses the original nominal STL, the same ordered CloudCompare validation query, and the render-independent `TriangleMeshDistanceIndex`.

| Check | Result |
| --- | --- |
| Raw-float XYZ order | `0 / 3,965,430` mismatches |
| Unsigned maximum absolute difference | `7.1853447186631669e-7 mm` |
| Unsigned mean/std difference | `4.4292640111187609e-9` / `4.0554870017750488e-9 mm` |
| Direct face-interior sign coverage | `3,893,224 / 3,965,430` (`98.179112984%`) |
| Direct unresolved population | `72,206`: `71,394` edge and `812` vertex |
| Robust recovery | `72,206 / 72,206` |
| Material sign mismatches | `0` |
| Near-zero sign equivalents | `1` at `1.1920928955078125e-7 mm` zero tolerance |
| Signed maximum absolute difference | `7.1853447186631669e-7 mm` |
| Final signed coverage | `3,965,430 / 3,965,430` (`100%`) |

The first full run correctly failed because one face-interior point at ordered index `1,373,208` was `0` in OpenVisionLab and `-5.872529129646864e-8 mm` in CloudCompare. Both magnitudes are below the existing robust float-distance epsilon. The parity gate now records such cases as `nearZeroSignEquivalent` only when both values are inside that epsilon. A material opposite sign still fails. The synthetic verification covers both outcomes and passes `18/18`.

OpenVisionLab and CloudCompare threshold counts are exact at `0.05`, `0.50`, and `1.00 mm`. OpenVisionLab is lower by one point at `0.10` and `0.25 mm`; the pointwise distance difference remains below `7.2e-7 mm`. Therefore this evidence supports declared point and aggregate tolerances, not bit-identical boundary classification. Product tolerance decisions still require an explicit comparison tolerance or guard-band policy.

Observed local execution, not a performance contract:

```text
Full parity calculation: 152,588.693 ms
Full parity total: 152,932.850 ms
Observed peak process working set: 33,656,832 bytes
```

## Repeatable Commands

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
python scripts\verify-cloudcompare-c2m-ply.py --self-test

$root = 'artifacts\research_samples\nist_overhang_x4_part2\cloudcompare_deviation_20260715'
python scripts\verify-cloudcompare-c2m-ply.py --source "$root\measured_vertices_full.ply" --candidate "$root\identity_measured_to_nominal_c2m_unsigned.ply" --units mm --threshold 0.05 --threshold 0.1 --threshold 0.25 --threshold 0.5 --threshold 1.0 --expected-mean 0.194395 --expected-std 0.211637 --stat-tolerance 0.000001 --require-nonnegative --report "$root\identity_measured_to_nominal_c2m_unsigned_verify.txt"
python scripts\verify-cloudcompare-c2m-ply.py --source "$root\measured_vertices_full.ply" --candidate "$root\identity_measured_to_nominal_c2m_signed.ply" --scalar scalar_C2M_signed_distances --units mm --expected-mean 0.0181653 --expected-std 0.286792 --stat-tolerance 0.000001 --report "$root\identity_measured_to_nominal_c2m_signed_verify.txt"

dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-mesh-deviation --report artifacts\mesh_deviation\mesh_deviation_golden_part2_zero_sign_20260715.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --mesh-deviation-parity "$root\measured_vertices_full.ply" --nominal-stl artifacts\research_samples\nist_overhang_x4\OverhangPart_9x5x5mm.STL --cloudcompare-unsigned "$root\identity_measured_to_nominal_c2m_unsigned.ply" --cloudcompare-signed "$root\identity_measured_to_nominal_c2m_signed.ply" --unit mm --report "$root\openvisionlab_full_parity.txt"
```

Ignored evidence is retained under:

```text
artifacts/research_tools/cloudcompare_2.13.2
artifacts/research_samples/nist_overhang_x4_part2/cloudcompare_deviation_20260715
artifacts/mesh_deviation/mesh_deviation_golden_part2_zero_sign_20260715.txt
```

## Remaining Gate

1. Preserve the passed Part 2 visible workflow separately; see `OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md`.
2. Preserve the Stanford point/aggregate supplied-transform baseline separately; see `OPENVISIONLAB_3D_STANFORD_TRANSFORM_BASELINE_20260715.md`.
3. Audit and close only the missing difficult-geometry controlled outcomes before reconsidering the Phase 2 decision.
4. Keep Phase 3 blocked until calibration provenance, uncertainty assumptions, and independent metrology evidence exist.
