# OpenVisionLab 3D Next Session Handoff

Updated: 2026-07-14

## Current State

- Repository: `C:\Git\OpenVisionLab-3D-Studio`
- Status: SharpGL WPF viewer MVP now renders generated geometry, the local C3D height-grid sample, the public `Box.glb` mesh sample, the public `BoxVertexColors.glb` vertex-color mesh sample, the public `BoxTextured.glb` embedded-texture mesh sample, the public `SimpleInstancing.glb` static `EXT_mesh_gpu_instancing` sample, the public `Avocado.glb` realistic non-box textured mesh sample, the public `ToyCar.glb` larger CC0 textured GLB fixed-matrix sample, the public `3DBenchy.stl` larger public-domain STL fixed-matrix sample, the public `xyzrgb_manuscript.laz` compressed point-cloud sample, and the public `interesting.las` small uncompressed point-cloud sample in standalone Viewer and Shell; the Viewer maps LAS/LAZ source coordinates into a local viewer origin while preserving source coordinates in contracts and measurement summaries; the Viewer and Shell smoke paths can fit LAZ/LAS camera distance from point-cloud bounds, switch LAZ/LAS point-cloud RGB/height color modes, show point-cloud height color legend/range evidence, apply Fast/Balanced/Detailed render-density sampling to LAZ/LAS point clouds, record LAZ/LAS load time plus sampling ratio telemetry, pick a GLB triangle surface with triangle-index/normal metadata and visible surface-normal overlay, measure GLB two-point distance/model-Y height on `SimpleInstancing.glb`, the `Avocado.glb` non-box mesh, and `ToyCar.glb`, functionally load/pick/measure the 225,706-triangle `3DBenchy.stl` STL sample, pick a sampled LAZ/LAS point, measure two sampled LAZ/LAS points with source/viewer coordinates, RGB, distance, dX/dY/dZ, and source-Z height delta in the HUD contract, publish that LAZ/LAS two-point measurement as a separate preview/result layer with metrics and overlays, edit distance/source-Z height acceptance parameters in Tool / Inspector, save those LAZ/LAS acceptance values to recipe JSON, reopen the saved LAZ/LAS recipe in Viewer/Shell, and replay the saved recipe through Runner; Shell Data/Layers, Tool/Inspector, and Linked View now mirror active LAZ/LAS and GLB viewer context instead of showing C3D-only context; the headless Runner proves decoded LAZ/LAS XYZ/RGB points and bounds matching, replays the LAZ/LAS two-point measurement recipe against the Viewer publish contract, applies distance/source-Z height acceptance tolerances for Pass/Fail reports, and Viewer/Shell Tool / Inspector shows LAZ/LAS acceptance state backed by smoke contract output; previews/publishes the first C3D height deviation rule; shows the rule with a deviation color scale/tolerance legend; exposes point size/render-density controls; loads that rule from JSON recipe in smoke mode and the visible Open Recipe command; saves edited tolerance/source/transform/ROI state as a new JSON recipe; exposes numeric recipe-owned transform/ROI edit controls in both standalone Viewer and Shell Tool / Inspector; provides a minimal `Align From ROI` workflow that translates the aligned coordinate frame from the current left/right ROI pair; blocks invalid overlapped ROI recipes with visible validation warnings; displays linked Height Map and Section/Profile views for the C3D sample; exposes a Viewer-internal coordinate/measurement/performance HUD with two-point distance, height delta, interactive ROI step-height comparison, and transform/alignment state; replays/compares that rule and recipe-owned ROI/alignment state through a non-UI recipe runner; shows C3D and LAZ/LAS runner/UI comparison evidence and a minimal key-metric run-history row inside the first Shell workbench layout skeleton; Core now owns shared contract-line formatting used by Viewer and Runner evidence; and has a Windows GitHub Actions CI build with headless runner smoke.
- Reference repo checked: `C:\Git\OpenVisionLab_Dev`
- App project: `src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj`
- Solution: `OpenVisionLab.ThreeDStudio.slnx`
- Architecture note: the SharpGL viewer should remain a separate 3D viewer project/library. The future main workspace should host it through a docking shell patterned after Dev's `Library\OpenVisionLab.Docking.Controls`.
- Migration note: .NET 10 migration passed on 2026-07-12. Core/Data/Tools/Runner target `net10.0`; Viewer/Docking/Shell/app target `net10.0-windows`; SDK selection is governed by `global.json`.
- Viewer deployment boundary: `OpenVisionLab.ThreeD.Viewer` remains a separate WPF DLL project. `scripts/build-viewer-dll.ps1` creates the complete Core/Data/Tools/SharpGL/LASzip dependency bundle and SHA-256 manifest for external WPF hosts; do not distribute the Viewer DLL alone.
- Viewer Host API v1.0: external code-behind uses `IOpenVisionThreeDViewerHost`, immutable `ViewerHostState`, filtered `HostStateChanged` events, and basic host commands. Shell C3D visibility/status bridging no longer subscribes directly to the concrete Viewer ViewModel.
- Shell screenshot evidence now rejects undersized, predominantly black, predominantly white, or low-contrast captures, preserves rejected attempts, retries at most three times, and exits with failure when no valid frame is produced.
- C3D trust note: fixed-sample display-frame fidelity now passes. The local `1301 x 1967` PNG confirms the unflipped row/column orientation, 10/10 synthetic mapping cases pass, all 1,653,562 valid points roundtrip through a point-only PLY with zero .NET XYZ/RGB error, and an independent Python implementation reports maximum coordinate error `2.37e-7` with zero RGB error. `scripts\ply-coordinate-signature.py` records deterministic PLY signatures. A local Open3D 0.19.0 re-save of the sampled PLY preserved `66,212` vertices and RGB with maximum coordinate drift `5e-6` Viewer units under a `1e-5` external ASCII tolerance. Physical scale and commercial metrology parity remain unverified.
- CloudCompare trust closure: stable portable CloudCompare 2.13.2 independently loaded and re-saved the full `1,653,562`-point C3D export with zero global shift, unchanged point order/RGB, and maximum coordinate-component drift `5.00000001e-7` Viewer units. Its own C2C mean/std are `4.91657e-7` / `1.49337e-7`, and selected-cell distance, width, height delta, and signed elevation angle also pass display-frame tolerances. Treat T3 independent renderer/interchange as passed for this fixed sample; physical calibration and licensed metrology comparison remain blocked. See `docs\OPENVISIONLAB_3D_CLOUDCOMPARE_PARITY_20260713.md`.
- C3D trust CI guard: the independent Python step now evaluates stride-aligned cells `(85,1190)` and `(10,995)` in the existing `66,212`-point point-only PLY, so Windows CI covers coordinate/RGB plus point-pair distance, width, height delta, raw-height delta, and signed-angle calculations without generating the 72 MB full-resolution audit file. The recipe-owned CloudCompare audit remains `(84,1190)` and `(7,994)` at full resolution.
- C3D trust CI closure: commit `cebdc8f` passed Windows Actions run `29288595132` on 2026-07-14. The new independent Python point-pair step and every BinaryHost, Shell screenshot-quality, Runner, golden, actual-map, PLY-signature, and upload step succeeded. Artifact `8294167228` is `1,167,597` bytes with digest `sha256:485c6bbcfb0389ed2af2584eb9dfb359365fd95927bcfb3e3b2ccd4342d9b7bc`.
- Viewer validation closure: current-source revalidation on 2026-07-12 passed the solution build with zero warnings/errors and recorded 129 passes with zero failures in `artifacts\viewer_validation_20260712\matrix_smoke_summary_after.txt`. The matrix covers the fixed C3D/GLB/STL/LAS/LAZ samples, pick/measurement/color/density paths, Shell hosting, evidence contracts, and controlled missing/corrupt inputs. C3D detailed display, pick, two-point measurement, independent Python mapping, and Open3D interchange evidence also passed. Treat Viewer Foundation v1 as closed for this fixed scope and preserve it as regression coverage.
- Gap/Flush typed-slice closure: `recipes\c3d-gap-flush.recipe.json` owns two explicit regions, stable step/source/reference IDs, signed aligned-X gap, signed raw-height flush, separate tolerances, and a fixed 140,000-point measurement budget. Viewer Preview/Publish and save/reopen, Runner parity, Shell Steps evidence, and `8/8` analytic/error golden cases pass. The fixed sample reports gap `1.322` model and flush `243.544` raw-height. This is not automatic seam detection or calibrated physical measurement.
- Volume typed-slice closure: `recipes\c3d-volume.recipe.json` owns explicit reference-plane and measurement regions, stable step/source/reference IDs, signed above/below/net integration, tolerance, and a fixed 140,000-point budget. Viewer Preview/Publish and save/reopen, Runner parity, Shell Steps evidence, and `9/9` analytic/error golden cases pass. The fixed sample reports above `0.874`, below `0.972`, and net `-0.098 model^3`. This is uncalibrated display-frame volume, not physical volume or closed-mesh volume.
- Cross-section Dimensions typed-slice closure: `recipes\c3d-cross-section-dimensions.recipe.json` owns exact source row `983` and inclusive columns `200..1100`, stable step/source/reference IDs, aligned-X width and raw-height-range tolerances. Viewer Preview/Publish and save/reopen, Runner parity, Shell Steps evidence, and `9/9` analytic/error golden cases pass. The fixed sample reports `836` valid cells, width `4.247 model`, and raw-height range `1708.232`. This is not automatic feature finding or calibrated physical measurement.
- Durable Run Record v1.1 closure: a current Cross-section replay under `artifacts\run_record_identity_20260712` records product `0.1.0-dev`, Viewer Host API `1.0`, Git commit/tree state, `.NET 10.0.9`, OS, and X64 alongside the existing recipe/source hashes, metrics, overlays, Matched state, and artifact paths. JSON, HTML, and CSV identities agree, and Shell reads both schema `1.0` and `1.1`. This is a one-run baseline, not batch/SPC/database/PDF infrastructure.
- .NET 10 closure: restore/build passes with zero warnings/errors; all six golden suites, SharpGL C3D/textured GLB screenshots, WPF-UI/AvalonDock Shell, LASzip compressed decode, and the 128-check matrix pass. SharpGL.WPF remains a legacy-compatible package boundary rather than a native .NET 10 package.
- Viewer binary-host closure: `samples\OpenVisionLab.ThreeD.Viewer.BinaryHost` contains no `ProjectReference`; `scripts\verify-viewer-dll-host.ps1` builds the published bundle and sample, confirms 12/12 host/runtime outputs and dependency entries, launches the generated EXE directly, and records C3D render/pick screenshot plus contract evidence under `artifacts\viewer-dll-host-direct-20260712`.
- CI closure: Windows Actions run `29195744796` passed on 2026-07-12. The binary-host direct-EXE step and all Runner/golden/C3D map steps succeeded; artifact `openvisionlab-3d-ci-artifacts` (`788,909` bytes) contains the binary-host report, contract, screenshot, and the existing CI evidence.
- Shell screenshot CI closure: local evidence passed on the first attempt with black ratio `0.0609`, white ratio `0.6215`, luminance `0..255`, and `1,024,000` sampled pixels. Windows Actions run `29196380343` then passed BinaryHost, full Shell C3D quality, release identity, Runner/golden/map checks, and uploaded `openvisionlab-3d-ci-artifacts` (`921,351` bytes).
- Release/version policy: `docs\OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md` defines independent product, Host API, Viewer manifest, Run Record, and recipe version rules plus RC gates, tag/artifact conventions, commands, and an evidence template. Product version `0.1.0-rc.1` is published as GitHub prerelease tag `v0.1.0-rc.1` at commit `ac57687`.
- Release identity CI closure: Windows Actions run `29198517611` passed at commit `ac57687`. Its uploaded Viewer manifest and schema `1.1` Run Record agree on product `0.1.0-rc.1`, Host API `1.0`, commit/tree identity, runtime identity, and Cross-section Viewer/Runner `Matched` state. The public Viewer ZIP is `501,880` bytes with SHA-256 `b9a9b6d002f507da63da32934d93bf6e8deaff2d7c1b00ff70a6f36d6b784a83`.
- Public Viewer-bundle host acceptance closure: on 2026-07-13 a fresh GitHub ZIP download matched `SHA256SUMS.txt`. `scripts\verify-viewer-dll-host.ps1 -ViewerBundlePath <extracted-bundle>` enforced all 13 manifest file paths, sizes, and SHA-256 values, built the zero-`ProjectReference` WPF host with zero warnings/errors, confirmed 12/12 outputs, and directly passed C3D render/pick plus screenshot quality on attempt 1 (`blackRatio=0.0045`, `whiteRatio=0.3578`, luminance `0..255`). A 4/4 rejection matrix blocked outside-bundle, missing, wrong-size, and same-size hash-mismatched entries before Host build.
- Viewer Host API consumer closure: the public RC bundle and current-source bundle both pass explicit Host API v1.0 evidence for a C3D state snapshot, nonzero state-change events, `ResetView`/`FitAll`/`FitSelection`, and `SaveRecipe` with valid `c3d-height-deviation` JSON. BinaryHost now returns the WPF application exit code; a controlled missing-recipe smoke records `smokeExitCode=1` and exits with process code `1`.
- Viewer Host API consumer CI closure: Windows Actions run `29216983045` passed at commit `95dd8da`. BinaryHost and every Shell/Runner/golden/map step succeeded; artifact `8266920376` is `1,167,342` bytes with digest `sha256:254145a80071df39f88d4c199372d1c30c64057f6b931062de4c8dfbdc476c16`.
- Registration prototype decision: Open3D `DemoICPPointClouds` is an external alignment golden, not calibrated or nominal/actual evidence. Same-tag Open3D `0.19.0` source commit `1e7b17438687a0b0c1e5a7187321ac7044afe275` now passes both the recovered build and an independent clean single-shot non-GUI Release build/install. The clean run configured in 46.119 seconds and built/installed in 3,387.699 seconds with exit code `0` and no actual error line. Its 873 paths and 88,977,375 bytes match the recovered install; 871 hashes are identical, and the two rebuilt DLLs retain sizes, export contracts, dependencies, and registration behavior despite different PE timestamps and hashes. The source-built three-file probe runtime remains 58,520,064 bytes versus 141,536,768 bytes for the official package. All 33 clean-build robustness runs and all three current `0 -> 1` DemoICP runs match official-binary output exactly; only 5 predeclared robustness outcomes match, so acceptance must guard correspondence count and fitness before RMSE. Both runtimes reject `1 -> 2` because `cloud_bin_2.pcd` contains 771 non-finite normals. A schema-valid 33-component CycloneDX candidate now records the direct clean-build evidence, but unresolved Assimp and prebuilt BoringSSL/MKL/VTK provenance keep the complete manifest gate open. Distribution also remains blocked by final notices, VC/OpenMP clean-host prerequisite evidence, product integration impact, and owner/legal approval; Viewer/Runner parity remains open. `PclNET 0.8.3` remains rejected, and no product dependency, PCD loader, or fixed sample was added. See `docs\OPENVISIONLAB_3D_REGISTRATION_ENGINE_PROTOTYPE_20260713.md`, `docs\OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`, and `docs\OPENVISIONLAB_3D_OPEN3D_SBOM_CANDIDATE_20260713.md`.
- Hardened BinaryHost CI closure: Windows Actions run `29215566528` passed at commit `c50d196`. BinaryHost, Shell screenshot quality, Runner/golden/map checks, actual C3D roundtrip, independent Python mapping, and artifact upload all succeeded; artifact `8266449434` records digest `sha256:230b5607524e668ed47f59d85e08514bace873e631f676bb44a32282d2eb4c65`.
- Viewer screenshot quality gate: standalone Viewer smoke and Shell smoke now use one shared WPF pixel assessment, retain rejected frames, retry at most three times, and fail when no acceptable frame is produced. CI requires an accepted Cross-section Viewer quality report before creating the `Matched` Run Record.

