# OpenVisionLab 3D Next Session Handoff

Updated: 2026-07-08

## Current State

- Repository: `C:\Git\OpenVisionLab-3D-Studio`
- Status: SharpGL WPF viewer MVP now renders generated geometry, the local C3D height-grid sample, the public `Box.glb` mesh sample, the public `BoxVertexColors.glb` vertex-color mesh sample, the public `BoxTextured.glb` embedded-texture mesh sample, the public `Avocado.glb` realistic non-box textured mesh sample, the public `xyzrgb_manuscript.laz` compressed point-cloud sample, and the public `interesting.las` small uncompressed point-cloud sample in standalone Viewer and Shell; the Viewer maps LAS/LAZ source coordinates into a local viewer origin while preserving source coordinates in contracts and measurement summaries; the Viewer and Shell smoke paths can fit LAZ/LAS camera distance from point-cloud bounds, switch LAZ/LAS point-cloud RGB/height color modes, show point-cloud height color legend/range evidence, apply Fast/Balanced/Detailed render-density sampling to LAZ/LAS point clouds, record LAZ/LAS load time plus sampling ratio telemetry, pick a GLB triangle surface with triangle-index/normal metadata and visible surface-normal overlay, measure GLB two-point distance/model-Y height on the `Avocado.glb` non-box mesh, pick a sampled LAZ/LAS point, measure two sampled LAZ/LAS points with source/viewer coordinates, RGB, distance, dX/dY/dZ, and source-Z height delta in the HUD contract, publish that LAZ/LAS two-point measurement as a separate preview/result layer with metrics and overlays, edit distance/source-Z height acceptance parameters in Tool / Inspector, save those LAZ/LAS acceptance values to recipe JSON, reopen the saved LAZ/LAS recipe in Viewer/Shell, and replay the saved recipe through Runner; Shell Data/Layers, Tool/Inspector, and Linked View now mirror active LAZ/LAS and GLB viewer context instead of showing C3D-only context; the headless Runner proves decoded LAZ/LAS XYZ/RGB points and bounds matching, replays the LAZ/LAS two-point measurement recipe against the Viewer publish contract, applies distance/source-Z height acceptance tolerances for Pass/Fail reports, and Viewer/Shell Tool / Inspector shows LAZ/LAS acceptance state backed by smoke contract output; previews/publishes the first C3D height deviation rule; shows the rule with a deviation color scale/tolerance legend; exposes point size/render-density controls; loads that rule from JSON recipe in smoke mode and the visible Open Recipe command; saves edited tolerance/source/transform/ROI state as a new JSON recipe; exposes numeric recipe-owned transform/ROI edit controls in both standalone Viewer and Shell Tool / Inspector; provides a minimal `Align From ROI` workflow that translates the aligned coordinate frame from the current left/right ROI pair; blocks invalid overlapped ROI recipes with visible validation warnings; displays linked Height Map and Section/Profile views for the C3D sample; exposes a Viewer-internal coordinate/measurement/performance HUD with two-point distance, height delta, interactive ROI step-height comparison, and transform/alignment state; replays/compares that rule and recipe-owned ROI/alignment state through a non-UI recipe runner; shows C3D and LAZ/LAS runner/UI comparison evidence and a minimal key-metric run-history row inside the first Shell workbench layout skeleton; Core now owns shared contract-line formatting used by Viewer and Runner evidence; and has a Windows GitHub Actions CI build with headless runner smoke.
- Reference repo checked: `C:\Git\OpenVisionLab_Dev`
- App project: `src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj`
- Solution: `OpenVisionLab.ThreeDStudio.slnx`
- Architecture note: the SharpGL viewer should remain a separate 3D viewer project/library. The future main workspace should host it through a docking shell patterned after Dev's `Library\OpenVisionLab.Docking.Controls`.
- Migration note: .NET 10 is the intended product direction, but framework migration should be verified separately from feature work.

## Immediate Priority

Complete the 3D Viewer before starting 3D algorithm development. Treat the viewer as a 3D vision inspection workbench, not as a generic model viewer.

Completed in the first implementation slice:

- WPF/.NET solution and app project.
- SharpGL WPF dependency.
- Generated unit cube render.
- Generated point-cloud render.
- Local `3D/Thickness` C3D sample render as a downsampled height-grid point cloud.
- Public `3D/PublicSamples/glTF/Box.glb` render as the first external GLB mesh import baseline.
- Public `3D/PublicSamples/glTF/BoxVertexColors.glb` render with GLB `COLOR_0` vertex colors as the second external GLB import baseline.
- Public `3D/PublicSamples/glTF/BoxTextured.glb` render with GLB `TEXCOORD_0` and embedded PNG base-color texture as the third external GLB import baseline.
- Public `3D/PublicSamples/glTF/Avocado.glb` render as the first realistic non-box textured mesh import baseline, with fit camera distance, triangle-surface pick, triangle-index/normal metadata, visible surface-normal overlay, and two-point distance/model-Y height smoke evidence.
- Local generated `3D/PublicSamples/STL/Tetrahedron.stl` renders through the imported-mesh path, with STL contract output, bounds, triangle-surface pick, surface-normal overlay, Viewer/Shell smoke screenshots, and two-point distance/model-Y height evidence.
- Public `3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz` metadata import with LAS 1.2 header, LASzip compression detection, point-count/bounds contract evidence, and bounds-frame rendering.
- Public `3D/PublicSamples/PointCloud/interesting.las` import with uncompressed LAS 1.2 point format 3, 1,065 XYZ/RGB decoded points, point-count/bounds contract evidence, local viewer-origin coordinate mapping, Viewer/Shell rendering, picking, and two-point measurement.
- Headless `--laz-probe` decodes the public LAZ sample through `OpenVisionLab.ThreeD.Data.LazPointCloud`, records XYZ/RGB sampled points, and verifies decoded bounds match metadata bounds.
- Viewer `--smoke-laz-points` renders decoded LAZ/LAS sampled RGB points and records a `decoder=points-decoded` contract.
- Shell-hosted `--smoke-laz-points` renders decoded LAZ/LAS sampled RGB points inside the docked workbench and records matching Viewer contract evidence.
- Viewer and Shell `--smoke-pick laz` select a rendered sampled LAZ/LAS point, show source/viewer coordinates plus RGB in the Viewer HUD, and record `LAZPick|selected=True`.
- Viewer and Shell `--smoke-laz-points ... --smoke-measure two-point` measure two rendered sampled LAZ/LAS points, draw the measurement overlay, show distance/dX/dY/dZ/source-Z height delta in the Viewer HUD, and record `TwoPoint|visible=True` plus `LAZPick|selected=True`.
- Viewer and Shell `--smoke-laz-points ... --smoke-measure two-point --smoke-publish-result` publish the LAZ/LAS two-point measurement into `layer.preview.laz-two-point-measurement` and `layer.result.laz-two-point-measurement`, with 5 metrics and 2 overlays while preserving the source point cloud.
- `recipes/laz-two-point-measurement.recipe.json` replays that LAZ/LAS two-point measurement in the headless Runner, applies configured distance/source-Z height tolerances, and compares the first metric/status against `artifacts/laz_two_point_publish_after.txt`; `recipes/laz-two-point-measurement-fail.recipe.json` proves the same rule can report Fail.
- Viewer and Shell Tool / Inspector show editable LAZ/LAS acceptance fields for expected distance, distance tolerance, expected source-Z height delta, and height-delta tolerance; smoke contracts record `LAZAcceptanceParameters`, saved JSON carries those values, Viewer/Shell `--smoke-recipe` reopens the saved LAZ/LAS recipe, and Runner replays the saved recipe as Pass.
- Shell `Data & Layers` and `Tool / Inspector` now bind their active source/tool/measurement text to the hosted Viewer ViewModel, so full-shell LAZ/LAS measurement smoke shows `LAZ/LAS Two Point Measurement` in the side panes as well as inside the Viewer HUD.
- Shell `Linked View` now switches away from C3D Height Map/Profile for LAZ and GLB contexts, showing a Point Cloud Sample or Mesh Sample summary plus linked measurement/pick state.
- Point-cloud color modes: `Solid`, `Height`, `RGB`, and `Deviation`; Height mode now shows source-Z range legend evidence for LAZ/LAS point clouds in standalone Viewer and Shell Inspector, non-result LAZ/LAS `Deviation` requests are guarded back to RGB with contract evidence, render density now applies to LAZ/LAS sample budgets as well as C3D point budgets, and LAZ/LAS contracts now record load time, sampling ratio, and sample stride.
- Orbit/pan/zoom/reset/fit-all/fit-selection camera controls.
- Cube picking and coordinate status.
- C3D height-grid point picking and raw height status.
- Viewer-only selection states and overlays: point, box ROI, section plane.
- Measurement overlay.
- Viewer-only result overlay primitives: pass band, profile line, and fail markers.
- Screenshot smoke command for cube picking.
- Screenshot smoke commands for point, box ROI, and section-plane selection scenes.
- Screenshot smoke command for the result overlay scene.
- MVVM target recorded in `AGENTS.md`; durable viewer state is in `OpenVisionLab.ThreeD.Viewer\ViewModels\MainWindowViewModel`, and shell status state is in `ShellMainWindowViewModel`.
- Camera/picking math and measurement/selection/result overlay drawing are split into small `Rendering` support classes.
- Minimal inferred-layout C3D reader is now shared in `src/OpenVisionLab.ThreeD.Data/`.
- GLB and STL loaders now return the shared `ImportedMesh` data model, while `GlbMesh.Load` remains a GLB loader wrapper for compatibility with existing Viewer call sites.
- Viewer UI labels and private viewer state now use `Imported Mesh` for the shared GLB/STL path, and `LAZ/LAS` wording for point-cloud display/acceptance while preserving existing GLB/LAZ contract IDs and smoke CLI names for compatibility.
- Minimal source/result/layer/metric/overlay/tool-result contracts are in `src/OpenVisionLab.ThreeD.Core/`.
- `OpenVisionLab.ThreeD.Core.InspectionContractText` formats shared evidence lines for source entities, entity layers, `ToolResult`, metrics, overlays, and published result entities so Viewer and Runner contract text stays aligned.
- Viewer sample state is wired to core `SourceEntity` and `EntityLayer` contracts without changing rendering behavior.
- Result overlay scene now exposes a viewer-only synthetic `ToolResult` preview with metrics and overlays.
- Synthetic preview can now be explicitly published into a separate `ResultEntity` and `LayerKind.Result` layer.
- Minimal shell/docking boundary is in place: `src/OpenVisionLab.ThreeD.Docking.Controls` owns AvalonDock and `src/OpenVisionLab.ThreeD.Shell` consumes only that wrapper.
- Minimal hostable viewer boundary is in place: `src/OpenVisionLab.ThreeD.Viewer` owns a SharpGL `UserControl`, and Shell hosts it inside the docking document slot.
- Camera/picking/rendering helpers and viewer ViewModel state have moved from `OpenVisionLab.ThreeDStudio` into `OpenVisionLab.ThreeD.Viewer`; C3D parsing moved into `OpenVisionLab.ThreeD.Data` so Runner can reuse it.
- The richer Studio render loop and viewer UI are now hosted by `OpenVisionLab.ThreeD.Viewer`; `OpenVisionLab.ThreeDStudio` is a thin standalone host.
- Dev's WPF UI library boundary is mirrored: `src/OpenVisionLab.ThreeD.Shell` owns `WPF-UI` and app theme resources, while `OpenVisionLab.ThreeD.Viewer` keeps SharpGL/viewer concerns and `OpenVisionLab.ThreeD.Docking.Controls` keeps AvalonDock concerns.
- Shell smoke now delegates to the embedded `OpenVisionThreeDViewerControl`, so Shell can exercise the same C3D/result overlay smoke scenes as the standalone Studio host.
- First rule-tool library is in place: `src/OpenVisionLab.ThreeD.Tools` owns `HeightDeviationRule` and `HeightDeviationRecipe`, and does not depend on WPF or SharpGL.
- The C3D height deviation rule evaluates the local `3D/Thickness` sample from loaded height-grid statistics, produces a failing `ToolResult` with 6 metrics and 3 overlays, and keeps source geometry separate from preview/result layers.
- First recipe and runner path are in place: `recipes/c3d-height-deviation.recipe.json` and `src/OpenVisionLab.ThreeD.Runner` replay the rule outside the UI and write `artifacts/runner_c3d_height_rule_after.txt`.
- Viewer and Shell smoke can load the JSON recipe with `--smoke-recipe`; the shared Viewer toolbar exposes `Open Recipe` for manual JSON selection; Runner can compare its result against UI contracts with `--compare-contract`.
- Shell now has a docked `Recipe Comparison` pane. It reads persisted UI contract and runner report artifacts, shows status/peak-deviation comparison, and can be refreshed without coupling Shell to Runner internals.
- Shell-wide screenshot evidence now uses `--shell-smoke-screenshot`; existing `--smoke-screenshot` remains the embedded Viewer capture path.
- Shell now has a commercial-style workbench layout skeleton through `OpenVisionLab.ThreeD.Docking.Controls`: `Data & Layers`, `3D Inspection View`, `Tool / Inspector`, `Evidence Workbench`, and `Linked View`.
- Shell hosts the Viewer as a center inspection surface with `SidePanelsVisible=false`; the standalone Viewer host keeps the original side panels.
- The C3D height deviation rule smoke scene now uses `Deviation` color mode and displays a viewport legend with pass/fail colors, peak deviation, and tolerance.
- Viewer and Shell `Data & Layers` now expose point size and C3D render-density controls. The smoke path can set them with `--smoke-point-size` and `--smoke-density`.
- Viewer and Shell can edit the C3D height deviation tolerance and save the current source/tolerance as a JSON recipe. The smoke path can set the tolerance with `--smoke-tolerance` and save with `--smoke-save-recipe`.
- The existing Section Plane selection now has a minimal linked C3D center-section profile. Shell `Linked View` shows the profile chart, sample count, and raw-height range.
- Shell `Linked View` now shows a C3D height-map bitmap generated from the same rendered C3D height-grid points, with source dimensions, rendered point count, and raw-height range.
- Viewer now owns an internal measurement HUD that remains visible when hosted in Shell: axis meaning, coordinate frame, selected mode, pick coordinate, two-point distance/dX/dY/dZ/raw-height delta, ROI left/right mean raw-height step comparison, FPS, draw time, and rendered C3D point count.
- Viewer and Shell now expose C3D transform/alignment state: source-to-aligned mapping, translation/rotation/scale, alignment summary, and smoke contract evidence. C3D drawing, picking, two-point measurement, and ROI comparison use the aligned display coordinates while raw-height values remain source data.
- ROI Step Compare now supports interactive ROI center selection: first click sets the left ROI center, second click sets the right ROI center, and a third click starts a new pair. The smoke path can prove this with `--smoke-measure roi-interactive`.
- Height-deviation recipes now persist `transform` and `roiStep` sections. Viewer save/load roundtrips C3D alignment and ROI region definitions, and Runner reports `RecipeTransform`, `RecipeRoiStep`, and `RoiStepResult` from the same recipe.
- GitHub Actions CI is defined in `.github/workflows/ci.yml`; it restores and builds the solution on `windows-latest`, runs the headless C3D recipe runner smoke, and uploads CI artifacts.
- Shell `Evidence Workbench` now has a minimal `History` tab row sourced from the current recipe runner report and UI contract, showing run time, status, a key metric, match state, and report path. C3D uses peak deviation; LAZ/LAS uses distance and source-Z height delta.
- Standalone Viewer and Shell `Tool / Inspector` now expose numeric recipe-owned transform and ROI fields. Edits update the ROI overlay, save to recipe JSON, and replay through Runner with `RecipeTransform`, `RecipeRoiStep`, and `RoiStepResult` evidence.
- Standalone Viewer and Shell `Tool / Inspector` now expose `Align From ROI`. It translates the current transform so the selected left/right ROI pair center and average model height become the aligned reference, updates ROI regions into that new coordinate frame, saves to recipe JSON, and replays through Runner evidence.
- ROI/alignment validation warnings are now visible in Viewer and Shell. Invalid overlapped ROI regions block recipe save; valid ROI/alignment recipes still save and replay through Runner.
- Runner, Viewer, and Shell invalid-input handling is now controlled for smoke use: malformed LAZ sample-count CLI values return usage failure, missing recipe source/rule/measurement fields are reported as validation errors, Viewer/Shell show recipe load failures in `RecipeValidationSummary`, and Shell smoke propagates embedded Viewer smoke failure exit codes.
- Viewer and Shell loader smoke failures are now explicit for missing GLB/STL/LAS/LAZ paths and corrupt GLB/STL/LAZ fixtures: Viewer contract output records attempted source paths, missing/corrupt cause summaries, and `ViewerStatus|summary=...|smokeExitCode=1`; Shell smoke returns failure, switches the active context to the failed GLB/STL/LAZ source, marks the shell header/Evidence Workbench as viewer smoke failure instead of stale recipe-comparison success, and keeps long Linked View failure details reachable through vertical scrolling.
- `scripts/run-data-loading-matrix-smoke.ps1` now runs the current C3D/GLB/STL/LAS/LAZ data loading matrix, expected missing/corrupt loader failures, Shell positive/failure smokes, and contract text checks as one repeatable local command.
- `3D/PublicSamples/Invalid/corrupt.glb`, `3D/PublicSamples/Invalid/corrupt.stl`, and `3D/PublicSamples/Invalid/corrupt.laz` are intentional invalid fixtures used to prove corrupted-file loader failure handling in Viewer and Shell smoke.

