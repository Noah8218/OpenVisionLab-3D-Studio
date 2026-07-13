# OpenVisionLab 3D Registration Engine Prototype

Checked: 2026-07-13

## Decision

Do not add a registration dependency or PCD loader to the product yet.

Open3D `DemoICPPointClouds` is accepted as a probe-only alignment golden candidate. It is not a calibrated physical dataset, a CAD nominal, or a measured/nominal inspection pair. `PclNET 0.8.3` is rejected as the current runtime engine because its .NET 10 package loads PCD data but exposes no ICP/registration API and adds a large native runtime surface.

The official Open3D `0.19.0` Windows C++ package and a same-tag source build are technically viable separate-process registration candidates, not approved product dependencies. The source configuration now passes both a recovered build and an independent clean single-shot Release build/install and reduces the adjacent probe runtime from `141,536,768` to `58,520,064` bytes. The clean-build probe matches the official binary exactly in all 33 robustness runs and all three current `0 -> 1` DemoICP runs. Both runtimes reject `1 -> 2` because `cloud_bin_2.pcd` contains 771 non-finite normals; the older successful `1 -> 2` metrics predate this guard. A schema-valid 33-component CycloneDX candidate records the direct clean-build evidence, but unresolved Assimp and prebuilt BoringSSL/MKL/VTK provenance keep the full manifest gate open. The 2026-07-13 distribution audit still blocks publication because final notices, Microsoft runtime handling, clean-host evidence, product integration impact, and owner/legal approval have not passed; see `docs/OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`. Viewer/Runner transform parity also remains open.

No downloaded candidate file or prototype dependency is committed to the fixed sample matrix.

## Public Candidate

Source ZIP:

```text
https://github.com/isl-org/open3d_downloads/releases/download/20220301-data/DemoICPPointClouds.zip
ZIP bytes: 10,829,466
ZIP SHA-256: b94e0146c1d48c5edfc11af71b4af39ffca604485668c55a127c3b43203a6bd5
License: CC BY 3.0, as recorded by the Open3D DemoICPPointClouds API documentation
```

Probe location:

```text
artifacts/public-sample-candidates/open3d-demo-icp-20260713/
```

| File | Bytes | Points | SHA-256 |
| --- | ---: | ---: | --- |
| `cloud_bin_0.pcd` | 6,362,965 | 198,835 | `e1e100802c29ef454c6b523084668ee0e2f365ec52eaeebe79ae804c20447b15` |
| `cloud_bin_1.pcd` | 4,410,901 | 137,833 | `a4c3dc0ad7b1279736491b9b2638991d4c808605997be4f9ab174c24a9fa6e52` |
| `cloud_bin_2.pcd` | 6,124,949 | 191,397 | `1e68e194ebc1941f0f29764e4daf89340e69b224d2b80db5efbc1373a17f8b4a` |
| `init.log` | 385 | n/a | `609896dbdd666b7ae0bb7390c52730ca8aca10c2b5886b895acdb36f4a202156` |

All three PCD files are binary PCD v0.7 with XYZ, RGB, normals, and curvature. A standard-library binary field audit finds zero non-finite XYZ values in all three files, zero non-finite normals in `cloud_bin_0.pcd` and `cloud_bin_1.pcd`, and 771 non-finite normals in `cloud_bin_2.pcd`. `init.log` contains rigid transforms for pairs `0/1` and `1/2`.

## Open3D Golden Probe

The local probe used Open3D `0.19.0`, a correspondence threshold of `0.02`, point-to-plane ICP, and 30 iterations. For Open3D source-to-target evaluation, the inverse of each logged matrix produced the expected overlap.

| Pair | Initial fitness | Initial RMSE | Refined fitness | Refined RMSE |
| --- | ---: | ---: | ---: | ---: |
| `0 -> 1` | 0.420152388 | 0.010373945 | 0.621032514 | 0.006565227 |
| `1 -> 2` | 0.613851545 | 0.010105511 | 0.745750292 | 0.007246448 |

The table records the earlier exploratory probe before non-finite-normal rejection was added. The current hardened probe reproduces `0 -> 1` but rejects `1 -> 2` before registration because its target contains 771 non-finite normals. The logged rotation blocks are rigid within approximately `1e-5` orthogonality error and determinant error. These values are useful as an alignment regression baseline only. They do not establish dimensional accuracy.