## Immediate Priority

Viewer Foundation v1 passed and was revalidated on 2026-07-12 for the current fixed sample matrix. Preserve its rendering, camera, visibility, picking, selection, overlay, color-mode, hosting, screenshot, and external-interchange contracts as regression coverage. Do not add viewer-only work unless an inspection slice exposes a concrete gap.

The Inspection Recipe v1 baseline passes for five independent typed C3D slices, Durable Run Record v1.1 passes for one real Cross-section replay, the repository runs on .NET 10, and the binary-only external WPF Host, Viewer/Shell screenshot quality, release identity, archive hash, and Viewer/Runner comparison pass locally and in Windows CI. These remain tool-specific single-step recipe families, not a general multi-step executor or metrology certification. Calibration and measured/nominal work remain blocked by missing metadata/data; `v0.1.0-rc.1` is the published Viewer-bundle prerelease, not a stable or full-application release.

Completed in the first implementation slice:

- WPF/.NET solution and app project.
- SharpGL WPF dependency.
- Generated unit cube render.
- Generated point-cloud render.
- Local `3D/Thickness` C3D sample render as a downsampled height-grid point cloud.
- Public `3D/PublicSamples/glTF/Box.glb` render as the first external GLB mesh import baseline.
- Public `3D/PublicSamples/glTF/BoxVertexColors.glb` render with GLB `COLOR_0` vertex colors as the second external GLB import baseline.
- Public `3D/PublicSamples/glTF/BoxTextured.glb` render with GLB `TEXCOORD_0` and embedded PNG base-color texture as the third external GLB import baseline.
- Public `3D/PublicSamples/glTF/SimpleInstancing.glb` now expands static `EXT_mesh_gpu_instancing` into imported mesh geometry, proving 125 cube instances as 3,000 vertices and 1,500 triangles with Viewer/Shell load, pick, and two-point measurement evidence.
- Public `3D/PublicSamples/glTF/Avocado.glb` render as the first realistic non-box textured mesh import baseline, with fit camera distance, triangle-surface pick, triangle-index/normal metadata, visible surface-normal overlay, and two-point distance/model-Y height smoke evidence.
- Public `3D/PublicSamples/glTF/ToyCar.glb` fixed-matrix coverage for larger GLB behavior. Smoke evidence now includes Viewer/Shell load, pick, and two-point measurement with `77,429` vertices, `108,936` triangles, `77,429` UVs, embedded PNG texture evidence, and bounds-fit first-load/measure camera distance `0.350`.
- Local generated `3D/PublicSamples/STL/Tetrahedron.stl` renders through the imported-mesh path, with STL contract output, bounds, triangle-surface pick, surface-normal overlay, Viewer/Shell smoke screenshots, and two-point distance/model-Y height evidence.
- Public `3D/PublicSamples/STL/3DBenchy.stl` fixed-matrix coverage for high-triangle STL behavior. Smoke evidence now includes Viewer/Shell load, pick, and two-point measurement with `677,118` vertices and `225,706` source triangles; Balanced render density displays `56,427` triangles with render stride `4`, bounds-fit first-load camera distance is `170.030`, and the full run completes in about 22 seconds.
- Large imported-mesh samples remain probe-only unless they expose a loader, camera, picking, measurement, Shell, or contract gap not covered by the fixed matrix and the extra routine runtime is justified. `ToyCar.glb` and `3DBenchy.stl` are exceptions now promoted to fixed-matrix coverage.
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
- C3D point-pair distance, XZ planar width, and signed elevation angle acceptance using exact row/column references; Viewer/Shell fields, HUD, endpoint/line overlay, Preview/Publish, recipe roundtrip, Runner parity, and analytic/error golden verification are complete for the fixed sample.
- C3D signed Gap/Flush acceptance using two explicit recipe-owned regions; Viewer/Shell fields, HUD, ROI/gap/flush overlays, Preview/Publish, recipe roundtrip, Runner parity, Shell step row, and analytic/error golden verification are complete for the fixed sample.
- C3D reference-plane Volume using explicit reference and measurement regions; Viewer/Shell fields, HUD, plane/ROI/deviation overlays, Preview/Publish, recipe roundtrip, Runner parity, Shell step row, and analytic/error golden verification are complete for the fixed sample.
- C3D Cross-section Dimensions using an exact source row/range; Viewer/Shell fields, HUD, section overlay/profile, Preview/Publish, recipe roundtrip, Runner parity, Shell step row, and analytic/error golden verification are complete for the fixed sample.
- Viewer-only selection states and overlays: point, box ROI, section plane.
- Measurement overlay.
- Viewer-only result overlay primitives: pass band, profile line, and fail markers.
- Screenshot smoke command for cube picking.
- Screenshot smoke commands for point, box ROI, and section-plane selection scenes.
- Screenshot smoke command for the result overlay scene.
- MVVM target recorded in `AGENTS.md`; durable viewer state is in `OpenVisionLab.ThreeD.Viewer\ViewModels\MainWindowViewModel`, and shell status state is in `ShellMainWindowViewModel`.
- Camera/picking math and measurement/selection/result overlay drawing are split into small `Rendering` support classes.
- Minimal inferred-layout C3D reader is now shared in `src/OpenVisionLab.ThreeD.Data/`.
- GLB and STL loaders now return the shared `ImportedMesh` data model; `GlbMesh.Load` remains a GLB loader wrapper for compatibility with existing Viewer call sites and expands scene nodes plus simple static `EXT_mesh_gpu_instancing` into ordinary mesh vertices/indices.
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
- Viewer now owns an internal measurement HUD that remains visible when hosted in Shell: axis meaning, coordinate frame, selected mode, pick coordinate, two-point distance/dX/dY/dZ/raw-height delta, ROI left/right mean raw-height step comparison, FPS, draw time, and rendered C3D point count. HUD detail rows can be toggled, GLB/STL/LAZ smoke starts compact, source-specific HUD detail rows only show for the active source type, and contract text records `ViewerInternalHud|detailsVisible=...`.
- Viewer and Shell now expose C3D transform/alignment state: source-to-aligned mapping, translation/rotation/scale, alignment summary, and smoke contract evidence. C3D drawing, picking, two-point measurement, and ROI comparison use the aligned display coordinates while raw-height values remain source data.
- ROI Step Compare now supports interactive ROI center selection: first click sets the left ROI center, second click sets the right ROI center, and a third click starts a new pair. The smoke path can prove this with `--smoke-measure roi-interactive`.
- Height-deviation recipes now persist `transform` and `roiStep` sections. Viewer save/load roundtrips C3D alignment and ROI region definitions, and Runner reports `RecipeTransform`, `RecipeRoiStep`, and `RoiStepResult` from the same recipe.
- GitHub Actions CI is defined in `.github/workflows/ci.yml`; it restores and builds the solution on `windows-latest`, runs the headless C3D recipe runner smoke, and uploads CI artifacts.
- Shell `Evidence Workbench` now has a minimal `History` tab row sourced from the current recipe runner report and UI contract, showing run time, status, a key metric, match state, and report path. C3D uses peak deviation; LAZ/LAS uses distance and source-Z height delta.
- Shell `Evidence Workbench` now has a `Run Snapshot` tab that bundles the current runner/UI match state, status, key metric, run time, recipe path, UI contract path, runner report path, and Shell screenshot target so one inspection run can be reviewed without leaving the hosted workbench.
- Shell `Evidence Workbench` Run Snapshot now exposes `UI Contract`, `Runner Report`, and `Screenshot` open actions. The Shell code-behind is only the OS file-open bridge; the ViewModel owns the command state and selected artifact paths.
- Shell `Evidence Workbench` now has a `Steps` tab that summarizes the current inspection execution sequence as `Recipe -> Source -> Viewer preview -> Runner replay -> Evidence compare`, with compact status text visible in the docked workbench and detailed rows available below it.
- Viewer and Shell now expose an explicit `Fit C3D Plane` command and fitted distance-to-plane measurement. `--smoke-measure plane-distance` fits `y = ax + bz + c` through a fixed measurement sample independent of render density, reports normal/RMS/largest orthogonal residual, draws the fitted plane plus projected distance line, mirrors the result in Tool / Inspector, and records `PlaneReference` contract evidence. Three-point/ROI reference selection remains later work.
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
- `3D/PublicSamples/glTF/SimpleInstancing.glb`
- `3D/PublicSamples/glTF/Avocado.glb`
- `3D/PublicSamples/glTF/ToyCar.glb`
- `3D/PublicSamples/STL/Tetrahedron.stl`
- `3D/PublicSamples/STL/3DBenchy.stl`
- `3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz`
- `3D/PublicSamples/PointCloud/interesting.las`

