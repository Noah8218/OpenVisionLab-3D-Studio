# CloudCompare Full-Resolution Viewer-Frame Parity

Checked: 2026-07-13

## Decision

CloudCompare `2.13.2` independently loads and re-saves the current OpenVisionLab full-resolution C3D point cloud without changing point order, point count, axes, global shift, or RGB. All `1,653,562` vertices stay within `5.00000001e-7` Viewer units per coordinate, passing the `1e-6` display-frame tolerance. CloudCompare's own cloud-to-cloud calculation also passes.

This strengthens Viewer trust gate T3 for neutral interchange and derived display-frame measurements. It does not prove physical scale, calibration, uncertainty, certified metrology, or ZEISS/PolyWorks measurement equivalence.

## Tool Identity

```text
Product: CloudCompare stable portable archive
Version: 2.13.2 Kharkiv
Official archive MD5: 55F90E46BD9565907E50B49A2F5F9490
Observed archive MD5: 55F90E46BD9565907E50B49A2F5F9490
Archive SHA-256: B5C534443EB58A80B54CEF23C525D1C92534400E76FE068BB45AD13CB0A1FF29
CloudCompare.exe SHA-256: 4695ABC490711ABD824714157D5258D3C16646AB107A86422BFD6CDA17D7CAF1
Acquisition: https://www.cloudcompare.org/release/CloudCompare_v2.13.2_bin_x64.7z
```

The portable tool remains under ignored `artifacts/dependency-candidates`. It is not an OpenVisionLab dependency or release asset.

## Fixed Inputs

```text
C3D: 3D/Thickness/Ori_20240116_094414.C3D
C3D SHA-256: 79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C
Grid: 1301 x 1967
Valid points: 1,653,562
OpenVisionLab PLY SHA-256: 83AD9A14704A26A3369F77013D69523155117957AB48DA2D7E4856E86BF193D4
CloudCompare re-save SHA-256: 2BF0F29055F859BAB5E60C796E5802E7D218C47988EF99DF0CFB54F7DBCDF4B4
Mapping: column -> X, raw height -> Y, row -> Z
Global shift: (0,0,0)
Physical scale: Unverified
```

CloudCompare was run with auto-save disabled, zero global shift with original coordinates fixed, ASCII PLY output, no timestamp, and no alignment, smoothing, resampling, or filtering.

## Full-Cloud Results

| Check | Result |
| --- | --- |
| Current solution build | Pass; zero warnings and zero errors |
| OpenVisionLab full-resolution PLY | Pass; `1,653,562` vertices, RGB, zero .NET roundtrip error |
| Independent Python mapping | Pass; maximum source-to-PLY coordinate error `2.36938477e-7`, RGB error `0` |
| CloudCompare load | Pass; one cloud with `1,653,562` points |
| CloudCompare global shift | `{0,0,0}` before and after |
| CloudCompare re-save count/order | Pass; `1,653,562` ordered vertices |
| OpenVisionLab vs CloudCompare coordinates | Pass at `1e-6`; maximum component error `5.00000001e-7` |
| RGB | Pass; maximum channel error `0` and identical RGB signature |
| Controlled strict boundary | `1e-7` correctly fails because CloudCompare writes six decimal places |
| CloudCompare C2C in-memory | Mean `4.91657e-7`, standard deviation `1.49337e-7` Viewer units |
| Serialized CloudCompare C2C scalar | Maximum `1e-6` at six-decimal ASCII precision; acceptance passed |

The differing file and quantized XYZ hashes are expected because CloudCompare writes six decimal places and adds its own PLY metadata. Point order, point count, axes, RGB, and accepted coordinate values remain stable.

## Point-Pair Results

The existing recipe-owned cells were reused without display picking:

```text
First:  row 84, column 1190
Second: row 7, column 994
```

