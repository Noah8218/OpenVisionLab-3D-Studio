# OpenVisionLab 3D Data Loading Test Matrix

Updated: 2026-07-07

## Purpose

Before adding more 3D algorithms, the viewer must prove that it can load representative 3D data, render it, expose source/result contracts, and leave repeatable evidence. This matrix is the current acceptance checklist for that gate.

This is not a performance benchmark and not a production format certification. It is the smallest current evidence set that keeps OpenVisionLab 3D Studio viewer-first and inspection-oriented.

## Acceptance Rules

For a sample to be considered covered, it must have at least one current-source evidence path:

| Level | Required evidence |
| --- | --- |
| Inventory | Sample path, size, source/license note, and intended use are documented. |
| Import | Loader records dimensions, count, bounds, material/color/texture fields, or equivalent metadata in contract text. |
| Render | Viewer smoke writes a screenshot from the current build. |
| Inspect | Picking, measurement, layer state, or overlay state is recorded when supported by that data type. |
| Replay | Runner report exists when the data type has a rule or probe path. |
| Shell | Docked workbench smoke exists when the feature affects the main workbench context. |

For UI-facing additions, keep the implementation order explicit: View first for the visible surface, ViewModel next for durable state and commands, Model/data layer last only when the test exposes a real loader or contract gap.

## Current Matrix

