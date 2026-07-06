# OpenVisionLab 3D Studio

OpenVisionLab 3D Studio is the starting repository for a rule-based 3D inspection workbench.

The reference product is `C:\Git\OpenVisionLab_Dev`, a 2D OpenCV/OpenCvSharp workbench built around explicit layers, tool previews, result metrics, overlays, acceptance rules, XML recipes, and repeatable validation. This repository translates that model into 3D, starting with a reliable 3D viewer.

## Current State

As of 2026-07-06, this repository has the first SharpGL WPF viewer MVP skeleton.

The current viewer MVP can:

- Use SharpGL as the first viewer library.
- Render a generated unit cube.
- Render a generated point cloud with per-point colors.
- Render the local `3D/Thickness` C3D sample as a downsampled height-grid point cloud.
- Switch point-cloud color modes between `Solid`, `Height`, and `Deviation`.
- Orbit, pan, zoom, reset camera, fit all, and fit the selected entity.
- Show object/entity tree and visibility toggles.
- Pick the cube or C3D height-grid points and display model coordinates.
- Show viewer-only selection states for point, box ROI, and section plane.
- Draw viewer-only measurement and result overlays.
- Publish the synthetic preview into an explicit result entity/layer without mutating source geometry.
- Capture screenshot smoke artifacts from the running app.

Still not included: general file-open import, external mesh import, CAD import, persisted recipes, and headless rule execution.

## Build And Smoke

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_pick_after_cube.png --smoke-pick cube
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_after.png --smoke-c3d thickness
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_pick_after.png --smoke-c3d thickness --smoke-pick c3d
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_contracts_after.png --smoke-c3d thickness --smoke-contracts artifacts\viewer_contracts_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_tool_result_after.png --smoke-overlay result --smoke-contracts artifacts\viewer_tool_result_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_publish_after.png --smoke-overlay result --smoke-publish-result --smoke-contracts artifacts\viewer_publish_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_point.png --smoke-selection point
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_box.png --smoke-selection box
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_section.png --smoke-selection section
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_result_overlay_after.png --smoke-overlay result
```

## Document Map

- `AGENTS.md`: repository working rules for Codex.
- `docs/CODEBASE_STRUCTURE.md`: current and planned repository structure.
- `docs/OPENVISIONLAB_3D_PLATFORM_DIRECTION.md`: product direction and contracts.
- `docs/OPENVISIONLAB_3D_VIEWER_MVP_PLAN.md`: first viewer milestone and acceptance checks.
- `docs/OPENVISIONLAB_3D_RESEARCH_NOTES_20260706.md`: local and web research notes.
- `docs/OPENVISIONLAB_3D_VIEWER_USAGE_RESEARCH_20260706.md`: commercial 3D vision viewer usage patterns and next development track.
- `docs/OPENVISIONLAB_3D_SAMPLE_DATA.md`: local 3D sample inventory and inferred C3D layout notes.
- `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`: immediate next work.

## Core Contracts

`src/OpenVisionLab.ThreeD.Core` now contains the first source/result/layer/metric/overlay contracts. The viewer sample state is wired to `SourceEntity` and `EntityLayer`; the result overlay scene exposes a synthetic `ToolResult` preview that can be explicitly published into a `ResultEntity` and result layer. Rule algorithms are not wired yet.

## Shell Direction

The SharpGL viewer is expected to stay as a separate 3D viewer project/library. The future main workspace should follow the `C:\Git\OpenVisionLab_Dev` docking pattern: a dedicated docking controls library owns AvalonDock integration, while the app hosts documents and tools through that wrapper. .NET 10 migration is planned, but should be handled as a separate compatibility pass before feature work depends on it.

## First Principle

Do not start with a large 3D platform. Complete the viewer first, verify it with screenshots and smoke checks, then attach rule-based 3D algorithms to visible geometry.