## Official Open3D C++ Prototype

Probe location:

```text
artifacts/dependency-candidates/open3d-cpp-0.19.0/
```

Package evidence:

```text
Release: Open3D 0.19.0 official Windows AMD64 development package
ZIP bytes: 69,318,048
ZIP SHA-256: a39a205e4d9db029e2a98313acae20d8cc8e3f60f89437ee4f3f00cdc231dc87
Extracted files: 1,095
Extracted bytes: 223,575,567
Build: Visual Studio 2022 / MSVC 19.44 / Release x64
```

The artifact-only CLI reads source and target PCD files plus a selected transform-log record, inverts the logged matrix for Open3D source-to-target evaluation, runs point-to-plane ICP with threshold `0.02` and 30 iterations, and writes point counts, initial/refined correspondence counts, fitness, RMSE, elapsed time, and the refined transform as JSON.

| Pair | Source / target points | Initial fitness / RMSE | Refined fitness / RMSE | Observed ICP time |
| --- | ---: | ---: | ---: | ---: |
| `0 -> 1` | `198,835 / 137,833` | `0.420152387658 / 0.010373944820` | `0.621032514396 / 0.006565226689` | `654-891 ms` |
| `1 -> 2` | `137,833 / 191,397` | `0.613851544986 / 0.010105510712` | `0.745750292020 / 0.007246447955` | `389 ms` |

Three current `0 -> 1` runs produced identical fitness and RMSE values. Missing PCD, missing log-record, and non-finite-normal cases return process exit code `1` without a report. The `1 -> 2` row above is retained as historical pre-hardening output and is not a current pass.

Minimal adjacent runtime files:

| File | Bytes |
| --- | ---: |
| `open3d-registration-probe.exe` | 70,656 |
| `Open3D.dll` | 141,146,112 |
| `tbb12.dll` | 320,000 |
| **Total** | **141,536,768** |

`dumpbin` also reports Microsoft C++ runtime and OpenMP dependencies. The monolithic `Open3D.dll` imports Windows OpenGL, UI, media, USB, and networking system libraries even though this probe is non-GUI. The Open3D source is MIT-licensed, but the downloaded development ZIP did not contain a top-level license file in the inspected package tree; product distribution therefore requires an explicit Open3D and bundled third-party notice audit rather than relying on the probe package alone.

## Same-Tag Source Build

The fixed non-GUI configuration for source commit `1e7b17438687a0b0c1e5a7187321ac7044afe275` first completed Release build and install after five interrupted 0-byte Embree object files were removed and regenerated. No Open3D source file was modified. A subsequent single-process incremental `INSTALL` returned exit code `0`, reported all install outputs up to date, and found zero 0-byte Embree compile objects.

```text
Install files: 873
Install bytes: 88,977,375
Open3D.dll SHA-256: 4d8cd9ea3bb1310851f8a942fb2f21bb8313bf182e29e993e856b0a7ad842d5e
Install manifest SHA-256: c6006e8955a6f226c936ba40b1d335a2b7731661be2bc646343cff72833e7b7a
CMakeCache SHA-256: d73019329d0b00ee3dce00b6712d9db4ee6c01e443044a3780db02783bedf952
```

Source-built adjacent runtime:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `open3d-registration-probe.exe` | 70,656 | `5214ee9e4b9664e34f6e28e29673ba64dcc7caf21cf913022efa1470ebbc8d3e` |
| `Open3D.dll` | 58,120,704 | `4d8cd9ea3bb1310851f8a942fb2f21bb8313bf182e29e993e856b0a7ad842d5e` |
| `tbb12.dll` | 328,704 | `4014138f76a813cda99570c238b7e34489969782c8b5896f851c0ec0ce877388` |
| **Total** | **58,520,064** | n/a |

`dumpbin /DEPENDENTS` identifies `tbb12.dll` as the only adjacent non-system DLL; Microsoft C++ and OpenMP runtimes remain prerequisites. The recovered build showed the missing-NASM performance warning, a libzmq `/LTCG` recommendation, and Open3D DLL export `LNK4286` warnings. The independent clean run below preserves their complete warning inventory and confirms that they did not fail the build.