The C3D files currently appear to be `int32 width`, `int32 height`, then `float32` height/depth samples. The Thickness and Warpage samples are byte-identical as of the latest check, so do not assume different measurement meaning yet.

Public sample source, license, size, and SHA256 details are recorded in `3D/PublicSamples/README.md`.

Current loader/viewer acceptance coverage is tracked in `docs/OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`.
For a new one-off GLB/STL/LAS/LAZ file, start with `scripts\probe-3d-sample.ps1 -SamplePath <path> -ArtifactDir artifacts\probe_<name>_after` before adding it to the fixed matrix; it records GLB/STL pick/measurement evidence, LAS/LAZ runner, pick, height-color, and two-point measurement evidence, plus Shell GLB/STL pick/measurement and Shell LAS/LAZ height-color/measurement screenshots. Use `-RenderDensity` and `-MaxSampledPoints` when a large point cloud needs faster or denser probing. Promote a sample only if the data-loading matrix policy says it adds missing coverage; otherwise keep it probe-only. Unsupported common formats write `FORMAT_CANDIDATE` in `probe_summary.txt` so the next loader task is explicit.

Next implementation should stay inspection-workflow-first while preserving the Viewer v1 baseline:

1. Keep AvalonDock usage inside `OpenVisionLab.ThreeD.Docking.Controls`, app-level `WPF-UI` usage inside `OpenVisionLab.ThreeD.Shell`, and viewer state/rendering inside `OpenVisionLab.ThreeD.Viewer`.
2. Preserve the completed plane-flatness, point-pair-dimensions, Gap/Flush, Volume, and Cross-section recipe, parity, screenshot, and analytic/error regression baselines.
3. Obtain C3D X/Z pitch, height scale/offset, units, axis orientation, and calibration identity; add an explicit mapping profile without changing the verified uncalibrated profile silently.
4. Build one measured/nominal comparison slice when a distinct local sample pair is available; otherwise preserve the published `v0.1.0-rc.1` local/remote CI and archive gates.
5. Extract only concrete shared recipe/execution code proven by the completed tools; do not create a speculative graph engine.