| Metric | C3D/OpenVisionLab reference | CloudCompare re-save | Absolute error |
| --- | ---: | ---: | ---: |
| 3D distance | `3.65906378057785` | `3.65906403063256` | `2.50054715e-7` model |
| XZ planar width | `1.07112185411136` | `1.07112188241348` | `2.83021184e-8` model |
| Model Y delta | `3.49877774715424` | `3.498778` | `2.52845764e-7` model |
| Raw-height delta reconstructed from Y | `5831.29598999023` | `5831.29638969495` | `0.000399704716` raw-height |
| Signed elevation angle | `72.9784646009769` | `72.9784653362022` | `7.35225342e-7` degree |

The current Runner independently reports the same rounded recipe evidence: distance `3.659`, XZ width `1.071`, Y delta `3.499`, raw-height delta `5831.296`, and elevation angle `72.978` degrees.

## Repeatable Commands

Generate and independently verify the full-resolution point-only PLY:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --c3d-map-probe 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity_cloudcompare_20260713\openvision_c3d_full_resolution_point_only.ply --report artifacts\map_fidelity_cloudcompare_20260713\openvision_c3d_full_resolution_point_only_dotnet.txt --max-sampled-points 2147483647 --point-only
python scripts\verify-c3d-map-ply.py --source 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity_cloudcompare_20260713\openvision_c3d_full_resolution_point_only.ply --report artifacts\map_fidelity_cloudcompare_20260713\openvision_c3d_full_resolution_point_only_python.txt --max-sampled-points 2147483647 --first-cell 84,1190 --second-cell 7,994
```

After CloudCompare re-saves the file, verify every ordered vertex and the same point pair:

```powershell
python scripts\ply-coordinate-signature.py --reference artifacts\map_fidelity_cloudcompare_20260713\openvision_c3d_full_resolution_point_only.ply --candidate artifacts\map_fidelity_cloudcompare_20260713\cloudcompare_resaved_c3d_full_resolution_point_only.ply --report artifacts\map_fidelity_cloudcompare_20260713\cloudcompare_resaved_c3d_full_resolution_compare_1e6.txt --ignore-faces --tolerance 0.000001
python scripts\verify-c3d-map-ply.py --source 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity_cloudcompare_20260713\cloudcompare_resaved_c3d_full_resolution_point_only.ply --report artifacts\map_fidelity_cloudcompare_20260713\cloudcompare_resaved_c3d_full_resolution_python.txt --max-sampled-points 2147483647 --first-cell 84,1190 --second-cell 7,994
```

## Evidence

```text
artifacts/map_fidelity_cloudcompare_20260713/cloudcompare_full_resolution_cli.log
artifacts/map_fidelity_cloudcompare_20260713/cloudcompare_resaved_c3d_full_resolution_python.txt
artifacts/map_fidelity_cloudcompare_20260713/cloudcompare_resaved_c3d_full_resolution_compare_1e6.txt
artifacts/map_fidelity_cloudcompare_20260713/cloudcompare_resaved_c3d_full_resolution_compare_1e7.txt
artifacts/map_fidelity_cloudcompare_20260713/cloudcompare_c2c_full_resolution_cli.log
artifacts/map_fidelity_cloudcompare_20260713/cloudcompare_c2c_full_resolution_stats.txt
artifacts/map_fidelity_cloudcompare_20260713/runner_point_pair_dimensions_current.txt
```

## Remaining Trust Boundary

- T3 independent display-frame interchange now passes with both Open3D and CloudCompare, including CloudCompare full-resolution C2C and point-pair derived metrics.
- T4 remains blocked until X/Z pitch, height scale/offset, units, axis directions/origins, and calibration provenance are supplied.
- T5 remains blocked until a calibrated reference artifact and recorded measurements are compared in a licensed metrology application.

## Sources

- CloudCompare stable download and official MD5: https://www.cloudcompare.org/release/
- CloudCompare command-line PLY, save, and global-shift options: https://www.cloudcompare.org/doc/wiki/index.php/Command_line_mode
- CloudCompare cloud-to-cloud distance: https://www.cloudcompare.org/doc/wiki/index.php?title=Cloud-to-Cloud_Distance
