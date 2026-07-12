# OpenVisionLab 3D Map Fidelity Validation

Updated: 2026-07-11

## Decision

The current C3D map is verified in the OpenVisionLab **viewer display frame**. It is not yet verified in calibrated physical units and must not be described as metrology-grade.

| Question | Current result | Evidence |
| --- | --- | --- |
| Is the C3D grid read with the correct dimensions and row/column order? | Pass for the fixed Thickness sample. | C3D and reference PNG are both `1301 x 1967`; identity orientation has invalid-mask IoU `0.954954451` and raw-height/PNG-green correlation `0.965939`. |
| Are exported Viewer points identical to the points rendered by OpenVisionLab? | Pass. | The sampled compatibility mesh roundtrips `66,212` points with zero .NET XYZ/RGB error. The point-only full-resolution audit roundtrips all `1,653,562` valid points with zero .NET error. |
| Does a separate implementation calculate the same map? | Pass in the Viewer display frame. | A dependency-free Python verifier independently reads C3D and PLY, recalculates every full-resolution XYZ/RGB value, and reports maximum XYZ error `2.36938477e-7` with RGB error `0`. |
| Does another proprietary viewer show the same major geometry? | Pass for independent shape rendering, not metrology. | Microsoft 3D Viewer loads the compatibility-mesh PLY and shows the same long gaps, outer boundary, lower protrusion, and high spikes. Camera, shading, and color behavior differ. The installed app also identifies itself as no longer supported. |
| Can an external tool re-save be checked numerically? | Pass for local Open3D point-cloud re-save, not metrology. | Open3D 0.19.0 read and re-saved the sampled PLY as ASCII point cloud: `66,212` vertices and RGB values are preserved, faces drop from `128,516` to `0` because faces are visualization-only, strict `1e-6` comparison fails with max coordinate drift `5e-6`, and external ASCII re-save tolerance `1e-5` passes. CloudCompare/MeshLab CLI are not installed in the current workspace. |
| Are the X/Y/Z values calibrated physical coordinates? | Unverified. | The C3D layout and scale are inferred; pixel pitch, height scale/offset, units, axis convention, and calibration provenance are unavailable. |
| Does the Viewer match ZEISS/PolyWorks measurement results? | Not tested. | No licensed commercial metrology application or calibrated reference dataset is available in the workspace. |

## What Equal Means

Screenshots alone cannot establish 3D-map equality. Validation is separated into four levels:

1. **Source-grid fidelity**: dimensions, valid/invalid cells, row/column orientation, and raw values.
2. **Display-frame fidelity**: deterministic conversion from source cells to OpenVisionLab XYZ and RGB values.
3. **Independent rendering parity**: a neutral file displays the same major geometry in another viewer.
4. **Physical/metrology fidelity**: calibrated units, transforms, uncertainty, and measurements agree with an independent reference system.

Levels 1-3 have evidence for the fixed sample, including a full-resolution cross-runtime comparison. Level 4 remains blocked.

## Current Mapping Contract

The inferred source layout is:

```text
int32 width
int32 height
float32 samples[width * height]
```

Finite non-zero samples are mapped to a right-handed, Y-up display frame:

```text
horizontalScale = 10 / max(1, width - 1, height - 1)
X = (column - (width - 1) / 2) * horizontalScale
Y = (rawHeight - meanRawHeight) * 0.0006
Z = (row - (height - 1) / 2) * horizontalScale
```

`10` and `0.0006` are viewer normalization values, not physical calibration. The model unit is therefore `unitless` and the source height unit remains `raw-height`.

## Fixed-Sample Evidence

Source identity:

```text
C3D SHA256: 79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C
PNG SHA256: 97C8CAE2D39746398BEDE57FC66FD552AC95910287FA48C9B13968E4175A31A8
Grid: 1301 x 1967
Valid samples: 1,653,562
Zero samples: 905,505
```

The local PNG comparison used every fifth row and column for color correlation. Full masks were used for the reported invalid-cell overlap:

| Candidate orientation | Invalid-mask IoU | Raw/green correlation |
| --- | ---: | ---: |
| Identity | `0.954954451` | `0.965939` |
| Flip X | `0.319198343` | `0.032929` |
| Flip Y | `0.609604016` | `0.496212` |
| Flip X and Y | `0.282384706` | `0.099286` |

All C3D invalid cells are black in the reference PNG. The PNG contains additional black pixels, so it is supporting orientation and topology evidence, not a lossless encoding of every raw value.

