# Stanford Drill Non-Identity Transform Baseline

Date: 2026-07-15

## Decision

The fixed Stanford Drill transform gate passes locally for the published twelve-scan `.conf` set.

- Independent Python and OpenVisionLab Runner implementations parse all `12` binary-big-endian range-grid PLY scans and all `50,643` source points.
- They match at `36` ordered point checkpoints, every per-scan statistic, and the full transformed aggregate with maximum observed numeric difference `0`.
- CloudCompare `2.13.2` independently reads every original scan, applies the generated 4x4 matrices, and preserves ordered point parity with maximum coordinate difference `3.0913966692081019e-8`, below the declared `1e-7` external float32 tolerance.
- A reference with one transformed coordinate changed by `0.001` is rejected with Runner exit code `5`.
- A CloudCompare output with one coordinate changed by `0.001` is also rejected with Python exit code `5` and a failure report naming the scan and point index.
- This closes the Phase 2 known non-identity transform entry gate only. It is not Viewer alignment UI, registration recovery, physical-unit validation, or metrology evidence.

The source data and generated reference remain ignored research artifacts. Stanford's data terms do not permit commercial/product use without permission, so no scan, reconstruction, or derived point set is shipped or added to CI.

## Executable Transform Contract

The [VripPack guide](https://graphics.stanford.edu/software/vrip/guide/) defines each configuration row as:

```text
bmesh <range_image> <tx> <ty> <tz> <qi> <qj> <qk> <ql>
```

It states that the range image is rotated by the quaternion and then translated. The executable convention was resolved from the official VripPack `0.31` source archive rather than inferred from that sentence alone:

- archive: `9,115,284` bytes, SHA-256 `1A714D5A629688FDCF82696D033F1771342E7E4D8137F7BDBF54DF3F0678C7CD`;
- quaternion order: `x,y,z,w`;
- `Quaternion::toMatrix` uses the non-unit Shoemake scale `2 / normSquared`;
- `parse_transformation` converts the supplied quaternion to a matrix and transposes it;
- the software renderer composes translation after that rotation;
- `vripsplit` independently corroborates the convention by negating `w` before calling `plyxform` for world-space bounds.

The fixed mathematical contract is therefore:

```text
worldPoint = transpose(ShoemakeQuaternionMatrix(q)) * sourcePoint + translation
```

The `.conf` camera row is retained as provenance but is not applied to `bmesh` points. Input float32 values are decoded exactly and calculations use float64. The source README does not state the coordinate unit, so reports deliberately use `source-unspecified`; the `1.6 mm` name describes the drill, not a proven file-unit declaration.

## Fixed Source Identity

| Item | Bytes | SHA-256 |
| --- | ---: | --- |
| Stanford Drill archive | 555,087 | `2DA2ACABA36903A9893C920C11506E1BD01C531BDF738D037BC505B313DF5E1B` |
| Transform configuration | 1,261 | `95686B12D5CFF5CC58599D211D81047B92F26186F42EB4AC57CF833FA0A2352F` |
| Twelve scans | 3,275,412 total | Each file identity is fixed in `scripts/verify-stanford-drill-transforms.py` and emitted in both reports. |

Each scan must have `512 x 400 = 204,800` range-grid cells. The verifier rejects unexpected PLY elements/properties, non-finite points, multiple/duplicate/out-of-range grid indices, missing vertex coverage, truncated data, trailing bytes, wrong file size, or wrong SHA-256.

## Independent Reference

`scripts/verify-stanford-drill-transforms.py` uses only the Python standard library. Its six-case self-test covers identity, inverse quarter-turn plus translation, non-unit quaternion equivalence, valid big-endian range-grid parsing, trailing-payload rejection, and CloudCompare-style little-endian point-only PLY parsing.

The fixed run produces a deterministic `41,166`-byte JSON reference with SHA-256 `D13D18155BBA2D5CE8FA4E7BE4109C6FF6FCEE1A4A983D55C817A903F3BF43EA`.

| Aggregate metric | Value |
| --- | --- |
| Points | `50,643` |
| Bounds minimum | `(-0.12745644317831811, 0.075360091190645795, -0.063152355909667834)` |
| Bounds maximum | `(0.06126712518165494, 0.1989980581049299, 0.030363746263361243)` |
| Centroid | `(0.0011857907018215663, 0.11727645377500152, 0.01408345056585864)` |
| Coordinate sum | `(60.051998512349584, 5939.2314485274019, 713.22818700677908)` |
| Ordered weighted sum | `(-2056104.5537065896, 151546657.15834346, 17573917.127429131)` |

The published quaternion angles span `0..179.7624571566929` degrees. All twelve generated matrices have determinant within `6.7e-16` of `1` and maximum row-orthogonality error at most `6.7e-16`.

## CloudCompare Cross-Check

The same CloudCompare `2.13.2` executable used by the NIST and C3D baselines was reused without ICP, center matching, scale adjustment, subsampling, or global shift. The Python verifier launches all twelve applications directly, requires a successful exit and output for each scan, and then checks the exported points:

- executable: `3,611,824` bytes, SHA-256 `4695ABC490711ABD824714157D5258D3C16646AB107A86422BFD6CDA17D7CAF1`;
- command contract: `-APPLY_TRANS` with four matrix rows and translation in the fourth column, as documented by the [CloudCompare command-line reference](https://www.cloudcompare.org/doc/wiki/index.php?title=Command_line_mode);
- input: each original Stanford binary-big-endian range-grid PLY;
- output: ordered binary-little-endian float32 point-only PLY;
- result: `12/12` scans and `50,643/50,643` points pass;
- maximum ordered coordinate difference from the Python float64 reference: `3.0913966692081019e-8`;
- aggregate bounds maximum difference: `9.2033719811812631e-9`;
- aggregate centroid maximum difference: `8.8955852406424896e-10`.
- controlled output tamper: `0.0010000008982413121` observed difference, reported and rejected with exit `5`.

This provides an external transform-application implementation. The tolerance covers CloudCompare's float32 PLY output rounding; it is not physical uncertainty.

## Runner Parity And Failure Evidence

`StanfordTransformParityVerification` independently re-parses the `.conf`, hashes and parses all scans, applies the transform, and compares source/transformed checkpoints plus source/transformed and aggregate statistics.

| Check | Result |
| --- | --- |
| Full solution build | Pass, `0` warnings and `0` errors |
| Synthetic transform checks | Pass, `4/4` |
| External CloudCompare transform | Pass, `12/12` scans and `50,643/50,643` points; maximum delta `3.0913966692081019e-8`; controlled `0.001` output tamper rejected |
| Point checkpoints | Pass, `36/36`, maximum coordinate delta `0`, tolerance `1e-12` |
| Per-scan statistics | Pass, maximum component delta `0` |
| Aggregate statistics | Pass, maximum component delta `0` |
| Controlled tamper | Pass: a `0.001` point-coordinate change is reported and rejected with exit `5` |
| Mesh-deviation regression | Pass, `18/18` |
| Fixed Viewer/Shell matrix | Pass, `128/128` |

The point tolerance is `1e-12`, sum tolerance is `1e-9`, and ordered weighted-sum tolerance is `1e-6`. These tolerances compare two float64 implementations; they are not physical uncertainty limits.

## Commands

```powershell
py -3 scripts\verify-stanford-drill-transforms.py --self-test

$root = 'artifacts\research_samples\stanford_drill\drill\data'
$out = 'artifacts\stanford_transform_reliability_20260715'
$cc = 'artifacts\research_tools\cloudcompare_2.13.2\portable\CloudCompare_v2.13.2.preview_bin_x64\CloudCompare.exe'
py -3 scripts\verify-stanford-drill-transforms.py `
  --conf "$root\drill_1.6mm_cyb.conf" `
  --reference "$out\python_reference.json" `
  --report "$out\python_reference_report.txt" `
  --cloudcompare-exe $cc `
  --cloudcompare-matrix-dir "$out\cloudcompare_matrices" `
  --cloudcompare-output-dir "$out\cloudcompare_outputs" `
  --cloudcompare-report "$out\cloudcompare_parity.txt"

dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj `
  -c Debug --no-build -- `
  --stanford-transform-parity "$root\drill_1.6mm_cyb.conf" `
  --transform-reference "$out\python_reference.json" `
  --report "$out\runner_parity.txt"
```

Evidence is under ignored `artifacts/stanford_transform_reliability_20260715`:

- `vrippack_source_audit.txt`;
- `python_reference.json`;
- `python_reference_report.txt`;
- `runner_parity.txt`;
- `runner_tampered_rejection.txt`;
- `cloudcompare_matrices/*.matrix.txt`;
- `cloudcompare_outputs/*.cloudcompare.ply` and `*.log`;
- `cloudcompare_parity.txt`;
- `cloudcompare_tampered_rejection.txt`;
- `mesh_deviation_golden.txt`;
- `matrix/matrix_smoke_summary_after.txt`.

## Remaining Limits

- Stanford Drill is a transform-truth research sample, not CAD nominal/actual inspection evidence.
- The transform is supplied by Stanford; this gate proves application of that transform, not registration recovery from unaligned scans.
- File units, calibration uncertainty, and pose uncertainty are not supplied and are not inferred.
- Reconstruction PLYs are derived surfaces and are excluded from point-level expected output.
- The reference is local-only because the source terms block product distribution. This gate is not a public or Windows-CI portability claim.

## Next Gate

The Phase 2 second-pair and supplied-transform gates now pass: the second physical NIST instance has external/non-visual plus visible Viewer/Runner evidence, and this separate published non-identity transform has point/aggregate execution evidence. Phase 2 remains open because the difficult-geometry controlled-outcome matrix and accepted registration path are incomplete.

Audit existing geometry/error goldens and add only missing controlled cases. Keep this supplied-transform claim separate from registration recovery, and do not integrate a registration runtime until its distribution and acceptance prerequisites are resolved.
