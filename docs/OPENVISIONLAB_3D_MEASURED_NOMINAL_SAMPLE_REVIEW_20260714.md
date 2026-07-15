# Measured/Nominal Sample Review

Updated: 2026-07-14

## Decision

Use NIST AMMT `Overhang Part X4` as the first local external measured/nominal trust candidate.

- Nominal: `OverhangPart_9x5x5mm.STL`.
- Measured: `OverhangPartX4 Part1 Surface_cleaned.stl`, extracted from the Part 1 XCT surface archive.
- Keep both under ignored `artifacts/research_samples/nist_overhang_x4`; do not commit or add them to the fixed CI matrix yet.
- This pair is suitable for a source-provenance, alignment, and mesh-deviation prototype. It is not prevalidated OpenVisionLab metrology evidence and does not authorize calibrated or certified claims.

The NIST XCT description identifies four nominally identical fabricated parts, the `9 mm x 5 mm x 5 mm` CAD design, millimetre units, the part coordinate convention, Zeiss Metrotom 800ii acquisition, manufacturer calibration routines, and `11.95 um` isotropic voxels. The catalog marks the data for public access and points to the NIST data-use notice. Because contributors include NIST and Georgia Tech, retain citation and disclaimer records and complete an owner/legal redistribution check before committing a source or derived asset.

Primary sources:

- Dataset: https://doi.org/10.18434/mds2-2291
- XCT method and data description: https://doi.org/10.6028/jres.125.031
- NIST data-use statements: https://www.nist.gov/open/copyright-fair-use-and-licensing-statements-srd-data-software-and-technical-series-publications
- Later fully registered numeric dataset: https://data.nist.gov/od/id/mds2-3761
- Registration and uncertainty description: https://doi.org/10.6028/NIST.AMS.100-69

## Acceptance Criteria

A measured/nominal candidate must record all of the following before product use:

1. Nominal and measured files are distinct and represent the same designed part.
2. Source URL, citation, byte length, SHA-256, and redistribution terms are retained.
3. Units, coordinate axes, origin/alignment state, and measurement provenance are explicit.
4. Viewer and headless paths report controlled success or controlled failure without excessive memory use.
5. Any decimated or converted derivative records its source hash, transform, method, parameters, output hash, and independent bounds/deviation check.
6. Render density never changes inspection sampling silently.
7. A nominal/actual product slice must still provide recipe persistence, explicit Preview/Publish, metrics, signed deviation overlay, tolerance state, Runner parity, and current Viewer/Shell evidence.

## Candidate Review

| Candidate | Correspondence | Use decision |
| --- | --- | --- |
| NIST AMMT Overhang X4 | CAD/design STL plus XCT-extracted surfaces from four nominally identical manufactured parts; official method, units, coordinate convention, and calibration context are available. | Selected as local external trust candidate. Original Part 1 measured surface is too large for the current Viewer limit, so no fixed-matrix promotion yet. |
| Stanford Drill | Twelve raw range scans plus zipper/VRIP reconstructions of the same `1.6 mm` drill; alignment transforms are present. | Research-only fallback. Stanford permits research use but prohibits commercial/product use without permission, and the data are PLY rather than a CAD nominal. Do not commit. Source: https://graphics.stanford.edu/data/3Dscanrep/ |
| MVTec 3D-AD | Defect-free and anomalous industrial 3D scans with ground truth. | Not a CAD measured/nominal pair; CC BY-NC-SA 4.0 and download registration make it unsuitable for the public product baseline. Source: https://www.mvtec.com/research-teaching/datasets/mvtec-3d-ad |
| Open3D DemoICPPointClouds | Three Redwood RGB-D fragments intended for ICP. | Preserve as alignment-engine golden only. It is neither a CAD nominal nor calibrated inspection evidence. Source: https://www.open3d.org/docs/latest/tutorial/data/index.html |
| Local synthetic mesh pair | Exact expected transform and deviations can be generated under project-owned terms. | Useful only for deterministic algorithm goldens. It cannot close the external real-sensor trust gate by itself. |

## Local Probe Evidence

Downloaded files are intentionally ignored by Git.

| Item | Bytes | SHA-256 |
| --- | ---: | --- |
| NIST nominal STL | 145,284 | `d9fc086ca8c0bc3722709e5c03a39c5c1cf60553845ff62f5699780e1d3c1734` |
| NIST Part 1 measured ZIP | 209,972,813 | `d31dd4b72101cde9ec7422a74b4e1b2c87c21c0e92e1ee9ba71a54e18904f583` |
| NIST Part 1 extracted measured STL | 428,004,884 | `2108e1b17b2cce59138c74e5df4951d407f52a3649c257c3fe942de874faca00` |
| Stanford Drill archive | 555,087 | `2da2acaba36903a9893c920c11506e1bd01c531bdf738d037bc505b313df5e1b` |

Observed NIST geometry:

| Geometry | Triangles | Bounds | Viewer result |
| --- | ---: | --- | --- |
| Nominal | 2,904 | `(0,0,0)..(9,5,5)` | Loaded; 8,712 expanded vertices; screenshot accepted on attempt 1. |
| Part 1 measured | 8,560,096 | `(-0.082950,-0.093473,-0.138919)..(8.983799,5.001796,4.794334)` | Controlled rejection at the current 1,000,000-triangle limit. |

