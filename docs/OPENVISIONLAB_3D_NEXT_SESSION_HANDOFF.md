# OpenVisionLab 3D Next Session Handoff

Updated: 2026-07-06

## Current State

- Repository: `C:\Git\OpenVisionLab-3D-Studio`
- Status: SharpGL WPF viewer MVP now renders a generated cube and generated point cloud.
- Reference repo checked: `C:\Git\OpenVisionLab_Dev`
- App project: `src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj`
- Solution: `OpenVisionLab.ThreeDStudio.slnx`

## Immediate Priority

Complete the 3D Viewer before starting 3D algorithm development. Treat the viewer as a 3D vision inspection workbench, not as a generic model viewer.

Completed in the first implementation slice:

- WPF/.NET solution and app project.
- SharpGL WPF dependency.
- Generated unit cube render.
- Generated point-cloud render.
- Point-cloud color modes: `Solid`, `Height`, and `Deviation`.
- Orbit/pan/zoom/reset/fit-all/fit-selection camera controls.
- Cube picking and coordinate status.
- Viewer-only selection states and overlays: point, box ROI, section plane.
- Measurement overlay.
- Screenshot smoke commands for point, box ROI, and section-plane selection scenes.
- MVVM target recorded in `AGENTS.md`; durable shell state is in `MainWindowViewModel`.

Local sample data now exists:

- `3D/Thickness/Ori_20240116_094414.C3D`
- `3D/Thickness/Ori_20240116_094414.png`
- `3D/Warpage/Ori_20240116_094430.C3D`
- `3D/Warpage/Ori_20240116_094430.png`

The C3D files currently appear to be `int32 width`, `int32 height`, then `float32` height/depth samples. The Thickness and Warpage samples are byte-identical as of the latest check, so do not assume different measurement meaning yet.

Next implementation should stay viewer-only and complete the viewer gate:

1. Render reusable measurement/result overlay primitives that future tools can reuse.
2. Move reusable camera/picking math out of `MainWindow.xaml.cs` when the next viewer feature needs it.
3. Add a minimal C3D height-grid viewer path only after generated point-cloud interaction is stable.

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
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_point.png --smoke-selection point`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_box.png --smoke-selection box`
- `dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_section.png --smoke-selection section`
- Before screenshot: `artifacts\viewer_selection_before.png`
- Point selection after screenshot: `artifacts\viewer_selection_after_point.png`
- Box ROI after screenshot: `artifacts\viewer_selection_after_box.png`
- Section plane after screenshot: `artifacts\viewer_selection_after_section.png`

## Guardrails

- Do not modify `C:\Git\OpenVisionLab_Dev` without explicit user approval.
- Do not add multiple 3D engines at once.
- Do not build CAD editing before viewer inspection.
- Do not start 3D algorithm work before the viewer completion gate.
- Do not treat OK/NG text as enough; every rule needs metrics and overlay evidence.
- Do not report UI completion without a running-build screenshot or smoke artifact.
