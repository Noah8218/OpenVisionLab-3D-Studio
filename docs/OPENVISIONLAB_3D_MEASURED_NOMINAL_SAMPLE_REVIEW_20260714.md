# Measured/Nominal Sample Review

Updated: 2026-07-15

## Decision

Use NIST AMMT `Overhang Part X4` as the first local external measured/nominal trust candidate.

- Nominal: `OverhangPart_9x5x5mm.STL`.
- Measured: `OverhangPartX4 Part1 Surface_cleaned.stl`, extracted from the Part 1 XCT surface archive.
- Keep both under ignored `artifacts/research_samples/nist_overhang_x4`; do not commit or add them to the fixed CI matrix yet.
- This pair is suitable for a source-provenance, alignment, and mesh-deviation prototype. It is not prevalidated OpenVisionLab metrology evidence and does not authorize calibrated or certified claims.

Use `Overhang Part X4` Part 2 as the second physical-instance candidate. Its external, non-visual algorithm, visible Viewer/Shell, recipe, Runner, and Run Record gates now pass for the fixed identity-frame query.

- Nominal: reuse the same source design `OverhangPart_9x5x5mm.STL` because NIST identifies the four parts as nominally identical.
- Measured: `OverhangPartX4 Part2 Surface_cleaned.stl`, kept under ignored `artifacts/research_samples/nist_overhang_x4_part2`.
- Part 2 is a separately manufactured and measured object with a different source hash, triangle count, and bounds from Part 1. It can test multi-piece generalization but not new nominal geometry, sensor modality, or topology family by itself.
- Stanford Drill remains a separate transform-truth candidate only. Its published `.conf` supplies non-identity translations and quaternion rotations for real range scans, but its reconstructed mesh is not CAD nominal and Stanford excludes commercial/product use without permission.

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
| NIST AMMT Overhang X4 Part 2 | A separately manufactured XCT surface for the same public nominal design, with NIST method, units, datum construction, and public provenance. | External CloudCompare C2M, non-visual OpenVisionLab parity, visible recipe/UI, Viewer/Runner `Matched`, and Run Record evidence pass for the fixed identity-frame query. Non-identity registration and broader geometry remain open. Do not commit the source archive or STL. |
| Stanford Drill | Twelve real range scans plus zipper/VRIP reconstructions of the same `1.6 mm` drill; the published `.conf` contains translation and quaternion rotation for every scan. | Local transform-truth gate passed at point/aggregate level. It is not CAD nominal, and Stanford prohibits commercial/product use without permission. Do not commit or ship it. Source: https://graphics.stanford.edu/data/3Dscanrep/ |
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
| NIST Part 2 measured ZIP | 197,482,785 | `bda2bc07b0f2e2920e3f5ae378849319d75b22f36ae078fcaf6ed5cb12ac96f9` |
| NIST Part 2 extracted measured STL | 402,032,984 | `0f74d3a949488c161dac71681420a171b1eda3e478ed24d492d33aa6c9f7f032` |
| Stanford Drill archive | 555,087 | `2da2acaba36903a9893c920c11506e1bd01c531bdf738d037bc505b313df5e1b` |
| Stanford Drill transform configuration | 1,261 | `95686b12d5cff5cc58599d211d81047b92f26186f42eb4ac57cf833fa0a2352f` |

Observed NIST geometry:

| Geometry | Triangles | Bounds | Viewer result |
| --- | ---: | --- | --- |
| Nominal | 2,904 | `(0,0,0)..(9,5,5)` | Loaded; 8,712 expanded vertices; screenshot accepted on attempt 1. |
| Part 1 measured | 8,560,096 | `(-0.082950,-0.093473,-0.138919)..(8.983799,5.001796,4.794334)` | Controlled rejection at the current 1,000,000-triangle limit. |
| Part 2 measured | 8,040,658 | `(-0.081858,-0.114425,-0.150348)..(8.979870,5.039510,4.826532)` | Current Runner streamed every source triangle in order; direct Viewer loading remains intentionally unsupported at this size. |

The files are distinct and their spans are consistent with the same part. The NIST XCT method confirms that the measured data was already aligned by a 3-2-1 datum construction and exported in the part coordinate system. The external signed and unsigned surface-deviation distribution is now fixed by the CloudCompare baseline below.

The Part 2 stream probe independently reproduced its extracted STL SHA-256, processed all `8,040,658` declared triangles, and recorded `24,121,974` expanded vertices without render-density sampling. Part 1 and Part 2 therefore represent different measured source geometry. They still share one nominal design and one XCT family, so Part 2 does not close broader shape/sensor generalization.