| Sample | Type | Current coverage | Current evidence command/artifact | Gap before algorithms |
| --- | --- | --- | --- | --- |
| `3D/Thickness/Ori_20240116_094414.C3D` | Inferred C3D height grid | Inventory, import, render, pick, two-point, ROI step, transform, C3D height-deviation rule, Runner replay. | `artifacts/matrix_c3d_thickness_after.png`, `artifacts/matrix_c3d_thickness_after.txt` | Needs official C3D format confirmation or another non-identical C3D sample before treating Thickness/Warpage as separate real cases. |
| `3D/Warpage/Ori_20240116_094430.C3D` | Inferred C3D height grid | Inventory only; currently byte-identical to Thickness. | `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md` | Needs a non-identical Warpage sample before rule or UI behavior can be distinguished. |
| `3D/PublicSamples/glTF/Box.glb` | GLB mesh | Inventory, import, render, mesh bounds, triangle/vertex contract. | `artifacts/matrix_glb_box_after.png`, `artifacts/matrix_glb_box_after.txt` | Minimal baseline only; non-box mesh UX is now tracked by `Avocado.glb`. |
| `3D/PublicSamples/glTF/BoxVertexColors.glb` | GLB mesh with `COLOR_0` | Inventory, import, render, vertex-color contract. | `artifacts/matrix_glb_vertex_color_after.png`, `artifacts/matrix_glb_vertex_color_after.txt` | Needs larger colored mesh to test color-mode and picking readability. |
| `3D/PublicSamples/glTF/BoxTextured.glb` | GLB mesh with `TEXCOORD_0` and embedded PNG texture | Inventory, import, render, UV/texture upload contract. | `artifacts/matrix_glb_textured_after.png`, `artifacts/matrix_glb_textured_after.txt` | Needs external texture and larger texture cases before calling material support broad. |
| `3D/PublicSamples/glTF/Avocado.glb` | GLB realistic non-box textured mesh | Inventory, import, render, mesh bounds, triangle/vertex contract, UV/texture upload contract, fit camera distance, triangle-surface pick, triangle-index/normal metadata, visible surface-normal overlay, two-point distance/model-Y height comparison, Shell context. | `artifacts/matrix_glb_avocado_after.png`, `artifacts/matrix_glb_avocado_pick_after.png`, `artifacts/matrix_glb_avocado_measure_after.png`, `artifacts/matrix_shell_glb_avocado_measure_after.png`, `artifacts/matrix_glb_avocado_measure_after.txt` | Needs more real mesh samples before broad CAD/material confidence. |
| `3D/PublicSamples/STL/Tetrahedron.stl` | STL triangle mesh | Inventory, import, render, mesh bounds, triangle contract, surface pick, normal overlay, two-point distance/model-Y height comparison, Shell context. | `artifacts/matrix_stl_tetrahedron_after.png`, `artifacts/matrix_stl_tetrahedron_pick_after.png`, `artifacts/matrix_stl_tetrahedron_measure_after.png`, `artifacts/matrix_shell_stl_tetrahedron_measure_after.png`, `artifacts/matrix_stl_tetrahedron_measure_after.txt` | Local generated fixture only; needs real STL parts before broad mesh confidence. |
| `3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz` | LAZ point cloud | Inventory, metadata import, sampled XYZ/RGB decode, bounds-fit camera, render, render-density sampling, load/sample telemetry, pick, two-point measurement, LAZ/LAS acceptance, Runner probe, Runner recipe replay, Shell context. | `artifacts/matrix_laz_points_after.png`, `artifacts/matrix_laz_points_fast_after.png`, `artifacts/matrix_laz_points_after.txt`, `artifacts/matrix_laz_probe_after.txt`, `artifacts/matrix_shell_laz_points_after.png` | Needs a real inspection tolerance/use case before adding another point-cloud rule. |
| `3D/PublicSamples/PointCloud/interesting.las` | LAS point cloud | Inventory, metadata import, uncompressed XYZ/RGB decode, local viewer-origin mapping for large source coordinates, bounds-fit camera, RGB/height color rendering, height color legend/range, non-result deviation-mode guard, load/sample telemetry, pick, two-point measurement, Runner probe, Shell context. | `artifacts/matrix_las_interesting_after.png`, `artifacts/matrix_las_interesting_height_after.png`, `artifacts/matrix_las_interesting_deviation_guard_after.png`, `artifacts/matrix_las_interesting_pick_after.png`, `artifacts/matrix_las_interesting_measure_after.png`, `artifacts/matrix_las_interesting_probe_after.txt`, `artifacts/matrix_shell_las_interesting_after.png`, `artifacts/matrix_shell_las_interesting_height_after.png`, `artifacts/matrix_shell_las_interesting_measure_after.png` | Needs recipe/replay coverage only if this sample becomes a validation recipe source. |
| `3D/PublicSamples/Invalid/corrupt.glb` | Invalid GLB fixture | Inventory, controlled Viewer failure, controlled Shell failure, attempted path plus corrupt-header cause in contract status. | `artifacts/viewer_corrupt_glb_cause_after.png`, `artifacts/viewer_corrupt_glb_cause_after.txt`, `artifacts/shell_corrupt_glb_cause_after.png` | Failure fixture only; do not treat as positive loader coverage. |
| `3D/PublicSamples/Invalid/corrupt.stl` | Invalid STL fixture | Inventory, controlled Viewer failure, controlled Shell failure, attempted path plus invalid-vertex cause in contract status. | `artifacts/viewer_corrupt_stl_cause_after.png`, `artifacts/viewer_corrupt_stl_cause_after.txt`, `artifacts/shell_corrupt_stl_cause_after.png` | Failure fixture only; do not treat as positive loader coverage. |
| `3D/PublicSamples/Invalid/corrupt.laz` | Invalid LAZ fixture | Inventory, controlled Viewer failure, controlled Shell failure, attempted path plus corrupt-header cause in contract status. | `artifacts/viewer_corrupt_laz_cause_after.png`, `artifacts/viewer_corrupt_laz_cause_after.txt`, `artifacts/shell_corrupt_laz_cause_after.png` | Failure fixture only; do not treat as positive loader coverage. |

## Refresh Commands

Run these after a current build when validating loader coverage:

One-command refresh:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1
```

The script writes `artifacts/matrix_smoke_summary_after.txt` and treats expected invalid-sample loader failures as passing checks when the process exit code is `1`.
Use `-ArtifactDir artifacts\matrix_custom_after` to isolate a matrix refresh from the default evidence files.

Ad hoc probe for a new GLB/STL/LAS/LAZ file:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\probe-3d-sample.ps1 -SamplePath C:\path\to\sample.las -ArtifactDir artifacts\probe_new_sample_after
```