The files are distinct and their spans are consistent with the same part. The NIST XCT method confirms that the measured data was already aligned by a 3-2-1 datum construction and exported in the part coordinate system. The external signed and unsigned surface-deviation distribution is now fixed by the CloudCompare baseline below.

## External Deviation Baseline

CloudCompare `2.13.2` extracted all `4,223,524` unique measured vertices and compared them to the original nominal mesh without ICP, best-fit, center matching, scale adjustment, clipping, smoothing, or a maximum-distance cap. Preserving the NIST 3-2-1 part frame avoids silently changing the inspection datum.

- unsigned C2M mean/std: `0.192040211` / `0.208181684 mm`;
- unsigned median/P95/max: `0.109370261` / `0.576279637` / `1.22322023 mm`;
- signed C2M mean/std: `0.0124131265` / `0.282957542 mm`;
- signed min/max: `-0.454320908` / `1.22322023 mm`;
- exact XYZ preservation in both outputs: maximum error `0`;
- independent log agreement: below `1e-6 mm` for mean and standard deviation;
- `abs(signed)` versus unsigned: maximum difference `2.38418579e-7 mm`.

The nominal STL is closed and consistently outward-wound: 4,356 unique edges, no boundary/non-manifold/same-direction edges, no degenerate triangles, all 2,904 stored normals aligned, and positive signed volume. The signed result therefore has a controlled normal-direction contract.

This closes the independent external baseline prerequisite only. CloudCompare merges duplicate vertices/faces during STL import, so its result is a vertex-weighted reference and must not replace the original OpenVisionLab inspection geometry. See `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md`.

## Viewer Stability Finding

Before the 2026-07-14 fix, `StlMesh.Load` read the entire `428,004,884`-byte measured STL before checking its triangle limit. A current-build preflight now reads the 84-byte binary header first, confirms the length/count contract, and rejects unsupported triangle counts before allocating the source file buffer.

| Build | Exit | Elapsed | Peak working set | Result |
| --- | ---: | ---: | ---: | --- |
| Committed baseline | 1 | 2,695 ms | 574,529,536 bytes | Controlled failure after full-file read. |
| Fixed source | 1 | 2,493 ms | 152,526,848 bytes | Controlled failure before full-file read. |

Peak working set fell by `422,002,688` bytes (`73.45%`). WPF startup dominates elapsed time, so memory reduction is the meaningful acceptance signal.

Current evidence:

- `artifacts/research_samples/nist_overhang_x4/viewer_limit_before.png`
- `artifacts/research_samples/nist_overhang_x4/viewer_limit_before_contract.txt`
- `artifacts/research_samples/nist_overhang_x4/viewer_limit_after_final.png`
- `artifacts/research_samples/nist_overhang_x4/viewer_limit_after_final_contract.txt`
- `artifacts/research_samples/nist_overhang_x4/viewer_limit_after_final_quality.txt`
- `artifacts/research_samples/nist_overhang_x4/viewer_nominal.png`
- `artifacts/research_samples/nist_overhang_x4/viewer_nominal_contract.txt`
- `artifacts/research_samples/nist_overhang_x4/viewer_nominal_quality.txt`
- `artifacts/matrix_smoke_summary_after.txt`: `129` passes, `0` failures.

## Foundation Gate Progress

Do not raise the Viewer triangle limit or silently drop measured triangles. The non-visual inspection foundation now has this status:

1. **Pass:** the original measured STL streams without retaining the complete file buffer; all `8,560,096` triangles are processed in source order.
2. **Pass:** the inspection reader and distance index have no Viewer or render-density dependency.
3. **Pass:** the probe records millimetre units, source SHA-256, original triangle count/order, bounds, and the explicit identity/no-alignment frame contract.
4. **Pass for the fixed identity-frame query set:** controlled stream/ordered-PLY/distance/robust-sign goldens pass `17/17`. All `4,223,524` ordered measured vertices preserve exact XYZ, unsigned and robust-signed C2M differ from CloudCompare by less than `1e-6 mm`, and all `77,915` direct edge/vertex unresolved cases are recovered with zero sign mismatches.
5. **View/ViewModel pass; execution open:** the layout, current-source Viewer/Shell before/after captures, and `NominalActualComparisonViewModel` state/command verifier now pass. Shared execution/model contracts, real result visuals, recipe persistence, and Runner replay have not started.

The full measured scan completed locally in `5.276 s` with a 25 ms sampled peak process working set of `21.367 MiB`. Its SHA-256, triangle count, and bounds match the intake record. See `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md` for commands and evidence.

## Next Gate

The standalone Viewer/Shell binding surfaces and `NominalActualComparisonViewModel` passed on 2026-07-14; current evidence is under `artifacts/nominal_actual_viewmodel_20260714`. Add only the required shared input/result/execution contracts next, then connect Preview without moving numerical work into the Viewer. Keep the CloudCompare de-duplicated PLY as a validation query set; do not silently replace the original measured source identity or claim the fixed parity result generalizes to other meshes or alignment states.