Local sample data now exists:

- `3D/Thickness/Ori_20240116_094414.C3D`
- `3D/Thickness/Ori_20240116_094414.png`
- `3D/Warpage/Ori_20240116_094430.C3D`
- `3D/Warpage/Ori_20240116_094430.png`
- `3D/PublicSamples/glTF/Box.glb`
- `3D/PublicSamples/glTF/BoxTextured.glb`
- `3D/PublicSamples/glTF/BoxVertexColors.glb`
- `3D/PublicSamples/glTF/Avocado.glb`
- `3D/PublicSamples/STL/Tetrahedron.stl`
- `3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz`
- `3D/PublicSamples/PointCloud/interesting.las`

The C3D files currently appear to be `int32 width`, `int32 height`, then `float32` height/depth samples. The Thickness and Warpage samples are byte-identical as of the latest check, so do not assume different measurement meaning yet.

Public sample source, license, size, and SHA256 details are recorded in `3D/PublicSamples/README.md`.

Current loader/viewer acceptance coverage is tracked in `docs/OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`.
For a new one-off GLB/STL/LAS/LAZ file, start with `scripts\probe-3d-sample.ps1 -SamplePath <path> -ArtifactDir artifacts\probe_<name>_after` before adding it to the fixed matrix; it records GLB/STL pick/measurement evidence, LAS/LAZ runner, pick, height-color, and two-point measurement evidence, plus Shell GLB/STL pick/measurement and Shell LAS/LAZ height-color/measurement screenshots. Use `-RenderDensity` and `-MaxSampledPoints` when a large point cloud needs faster or denser probing. Unsupported common formats write `FORMAT_CANDIDATE` in `probe_summary.txt` so the next loader task is explicit.

Next implementation should stay viewer-first now that the first recipe can replay, compare outside the UI, load through a visible command, save edited tolerance/source/transform/ROI state, save/reopen/replay edited LAZ/LAS acceptance state, display persisted comparison evidence, and sit inside the workbench layout:

1. Keep AvalonDock usage inside `OpenVisionLab.ThreeD.Docking.Controls`, app-level `WPF-UI` usage inside `OpenVisionLab.ThreeD.Shell`, and viewer state/rendering inside `OpenVisionLab.ThreeD.Viewer`.
2. Run the next real 3D datasets through the current data-loading matrix, then let the first concrete loader/viewer gap drive the next View -> ViewModel -> Model change.
3. Add a small contract parser/checker only if the next comparison path needs more than the current Shell/Runner text parsing.
4. Decide whether the next alignment step needs rotation/plane fitting only after more real 3D samples expose a concrete need.