Use `-RenderDensity Fast|Balanced|Detailed` for Viewer/Shell point-cloud density and `-MaxSampledPoints <count>` for the LAS/LAZ runner probe sample budget.
The probe currently supports `.glb`, `.stl`, `.las`, and `.laz`. It writes first-pass loader/viewer evidence including GLB/STL pick and two-point measurement contracts, LAS/LAZ runner, pick, height-color, and two-point measurement evidence, Shell GLB/STL pick/measurement screenshots, and Shell LAS/LAZ height-color and measurement screenshots before deciding whether a new sample belongs in the fixed matrix. Missing files, invalid probe options, and unsupported extensions still write a failing `probe_summary.txt`; common unsupported formats add a `FORMAT_CANDIDATE` line for the next loader task.

Individual refresh commands:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_c3d_thickness_after.png --smoke-c3d thickness --smoke-contracts artifacts\matrix_c3d_thickness_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_glb_box_after.png --smoke-glb 3D\PublicSamples\glTF\Box.glb --smoke-contracts artifacts\matrix_glb_box_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_glb_vertex_color_after.png --smoke-glb 3D\PublicSamples\glTF\BoxVertexColors.glb --smoke-contracts artifacts\matrix_glb_vertex_color_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_glb_textured_after.png --smoke-glb 3D\PublicSamples\glTF\BoxTextured.glb --smoke-contracts artifacts\matrix_glb_textured_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_glb_avocado_after.png --smoke-glb 3D\PublicSamples\glTF\Avocado.glb --smoke-contracts artifacts\matrix_glb_avocado_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_glb_avocado_pick_after.png --smoke-glb 3D\PublicSamples\glTF\Avocado.glb --smoke-pick glb --smoke-contracts artifacts\matrix_glb_avocado_pick_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_glb_avocado_measure_after.png --smoke-glb 3D\PublicSamples\glTF\Avocado.glb --smoke-measure glb-two-point --smoke-contracts artifacts\matrix_glb_avocado_measure_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_stl_tetrahedron_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-contracts artifacts\matrix_stl_tetrahedron_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_stl_tetrahedron_pick_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-pick mesh --smoke-contracts artifacts\matrix_stl_tetrahedron_pick_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_stl_tetrahedron_measure_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-measure mesh-two-point --smoke-contracts artifacts\matrix_stl_tetrahedron_measure_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_laz_points_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\matrix_laz_points_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_laz_points_fast_after.png --smoke-density Fast --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\matrix_laz_points_fast_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_las_interesting_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\interesting.las --smoke-contracts artifacts\matrix_las_interesting_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_las_interesting_height_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\interesting.las --smoke-action color-height --smoke-contracts artifacts\matrix_las_interesting_height_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_las_interesting_deviation_guard_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\interesting.las --smoke-action color-deviation --smoke-contracts artifacts\matrix_las_interesting_deviation_guard_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_las_interesting_pick_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\interesting.las --smoke-pick laz --smoke-contracts artifacts\matrix_las_interesting_pick_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\matrix_las_interesting_measure_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\interesting.las --smoke-measure two-point --smoke-contracts artifacts\matrix_las_interesting_measure_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --laz-probe 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --report artifacts\matrix_laz_probe_after.txt --max-sampled-points 50000
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --laz-probe 3D\PublicSamples\PointCloud\interesting.las --report artifacts\matrix_las_interesting_probe_after.txt --max-sampled-points 50000
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\matrix_shell_laz_points_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\matrix_shell_las_interesting_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\interesting.las
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\matrix_shell_las_interesting_height_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\interesting.las --smoke-action color-height
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\matrix_shell_las_interesting_measure_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\interesting.las --smoke-measure two-point
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\matrix_shell_glb_avocado_measure_after.png --smoke-glb 3D\PublicSamples\glTF\Avocado.glb --smoke-measure glb-two-point
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\matrix_shell_stl_tetrahedron_measure_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-measure mesh-two-point
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_missing_stl_cause_after.png --smoke-stl 3D\PublicSamples\STL\missing.stl --smoke-contracts artifacts\viewer_missing_stl_cause_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_corrupt_glb_cause_after.png --smoke-glb 3D\PublicSamples\Invalid\corrupt.glb --smoke-contracts artifacts\viewer_corrupt_glb_cause_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_corrupt_stl_cause_after.png --smoke-stl 3D\PublicSamples\Invalid\corrupt.stl --smoke-contracts artifacts\viewer_corrupt_stl_cause_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_corrupt_laz_cause_after.png --smoke-laz-points 3D\PublicSamples\Invalid\corrupt.laz --smoke-contracts artifacts\viewer_corrupt_laz_cause_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_corrupt_stl_cause_after.png --smoke-stl 3D\PublicSamples\Invalid\corrupt.stl
```

## Contract Checks

The refreshed text artifacts should include these signals:

| Artifact | Expected contract signal |
| --- | --- |
| `artifacts/matrix_c3d_thickness_after.txt` | `source.c3d-thickness|HeightGrid`, `layer.source.c3d-thickness|Source|visible=True`, `CoordinateFrame|visible=True`, `RenderDensity`, `HeightMap|visible=True`, and explicit transform/alignment lines. |
| `artifacts/matrix_glb_box_after.txt` | `GLB|loaded=True`, vertex count, triangle count, mesh bounds. |
| `artifacts/matrix_glb_vertex_color_after.txt` | `GLB|loaded=True`, `usesVertexColors=True`. |
| `artifacts/matrix_glb_textured_after.txt` | `GLB|loaded=True`, UV/texture evidence such as texture upload or texture byte count. |
| `artifacts/matrix_glb_avocado_after.txt` | `GLB|loaded=True`, `Avocado.glb`, mesh bounds, triangle count, UVs, `hasTexture=True`, `HeightMap|visible=False`, and `SectionProfile|visible=False`. |
| `artifacts/matrix_glb_avocado_pick_after.txt` | `GLBPick|selected=True|kind=mesh surface`, `triangleIndex=`, `normal=`, `GLBSurfaceOverlay|visible=True`, and `Camera|...|distance=0.350` with selected mesh-surface coordinates and small-mesh fit evidence. |
| `artifacts/matrix_glb_avocado_measure_after.txt` | `SelectionMode|value=Two Point Measure`, `MeasurementOverlay|visible=True`, `LAZAcceptance|visible=False`, and `TwoPoint|visible=True` with distance, dX/dY/dZ, and model-Y height delta. |
| `artifacts/matrix_stl_tetrahedron_after.txt` | `STL|loaded=True`, 12 vertices, 4 triangles, no texture, no vertex colors, and bounds `(0.000, 0.000, 0.000)` to `(1.000, 1.000, 1.000)`. |
| `artifacts/matrix_stl_tetrahedron_pick_after.txt` | `STLPick|selected=True|kind=mesh surface`, `triangleIndex=`, `normal=`, and `STLSurfaceOverlay|visible=True`. |
| `artifacts/matrix_stl_tetrahedron_measure_after.txt` | `SelectionMode|value=Two Point Measure`, `MeasurementOverlay|visible=True`, `LAZAcceptance|visible=False`, and `TwoPoint|visible=True` with distance and model-Y height delta. |
| `artifacts/matrix_laz_points_after.txt` | `LAZ|loaded=True`, `decoder=points-decoded`, source bounds, sampled point count, bounds-fit `Camera|...|distance=343...`, `PointCloudPerformance` with `samplePercent=2.320`, and `LAZ/LAS` sampling/render-density wording. |
| `artifacts/matrix_laz_points_fast_after.txt` | `RenderDensity|mode=Fast`, `maxLazSampledPoints=25000`, `sampledLazPoints=25000`, and `samplePercent=1.160`. |
| `artifacts/matrix_las_interesting_after.txt` | `LAZ|loaded=True`, `interesting.las`, `compressed=False`, `pointFormat=3`, `decoder=points-decoded`, bounds-fit `Camera|...|distance=9337...`, `samplePercent=100.000`, `HeightMap|visible=False`, and `SectionProfile|visible=False`. |
| `artifacts/matrix_las_interesting_height_after.txt` | `ColorMode|mode=Height`, `PointCloudColorLegend|visible=True`, and the same large-coordinate LAS bounds-fit camera. |
| `artifacts/matrix_las_interesting_deviation_guard_after.txt` | `ColorMode|mode=RGB` and `Deviation requires an active result`, proving LAZ/LAS does not advertise a false deviation map before a result exists. |
| `artifacts/matrix_las_interesting_pick_after.txt` | `LAZPick|selected=True` with source coordinates and local viewer coordinates. |
| `artifacts/matrix_las_interesting_pick_after.txt` | `LAZPick|selected=True` and `Smoke pick: LAZ/LAS sampled point` status wording. |
| `artifacts/matrix_las_interesting_measure_after.txt` | `MeasurementOverlay|visible=True`, `LAZ/LAS Two Point Measurement` tool wording, `LAZAcceptance|visible=True` with `LAZ/LAS acceptance` wording, and `TwoPoint|visible=True` with distance, dX/dY/dZ, and source-Z height delta. |
| `artifacts/matrix_laz_probe_after.txt` | decoded sampled points, metadata bounds, decoded bounds, and bounds-match result. |
| `artifacts/matrix_las_interesting_probe_after.txt` | decoded 1,065 uncompressed LAS points, RGB availability, and bounds-match result. |
| `artifacts/matrix_smoke_summary_after.txt` | PASS lines for build, C3D/GLB/STL/LAS/LAZ positive samples, expected missing/corrupt failures, and contract text checks. |
| `artifacts/viewer_corrupt_glb_cause_after.txt` | `GLB|loaded=False|source=3D\PublicSamples\Invalid\corrupt.glb` and `ViewerStatus|summary=Smoke GLB failed: Unsupported or corrupt GLB`. |
| `artifacts/viewer_missing_stl_cause_after.txt` | `STL|loaded=False|source=3D\PublicSamples\STL\missing.stl` and `ViewerStatus|summary=Smoke STL failed: Missing STL sample`. |
| `artifacts/viewer_corrupt_stl_cause_after.txt` | `STL|loaded=False|source=3D\PublicSamples\Invalid\corrupt.stl` and `ViewerStatus|summary=Smoke STL failed: Unsupported or corrupt STL`. |
| `artifacts/viewer_corrupt_laz_cause_after.txt` | `LAZ|loaded=False|source=3D\PublicSamples\Invalid\corrupt.laz` and `ViewerStatus|summary=Smoke LAZ/LAS points failed: Unsupported or corrupt LAZ/LAS point decode`. |

## Known Limits

- C3D parsing is still inferred from the two local files.
- Thickness and Warpage are currently byte-identical, so they cannot prove separate inspection behavior yet.
- GLB support is intentionally minimal: mesh geometry, vertex colors, UVs, and embedded texture smoke evidence. It is not full glTF material, skinning, animation, scene-graph, or extension coverage.
- LAS/LAZ support is currently proven on one compressed LAZ sample and one small uncompressed LAS sample. It is not broad point-cloud format coverage.
- The matrix does not cover OBJ, PLY, PCD, E57, STEP, or IGES yet.

## Next Data Priority

Run the next real GLB/STL/LAS/LAZ dataset through `scripts\probe-3d-sample.ps1` first, then add it to this fixed matrix only if it exposes a new loader, camera, picking, measurement, Shell, or contract gap. Unsupported OBJ, PLY, PCD, E57, STEP, or IGES files should be recorded as format-expansion candidates instead of forcing them into the current supported-format matrix.