The source-built probe has exact official-binary parity excluding elapsed time for all 33 robustness runs, and each of the 11 cases remains deterministic. For current DemoICP evidence, pair `0 -> 1` has exact parity in 3/3 runs; both runtimes return controlled exit code `1` with no report for pair `1 -> 2` and the same `Target point cloud contains a non-finite normal.` diagnostic.

An independent clean directory then configured once and completed one uninterrupted, single-process Release `INSTALL` invocation:

```text
Configure: 46.119 seconds, exit 0
Build and install: 3,387.699 seconds, exit 0
Install files: 873
Install bytes: 88,977,375
Manifest roundtrip missing / mismatch: 0 / 0
Actual build error lines: 0
Compiler / linker warning lines: 56 / 152
Build / configure CMake warning headers: 29 / 5
Embree zero-byte compile objects: 0
```

The only zero-byte `*.obj` path is Assimp's intentional `test/models/invalid/empty.obj` input fixture, not a compiler output. All 873 clean paths and sizes match the recovered install; 871 hashes also match. The rebuilt `Open3D.dll` and `tbb12.dll` have different PE timestamps and SHA-256 values, but their byte sizes, export ordinal/name contracts, and dependency lists match. This is contract and behavior reproducibility evidence, not a byte-for-byte reproducible-build claim.

| Clean runtime file | Bytes | SHA-256 |
| --- | ---: | --- |
| `open3d-registration-probe.exe` | 70,656 | `55b88b951070773df103dc98ec8641b43e7a80205228c1426c46a2155b13bc8f` |
| `Open3D.dll` | 58,120,704 | `53062a532951e85612a724dad3908f0587a247874e93248ddfef6f8ecd150712` |
| `tbb12.dll` | 328,704 | `10e38577698271acbee58b77d1b936b17f85e6f1bab282feea779d70787d4e9d` |
| **Total** | **58,520,064** | n/a |

The clean probe again matched official output in 33/33 robustness runs, remained deterministic in 11/11 cases, and reproduced the same 5/11 predeclared outcomes. Current DemoICP `0 -> 1` matched official output in 3/3 runs; `1 -> 2` produced the same controlled non-finite-normal rejection from both engines.

Ignored evidence:

```text
artifacts/o3d/install-summary.txt
artifacts/o3d/install-manifest.json
artifacts/o3d/open3d-dependents.txt
artifacts/o3d/incremental-install-verification.log
artifacts/o3d/probe-results/robustness-source-build/source-build-parity-summary.json
artifacts/o3d/probe-results/demo-current/pair-0-to-1-parity.json
artifacts/o3d/probe-results/demo-current/pair-1-to-2-controlled-failure-parity.json
artifacts/o3d/probe-results/demo-current/pcd-nonfinite-audit.txt
artifacts/o3d-clean/build-summary.txt
artifacts/o3d-clean/build-release-install.log
artifacts/o3d-clean/build-log-audit.txt
artifacts/o3d-clean/install-manifest.json
artifacts/o3d-clean/install-manifest-verification.txt
artifacts/o3d-clean/binary-contract-comparison.json
artifacts/o3d-clean/probe-results/robustness/clean-build-parity-overall.json
artifacts/o3d-clean/probe-results/demo-current/pair-0-to-1-clean-parity.json
artifacts/o3d-clean/probe-results/demo-current/pair-1-to-2-clean-controlled-failure-parity.json
```

## Synthetic Known-Transform Golden

The artifact-only generator uses only Python standard-library math and writes a deterministic asymmetric curved surface as ASCII PCD with analytic normals. Each case contains `1,517` source points, applies a known transform to create the target, and supplies an initial transform with approximately `0.0041-0.0054` translation error and `0.71-0.88` degrees rotation error.

| Case | Repeated runs | Checks per run | Refined translation error | Refined rotation error | Refined matrix max error |
| --- | ---: | ---: | ---: | ---: | ---: |
| Translation | 3 | `8/8` | `0` | `0 deg` | `0` |
| Rotation | 3 | `8/8` | `0` | `0 deg` | `5.97e-13` |
| Rigid rotation + translation | 3 | `8/8` | `0` | `2.78e-5 deg` | `4.41e-13` |

