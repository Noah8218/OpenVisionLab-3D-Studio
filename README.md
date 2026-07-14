# OpenVisionLab 3D Studio

[![CI](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml)

OpenVisionLab 3D Studio is an early-stage Windows desktop project for rule-based 3D inspection. The immediate goal is not a full production platform; it is a reliable 3D viewer and inspection workbench for loading local 3D data, measuring geometry, showing result overlays, and replaying repeatable validation recipes.

This repository is under active development and is not production-ready yet.

## 1 Minute Summary

- Product direction: local 3D vision inspection workbench.
- Current focus: reliable typed inspection recipes on top of the passed SharpGL/WPF Viewer Foundation v1 baseline.
- Current viewer scope: camera control, C3D height-grid rendering, GLB scene/node/static-instancing mesh rendering, STL/LAS/LAZ sample rendering and picking, LAZ/LAS two-point distance/height preview/publish result contracts with editable Viewer/Shell acceptance parameters, Shell active-context panes, entity visibility, measurement HUD, two-point and ROI step-height measurement, transform/alignment state, overlays, recipe-owned ROI/alignment edit controls, recipe load/save, and screenshot smoke evidence.
- Current rule scope: C3D height deviation plus complete typed plane-flatness, explicit C3D point-pair distance/width/signed-angle, two-region signed Gap/Flush, reference-plane Volume, and exact-row Cross-section Dimensions slices, analytic golden/error verification, editable tolerances, explicit Preview/Publish, recipe save/reopen, headless Runner parity, Shell actual-step evidence, editable LAZ/LAS two-point acceptance replay, and shared Core evidence formatting.
- Current C3D trust scope: fixed-sample row/column orientation confirmed against the local reference PNG, 10/10 mapping golden cases, a full-resolution 1,653,562-point point-only PLY roundtrip with zero C# XYZ/RGB error, independent Python recalculation within `2.37e-7`, and CloudCompare 2.13.2 full-resolution interchange/C2C and point-pair parity within `1e-6` Viewer units. Physical units, calibration, and licensed metrology parity remain unverified.
- Out of early scope: industrial camera acquisition/control, PLC, robot, cloud, deployment management, production database, and full CAD editing.

## Requirements

- Windows
- .NET SDK 10.0.300 or newer compatible feature band
- Git

The checked build targets .NET 10. Non-WPF libraries use `net10.0`; Viewer/Shell/app projects use `net10.0-windows`.

## Install And Run

```powershell
git clone https://github.com/Noah8218/OpenVisionLab-3D-Studio.git
cd OpenVisionLab-3D-Studio
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug
```

To run the docked workbench shell:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug
```

## Sample Data

Local sample data is expected under `3D\`:

| Path | Role |
| --- | --- |
| `3D\Thickness\Ori_20240116_094414.C3D` | Current C3D height-grid sample used by viewer and runner smoke. |
| `3D\Thickness\Ori_20240116_094414.png` | 2D reference image for the Thickness sample. |
| `3D\Warpage\Ori_20240116_094430.C3D` | Current Warpage candidate sample. |
| `3D\Warpage\Ori_20240116_094430.png` | 2D reference image for the Warpage sample. |
| `3D\PublicSamples\glTF\Box.glb` | Public GLB baseline for the first external mesh import test. |
| `3D\PublicSamples\glTF\BoxVertexColors.glb` | Public GLB vertex-color import baseline. |
| `3D\PublicSamples\glTF\BoxTextured.glb` | Public GLB texture/material import baseline. |
| `3D\PublicSamples\glTF\SimpleInstancing.glb` | Public GLB static `EXT_mesh_gpu_instancing` import baseline. |
| `3D\PublicSamples\glTF\Avocado.glb` | Public GLB realistic non-box textured mesh import baseline. |
| `3D\PublicSamples\glTF\ToyCar.glb` | Public GLB complex textured car fixed-matrix sample. |
| `3D\PublicSamples\STL\Tetrahedron.stl` | Local generated STL triangle-mesh import baseline. |
| `3D\PublicSamples\STL\3DBenchy.stl` | Public STL complex real mesh fixed-matrix sample. |
| `3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz` | Public LAZ metadata/bounds and sampled XYZ/RGB point-cloud render baseline. |
| `3D\PublicSamples\PointCloud\interesting.las` | Public LAS small uncompressed XYZ/RGB point-cloud render baseline. |
| `3D\PublicSamples\Invalid\corrupt.glb` | Intentional invalid GLB fixture for controlled loader-failure smoke. |
| `3D\PublicSamples\Invalid\corrupt.laz` | Intentional invalid LAZ fixture for controlled loader-failure smoke. |

The current C3D layout is inferred as `int32 width`, `int32 height`, then `float32` height/depth samples. Treat that as an implementation observation until an official C3D specification is confirmed.

More details: `docs\OPENVISIONLAB_3D_SAMPLE_DATA.md`, `docs\OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`, and `3D\PublicSamples\README.md`.

## Build Command

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
```

Build the separately hostable Viewer DLL bundle:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-viewer-dll.ps1
```

The output under `artifacts\viewer-dll\net10.0-windows` includes `OpenVisionLab.ThreeD.Viewer.dll`, required runtime DLLs, and a SHA-256 manifest. The manifest also records product/Host API versions, Git commit/tree state, and .NET SDK version. See `docs\OPENVISIONLAB_3D_VIEWER_DLL_INTEGRATION.md` for WPF host integration.

Verify a binary-only external WPF host:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-viewer-dll-host.ps1
```

## Smoke Commands

Data loading matrix smoke:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1
```

Use `-ArtifactDir artifacts\matrix_custom_after` when the matrix output should be isolated from the default evidence folder.

Ad hoc GLB/STL/LAS/LAZ sample probe:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\probe-3d-sample.ps1 -SamplePath 3D\PublicSamples\PointCloud\interesting.las -ArtifactDir artifacts\probe_las_after
```

Use `-RenderDensity Fast|Balanced|Detailed` for Viewer/Shell point-cloud density and `-MaxSampledPoints <count>` for the LAS/LAZ runner probe sample budget.
The probe writes Viewer/Shell screenshots, contract text where available, GLB/STL pick and two-point measurement evidence, LAS/LAZ runner probe output, LAS/LAZ pick and two-point measurement evidence, Shell GLB/STL pick/measurement evidence, Shell LAS/LAZ height-color and measurement evidence, and `probe_summary.txt`. Missing files, invalid probe options, and unsupported extensions also write a failing `probe_summary.txt`; common unsupported formats add a `FORMAT_CANDIDATE` line for the next loader task. Current supported extensions are `.glb`, `.stl`, `.las`, and `.laz`.
Large mesh probes are promoted to fixed-matrix coverage only when they expose a new loader, camera, picking, measurement, Shell, or contract gap that routine smoke does not already cover. `ToyCar.glb` and `3DBenchy.stl` are already in fixed-matrix coverage, with their promotion details and contract expectations tracked in `docs\OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`.

Viewer screenshot and contract smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_two_point_after.png --smoke-c3d thickness --smoke-measure two-point --smoke-contracts artifacts\viewer_two_point_after.txt
```

C3D map fidelity smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-c3d-map-fidelity --report artifacts\map_fidelity\c3d_map_fidelity_golden.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --c3d-map-probe 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\c3d_map_fidelity_actual.txt --max-sampled-points 140000
python scripts\verify-c3d-map-ply.py --source 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\c3d_map_fidelity_python.txt --max-sampled-points 140000 --first-cell 85,1190 --second-cell 10,995
python scripts\ply-coordinate-signature.py --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\openvision_c3d_detailed_signature.txt
```

The PLY contains exact rendered sample vertices and deterministic height RGB values. Its faces are external-viewer compatibility surfaces only and are not measurement geometry. See `docs\OPENVISIONLAB_3D_MAP_FIDELITY_VALIDATION_20260711.md`.

External viewer parity check after re-saving the same PLY from CloudCompare, ZEISS INSPECT, PolyWorks, Open3D, MeshLab, or another trusted tool:

```powershell
python scripts\ply-coordinate-signature.py --reference artifacts\map_fidelity\openvision_c3d_detailed.ply --candidate artifacts\map_fidelity\external_resaved_c3d_detailed.ply --report artifacts\map_fidelity\external_resaved_c3d_detailed_compare.txt --ignore-faces --tolerance 0.00001
```

This compares ordered ASCII PLY vertices, RGB values, bounds-derived signatures, and file hashes. `--ignore-faces` is appropriate when a point-cloud tool drops OpenVisionLab's visualization-only compatibility faces. Open3D 0.19.0 ASCII re-save was observed to round coordinates to a maximum `5e-6` Viewer units, so external ASCII re-save parity uses `1e-5` while the internal .NET/Python C3D mapping still uses `1e-6`. If the external tool reorders points, use its own cloud-to-cloud distance report and keep this signature report as the import/export identity check.

CloudCompare 2.13.2 full-resolution validation passed for all `1,653,562` fixed-sample points/RGB at the stricter `1e-6` Viewer-frame tolerance and preserved the selected recipe point-pair metrics. This proves neutral interchange and derived display-frame consistency, not physical calibration or metrology-grade accuracy. See `docs/OPENVISIONLAB_3D_CLOUDCOMPARE_PARITY_20260713.md`.

Full-resolution point-only audit:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --c3d-map-probe 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity\c3d_map_full_resolution.ply --report artifacts\map_fidelity\c3d_map_full_resolution_dotnet.txt --max-sampled-points 2147483647 --point-only
python scripts\verify-c3d-map-ply.py --source 3D\Thickness\Ori_20240116_094414.C3D --ply artifacts\map_fidelity\c3d_map_full_resolution.ply --report artifacts\map_fidelity\c3d_map_full_resolution_python.txt --max-sampled-points 2147483647 --first-cell 84,1190 --second-cell 7,994
```

C3D reference-plane flatness recipe smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_flatness_after.png --smoke-recipe recipes\c3d-plane-flatness.recipe.json --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_plane_flatness.recipe.json --smoke-contracts artifacts\viewer_flatness_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_plane_flatness.recipe.json --report artifacts\runner_flatness_after.txt --expect-status Fail --compare-contract artifacts\viewer_flatness_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-plane-flatness --report artifacts\plane_flatness_golden_after.txt
```

C3D point-pair dimensions recipe smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_dimensions_after.png --smoke-c3d thickness --smoke-measure dimensions --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --smoke-contracts artifacts\viewer_dimensions_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_dimensions_reopen_after.png --smoke-recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --smoke-contracts artifacts\viewer_dimensions_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --report artifacts\runner_point_pair_dimensions_after.txt --expect-status Pass --compare-contract artifacts\viewer_dimensions_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-point-pair-dimensions --report artifacts\point_pair_dimensions_golden_after.txt
```

C3D Gap / Flush recipe smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_gap_flush_after.png --smoke-recipe recipes\c3d-gap-flush.recipe.json --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_gap_flush.recipe.json --smoke-contracts artifacts\viewer_gap_flush_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_gap_flush_reopen_after.png --smoke-recipe artifacts\saved_c3d_gap_flush.recipe.json --smoke-contracts artifacts\viewer_gap_flush_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_gap_flush.recipe.json --report artifacts\runner_gap_flush_after.txt --expect-status Pass --compare-contract artifacts\viewer_gap_flush_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-gap-flush --report artifacts\gap_flush_golden_after.txt
```

C3D Volume recipe smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_volume_after.png --smoke-recipe recipes\c3d-volume.recipe.json --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_volume.recipe.json --smoke-contracts artifacts\viewer_volume_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_volume_reopen_after.png --smoke-recipe artifacts\saved_c3d_volume.recipe.json --smoke-contracts artifacts\viewer_volume_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_volume.recipe.json --report artifacts\runner_volume_after.txt --expect-status Pass --compare-contract artifacts\viewer_volume_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-volume --report artifacts\volume_golden_after.txt
```

C3D Cross-section Dimensions recipe smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_cross_section_after.png --smoke-recipe recipes\c3d-cross-section-dimensions.recipe.json --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_cross_section_dimensions.recipe.json --smoke-contracts artifacts\viewer_cross_section_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_cross_section_reopen_after.png --smoke-recipe artifacts\saved_c3d_cross_section_dimensions.recipe.json --smoke-contracts artifacts\viewer_cross_section_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_cross_section_dimensions.recipe.json --report artifacts\runner_cross_section_after.txt --expect-status Pass --compare-contract artifacts\viewer_cross_section_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-cross-section --report artifacts\cross_section_golden_after.txt
```

Durable JSON/HTML/CSV run bundle:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_cross_section_dimensions.recipe.json --report artifacts\run_record_cross_section\runner.txt --expect-status Pass --compare-contract artifacts\viewer_cross_section_reopen_after.txt --viewer-screenshot artifacts\viewer_cross_section_reopen_after.png --run-record artifacts\run_record_cross_section\run.json --html-report artifacts\run_record_cross_section\report.html --csv-report artifacts\run_record_cross_section\metrics.csv
```

Run Record schema `1.1` adds application version, Viewer Host API version, Git commit/tree state, .NET runtime, OS, and process architecture. Schema `1.0` JSON remains readable by the current Shell.

Public GLB import smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_import_after.png --smoke-glb 3D\PublicSamples\glTF\Box.glb --smoke-contracts artifacts\glb_import_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_vertex_color_after.png --smoke-glb 3D\PublicSamples\glTF\BoxVertexColors.glb --smoke-contracts artifacts\glb_vertex_color_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_textured_after.png --smoke-glb 3D\PublicSamples\glTF\BoxTextured.glb --smoke-contracts artifacts\glb_textured_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_simple_instancing_after.png --smoke-glb 3D\PublicSamples\glTF\SimpleInstancing.glb --smoke-contracts artifacts\glb_simple_instancing_after.txt
```

Public STL import smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\stl_tetrahedron_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-contracts artifacts\stl_tetrahedron_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\stl_tetrahedron_measure_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-measure mesh-two-point --smoke-contracts artifacts\stl_tetrahedron_measure_after.txt
```

Public LAZ metadata smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_metadata_after.png --smoke-laz 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\laz_metadata_after.txt
```

Public LAZ point decode probe:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --laz-probe 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --report artifacts\laz_points_probe_after.txt --max-sampled-points 50000
```

Public LAZ point render smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_points_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\laz_points_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_pick_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz --smoke-contracts artifacts\laz_pick_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_two_point_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-contracts artifacts\laz_two_point_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_inspector_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result --smoke-contracts artifacts\laz_acceptance_inspector_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_edit_save_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\laz_acceptance_edit_save_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_recipe_reopen_viewer_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\laz_acceptance_recipe_reopen_viewer_after.txt
```

ROI step-height smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_step_after.png --smoke-c3d thickness --smoke-measure roi-step --smoke-contracts artifacts\viewer_roi_step_after.txt
```

Interactive ROI smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_interactive_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-measure roi-interactive --smoke-contracts artifacts\viewer_roi_interactive_after.txt
```

Transform/alignment smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_alignment_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-contracts artifacts\viewer_alignment_after.txt
```

Shell-hosted viewer smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_viewer_internal_hud_after.png --smoke-measure two-point
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_points_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\shell_laz_points_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_points_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_pick_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz --smoke-contracts artifacts\shell_laz_pick_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_pick_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_two_point_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-contracts artifacts\shell_laz_two_point_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_two_point_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_inspector_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result --smoke-contracts artifacts\shell_laz_acceptance_inspector_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_inspector_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_edit_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_shell_laz_two_point_acceptance_contract.recipe.json --smoke-contracts artifacts\shell_laz_acceptance_edit_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_edit_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_shell_laz_two_point_acceptance.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_recipe_reopen_viewer_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\shell_laz_acceptance_recipe_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_recipe_reopen_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt --recipe-comparison-report artifacts\runner_laz_run_history_after.txt --shell-smoke-screenshot artifacts\shell_laz_run_history_after.png --shell-evidence-tab history --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json
```

Headless recipe runner smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\ci\runner_c3d_height_rule.txt --expect-status Fail
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\laz-two-point-measurement.recipe.json --report artifacts\runner_laz_two_point_after.txt --expect-status Pass --compare-contract artifacts\laz_two_point_publish_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\laz-two-point-measurement-fail.recipe.json --report artifacts\runner_laz_two_point_fail_after.txt --expect-status Fail
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_acceptance_edit_save_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_edit_save_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_acceptance_recipe_reopen_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_run_history_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt
```

ROI/alignment recipe roundtrip smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_recipe_save_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-measure roi-interactive --smoke-save-recipe artifacts\saved_roi_alignment.recipe.json --smoke-contracts artifacts\viewer_roi_recipe_save_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment.recipe.json --report artifacts\runner_roi_alignment_recipe_after.txt --expect-status Fail
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_recipe_roundtrip_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-contracts artifacts\viewer_roi_recipe_roundtrip_after.txt
```

Recipe parameter edit smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_parameter_edit_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-edit-parameters roi-align --smoke-save-recipe artifacts\saved_roi_alignment_edited.recipe.json --smoke-contracts artifacts\viewer_recipe_parameter_edit_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_recipe_parameter_edit_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-edit-parameters roi-align
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment_edited.recipe.json --report artifacts\runner_recipe_parameter_edit_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_parameter_edit_after.txt
```

Interactive ROI alignment smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_interactive_alignment_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi --smoke-save-recipe artifacts\saved_roi_alignment_auto.recipe.json --smoke-contracts artifacts\viewer_interactive_alignment_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_interactive_alignment_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment_auto.recipe.json --report artifacts\runner_interactive_alignment_after.txt --expect-status Fail --compare-contract artifacts\viewer_interactive_alignment_after.txt
```

ROI validation smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_validation_valid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi --smoke-save-recipe artifacts\saved_roi_validation_valid.recipe.json --smoke-contracts artifacts\viewer_roi_validation_valid_after.txt
$invalidPath = 'artifacts\saved_roi_validation_invalid.recipe.json'; if (Test-Path $invalidPath) { Remove-Item -LiteralPath $invalidPath -Force }; dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_validation_invalid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-invalid-roi overlap --smoke-save-recipe $invalidPath --smoke-contracts artifacts\viewer_roi_validation_invalid_after.txt; if ($LASTEXITCODE -ne 1) { exit 1 }; if (Test-Path $invalidPath) { exit 1 }
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_validation_invalid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-invalid-roi overlap
```

The expected `Fail` status is intentional for the current sample recipe because the sample exceeds the configured height-deviation tolerance.

## CI

Windows CI restores/builds the .NET 10 solution, rejects vulnerable or deprecated direct/transitive NuGet packages, runs the binary-only Viewer Host direct-EXE smoke, verifies standalone Cross-section Viewer and full Shell C3D screenshots with pixel-quality gates, executes Runner and algorithm golden checks, verifies C3D map fidelity independently, and uploads `artifacts\ci\**`.

GitHub Actions workflow: `.github\workflows\ci.yml`.

CI currently runs on `windows-latest` and performs:

1. `dotnet restore OpenVisionLab.ThreeDStudio.slnx`
2. Direct/transitive NuGet vulnerable/deprecated JSON gate with raw response evidence
3. `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore`
4. Binary-only Viewer Host and standalone Cross-section Viewer screenshot-quality smokes
5. Full Shell C3D screenshot-quality smoke
6. Headless recipe Runner smokes with Cross-section Viewer/Runner `Matched` evidence
7. Analytic/error golden verification for all current typed C3D slices and map mapping
8. Actual fixed-sample C3D-to-PLY .NET roundtrip verification
9. Cross-runtime C3D-to-PLY verification through a dependency-free Python implementation
10. PLY coordinate signature generation for external-viewer parity checks
11. CI artifact upload from `artifacts\ci\`

## Release Notes

The first public prerelease is available at [OpenVisionLab 3D Studio 0.1.0-rc.1](https://github.com/Noah8218/OpenVisionLab-3D-Studio/releases/tag/v0.1.0-rc.1).

The release contains `OpenVisionLab.ThreeD.Viewer-0.1.0-rc.1-windows.zip`, the complete Viewer dependency bundle, plus `SHA256SUMS.txt`. The ZIP SHA-256 is `b9a9b6d002f507da63da32934d93bf6e8deaff2d7c1b00ff70a6f36d6b784a83`.

The release tag points to commit `ac57687`. Local release checks and Windows Actions run `29198517611` pass, including binary-only Host execution, Viewer/Shell screenshot quality, `Matched` Cross-section Viewer/Runner evidence, algorithm goldens, and C3D map fidelity.

Current development snapshot:

- SharpGL/WPF viewer foundation.
- Binary-only WPF Host sample and direct-EXE verification for the separately published Viewer DLL bundle.
- Standalone viewer host and docked shell host.
- C3D height-grid sample rendering and picking.
- C3D map fidelity now locks the inferred display mapping with 10/10 synthetic cases, supports point-only full-resolution PLY, verifies all 1,653,562 valid fixed-sample points through .NET and independent Python implementations, and records source/export hashes, bounds, stride, and explicit physical-scale status.
- PLY coordinate signature tooling now records deterministic point-count, bounds, centroid, RGB, and quantized-coordinate hashes so external viewer re-saves can be checked without relying only on screenshots. Open3D 0.19.0 preserved all sampled `66,212` vertices/RGB within `5e-6`; CloudCompare 2.13.2 preserved all full-resolution `1,653,562` vertices/RGB within `5.00000001e-7` Viewer units and retained selected point-pair distance/height/angle values.
- Viewer-internal coordinate, measurement, and performance HUD.
- Two-point distance and height-delta measurement smoke.
- ROI step-height comparison smoke.
- Interactive ROI center selection smoke for step-height comparison.
- Viewer and Shell transform/alignment state display with smoke contract evidence.
- Transform and ROI step parameters persisted in recipes and repeated by the headless runner.
- Viewer and Shell expose numeric edit controls for recipe-owned transform and ROI step parameters, with save/replay smoke evidence.
- Viewer and Shell expose a minimal `Align From ROI` workflow that centers the selected ROI pair and reference height in the aligned coordinate frame.
- Viewer and Shell expose `Fit C3D Plane`, using a render-density-independent measurement sample to report fitted normal, RMS, and maximum orthogonal distance with overlay and contract evidence.
- Viewer and Shell expose numeric reference-ROI plane flatness with signed deviation coloring, tolerance status, explicit Preview/Publish, stable step/source/reference IDs, recipe save/reopen, Runner parity, and Shell actual-step evidence.
- Runner `--verify-plane-flatness` uses an analytic plane and known signed offsets to verify exact fit/flatness/RMS answers plus controlled invalid-reference/input states; CI preserves this regression.
- Viewer and Shell expose explicit C3D point-pair distance, XZ planar width, and signed elevation angle with separate tolerances, stable source-cell IDs, Preview/Publish, recipe roundtrip, and Runner parity.
- Runner `--verify-point-pair-dimensions` verifies a known `(3,4,4)` vector, signed angle, tolerance failure, and controlled invalid-input states; CI preserves this regression.
- Viewer and Shell expose signed C3D Gap/Flush from two explicit recipe-owned regions with separate tolerances, source/result separation, recipe roundtrip, Runner parity, and a real Shell step row. Gap uses aligned model X and Flush uses raw-height until calibration is available.
- Runner `--verify-gap-flush` verifies signed separation/overlap, independent gap/flush failures, empty regions, non-finite statistics, invalid tolerances, and missing units; CI preserves this regression.
- Viewer and Shell expose C3D above-plane, below-plane, and signed net Volume for explicit reference and measurement ROIs, with Preview/Publish, recipe roundtrip, Runner parity, and a real Shell step row. Values use uncalibrated `model^3`, not physical volume.
- Runner `--verify-volume` verifies exact signed integration, tolerance failure, insufficient/empty samples, invalid area/tolerance, non-finite input, and missing units; CI preserves this regression.
- Viewer and Shell expose exact-row C3D Cross-section Dimensions using an inclusive source-column range, aligned-X width, raw-height range, separate tolerances, source/result separation, recipe roundtrip, Runner parity, linked profile, and a real Shell step row.
- Runner `--verify-cross-section` verifies exact width/height range, independent tolerance failures, selector errors, insufficient/non-finite/out-of-range samples, invalid tolerance, and missing units; CI preserves this regression.
- Runner can emit a durable schema `1.1` JSON run record plus HTML and CSV reports containing recipe/source SHA-256 provenance, UTC time, status, all metrics/overlays, Viewer/Runner match state, execution environment, and evidence paths. Shell Run Snapshot exposes explicit open commands for all six evidence artifacts and remains compatible with schema `1.0`.
- ROI validation warnings block invalid overlapped ROI recipes from being saved.
- Public `Box.glb` import smoke renders a first external GLB mesh and records vertex/triangle/bounds contract evidence.
- Public `BoxVertexColors.glb` import smoke renders per-vertex colors and records vertex-color contract evidence.
- Public `BoxTextured.glb` import smoke renders embedded PNG base-color texture and records UV/texture contract evidence.
- Public `Avocado.glb` import smoke renders a realistic non-box textured mesh and records mesh/UV/texture, fit camera distance, triangle-surface pick, triangle-index/normal metadata, visible surface-normal overlay, and two-point distance/model-Y height evidence.
- Public `ToyCar.glb` fixed-matrix coverage renders a larger CC0 textured GLB with bounds-fit camera framing and records Viewer/Shell pick plus two-point measurement evidence.
- Public `3DBenchy.stl` fixed-matrix coverage proves the STL loader can handle a real 225,706-triangle binary STL through Viewer/Shell load, pick, bounds-fit camera framing, and two-point measurement. Imported-mesh render density now samples large mesh display while contracts keep full source triangle counts.
- Large imported-mesh samples are promoted to the fixed matrix only when they reveal a loader/measurement/UX/contract gap that routine matrix coverage does not already include.
- Viewer Measurement HUD now supports a details toggle; GLB/STL/LAZ smoke starts with compact HUD details so large first-load framing remains readable, source-specific HUD detail rows only show for the active source type, and contracts record `ViewerInternalHud|detailsVisible=...`.
- Public LAZ/LAS point-cloud smoke uses bounds-fit camera distances for both the dense `xyzrgb_manuscript.laz` and sparse large-coordinate `interesting.las` samples, proves RGB/height point-cloud color modes with height range legend evidence, guards non-result `Deviation` mode from advertising a false deviation map, and records load time plus sampling ratio for Balanced/Fast point-cloud density.
- Public `xyzrgb_manuscript.laz` smoke reads LAS/LAZ header metadata, decodes XYZ/RGB points, renders, picks, measures sampled point-cloud data, and publishes LAZ/LAS two-point preview/result layer evidence in standalone Viewer and Shell. Decoder notes are in `docs\OPENVISIONLAB_3D_LAZ_DECODER_REVIEW_20260707.md`.
- Public `interesting.las` smoke proves a small uncompressed LAS RGB point-cloud path, local viewer-origin mapping for large source coordinates, picking, two-point measurement, Shell hosting, and Runner bounds checks.
- LAZ/LAS two-point measurement now replays through `recipes\laz-two-point-measurement.recipe.json` and the headless Runner, uses distance/source-Z height acceptance tolerances, and matches the Viewer publish contract.
- Viewer and Shell Tool / Inspector now expose editable LAZ/LAS two-point acceptance fields; saved point-cloud recipe JSON reopens in Viewer/Shell and replays through Runner with matching `LAZAcceptanceParameters` contract evidence.
- Shell Evidence Workbench and History now compare C3D and point-cloud runner/UI evidence through generic `ToolResult` status plus key metrics: C3D peak deviation or point-cloud distance/source-Z height delta.
- `OpenVisionLab.ThreeD.Core` now owns shared evidence contract-line formatting for `ToolResult`, metrics, overlays, source entities, entity layers, and published result entities so Viewer and Runner do not drift in report syntax.
- Data loading coverage is tracked in `docs\OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md` so C3D, GLB, STL, LAS, and LAZ import/render/replay evidence stays explicit before algorithm expansion. Shell Inspector now carries point-cloud height scale text when the hosted Viewer hides standalone side panels.
- Runner, Viewer, and Shell smoke paths now handle invalid recipe/CLI input with controlled errors: invalid LAZ sample-count options return usage failure, missing recipe source fields report validation failures, and Shell smoke propagates embedded Viewer smoke failures through the process exit code.
- Viewer and Shell loader smoke paths now fail fast for missing GLB/STL/LAS/LAZ sample paths and invalid GLB/STL/LAZ fixtures, record attempted source paths plus missing/corrupt failure causes in Viewer contracts, and propagate `smokeExitCode=1` through Shell smoke.
- Intentional invalid GLB/STL/LAZ fixtures live under `3D\PublicSamples\Invalid\` and prove corrupted-file loader failures without relying only on missing file paths.
- First C3D height-deviation recipe and headless runner.
- Windows GitHub Actions CI build and runner smoke.

## Roadmap

1. Preserve the passed Viewer Foundation v1 and C3D display-frame fidelity regression baselines.
2. Obtain the C3D calibration contract and add an explicit mapping profile for pitch, height scale/offset, units, axes, and calibration identity.
3. Preserve the passed plane/flatness and point-pair-dimensions analytic/error regression baselines.
4. Preserve the completed basic surface slices: Gap/Flush, Volume, and Cross-section Dimensions.
5. Add measured-to-nominal comparison using one local sample pair before considering a CAD kernel or broad CAD formats.
6. Preserve the durable JSON run record and simple HTML/CSV one-run report baseline before adding batch trends or enterprise integration.

## Known Limitations

- The project is not production-ready.
- Current C3D parsing and viewer scale are inferred from local samples, not an official format or calibration contract. Display-frame fidelity is verified for the fixed sample; physical coordinate and metrology fidelity are not.
- The current Thickness and Warpage sample files may be byte-identical; do not assume they represent different measurements until new evidence is available.
- Algorithm coverage is intentionally narrow; Viewer Foundation v1 and five independent typed C3D inspection slices have passed, but there is no general multi-step executor or broad measurement coverage.
- ROI/alignment editing is currently an MVP. `Align From ROI` applies translation. Plane flatness supports a numeric operator-configured reference ROI, but interactive ROI drawing, three-point references, plane-derived rotation, 3-2-1, best-fit, and richer guided warnings are not implemented yet.
- Current measurements are not certified metrology results. Plane/flatness and point-pair dimensions have analytic synthetic golden coverage, but unit provenance, calibration, uncertainty, external reference datasets, automatic feature extraction, and broader independent validation are incomplete.
- No industrial camera acquisition/control, PLC, robot, cloud, deployment, account, or production database integration exists.
- The published prerelease contains the Viewer DLL dependency bundle only; no installer or full Studio/Shell application package exists yet.
- Run reporting is one-run JSON/HTML/CSV only; there is no PDF, database, retention policy, digital signing, batch trend, or SPC workflow.
- SharpGL.WPF 3.1.1 runs through its older compatible .NET Core asset rather than a direct .NET 10 build. Current C3D/GLB and full Viewer/Shell smokes pass, but this remains a maintained regression boundary.

## Documentation

- `AGENTS.md`: repository working rules and verification commands.
- `docs\CODEBASE_STRUCTURE.md`: project layout.
- `docs\OPENVISIONLAB_3D_DOTNET10_MIGRATION_20260712.md`: .NET 10 dependency boundary, evidence, and watch list.
- `docs\OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md`: product, Viewer Host API, manifest, Run Record, recipe, tag, and release-gate rules.
- `docs\OPENVISIONLAB_3D_PLATFORM_DIRECTION.md`: product direction and roadmap.
- `docs\OPENVISIONLAB_3D_PRODUCT_TARGET_AND_SELF_EVALUATION_20260711.md`: current product target, commercial comparison, maturity scorecard, gates, and default priorities.
- `docs\OPENVISIONLAB_3D_SAMPLE_DATA.md`: sample inventory and C3D observations.
- `docs\OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`: loader/viewer evidence matrix for current C3D, GLB, STL, LAS, and LAZ samples.
- `docs\OPENVISIONLAB_3D_MAP_FIDELITY_VALIDATION_20260711.md`: C3D source-grid, Viewer-frame, independent-renderer, and physical-fidelity gates.
- `docs\OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`: current engineering handoff.