Runner evidence:

```text
Golden cases: Pass (10/10)
Actual sampled points: 66,212
Compatibility faces: 128,516
Maximum coordinate roundtrip error: 0
Maximum RGB channel roundtrip error: 0
Viewer bounds min: (-2.085453,-1.187284,-5.000000)
Viewer bounds max: (3.255341,2.103985,4.994914)
```

Full-resolution evidence:

```text
Point-only PLY points: 1,653,562
Faces: 0
PLY bytes: 72,712,856
.NET maximum coordinate roundtrip error: 0
.NET maximum RGB channel error: 0
Python maximum independently calculated coordinate error: 2.36938477e-7
Python maximum RGB channel error: 0
Acceptance tolerance: 1e-6 Viewer units
Controlled mismatch: Pass; a wrong 50,000-point budget is rejected because 66,212 declared vertices do not match 25,892 expected vertices
```

Open3D external runtime evidence:

```text
Open3D version: 0.19.0
Input: artifacts/map_fidelity/openvision_c3d_detailed.ply
Output: artifacts/map_fidelity/open3d_resaved_c3d_detailed.ply
Read points: 66,212
Has colors: true
Open3D re-saved points: 66,212
Open3D re-saved faces: 0
Strict 1e-6 comparison: Fail; maximum coordinate error 5e-6
External ASCII 1e-5 comparison: Pass; maximum coordinate error 5e-6, maximum RGB channel error 0
```

The exported PLY vertices are exact OpenVisionLab rendered samples. Faces exist only because the installed Microsoft 3D Viewer rejected point-only PLY. Faces connect sampled neighbors for visualization and must never be used as inspection or measurement geometry because downsampling can bridge unsampled cells.

Evidence artifacts:

- `artifacts/map_fidelity/c3d_map_fidelity_golden.txt`
- `artifacts/map_fidelity/c3d_map_fidelity_actual.txt`
- `artifacts/map_fidelity/openvision_c3d_detailed.ply`
- `artifacts/map_fidelity/openvision_c3d_detailed.png`
- `artifacts/map_fidelity/c3d_map_full_resolution.ply`
- `artifacts/map_fidelity/c3d_map_full_resolution_dotnet.txt`
- `artifacts/map_fidelity/c3d_map_full_resolution_python.txt`
- `artifacts/map_fidelity/openvision_c3d_detailed_signature.txt`
- `artifacts/map_fidelity/c3d_map_full_resolution_signature.txt`
- `artifacts/map_fidelity/open3d_resaved_c3d_detailed.ply`
- `artifacts/map_fidelity/open3d_resaved_c3d_detailed_signature.txt`
- `artifacts/map_fidelity/open3d_resaved_c3d_detailed_compare_strict_1e6.txt`
- `artifacts/map_fidelity/open3d_resaved_c3d_detailed_compare_tolerance_1e5.txt`
- `artifacts/map_fidelity/microsoft_3d_viewer_c3d_ply_final.png` from the final PLY
- `artifacts/map_fidelity/microsoft_3d_viewer_c3d_ply_aligned.png`
- `artifacts/map_fidelity/microsoft_3d_viewer_window.png` for the controlled point-only PLY rejection

## Repeatable Commands

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug

dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-c3d-map-fidelity --report artifacts\map_fidelity\c3d_map_fidelity_golden.txt

dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --c3d-map-probe 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\c3d_map_fidelity_actual.txt --max-sampled-points 140000

python scripts\verify-c3d-map-ply.py --source 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\c3d_map_fidelity_python.txt --max-sampled-points 140000

dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --c3d-map-probe 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity\c3d_map_full_resolution.ply --report artifacts\map_fidelity\c3d_map_full_resolution_dotnet.txt --max-sampled-points 2147483647 --point-only

python scripts\verify-c3d-map-ply.py --source 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity\c3d_map_full_resolution.ply --report artifacts\map_fidelity\c3d_map_full_resolution_python.txt --max-sampled-points 2147483647

python scripts\ply-coordinate-signature.py --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\openvision_c3d_detailed_signature.txt

python scripts\ply-coordinate-signature.py --ply artifacts\map_fidelity\c3d_map_full_resolution.ply --report artifacts\map_fidelity\c3d_map_full_resolution_signature.txt

python scripts\ply-coordinate-signature.py --reference artifacts\map_fidelity\openvision_c3d_detailed.ply --candidate artifacts\map_fidelity\external_resaved_c3d_detailed.ply --report artifacts\map_fidelity\external_resaved_c3d_detailed_compare.txt --ignore-faces --tolerance 0.00001

dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\map_fidelity\openvision_c3d_detailed.png --smoke-c3d thickness --smoke-density Detailed --smoke-contracts artifacts\map_fidelity\openvision_c3d_detailed.txt
```

CI runs the golden cases, sampled .NET roundtrip, independent Python recalculation, and sampled PLY coordinate signature generation. The 72 MB full-resolution PLY remains an explicit local audit and is not uploaded on every CI run.

## Commercial Reliability Lessons

- ZEISS INSPECT emphasizes traceable, repeatable parametric inspection steps and nominal/actual visualization. OpenVisionLab should retain source identity, mapping parameters, recipe inputs, and evidence for every result: https://www.zeiss.com/metrology/us/software/zeiss-inspect.html
- PolyWorks Inspector emphasizes explicit alignment, visual feedback, reusable projects, point-cloud input, and validated mathematical behavior. OpenVisionLab must keep alignment and units explicit and avoid accuracy claims that have not been independently validated: https://www.polyworks.com/en-us/products/polyworks-inspector
- CloudCompare documents cloud-to-cloud distance computation and global-shift handling. It is a suitable independent numerical cross-check for exported vertices when it is available: https://www.cloudcompare.org/doc/wiki/index.php?title=Cloud-to-Cloud_Distance
- Open3D supports PLY point-cloud I/O and is another suitable independent parser for future automated interchange checks: https://www.open3d.org/docs/latest/tutorial/geometry/file_io.html

## External Verification Protocol

Use this protocol when CloudCompare, ZEISS INSPECT, PolyWorks, or another trusted tool is available:

1. Generate the PLY with a fixed sample budget and retain the C3D/PLY SHA256 values.
2. Import without automatic alignment, unit conversion, smoothing, hole filling, or global shift.
3. Confirm point count and XYZ bounds against the Runner report.
4. Re-save or export the imported cloud as ASCII PLY and compare it with `scripts\ply-coordinate-signature.py` when vertex order is preserved. Use `--ignore-faces` only when the external tool imports the PLY as a point cloud and drops visualization-only faces.
5. Compare PLY vertices to an independently imported source/reference cloud. Record maximum, mean, RMS, and percentile distance; do not accept screenshots as the only evidence.
6. Use PLY vertices only. Ignore compatibility faces for measurement.
7. Record application name/version, import settings, coordinate transform, units, and exported comparison report.
8. Capture matching top, front, side, and perspective views only after the numerical comparison passes.

Viewer-frame acceptance is maximum component error `<= 1e-6` Viewer units for the internal .NET/Python mapping of the same sampled vertices. External ASCII re-save acceptance is `<= 1e-5` Viewer units when the external writer rounds coordinate text, with RGB channel error still required to be `0`. Physical acceptance criteria cannot be defined until calibration metadata is available.

## Trust Gates

| Gate | Status | Exit condition |
| --- | --- | --- |
| T0 Parser and mapping golden cases | Passed | Known grid coordinates, colors, stride, direct-cell access, single-cell finite mapping, and controlled error cases pass. |
| T1 Local reference orientation | Passed for fixed sample | Dimensions, invalid mask, and height-color correlation identify the unflipped orientation. |
| T2 Neutral interchange roundtrip | Passed | Sampled and full-resolution PLY point counts, XYZ, and RGB match the .NET Viewer mapping; the independent Python calculation is within `1e-6` Viewer units. |
| T3 Independent renderer/interchange | Passed for shape and Open3D point-cloud re-save | Microsoft 3D Viewer displays matching major geometry. Open3D 0.19.0 preserves sampled PLY point count and RGB and stays within `5e-6` Viewer units after ASCII re-save. CloudCompare/ZEISS/PolyWorks numerical comparison remains pending. |
| T4 Physical calibration | Blocked | Obtain C3D specification or explicit X/Y pitch, height scale/offset, units, axis orientation, and calibration provenance. |
| T5 Independent metrology comparison | Blocked | Compare a calibrated reference dataset and recorded measurements in a trusted metrology application. |

## Next Required Work

Before treating C3D measurements as physical dimensions, add a recipe-owned `C3DMappingProfile` containing X pitch, Z pitch, height scale, height offset, source/display units, axis directions, and calibration identity. Preserve the current normalization profile only as an explicitly named uncalibrated display profile. After that gate passes, resume Gap/Flush as the next typed inspection slice.