All nine runs reported fitness `1.0`, RMSE `0`, identical stable metrics and transforms within each case, and improvement over the perturbed initial transform. The verifier independently recalculates translation, rotation-angle, and matrix errors from the CLI JSON and known transform.

Controlled invalid cases:

| Input | Expected exit | Report created | Result |
| --- | ---: | --- | --- |
| Empty PCD | `1` | No | Pass |
| Two-point PCD | `1` | No | Pass |
| PCD with non-finite coordinate | `1` | No | Pass |

This closes the prototype's analytic known-transform and basic input-validation gate. Robustness is characterized separately below; calibrated units and production scan variation remain unproven.

## Deterministic Robustness Characterization

The artifact-only generator reused the `1,517`-point asymmetric curved surface and known rigid transform. Noise, outlier, overlap, combined-degradation, and initial-transform conditions plus their acceptance thresholds were written before execution and were not relaxed after observing results. Each of the 11 cases ran three times. Stable output fields, including correspondence counts, fitness, RMSE, and transforms, were identical in all 33 CLI runs.

| Case | Predeclared expectation | Observed acceptance | Correspondences / fitness | RMSE | Translation / rotation error | Failed boundary |
| --- | --- | --- | ---: | ---: | ---: | --- |
| Clean | Accept | Accept | `1,517 / 1.000000` | `0` | `0 / 0 deg` | None |
| Normal noise `sigma=0.0005` | Accept | Accept | `1,517 / 1.000000` | `0.000919` | `0.000745 / 0.0564 deg` | None |
| Normal noise `sigma=0.002` | Accept | Reject | `1,517 / 1.000000` | `0.003244` | `0.002454 / 0.1621 deg` | Translation error `> 0.002` |
| Outliers `5%` | Accept | Accept | `1,517 / 0.952291` | `0` | `0 / 0 deg` | None |
| Outliers `15%` | Accept | Accept | `1,517 / 0.869341` | `0` | `0 / 0 deg` | None |
| Target overlap `80%` | Accept | Reject | `1,258 / 0.829268` | `0.001750` | `0.000081 / 0.00491 deg` | RMSE `> 0.0005` |
| Target overlap `60%` | Accept | Reject | `962 / 0.634146` | `0.002034` | `0.000080 / 0.00586 deg` | RMSE `> 0.001` |
| Target overlap `40%` | Accept | Reject | `703 / 0.463414` | `0.005118` | `0.000697 / 0.05789 deg` | RMSE `> 0.002` |
| Noise `sigma=0.001`, outliers `5%`, overlap `60%` | Accept | Reject | `999 / 0.627119` | `0.004688` | `0.001794 / 0.1682 deg` | RMSE `> 0.003` |
| Initial error `0.090843`, `22.3307 deg` | Reject | Accept | `1,517 / 1.000000` | `0` | `0 / 0 deg` | Negative expectation was too conservative |
| Initial error `0.464585`, `82.8309 deg` | Reject | Reject | `0 / 0` | `0` | `0.464585 / 82.8309 deg` | No correspondences, fitness, and transform limits |

Five cases met the predeclared acceptance criteria, five cases produced the predeclared outcome, and all 11 were deterministic. These are different claims: partial-overlap and combined cases recovered the known transform within their translation and rotation thresholds but exceeded the predeclared RMSE limit. The higher-noise case missed only its translation threshold. The medium initial error converged exactly despite being expected to reject.

The distant-initial case is the critical controlled-failure finding. Open3D reports inlier RMSE `0` when no correspondences exist, so RMSE alone can misclassify a complete registration failure as perfect. Any future product acceptance contract must require a minimum correspondence count and fitness before evaluating RMSE, must report `NoCorrespondences` explicitly, and must keep transform plausibility and scenario-specific residual limits separate. Real measured/nominal acceptance thresholds remain blocked until representative paired data and physical units are available.

Local evidence is under `artifacts/dependency-candidates/open3d-cpp-0.19.0/probe/robustness_v2/`; `robustness_summary.json` records the 11-case result. These ignored artifacts characterize the candidate and do not add it to the product or release bundle.