## Remaining Project Priority

After the viewer completion gate, define and implement the 3D core contracts:

- source entity/layer,
- result entity/layer,
- unit and transform,
- metric,
- overlay,
- tool result,
- recipe step.

Then make recipe save/replay reviewable in the Shell workflow before adding more tools.

## Evidence Already Gathered

Local documents reviewed:

- `C:\Users\user\.codex\AGENTS.md`
- `C:\Git\OpenVisionLab_Dev\AGENTS.md`
- `C:\Git\OpenVisionLab_Dev\docs\CODEBASE_STRUCTURE.md`
- `C:\Git\OpenVisionLab_Dev\docs\OPENVISIONLAB_PLATFORM_DIRECTION.md`
- `C:\Git\OpenVisionLab_Dev\docs\OPENVISIONLAB_PRODUCT_IDENTITY_AND_ROADMAP.md`
- `C:\Git\OpenVisionLab_Dev\docs\OPENVISIONLAB_STATUS_AND_NEXT_STEPS.md`

Web sources are recorded in `docs/OPENVISIONLAB_3D_RESEARCH_NOTES_20260706.md`.

Viewer usage research is recorded in `docs/OPENVISIONLAB_3D_VIEWER_USAGE_RESEARCH_20260706.md`.

Commercial viewer/workbench baseline review is recorded in `docs/OPENVISIONLAB_3D_COMMERCIAL_VIEWER_REVIEW_20260707.md`.

Workbench layout design is recorded in `docs/OPENVISIONLAB_3D_WORKBENCH_LAYOUT_DESIGN_20260707.md`.

Local sample data notes are recorded in `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md`.

Data loading coverage is recorded in `docs/OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`.

Build and smoke evidence:

- `dotnet restore OpenVisionLab.ThreeDStudio.slnx`
- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug`
- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_pick_after_cube.png --smoke-pick cube`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_import_after.png --smoke-glb 3D\PublicSamples\glTF\Box.glb --smoke-contracts artifacts\glb_import_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --laz-probe 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --report artifacts\laz_points_probe_after.txt --max-sampled-points 50000`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_points_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\laz_points_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_pick_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz --smoke-contracts artifacts\laz_pick_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_two_point_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-contracts artifacts\laz_two_point_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_two_point_publish_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result --smoke-contracts artifacts\laz_two_point_publish_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_inspector_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result --smoke-contracts artifacts\laz_acceptance_inspector_viewer_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_edit_save_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\laz_acceptance_edit_save_viewer_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_recipe_reopen_viewer_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\laz_acceptance_recipe_reopen_viewer_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\laz-two-point-measurement.recipe.json --report artifacts\runner_laz_two_point_after.txt --expect-status Pass --compare-contract artifacts\laz_two_point_publish_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\laz-two-point-measurement-fail.recipe.json --report artifacts\runner_laz_two_point_fail_after.txt --expect-status Fail`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_acceptance_edit_save_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_edit_save_viewer_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_acceptance_recipe_reopen_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_run_history_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_points_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\shell_laz_points_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_points_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_pick_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz --smoke-contracts artifacts\shell_laz_pick_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_pick_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_two_point_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-contracts artifacts\shell_laz_two_point_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_two_point_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_two_point_publish_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result --smoke-contracts artifacts\shell_laz_two_point_publish_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_two_point_publish_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_inspector_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result --smoke-contracts artifacts\shell_laz_acceptance_inspector_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_inspector_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_edit_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_shell_laz_two_point_acceptance_contract.recipe.json --smoke-contracts artifacts\shell_laz_acceptance_edit_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_edit_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_shell_laz_two_point_acceptance.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_recipe_reopen_viewer_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\shell_laz_acceptance_recipe_reopen_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_recipe_reopen_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt --recipe-comparison-report artifacts\runner_laz_run_history_after.txt --shell-smoke-screenshot artifacts\shell_laz_run_history_after.png --shell-evidence-tab history --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_context_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-contracts artifacts\shell_laz_context_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_context_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_linked_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-contracts artifacts\shell_laz_linked_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_linked_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_glb_linked_viewer_after.png --smoke-glb 3D\PublicSamples\glTF\Box.glb --smoke-contracts artifacts\shell_glb_linked_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_glb_linked_after.png --smoke-glb 3D\PublicSamples\glTF\Box.glb`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_after.png --smoke-c3d thickness`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_pick_after.png --smoke-c3d thickness --smoke-pick c3d`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_contracts_after.png --smoke-c3d thickness --smoke-contracts artifacts\viewer_contracts_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_tool_result_after.png --smoke-overlay result --smoke-contracts artifacts\viewer_tool_result_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_publish_after.png --smoke-overlay result --smoke-publish-result --smoke-contracts artifacts\viewer_publish_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_height_rule_after.png --smoke-rule height-deviation --smoke-contracts artifacts\viewer_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_height_rule_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_recipe_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_ui_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_recipe_ui_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_deviation_legend_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_deviation_legend_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_render_controls_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-point-size 4 --smoke-density Detailed --smoke-contracts artifacts\viewer_render_controls_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_save_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-tolerance 1500 --smoke-save-recipe artifacts\saved_c3d_height_deviation.recipe.json --smoke-contracts artifacts\viewer_recipe_save_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_section_profile_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-selection section --smoke-contracts artifacts\viewer_section_profile_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_height_map_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_height_map_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_two_point_after.png --smoke-c3d thickness --smoke-measure two-point --smoke-contracts artifacts\viewer_two_point_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_step_after.png --smoke-c3d thickness --smoke-measure roi-step --smoke-contracts artifacts\viewer_roi_step_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_interactive_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-measure roi-interactive --smoke-contracts artifacts\viewer_roi_interactive_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_recipe_save_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-measure roi-interactive --smoke-save-recipe artifacts\saved_roi_alignment.recipe.json --smoke-contracts artifacts\viewer_roi_recipe_save_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_recipe_roundtrip_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-contracts artifacts\viewer_roi_recipe_roundtrip_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_alignment_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-contracts artifacts\viewer_alignment_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_height_rule_publish_after.png --smoke-rule height-deviation --smoke-publish-result --smoke-contracts artifacts\viewer_height_rule_publish_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_point.png --smoke-selection point`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_box.png --smoke-selection box`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_section.png --smoke-selection section`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_result_overlay_after.png --smoke-overlay result`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_c3d_after.png --smoke-c3d thickness --smoke-contracts artifacts\shell_c3d_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_result_overlay_after.png --smoke-overlay result --smoke-contracts artifacts\shell_result_overlay_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_height_rule_after.png --smoke-rule height-deviation --smoke-contracts artifacts\shell_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_recipe_height_rule_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\shell_recipe_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_recipe_ui_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\shell_recipe_ui_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_deviation_legend_viewer_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\shell_deviation_legend_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_render_controls_viewer_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-point-size 4 --smoke-density Detailed --smoke-contracts artifacts\shell_render_controls_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_recipe_save_viewer_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-tolerance 1500 --smoke-save-recipe artifacts\saved_shell_viewer_c3d_height_deviation.recipe.json --smoke-contracts artifacts\shell_recipe_save_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_c3d_height_rule_after.txt --expect-status Fail`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_recipe_compare_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_recipe_ui_compare_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_ui_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_ui_compare_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_ui_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_compare_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_comparison_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_comparison_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_recipe_comparison_after.txt --recipe-comparison-report artifacts\runner_shell_recipe_comparison_after.txt --shell-smoke-screenshot artifacts\shell_recipe_comparison_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_workbench_layout_regression_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_workbench_layout_regression_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_workbench_layout_viewer_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\shell_workbench_layout_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_workbench_layout_after.txt --expect-status Fail --compare-contract artifacts\shell_workbench_layout_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_deviation_legend_after.txt --expect-status Fail --compare-contract artifacts\shell_deviation_legend_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_render_controls_after.txt --expect-status Fail --compare-contract artifacts\shell_render_controls_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_height_deviation.recipe.json --report artifacts\runner_recipe_save_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_save_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_shell_c3d_height_deviation.recipe.json --report artifacts\runner_shell_recipe_save_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_save_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment.recipe.json --report artifacts\runner_roi_alignment_recipe_after.txt --expect-status Fail`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment.recipe.json --report artifacts\runner_roi_alignment_recipe_compare_after.txt --expect-status Fail --compare-contract artifacts\viewer_roi_recipe_roundtrip_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\ci\runner_c3d_height_rule.txt --expect-status Fail`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_workbench_layout_after.txt --recipe-comparison-report artifacts\runner_shell_workbench_layout_after.txt --shell-smoke-screenshot artifacts\shell_workbench_layout_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_deviation_legend_after.txt --recipe-comparison-report artifacts\runner_shell_deviation_legend_after.txt --shell-smoke-screenshot artifacts\shell_color_legend_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_render_controls_after.txt --recipe-comparison-report artifacts\runner_shell_render_controls_after.txt --shell-smoke-screenshot artifacts\shell_render_controls_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-point-size 4 --smoke-density Detailed`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\viewer_recipe_save_after.txt --recipe-comparison-report artifacts\runner_recipe_save_after.txt --shell-smoke-screenshot artifacts\shell_recipe_save_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-tolerance 1500 --smoke-save-recipe artifacts\saved_shell_c3d_height_deviation.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\viewer_section_profile_after.txt --recipe-comparison-report artifacts\runner_recipe_save_after.txt --shell-smoke-screenshot artifacts\shell_section_profile_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-selection section`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\viewer_height_map_after.txt --recipe-comparison-report artifacts\runner_recipe_save_after.txt --shell-smoke-screenshot artifacts\shell_height_map_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_viewer_internal_hud_after.png --smoke-measure two-point`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_step_after.png --smoke-measure roi-step`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_interactive_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-measure roi-interactive`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_recipe_roundtrip_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_alignment_after.png --smoke-c3d thickness --smoke-alignment offset`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_parameter_edit_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-edit-parameters roi-align --smoke-save-recipe artifacts\saved_roi_alignment_edited.recipe.json --smoke-contracts artifacts\viewer_recipe_parameter_edit_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_recipe_parameter_edit_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-edit-parameters roi-align`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment_edited.recipe.json --report artifacts\runner_recipe_parameter_edit_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_parameter_edit_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_interactive_alignment_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi --smoke-save-recipe artifacts\saved_roi_alignment_auto.recipe.json --smoke-contracts artifacts\viewer_interactive_alignment_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_interactive_alignment_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment_auto.recipe.json --report artifacts\runner_interactive_alignment_after.txt --expect-status Fail --compare-contract artifacts\viewer_interactive_alignment_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_validation_valid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi --smoke-save-recipe artifacts\saved_roi_validation_valid.recipe.json --smoke-contracts artifacts\viewer_roi_validation_valid_after.txt`
- `$invalidPath = 'artifacts\saved_roi_validation_invalid.recipe.json'; if (Test-Path $invalidPath) { Remove-Item -LiteralPath $invalidPath -Force }; dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_validation_invalid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-invalid-roi overlap --smoke-save-recipe $invalidPath --smoke-contracts artifacts\viewer_roi_validation_invalid_after.txt; if ($LASTEXITCODE -ne 1) { exit 1 }; if (Test-Path $invalidPath) { exit 1 }`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_validation_invalid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-invalid-roi overlap`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_validation_valid.recipe.json --report artifacts\runner_roi_validation_valid_after.txt --expect-status Fail --compare-contract artifacts\viewer_roi_validation_valid_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_run_history_after.txt --expect-status Fail --compare-contract artifacts\viewer_height_map_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\viewer_height_map_after.txt --recipe-comparison-report artifacts\runner_run_history_after.txt --shell-smoke-screenshot artifacts\shell_run_history_after.png --shell-evidence-tab history --smoke-recipe recipes\c3d-height-deviation.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --laz-probe 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --report artifacts\runner_input_guard_invalid_int_after.txt --max-sampled-points not-a-number` returns usage failure with `--max-sampled-points must be an integer.`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_input_guard_missing_source_after.png --smoke-recipe artifacts\runner_input_guard_missing_source.recipe.json --smoke-contracts artifacts\viewer_input_guard_missing_source_after.txt` returns smoke failure and records `RecipeValidation|summary=Smoke recipe failed: Recipe source is required.`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_input_guard_missing_source_visible_after.png --smoke-recipe artifacts\runner_input_guard_missing_source.recipe.json` returns smoke failure and shows the recipe validation error in Tool / Inspector.
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_missing_glb_cause_after.png --smoke-glb 3D\PublicSamples\glTF\missing.glb --smoke-contracts artifacts\viewer_missing_glb_cause_after.txt` returns smoke failure and records `GLB|loaded=False|source=3D\PublicSamples\glTF\missing.glb|summary=Missing GLB sample: 3D\PublicSamples\glTF\missing.glb`.
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_missing_laz_cause_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\missing.laz --smoke-contracts artifacts\viewer_missing_laz_cause_after.txt` returns smoke failure and records `LAZ|loaded=False|source=3D\PublicSamples\PointCloud\missing.laz|summary=Missing LAZ/LAS sample: 3D\PublicSamples\PointCloud\missing.laz`.
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_missing_glb_cause_after.png --smoke-glb 3D\PublicSamples\glTF\missing.glb` returns smoke failure and shows the loader failure in Tool / Inspector.
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_corrupt_glb_cause_after.png --smoke-glb 3D\PublicSamples\Invalid\corrupt.glb --smoke-contracts artifacts\viewer_corrupt_glb_cause_after.txt` returns smoke failure and records `GLB|loaded=False|source=3D\PublicSamples\Invalid\corrupt.glb|summary=Unsupported or corrupt GLB`.
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_corrupt_laz_cause_after.png --smoke-laz-points 3D\PublicSamples\Invalid\corrupt.laz --smoke-contracts artifacts\viewer_corrupt_laz_cause_after.txt` returns smoke failure and records `LAZ|loaded=False|source=3D\PublicSamples\Invalid\corrupt.laz|summary=Unsupported or corrupt LAZ/LAS point decode`.
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_corrupt_glb_cause_after.png --smoke-glb 3D\PublicSamples\Invalid\corrupt.glb` returns smoke failure and shows the corrupted GLB loader failure in Tool / Inspector.
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_linked_failure_clip_after.png --smoke-laz-points 3D\PublicSamples\Invalid\corrupt.laz` returns smoke failure and shows the Linked View scroll region for long corrupt LAZ failure details.
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_linked_valid_laz_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz` returns success and preserves normal LAZ linked context.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1` returns success and writes `artifacts\matrix_smoke_summary_after.txt` with PASS lines for build, positive sample loads, expected loader failures, and contract checks. Rechecked after LAZ/LAS wording cleanup on 2026-07-08 02:54 KST with `-SkipBuild`.
- Before screenshot: `artifacts\viewer_selection_before.png`
- Cube picking after screenshot: `artifacts\viewer_pick_after_cube.png`
- C3D height-grid after screenshot: `artifacts\viewer_c3d_after.png`
- C3D picking after screenshot: `artifacts\viewer_c3d_pick_after.png`
- Core contract smoke report: `artifacts\viewer_contracts_after.txt`
- ToolResult preview smoke report: `artifacts\viewer_tool_result_after.txt`
- ToolResult publish smoke report: `artifacts\viewer_publish_after.txt`
- C3D height rule before screenshot: `artifacts\viewer_height_rule_before.png`
- C3D height rule after screenshot: `artifacts\viewer_height_rule_after.png`
- C3D height rule smoke report: `artifacts\viewer_height_rule_after.txt`
- Viewer recipe-loaded height rule screenshot: `artifacts\viewer_recipe_height_rule_after.png`
- Viewer recipe-loaded height rule smoke report: `artifacts\viewer_recipe_height_rule_after.txt`
- Viewer visible recipe UI before screenshot: `artifacts\viewer_recipe_ui_before.png`
- Viewer visible recipe UI after screenshot: `artifacts\viewer_recipe_ui_after.png`
- Viewer visible recipe UI smoke report: `artifacts\viewer_recipe_ui_after.txt`
- Viewer deviation legend after screenshot: `artifacts\viewer_deviation_legend_after.png`
- Viewer deviation legend smoke report: `artifacts\viewer_deviation_legend_after.txt`
- Viewer render controls after screenshot: `artifacts\viewer_render_controls_after.png`
- Viewer render controls smoke report: `artifacts\viewer_render_controls_after.txt`
- Viewer recipe save after screenshot: `artifacts\viewer_recipe_save_after.png`
- Viewer recipe save smoke report: `artifacts\viewer_recipe_save_after.txt`
- Saved Viewer recipe: `artifacts\saved_c3d_height_deviation.recipe.json`
- Viewer section/profile after screenshot: `artifacts\viewer_section_profile_after.png`
- Viewer section/profile smoke report: `artifacts\viewer_section_profile_after.txt`
- Viewer height-map after screenshot: `artifacts\viewer_height_map_after.png`
- Viewer height-map smoke report: `artifacts\viewer_height_map_after.txt`
- Viewer two-point closest-before screenshot: `artifacts\viewer_two_point_before.png`
- Viewer two-point after screenshot: `artifacts\viewer_two_point_after.png`
- Viewer two-point smoke report: `artifacts\viewer_two_point_after.txt`
- Viewer ROI step-height after screenshot: `artifacts\viewer_roi_step_after.png`
- Viewer ROI step-height smoke report: `artifacts\viewer_roi_step_after.txt`
- Viewer interactive ROI before screenshot: `artifacts\viewer_roi_interactive_before.png`
- Viewer interactive ROI after screenshot: `artifacts\viewer_roi_interactive_after.png`
- Viewer interactive ROI smoke report: `artifacts\viewer_roi_interactive_after.txt`
- Viewer transform/alignment before screenshot: `artifacts\viewer_transform_before.png`
- Viewer transform/alignment after screenshot: `artifacts\viewer_alignment_after.png`
- Viewer transform/alignment smoke report: `artifacts\viewer_alignment_after.txt`
- Viewer ROI/alignment recipe save screenshot: `artifacts\viewer_roi_recipe_save_after.png`
- Viewer ROI/alignment recipe save smoke report: `artifacts\viewer_roi_recipe_save_after.txt`
- Saved ROI/alignment recipe: `artifacts\saved_roi_alignment.recipe.json`
- Viewer ROI/alignment recipe roundtrip screenshot: `artifacts\viewer_roi_recipe_roundtrip_after.png`
- Viewer ROI/alignment recipe roundtrip smoke report: `artifacts\viewer_roi_recipe_roundtrip_after.txt`
- Shell ROI/alignment recipe roundtrip screenshot: `artifacts\shell_roi_recipe_roundtrip_after.png`
- C3D height rule publish screenshot: `artifacts\viewer_height_rule_publish_after.png`
- C3D height rule publish smoke report: `artifacts\viewer_height_rule_publish_after.txt`
- Point selection after screenshot: `artifacts\viewer_selection_after_point.png`
- Box ROI after screenshot: `artifacts\viewer_selection_after_box.png`
- Section plane after screenshot: `artifacts\viewer_selection_after_section.png`
- Result overlay after screenshot: `artifacts\viewer_result_overlay_after.png`
- Shell C3D after screenshot: `artifacts\shell_c3d_after.png`
- Shell C3D smoke report: `artifacts\shell_c3d_after.txt`
- Shell result overlay after screenshot: `artifacts\shell_result_overlay_after.png`
- Shell result overlay smoke report: `artifacts\shell_result_overlay_after.txt`
- Shell C3D height rule after screenshot: `artifacts\shell_height_rule_after.png`
- Shell C3D height rule smoke report: `artifacts\shell_height_rule_after.txt`
- Shell recipe-loaded height rule screenshot: `artifacts\shell_recipe_height_rule_after.png`
- Shell recipe-loaded height rule smoke report: `artifacts\shell_recipe_height_rule_after.txt`
- Shell visible recipe UI after screenshot: `artifacts\shell_recipe_ui_after.png`
- Shell visible recipe UI smoke report: `artifacts\shell_recipe_ui_after.txt`
- Shell color legend before screenshot: `artifacts\shell_color_legend_before.png`
- Shell color legend after screenshot: `artifacts\shell_color_legend_after.png`
- Shell embedded Viewer deviation legend screenshot: `artifacts\shell_deviation_legend_viewer_after.png`
- Shell deviation legend smoke report: `artifacts\shell_deviation_legend_after.txt`
- Shell render controls before screenshot: `artifacts\shell_render_controls_before.png`
- Shell render controls after screenshot: `artifacts\shell_render_controls_after.png`
- Shell embedded Viewer render controls screenshot: `artifacts\shell_render_controls_viewer_after.png`
- Shell render controls smoke report: `artifacts\shell_render_controls_after.txt`
- Shell recipe save before screenshot: `artifacts\shell_recipe_save_before.png`
- Shell recipe save after screenshot: `artifacts\shell_recipe_save_after.png`
- Shell embedded Viewer recipe save screenshot: `artifacts\shell_recipe_save_viewer_after.png`
- Shell recipe save smoke report: `artifacts\shell_recipe_save_after.txt`
- Saved Shell recipe: `artifacts\saved_shell_c3d_height_deviation.recipe.json`
- Shell section/profile after screenshot: `artifacts\shell_section_profile_after.png`
- Shell height-map after screenshot: `artifacts\shell_height_map_after.png`
- Shell-hosted Viewer internal HUD screenshot: `artifacts\shell_viewer_internal_hud_after.png`
- Shell-hosted ROI step-height screenshot: `artifacts\shell_roi_step_after.png`
- Shell-hosted interactive ROI screenshot: `artifacts\shell_roi_interactive_after.png`
- Shell-hosted transform/alignment screenshot: `artifacts\shell_alignment_after.png`
- Shell recipe comparison closest-before screenshot: `artifacts\shell_recipe_comparison_before.png`
- Shell recipe comparison full-window after screenshot: `artifacts\shell_recipe_comparison_after.png`
- Shell recipe comparison smoke report: `artifacts\shell_recipe_comparison_after.txt`
- Shell workbench layout before screenshot: `artifacts\shell_workbench_layout_before.png`
- Shell workbench layout after screenshot: `artifacts\shell_workbench_layout_after.png`
- Viewer workbench layout regression screenshot: `artifacts\viewer_workbench_layout_regression_after.png`
- Viewer workbench layout regression smoke report: `artifacts\viewer_workbench_layout_regression_after.txt`
- Shell workbench embedded Viewer screenshot: `artifacts\shell_workbench_layout_viewer_after.png`
- Shell workbench smoke report: `artifacts\shell_workbench_layout_after.txt`
- Runner C3D height rule report: `artifacts\runner_c3d_height_rule_after.txt`
- Runner-to-viewer compare report: `artifacts\runner_recipe_compare_after.txt`
- Runner-to-viewer visible recipe UI compare report: `artifacts\runner_recipe_ui_compare_after.txt`
- Runner-to-shell compare report: `artifacts\runner_shell_recipe_compare_after.txt`
- Runner-to-shell visible recipe UI compare report: `artifacts\runner_shell_recipe_ui_compare_after.txt`
- Runner-to-shell recipe comparison report: `artifacts\runner_shell_recipe_comparison_after.txt`
- Runner-to-shell workbench layout compare report: `artifacts\runner_shell_workbench_layout_after.txt`
- Runner-to-shell deviation legend compare report: `artifacts\runner_shell_deviation_legend_after.txt`
- Runner-to-shell render controls compare report: `artifacts\runner_shell_render_controls_after.txt`
- Runner saved Viewer recipe report: `artifacts\runner_recipe_save_after.txt`
- Runner saved Shell recipe report: `artifacts\runner_shell_recipe_save_after.txt`
- Runner ROI/alignment recipe report: `artifacts\runner_roi_alignment_recipe_after.txt`
- Runner ROI/alignment recipe compare report: `artifacts\runner_roi_alignment_recipe_compare_after.txt`
- Viewer recipe parameter edit before screenshot: `artifacts\viewer_recipe_parameter_edit_before.png`
- Viewer recipe parameter edit after screenshot: `artifacts\viewer_recipe_parameter_edit_after.png`
- Shell recipe parameter edit before screenshot: `artifacts\shell_recipe_parameter_edit_before.png`
- Shell recipe parameter edit after screenshot: `artifacts\shell_recipe_parameter_edit_after.png`
- Edited ROI/alignment recipe: `artifacts\saved_roi_alignment_edited.recipe.json`
- Viewer recipe parameter edit contract: `artifacts\viewer_recipe_parameter_edit_after.txt`
- Runner recipe parameter edit report: `artifacts\runner_recipe_parameter_edit_after.txt`
- Viewer interactive alignment before screenshot: `artifacts\viewer_interactive_alignment_before.png`
- Viewer interactive alignment after screenshot: `artifacts\viewer_interactive_alignment_after.png`
- Shell interactive alignment before screenshot: `artifacts\shell_interactive_alignment_before.png`
- Shell interactive alignment after screenshot: `artifacts\shell_interactive_alignment_after.png`
- Saved interactive alignment recipe: `artifacts\saved_roi_alignment_auto.recipe.json`
- Viewer interactive alignment contract: `artifacts\viewer_interactive_alignment_after.txt`
- Runner interactive alignment report: `artifacts\runner_interactive_alignment_after.txt`
- Viewer ROI validation before screenshot: `artifacts\viewer_roi_validation_before.png`
- Shell ROI validation before screenshot: `artifacts\shell_roi_validation_before.png`
- Viewer ROI validation valid screenshot: `artifacts\viewer_roi_validation_valid_after.png`
- Viewer ROI validation valid contract: `artifacts\viewer_roi_validation_valid_after.txt`
- Saved ROI validation valid recipe: `artifacts\saved_roi_validation_valid.recipe.json`
- Runner ROI validation valid report: `artifacts\runner_roi_validation_valid_after.txt`
- Viewer ROI validation invalid screenshot: `artifacts\viewer_roi_validation_invalid_after.png`
- Viewer ROI validation invalid contract: `artifacts\viewer_roi_validation_invalid_after.txt`
- Shell ROI validation invalid screenshot: `artifacts\shell_roi_validation_invalid_after.png`
- CI runner smoke report: `artifacts\ci\runner_c3d_height_rule.txt`
- LAZ/LAS point decode probe report: `artifacts\laz_points_probe_after.txt`
- LAZ/LAS point render screenshot: `artifacts\laz_points_after.png`
- LAZ/LAS point render contract: `artifacts\laz_points_after.txt`
- LAZ/LAS point pick screenshot: `artifacts\laz_pick_after.png`
- LAZ/LAS point pick contract: `artifacts\laz_pick_after.txt`
- Shell LAZ/LAS point embedded Viewer screenshot: `artifacts\shell_laz_points_viewer_after.png`
- Shell LAZ/LAS point render contract: `artifacts\shell_laz_points_after.txt`
- Shell LAZ/LAS point full workbench screenshot: `artifacts\shell_laz_points_after.png`
- Shell LAZ/LAS point pick embedded Viewer screenshot: `artifacts\shell_laz_pick_viewer_after.png`
- Shell LAZ/LAS point pick contract: `artifacts\shell_laz_pick_after.txt`
- Shell LAZ/LAS point pick full workbench screenshot: `artifacts\shell_laz_pick_after.png`
- LAZ/LAS two-point measurement screenshot: `artifacts\laz_two_point_after.png`
- LAZ/LAS two-point measurement contract: `artifacts\laz_two_point_after.txt`
- LAZ/LAS two-point publish screenshot: `artifacts\laz_two_point_publish_after.png`
- LAZ/LAS two-point publish contract: `artifacts\laz_two_point_publish_after.txt`
- LAZ/LAS acceptance inspector embedded Viewer screenshot: `artifacts\laz_acceptance_inspector_viewer_after.png`
- LAZ/LAS acceptance inspector contract: `artifacts\laz_acceptance_inspector_viewer_after.txt`
- LAZ/LAS acceptance edit/save screenshot: `artifacts\laz_acceptance_edit_save_viewer_after.png`
- LAZ/LAS acceptance edit/save contract: `artifacts\laz_acceptance_edit_save_viewer_after.txt`
- Saved LAZ/LAS acceptance recipe: `artifacts\saved_laz_two_point_acceptance.recipe.json`
- Runner saved LAZ/LAS acceptance report: `artifacts\runner_laz_acceptance_edit_save_after.txt`
- LAZ/LAS acceptance recipe reopen screenshot: `artifacts\laz_acceptance_recipe_reopen_viewer_after.png`
- LAZ/LAS acceptance recipe reopen contract: `artifacts\laz_acceptance_recipe_reopen_viewer_after.txt`
- Runner LAZ/LAS acceptance recipe reopen report: `artifacts\runner_laz_acceptance_recipe_reopen_after.txt`
- Runner LAZ/LAS run-history comparison report: `artifacts\runner_laz_run_history_after.txt`
- LAZ/LAS two-point runner replay report: `artifacts\runner_laz_two_point_after.txt`
- LAZ/LAS two-point runner fail report: `artifacts\runner_laz_two_point_fail_after.txt`
- Shell LAZ/LAS two-point embedded Viewer screenshot: `artifacts\shell_laz_two_point_viewer_after.png`
- Shell LAZ/LAS two-point contract: `artifacts\shell_laz_two_point_after.txt`
- Shell LAZ/LAS two-point full workbench screenshot: `artifacts\shell_laz_two_point_after.png`
- Shell LAZ/LAS two-point publish embedded Viewer screenshot: `artifacts\shell_laz_two_point_publish_viewer_after.png`
- Shell LAZ/LAS two-point publish contract: `artifacts\shell_laz_two_point_publish_after.txt`
- Shell LAZ/LAS two-point publish full workbench screenshot: `artifacts\shell_laz_two_point_publish_after.png`
- Shell LAZ/LAS acceptance inspector embedded Viewer screenshot: `artifacts\shell_laz_acceptance_inspector_viewer_after.png`
- Shell LAZ/LAS acceptance inspector contract: `artifacts\shell_laz_acceptance_inspector_after.txt`
- Shell LAZ/LAS acceptance inspector full workbench screenshot: `artifacts\shell_laz_acceptance_inspector_after.png`
- Shell LAZ/LAS acceptance edit embedded Viewer screenshot: `artifacts\shell_laz_acceptance_edit_viewer_after.png`
- Shell LAZ/LAS acceptance edit contract: `artifacts\shell_laz_acceptance_edit_after.txt`
- Shell LAZ/LAS acceptance edit full workbench screenshot: `artifacts\shell_laz_acceptance_edit_after.png`
- Shell LAZ/LAS acceptance recipe reopen embedded Viewer screenshot: `artifacts\shell_laz_acceptance_recipe_reopen_viewer_after.png`
- Shell LAZ/LAS acceptance recipe reopen contract: `artifacts\shell_laz_acceptance_recipe_reopen_after.txt`
- Shell LAZ/LAS acceptance recipe reopen full workbench screenshot: `artifacts\shell_laz_acceptance_recipe_reopen_after.png`
- Shell LAZ/LAS run-history matched screenshot: `artifacts\shell_laz_run_history_after.png`
- Shell LAZ/LAS active-context embedded Viewer screenshot: `artifacts\shell_laz_context_viewer_after.png`
- Shell LAZ/LAS active-context contract: `artifacts\shell_laz_context_after.txt`
- Shell LAZ/LAS active-context full workbench screenshot: `artifacts\shell_laz_context_after.png`
- Shell LAZ/LAS linked-view embedded Viewer screenshot: `artifacts\shell_laz_linked_viewer_after.png`
- Shell LAZ/LAS linked-view contract: `artifacts\shell_laz_linked_after.txt`
- Shell LAZ/LAS linked-view full workbench screenshot: `artifacts\shell_laz_linked_after.png`
- Shell GLB linked-view embedded Viewer screenshot: `artifacts\shell_glb_linked_viewer_after.png`
- Shell GLB linked-view contract: `artifacts\shell_glb_linked_after.txt`
- Shell GLB linked-view full workbench screenshot: `artifacts\shell_glb_linked_after.png`
- Data loading matrix smoke summary: `artifacts\matrix_smoke_summary_after.txt`
- Runner run-history report: `artifacts\runner_run_history_after.txt`
- Shell run-history closest-before screenshot: `artifacts\shell_run_history_before.png`
- Shell run-history after screenshot: `artifacts\shell_run_history_after.png`
- C3D run-history regression report: `artifacts\runner_c3d_run_history_regression_after.txt`
- C3D run-history regression screenshot: `artifacts\shell_c3d_run_history_regression_after.png`

## Guardrails

- Do not modify `C:\Git\OpenVisionLab_Dev` without explicit user approval.
- Do not add multiple 3D engines at once.
- Do not build CAD editing before viewer inspection.
- Do not start 3D algorithm work before the viewer completion gate.
- Do not treat OK/NG text as enough; every rule needs metrics and overlay evidence.
- Do not report UI completion without a running-build screenshot or smoke artifact.
