# OpenVisionLab 3D Next Session Handoff

Updated: 2026-07-07

## Current State

- Repository: `C:\Git\OpenVisionLab-3D-Studio`
- Status: SharpGL WPF viewer MVP now renders generated geometry, the local C3D height-grid sample, previews/publishes the first C3D height deviation rule, loads that rule from JSON recipe in smoke mode and the visible Open Recipe command, replays/compares that rule through a non-UI recipe runner, and shows the latest persisted Shell recipe comparison evidence.
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
- Point-cloud color modes: `Solid`, `Height`, and `Deviation`.
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
- Minimal source/result/layer/metric/overlay/tool-result contracts are in `src/OpenVisionLab.ThreeD.Core/`.
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

Local sample data now exists:

- `3D/Thickness/Ori_20240116_094414.C3D`
- `3D/Thickness/Ori_20240116_094414.png`
- `3D/Warpage/Ori_20240116_094430.C3D`
- `3D/Warpage/Ori_20240116_094430.png`

The C3D files currently appear to be `int32 width`, `int32 height`, then `float32` height/depth samples. The Thickness and Warpage samples are byte-identical as of the latest check, so do not assume different measurement meaning yet.

Next implementation should stay contract-first now that the first recipe can replay, compare outside the UI, load through a visible command, and display persisted comparison evidence:

1. Keep AvalonDock usage inside `OpenVisionLab.ThreeD.Docking.Controls`, app-level `WPF-UI` usage inside `OpenVisionLab.ThreeD.Shell`, and viewer state/rendering inside `OpenVisionLab.ThreeD.Viewer`.
2. Add a minimal recipe save/edit path so the visible loaded rule can produce a new JSON recipe intentionally.
3. Add a real run-history list only after there are at least two user-created recipe runs to compare.

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

Local sample data notes are recorded in `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md`.

Build and smoke evidence:

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_pick_after_cube.png --smoke-pick cube`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_after.png --smoke-c3d thickness`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_pick_after.png --smoke-c3d thickness --smoke-pick c3d`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_contracts_after.png --smoke-c3d thickness --smoke-contracts artifacts\viewer_contracts_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_tool_result_after.png --smoke-overlay result --smoke-contracts artifacts\viewer_tool_result_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_publish_after.png --smoke-overlay result --smoke-publish-result --smoke-contracts artifacts\viewer_publish_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_height_rule_after.png --smoke-rule height-deviation --smoke-contracts artifacts\viewer_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_height_rule_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_recipe_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_ui_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_recipe_ui_after.txt`
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
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_c3d_height_rule_after.txt --expect-status Fail`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_recipe_compare_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_recipe_ui_compare_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_ui_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_ui_compare_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_ui_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_compare_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_height_rule_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_comparison_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_comparison_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_recipe_comparison_after.txt --recipe-comparison-report artifacts\runner_shell_recipe_comparison_after.txt --shell-smoke-screenshot artifacts\shell_recipe_comparison_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json`
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
- Shell recipe comparison closest-before screenshot: `artifacts\shell_recipe_comparison_before.png`
- Shell recipe comparison full-window after screenshot: `artifacts\shell_recipe_comparison_after.png`
- Shell recipe comparison smoke report: `artifacts\shell_recipe_comparison_after.txt`
- Runner C3D height rule report: `artifacts\runner_c3d_height_rule_after.txt`
- Runner-to-viewer compare report: `artifacts\runner_recipe_compare_after.txt`
- Runner-to-viewer visible recipe UI compare report: `artifacts\runner_recipe_ui_compare_after.txt`
- Runner-to-shell compare report: `artifacts\runner_shell_recipe_compare_after.txt`
- Runner-to-shell visible recipe UI compare report: `artifacts\runner_shell_recipe_ui_compare_after.txt`
- Runner-to-shell recipe comparison report: `artifacts\runner_shell_recipe_comparison_after.txt`

## Guardrails

- Do not modify `C:\Git\OpenVisionLab_Dev` without explicit user approval.
- Do not add multiple 3D engines at once.
- Do not build CAD editing before viewer inspection.
- Do not start 3D algorithm work before the viewer completion gate.
- Do not treat OK/NG text as enough; every rule needs metrics and overlay evidence.
- Do not report UI completion without a running-build screenshot or smoke artifact.