## PclNET Prototype

Probe location:

```text
artifacts/dependency-candidates/pclnet-probe/
```

Observed result:

```text
Target framework: net10.0
Package: PclNET 0.8.3
PCD load: Pass, 198,835 points
Bounds: (0.55078125, 0.83203125, 0.55859375) to (3.9607717990875244, 2.4249000549316406, 2.5536561012268066)
Public ICP/registration members: 0
Published files: 61
Published bytes: 62,852,741
```

The package includes PCL, VTK, Boost, and related native DLLs. Loading worked on the current Windows/.NET 10 machine, but the tested managed API cannot execute the registration slice that would justify this deployment cost.

## Adoption Gate

A registration engine may be proposed again only when a local prototype proves all of the following:

1. Point-to-plane ICP or an equivalent inspectable rigid-registration method is exposed through a maintained Windows/.NET-compatible boundary.
2. PCD or another chosen interchange format loads the three candidate clouds without losing point count or coordinates.
3. Synthetic known-transform cases and the Open3D candidate both have deterministic correspondence-count, fitness, RMSE, transform, and controlled-failure checks; RMSE is never accepted without a minimum correspondence and fitness guard.
4. The dependency bundle, native runtime requirements, license, and Viewer DLL host impact are explicit.
5. Viewer and Runner consume the same transform and metrics without using render-density samples as inspection input.
6. A visible workflow is placed in the workbench layout before UI implementation and follows View -> ViewModel -> Model.

Until that gate passes, keep registration external to the product and do not describe the Open3D fragments as nominal/actual inspection data.

Current gate result:

| Gate | Status | Evidence / missing work |
| --- | --- | --- |
| Maintained Windows boundary with inspectable rigid registration | Pass for prototype | Official Open3D C++ `0.19.0`, CMake/MSVC, point-to-plane ICP |
| Candidate point-cloud loading without count loss | Partial | All three point counts are recorded, but the hardened probe rejects `cloud_bin_2.pcd` because 771 source normals are non-finite; `0 -> 1` remains the current valid pair |
| Deterministic candidate metrics and controlled failures | Pass for prototype | Three analytic transform cases pass 9 deterministic runs and three invalid-input checks |
| Robustness characterization and acceptance policy | Partial | 11 cases x 3 runs are deterministic; clean, low-noise, and up-to-15%-outlier cases pass, while high-noise and partial/combined cases miss predeclared limits; a distant initial transform proves zero-correspondence RMSE `0` must be rejected explicitly |
| Runtime, license, and host impact explicit | Partial; blocked for distribution | Same-tag recovered and independent clean builds, 873-file hash manifests, 58,520,064-byte runtime, export/dependency contract comparison, 33/33 official parity, and a schema-valid 33-component direct-evidence CycloneDX candidate pass; unresolved transitive provenance, final notices, VC/OpenMP clean-host evidence, product integration impact, and owner/legal approval remain open |
| Viewer/Runner transform and metric parity | Not started | No product integration was added |
| Workbench layout plus View -> ViewModel -> Model | Not started | Required only if the product workflow is approved and unblocked |

## Sources Checked

- Open3D dataset documentation: https://www.open3d.org/docs/latest/tutorial/data/index.html
- Open3D DemoICPPointClouds API and license: https://www.open3d.org/docs/0.19.0/cpp_api/classopen3d_1_1data_1_1_demo_i_c_p_point_clouds.html
- Open3D ICP tutorial and source ZIP: https://www.open3d.org/docs/release/tutorial/pipelines/icp_registration.html
- Open3D C++ integration boundary: https://www.open3d.org/docs/latest/cpp_project.html
- Open3D 0.19.0 release and Windows development package: https://github.com/isl-org/Open3D/releases/tag/v0.19.0
- Open3D 0.19.0 MIT license: https://github.com/isl-org/Open3D/blob/v0.19.0/LICENSE
- Open3D distribution audit: `docs/OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`
- PCL upstream project: https://github.com/PointCloudLibrary/pcl
- PclNET 0.8.3 package: https://www.nuget.org/packages/PclNET/0.8.3
