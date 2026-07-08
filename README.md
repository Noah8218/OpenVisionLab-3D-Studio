# OpenVisionLab 3D Studio

[![CI](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml)

OpenVisionLab 3D Studio is an early-stage Windows desktop project for rule-based 3D inspection. The immediate goal is not a full production platform; it is a reliable 3D viewer and inspection workbench for loading local 3D data, measuring geometry, showing result overlays, and replaying repeatable validation recipes.

This repository is under active development and is not production-ready yet.

## 1 Minute Summary

- Product direction: local 3D vision inspection workbench.
- Current focus: SharpGL/WPF viewer foundation before deeper 3D algorithms.
- Current viewer scope: camera control, C3D height-grid rendering, GLB/STL/LAS/LAZ sample rendering and picking, LAZ/LAS two-point distance/height preview/publish result contracts with editable Viewer/Shell acceptance parameters, Shell active-context panes, entity visibility, measurement HUD, two-point and ROI step-height measurement, transform/alignment state, overlays, recipe-owned ROI/alignment edit controls, recipe load/save, and screenshot smoke evidence.
- Current rule scope: first C3D height-deviation recipe, editable persisted transform/ROI parameters, editable LAZ/LAS two-point acceptance save/reopen/replay, headless runner path, Shell Evidence Workbench comparison/history for C3D and point-cloud key metrics, and shared Core evidence formatting for Viewer/Runner contracts.
- Out of early scope: industrial camera acquisition/control, PLC, robot, cloud, deployment management, production database, and full CAD editing.

## Requirements

- Windows
- .NET SDK 8.0.x
- Git

The project owner plans to evaluate .NET 10 later, but the current checked build targets .NET 8.

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
| `3D\PublicSamples\glTF\Avocado.glb` | Public GLB realistic non-box textured mesh import baseline. |
| `3D\PublicSamples\glTF\ToyCar.glb` | Public GLB complex textured car ad-hoc probe sample. |
| `3D\PublicSamples\STL\Tetrahedron.stl` | Local generated STL triangle-mesh import baseline. |
| `3D\PublicSamples\STL\3DBenchy.stl` | Public STL complex real mesh ad-hoc probe sample. |
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
Large mesh probes such as `ToyCar.glb` and `3DBenchy.stl` stay probe-only unless they expose a new loader, camera, picking, measurement, Shell, or contract gap that the fixed matrix does not already cover. The promotion policy is tracked in `docs\OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`.

Viewer screenshot and contract smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_two_point_after.png --smoke-c3d thickness --smoke-measure two-point --smoke-contracts artifacts\viewer_two_point_after.txt
```

Public GLB import smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_import_after.png --smoke-glb 3D\PublicSamples\glTF\Box.glb --smoke-contracts artifacts\glb_import_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_vertex_color_after.png --smoke-glb 3D\PublicSamples\glTF\BoxVertexColors.glb --smoke-contracts artifacts\glb_vertex_color_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_textured_after.png --smoke-glb 3D\PublicSamples\glTF\BoxTextured.glb --smoke-contracts artifacts\glb_textured_after.txt
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

GitHub Actions workflow: `.github\workflows\ci.yml`.

CI currently runs on `windows-latest` and performs:

1. `dotnet restore OpenVisionLab.ThreeDStudio.slnx`
2. `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore`
3. Headless C3D recipe runner smoke
4. CI artifact upload from `artifacts\ci\`

## Release Notes

No packaged binary release is published yet.

Current development snapshot:

- SharpGL/WPF viewer foundation.
- Standalone viewer host and docked shell host.
- C3D height-grid sample rendering and picking.
- Viewer-internal coordinate, measurement, and performance HUD.
- Two-point distance and height-delta measurement smoke.
- ROI step-height comparison smoke.
- Interactive ROI center selection smoke for step-height comparison.
- Viewer and Shell transform/alignment state display with smoke contract evidence.
- Transform and ROI step parameters persisted in recipes and repeated by the headless runner.
- Viewer and Shell expose numeric edit controls for recipe-owned transform and ROI step parameters, with save/replay smoke evidence.
- Viewer and Shell expose a minimal `Align From ROI` workflow that centers the selected ROI pair and reference height in the aligned coordinate frame.
- ROI validation warnings block invalid overlapped ROI recipes from being saved.
- Public `Box.glb` import smoke renders a first external GLB mesh and records vertex/triangle/bounds contract evidence.
- Public `BoxVertexColors.glb` import smoke renders per-vertex colors and records vertex-color contract evidence.
- Public `BoxTextured.glb` import smoke renders embedded PNG base-color texture and records UV/texture contract evidence.
- Public `Avocado.glb` import smoke renders a realistic non-box textured mesh and records mesh/UV/texture, fit camera distance, triangle-surface pick, triangle-index/normal metadata, visible surface-normal overlay, and two-point distance/model-Y height evidence.
- Public `ToyCar.glb` ad-hoc probe renders a larger CC0 textured GLB with bounds-fit camera framing and records Viewer/Shell pick plus two-point measurement evidence without adding it to the fixed matrix yet.
- Public `3DBenchy.stl` ad-hoc probe proves the STL loader can handle a real 225,706-triangle binary STL through Viewer/Shell load, pick, bounds-fit camera framing, and two-point measurement. Imported-mesh render density now samples large mesh display while contracts keep full source triangle counts.
- Large imported-mesh samples stay probe-only by default; promote them to the fixed matrix only when they reveal coverage that the matrix lacks and the added routine runtime is justified.
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

1. Finish the viewer completion gate: reliable display, camera, picking, selection, overlays, color modes, screenshots, and MVVM state separation.
2. Run additional real 3D datasets through the data loading matrix and let the first concrete loader/viewer gap drive the next viewer change.
3. Add a small contract parser/checker for Viewer/Runner evidence only if another comparison path starts duplicating text parsing.
4. Add more rule-based validation tools with UI preview and headless runner coverage.
5. Expand file formats and heavier geometry libraries only after the core inspection loop is verified.

## Known Limitations

- The project is not production-ready.
- Current C3D parsing is inferred from local samples, not an official format contract.
- The current Thickness and Warpage sample files may be byte-identical; do not assume they represent different measurements until new evidence is available.
- Algorithm coverage is intentionally narrow; the viewer is still being completed first.
- ROI/alignment editing is currently an MVP. `Align From ROI` applies translation from the selected ROI pair center/reference height; rotation, plane fitting, snapping, and richer guided warnings are not implemented yet.
- No industrial camera acquisition/control, PLC, robot, cloud, deployment, account, or production database integration exists.
- No packaged installer or binary release exists yet.
- .NET 10 migration is planned as a separate compatibility task, not mixed into current feature work.

## Documentation

- `AGENTS.md`: repository working rules and verification commands.
- `docs\CODEBASE_STRUCTURE.md`: project layout.
- `docs\OPENVISIONLAB_3D_PLATFORM_DIRECTION.md`: product direction and roadmap.
- `docs\OPENVISIONLAB_3D_SAMPLE_DATA.md`: sample inventory and C3D observations.
- `docs\OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`: loader/viewer evidence matrix for current C3D, GLB, STL, LAS, and LAZ samples.
- `docs\OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`: current engineering handoff.
