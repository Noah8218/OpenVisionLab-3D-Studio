# OpenVisionLab 3D Registration Engine Prototype

Checked: 2026-07-13

## Decision

Do not add a registration dependency or PCD loader to the product yet.

Open3D `DemoICPPointClouds` is accepted as a probe-only alignment golden candidate. It is not a calibrated physical dataset, a CAD nominal, or a measured/nominal inspection pair. `PclNET 0.8.3` is rejected as the current runtime engine because its .NET 10 package loads PCD data but exposes no ICP/registration API and adds a large native runtime surface.

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

All three PCD files are binary PCD v0.7 with XYZ, RGB, normals, and curvature. `init.log` contains rigid transforms for pairs `0/1` and `1/2`.

## Open3D Golden Probe

The local probe used Open3D `0.19.0`, a correspondence threshold of `0.02`, point-to-plane ICP, and 30 iterations. For Open3D source-to-target evaluation, the inverse of each logged matrix produced the expected overlap.

| Pair | Initial fitness | Initial RMSE | Refined fitness | Refined RMSE |
| --- | ---: | ---: | ---: | ---: |
| `0 -> 1` | 0.420152388 | 0.010373945 | 0.621032514 | 0.006565227 |
| `1 -> 2` | 0.613851545 | 0.010105511 | 0.745750292 | 0.007246448 |

The logged rotation blocks are rigid within approximately `1e-5` orthogonality error and determinant error. These values are useful as an alignment regression baseline only. They do not establish dimensional accuracy.

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
3. Synthetic known-transform cases and the Open3D candidate both have deterministic fitness, RMSE, transform, and controlled-failure checks.
4. The dependency bundle, native runtime requirements, license, and Viewer DLL host impact are explicit.
5. Viewer and Runner consume the same transform and metrics without using render-density samples as inspection input.
6. A visible workflow is placed in the workbench layout before UI implementation and follows View -> ViewModel -> Model.

Until that gate passes, keep registration external to the product and do not describe the Open3D fragments as nominal/actual inspection data.

## Sources Checked

- Open3D dataset documentation: https://www.open3d.org/docs/latest/tutorial/data/index.html
- Open3D DemoICPPointClouds API and license: https://www.open3d.org/docs/0.19.0/cpp_api/classopen3d_1_1data_1_1_demo_i_c_p_point_clouds.html
- Open3D ICP tutorial and source ZIP: https://www.open3d.org/docs/release/tutorial/pipelines/icp_registration.html
- Open3D C++ integration boundary: https://www.open3d.org/docs/latest/cpp_project.html
- PCL upstream project: https://github.com/PointCloudLibrary/pcl
- PclNET 0.8.3 package: https://www.nuget.org/packages/PclNET/0.8.3
