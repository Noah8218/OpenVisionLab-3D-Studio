# OpenVisionLab 3D Next Session Handoff

Updated: 2026-07-06

## Current State

- Repository: `C:\Git\OpenVisionLab-3D-Studio`
- Status: SharpGL WPF viewer MVP now renders a generated cube, generated point cloud, and local C3D height-grid sample.
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
- MVVM target recorded in `AGENTS.md`; durable shell state is in `MainWindowViewModel`.
- Camera/picking math and measurement/selection/result overlay drawing are split into small `Rendering` support classes.
- Minimal inferred-layout C3D reader is in `src/OpenVisionLab.ThreeDStudio/Data/`.
- Minimal source/result/layer/metric/overlay/tool-result contracts are in `src/OpenVisionLab.ThreeD.Core/`.
- Viewer sample state is wired to core `SourceEntity` and `EntityLayer` contracts without changing rendering behavior.
- Result overlay scene now exposes a viewer-only synthetic `ToolResult` preview with metrics and overlays.
- Synthetic preview can now be explicitly published into a separate `ResultEntity` and `LayerKind.Result` layer.
- Minimal shell/docking boundary is in place: `src/OpenVisionLab.ThreeD.Docking.Controls` owns AvalonDock and `src/OpenVisionLab.ThreeD.Shell` consumes only that wrapper.

Local sample data now exists:

- `3D/Thickness/Ori_20240116_094414.C3D`
- `3D/Thickness/Ori_20240116_094414.png`
- `3D/Warpage/Ori_20240116_094430.C3D`
- `3D/Warpage/Ori_20240116_094430.png`

The C3D files currently appear to be `int32 width`, `int32 height`, then `float32` height/depth samples. The Thickness and Warpage samples are byte-identical as of the latest check, so do not assume different measurement meaning yet.

Next implementation should stay contract-first and prepare the main workspace boundary before large feature work:

1. Extract or wrap the SharpGL viewer as a separately testable viewer module that the Shell can host.
2. Keep AvalonDock usage inside `OpenVisionLab.ThreeD.Docking.Controls`.
3. Then add one sample-backed height/deviation rule that produces a `ToolResult` and publishes only through the explicit `ResultEntity` path.

## Remaining Project Priority

After the viewer completion gate, define and implement the 3D core contracts:

- source entity/layer,
- result entity/layer,
- unit and transform,
- metric,
- overlay,
- tool result,
- recipe step.

Then build one sample-backed rule tool, such as a synthetic distance-to-plane or height-tolerance rule.

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

Local sample data notes are recorded in `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md`.

Build and smoke evidence:

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_pick_after_cube.png --smoke-pick cube`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_after.png --smoke-c3d thickness`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_pick_after.png --smoke-c3d thickness --smoke-pick c3d`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_contracts_after.png --smoke-c3d thickness --smoke-contracts artifacts\viewer_contracts_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_tool_result_after.png --smoke-overlay result --smoke-contracts artifacts\viewer_tool_result_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_publish_after.png --smoke-overlay result --smoke-publish-result --smoke-contracts artifacts\viewer_publish_after.txt`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_point.png --smoke-selection point`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_box.png --smoke-selection box`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_section.png --smoke-selection section`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_result_overlay_after.png --smoke-overlay result`
- `dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_docking_after.png`
- Before screenshot: `artifacts\viewer_selection_before.png`
- Cube picking after screenshot: `artifacts\viewer_pick_after_cube.png`
- C3D height-grid after screenshot: `artifacts\viewer_c3d_after.png`
- C3D picking after screenshot: `artifacts\viewer_c3d_pick_after.png`
- Core contract smoke report: `artifacts\viewer_contracts_after.txt`
- ToolResult preview smoke report: `artifacts\viewer_tool_result_after.txt`
- ToolResult publish smoke report: `artifacts\viewer_publish_after.txt`
- Point selection after screenshot: `artifacts\viewer_selection_after_point.png`
- Box ROI after screenshot: `artifacts\viewer_selection_after_box.png`
- Section plane after screenshot: `artifacts\viewer_selection_after_section.png`
- Result overlay after screenshot: `artifacts\viewer_result_overlay_after.png`
- Shell docking after screenshot: `artifacts\shell_docking_after.png`

## Guardrails

- Do not modify `C:\Git\OpenVisionLab_Dev` without explicit user approval.
- Do not add multiple 3D engines at once.
- Do not build CAD editing before viewer inspection.
- Do not start 3D algorithm work before the viewer completion gate.
- Do not treat OK/NG text as enough; every rule needs metrics and overlay evidence.
- Do not report UI completion without a running-build screenshot or smoke artifact.