## Remaining Project Priority

Obtain the C3D physical mapping/calibration contract using the intake template in `docs/OPENVISIONLAB_3D_MAP_FIDELITY_VALIDATION_20260711.md`, and obtain a genuinely distinct measured/nominal sample pair when available. The two current C3D files remain byte-identical and contain no trailing calibration block or sidecar. Until those prerequisites exist, preserve the published `v0.1.0-rc.1` Viewer/BinaryHost/Viewer-Shell-quality/release-identity/archive-hash/Viewer-Runner gates; do not promote it to stable or replace its assets without explicit approval. Full CAD/GD&T, device/PLC/robot integration, enterprise data management, and AI tuning remain out of scope.

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

Current product target, commercial comparison, capability scorecard, gate decision, and next development priorities are recorded in `docs/OPENVISIONLAB_3D_PRODUCT_TARGET_AND_SELF_EVALUATION_20260711.md`. The 2026-07-10 commercial-gap document is historical context.

Workbench layout design is recorded in `docs/OPENVISIONLAB_3D_WORKBENCH_LAYOUT_DESIGN_20260707.md`.

Local sample data notes are recorded in `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md`.

Data loading coverage is recorded in `docs/OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`.

Build and smoke evidence:

- `dotnet restore OpenVisionLab.ThreeDStudio.slnx`
- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug`
- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_dimensions_after.png --smoke-c3d thickness --smoke-measure dimensions --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --smoke-contracts artifacts\viewer_dimensions_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_dimensions_reopen_after.png --smoke-recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --smoke-contracts artifacts\viewer_dimensions_reopen_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --report artifacts\runner_point_pair_dimensions_after.txt --expect-status Pass --compare-contract artifacts\viewer_dimensions_reopen_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-point-pair-dimensions --report artifacts\point_pair_dimensions_golden_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_dimensions_viewer_after.png --smoke-contracts artifacts\shell_dimensions_after.txt --recipe-comparison-contract artifacts\viewer_dimensions_reopen_after.txt --recipe-comparison-report artifacts\runner_point_pair_dimensions_after.txt --shell-smoke-screenshot artifacts\shell_dimensions_after.png --shell-evidence-tab steps --smoke-recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json`
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
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_plane_distance_after.png --smoke-c3d thickness --smoke-measure plane-distance --smoke-contracts artifacts\viewer_plane_distance_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_plane_fit_after.png --smoke-c3d thickness --smoke-measure plane-distance --smoke-contracts artifacts\viewer_plane_fit_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_plane_fit_tilt_after.png --smoke-c3d thickness --smoke-alignment tilt --smoke-measure plane-distance --smoke-contracts artifacts\viewer_plane_fit_tilt_after.txt`
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
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_run_snapshot_viewer_after.png --smoke-rule height-deviation --smoke-contracts artifacts\shell_run_snapshot_contract_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_run_snapshot_after.txt --expect-status Fail --compare-contract artifacts\shell_run_snapshot_contract_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_run_snapshot_contract_after.txt --recipe-comparison-report artifacts\runner_shell_run_snapshot_after.txt --shell-smoke-screenshot artifacts\shell_run_snapshot_after.png --shell-evidence-tab snapshot --smoke-recipe recipes\c3d-height-deviation.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_run_snapshot_contract_after.txt --recipe-comparison-report artifacts\runner_shell_run_snapshot_after.txt --shell-smoke-screenshot artifacts\shell_evidence_actions_after.png --shell-evidence-tab snapshot --smoke-recipe recipes\c3d-height-deviation.recipe.json`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_run_snapshot_contract_after.txt --recipe-comparison-report artifacts\runner_shell_run_snapshot_after.txt --shell-smoke-screenshot artifacts\shell_steps_after.png --shell-evidence-tab steps --smoke-recipe recipes\c3d-height-deviation.recipe.json`
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
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_plane_distance_viewer_after.png --smoke-c3d thickness --smoke-measure plane-distance --smoke-contracts artifacts\shell_plane_distance_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_plane_distance_after.png --smoke-c3d thickness --smoke-measure plane-distance`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_plane_fit_viewer_after.png --smoke-c3d thickness --smoke-measure plane-distance --smoke-contracts artifacts\shell_plane_fit_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_plane_fit_after.png --smoke-c3d thickness --smoke-measure plane-distance`
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
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1` returns success and writes `artifacts\matrix_smoke_summary_after.txt` with PASS lines for build, positive sample loads, expected loader failures, and contract checks. Latest full artifact run is `artifacts\matrix_after_continue_full\matrix_smoke_summary_after.txt` and previously also includes `artifacts\matrix_smoke_summary_after.txt` from earlier matrix smoke baselines.
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
- Viewer plane-distance after screenshot: `artifacts\viewer_plane_distance_after.png`
- Viewer plane-distance smoke report: `artifacts\viewer_plane_distance_after.txt`
- Viewer fitted-plane closest-before screenshot: `artifacts\viewer_plane_fit_before.png`
- Viewer fitted-plane after screenshot/contract: `artifacts\viewer_plane_fit_after.png`, `artifacts\viewer_plane_fit_after.txt`
- Viewer tilted fitted-plane screenshot/contract: `artifacts\viewer_plane_fit_tilt_after.png`, `artifacts\viewer_plane_fit_tilt_after.txt`
- Viewer fitted-plane render-density stability contracts: `artifacts\viewer_plane_fit_fast_after.txt`, `artifacts\viewer_plane_fit_detailed_after.txt`
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
- Shell Run Snapshot embedded Viewer screenshot: `artifacts\shell_run_snapshot_viewer_after.png`
- Shell Run Snapshot contract: `artifacts\shell_run_snapshot_contract_after.txt`
- Runner Shell Run Snapshot report: `artifacts\runner_shell_run_snapshot_after.txt`
- Shell Run Snapshot full-window screenshot: `artifacts\shell_run_snapshot_after.png`
- Shell Evidence Artifact Actions before screenshot: `artifacts\shell_evidence_actions_before.png`
- Shell Evidence Artifact Actions after screenshot: `artifacts\shell_evidence_actions_after.png`
- Shell inspection Steps full-window screenshot: `artifacts\shell_steps_after.png`
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
- Shell C3D plane-distance embedded Viewer screenshot: `artifacts\shell_plane_distance_viewer_after.png`
- Shell C3D plane-distance contract: `artifacts\shell_plane_distance_after.txt`
- Shell C3D plane-distance full workbench screenshot: `artifacts\shell_plane_distance_after.png`
- Shell C3D fitted-plane closest-before screenshot: `artifacts\shell_plane_fit_before.png`
- Shell C3D fitted-plane embedded Viewer screenshot/contract: `artifacts\shell_plane_fit_viewer_after.png`, `artifacts\shell_plane_fit_after.txt`
- Shell C3D fitted-plane full workbench screenshot: `artifacts\shell_plane_fit_after.png`
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
- Data loading matrix smoke summary: `artifacts\matrix_after_continue_full\matrix_smoke_summary_after.txt`
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