The local Stanford Drill archive contains `12` binary-big-endian range-grid PLY scans with `50,643` total vertices. Eleven transforms are non-identity; quaternion norms are `0.999999572744..1.000000234254`, and the published rotations span `0..179.762457157` degrees. The VripPack guide defines rotate-then-translate, while the version `0.31` command parser, renderer, and bbox helper resolve its executable quaternion convention as `transpose(ShoemakeQuaternionMatrix(q))*point+translation` for `x,y,z,w`. Independent Python and Runner implementations match `36` point checkpoints plus every per-scan and aggregate statistic with maximum observed difference `0`; a `0.001` tamper is rejected with exit `5`. CloudCompare `2.13.2` independently applies all twelve matrices and matches all ordered points within `3.0913966692081019e-8`. Source units remain unspecified, so this proves transform application rather than physical pose accuracy. See `docs/OPENVISIONLAB_3D_STANFORD_TRANSFORM_BASELINE_20260715.md`.

Primary intake sources:

- NIST Part 2 resource and public-use metadata: https://catalog.data.gov/dataset/x-ray-computed-tomography-data-of-additive-manufacturing-metrology-testbed-ammt-parts-over-ff55f
- NIST registered four-part description: https://catalog.data.gov/dataset/a-fully-registered-in-situ-and-ex-situ-dataset-for-metal-powder-bed-fusion-additive-manufa
- NIST registration and uncertainty report: https://doi.org/10.6028/NIST.AMS.100-69
- Stanford data terms and scan provenance: https://graphics.stanford.edu/data/3Dscanrep/
- Stanford `.conf` transform convention: https://graphics.stanford.edu/software/vrip/guide/

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

This closes the independent external baseline prerequisite for Part 1. CloudCompare merges duplicate vertices/faces during STL import, so its result is a vertex-weighted reference and must not replace the original OpenVisionLab inspection geometry. See `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md`.

Part 2 now has the same independent baseline structure over `3,965,430` CloudCompare-unique vertices:

- unsigned mean/std: `0.194395138` / `0.211636619 mm`;
- signed mean/std: `0.0181653253` / `0.286791822 mm`;
- exact XYZ preservation and finite scalar values for all vertices;
- `abs(signed)` versus unsigned maximum difference `1.78813934326e-7 mm`;
- OpenVisionLab unsigned/signed maximum difference `7.1853447186631669e-7 mm`;
- direct sign coverage `98.179112984%`, robust recovery `72,206/72,206`, material sign mismatches `0`, and final coverage `100%`;
- one float-epsilon near-zero sign equivalent is recorded explicitly, while the synthetic `18/18` gate still rejects material opposite signs.

This closes the fixed Part 2 external and non-visual algorithm gate. Its subsequent visible product slice is recorded in `docs/OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md`; see `docs/OPENVISIONLAB_3D_NIST_PART2_CLOUDCOMPARE_DEVIATION_BASELINE_20260715.md` for the independent baseline.

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
5. **End-to-end Part 1 pass:** View, ViewModel, shared execution/model contracts, signed result visuals, explicit Preview/Publish, typed recipe save/reopen, Runner replay, schema `1.2` evidence, and Viewer/Runner `Matched` state pass for the fixed Part 1 query.
6. **End-to-end Part 2 pass:** source identity, independent CloudCompare signed/unsigned C2M, ordered-PLY verification, full-query OpenVisionLab algorithm parity over `3,965,430` validation vertices, current Viewer/Shell visuals, selected-point provenance, explicit Preview/Publish, typed recipe save/reopen, schema `1.2`, and Viewer/Runner `Matched` evidence pass.
7. **Known-transform entry pass:** the separate Stanford fixed source passes published non-identity transform application over all `50,643` points with `36` point checks, complete aggregate parity, source hashes, strict range-grid parsing, CloudCompare external execution, and controlled tamper rejection. It does not prove registration recovery or physical units.

The full measured scan completed locally in `5.276 s` with a 25 ms sampled peak process working set of `21.367 MiB`. Its SHA-256, triangle count, and bounds match the intake record. See `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md` for commands and evidence.

## Next Gate

The second-pair and supplied-transform Phase 2 gates now pass, but Phase 2 itself does not. Audit the existing geometry/error goldens for duplicate vertices, non-finite normals, open surfaces, edge/vertex nearest hits, sparse/dense query sampling, and no-correspondence behavior, then add only missing controlled cases. Registration recovery remains separate from Stanford's supplied-transform application and is blocked from product integration until its runtime/distribution prerequisites are resolved.
