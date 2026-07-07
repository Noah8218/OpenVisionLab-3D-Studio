# AGENTS.md

This file defines the working agreement for Codex in this repository.

## Work Location

- Primary work happens in `C:\Git\OpenVisionLab-3D-Studio`.
- `C:\Git\OpenVisionLab_Dev` is the 2D reference repository. Read it for product contracts, UX direction, validation style, and naming patterns.
- Do not modify, stage, commit, or prepare promotion work in `C:\Git\OpenVisionLab_Dev` unless the user explicitly asks for that step.
- Do not run `git push` unless the user explicitly requests it.

## Product Identity

- OpenVisionLab 3D Studio is a rule-based 3D inspection workbench.
- The product is not just a model viewer. The viewer is the first foundation for teaching, measuring, comparing, and validating 3D inspection rules.
- The 2D reference product validates image layers with tools, metrics, overlays, acceptance rules, recipes, and repeatable runner checks. The 3D product should keep that operating model, but use 3D entities instead of images.
- Early scope is local desktop work: load 3D data, inspect it, show overlays/measurements, and build repeatable rule-based validation. Do not start with camera, PLC, robot, cloud, or production-line integration.

## Stable Contracts

- Build the 3D viewer first. Rule authoring and 3D algorithm work must wait until the viewer completion gate passes.
- Viewer completion means reliable display, camera control, object/layer visibility, picking, selection, measurement/result overlay rendering, color modes, and screenshot smoke evidence.
- The first viewer implementation uses SharpGL because the project owner is already comfortable reading and debugging SharpGL-based code.
- The 3D viewer must remain a separate project/library. The eventual main workspace should host it as a document/tool view instead of merging viewer internals into the main shell.
- For the main workspace, follow the `C:\Git\OpenVisionLab_Dev` docking boundary: docking ownership belongs in a dedicated controls library like `Library\OpenVisionLab.Docking.Controls`; do not add AvalonDock or raw docking package usage directly to the app project.
- For app-level WPF UI styling, follow the Dev repository's `WPF-UI` boundary: the Shell app owns `WPF-UI` package/theme resources, while Viewer and Docking.Controls stay free of direct `WPF-UI` dependencies unless a reusable control explicitly needs that dependency.
- The project owner plans to move the product to .NET 10. Treat target framework migration as its own compatibility task; verify WPF, SharpGL, docking, vendored DLLs, and smoke checks before mixing it with feature work.
- MVVM is the target application structure. Keep view code-behind as a thin UI/OpenGL event bridge, and move durable state, commands, result data, and workflow logic into ViewModel, Controller, Presenter, Runtime, or Service classes as soon as they stop being trivial.
- Keep source geometry and result geometry separate. A validation result must not silently mutate the imported source model.
- Keep preview and publish separate. Preview is review state; publish creates or updates an explicit result layer/entity.
- Every validation tool must expose metrics and visual evidence, not only OK/NG text.
- Units must be explicit. Store model units, display units, tolerances, and transforms with the data they affect.
- Selection, picking, measurement, and camera state are product contracts. Do not break orbit, pan, zoom, fit-to-view, object visibility, or result overlay toggles while adding tools.
- Prefer simple, inspectable rule-based tools before AI or automatic tuning.
- Do not commit to a CAD kernel, point-cloud stack, or rendering engine without a small local prototype and verification evidence.

## Completion Means Evidence

Do not mark work complete by explanation alone. Completion requires the smallest meaningful evidence for the touched area.

For documentation-only work:

```powershell
git diff --check
rg -n "OpenVisionLab 3D|3D Viewer|rule-based|C:\\Git\\OpenVisionLab_Dev" .
```

For SharpGL viewer work, run:

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

For shell/docking work, also run:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_c3d_after.png --smoke-c3d thickness --smoke-contracts artifacts\shell_c3d_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_result_overlay_after.png --smoke-overlay result --smoke-contracts artifacts\shell_result_overlay_after.txt
```

UI/UX work requires current screenshots from the running build. Store before/after captures in an artifact folder and report the paths.

## Priority Direction

1. Establish the viewer MVP.
2. Harden the viewer to the completion gate: mesh/point-cloud display, camera, selection, overlays, color modes, screenshots, and MVVM state separation.
3. Define the 3D entity/layer/result contracts.
4. Add one rule-based validation tool with metrics and overlays.
5. Add recipe serialization and a headless runner path.
6. Expand data formats, CAD precision, point-cloud processing, and AI assistance only after the core loop is verified.

When starting after orientation, state the immediate priority and the remaining project priority before editing files or running follow-up commands.

When finishing any task, always include the next recommended priority in the final response. Base it on the current repository evidence, this priority direction, and the next-session handoff. If the task was documentation-only, still include a concrete next priority.

## No Guessing

- Check files, commands, sources, or local prototypes before making factual claims.
- If evidence conflicts, surface the conflict.
- If an engine or library is only a candidate, call it a candidate.
- If a behavior is inferred from the 2D reference repo, label it as an inference.

## Simplicity First

- Prefer the smallest viewer that proves load, render, camera, picking, measurement, overlay, and screenshot smoke.
- Prefer direct, understandable SharpGL code over a broad 3D framework until the MVP proves a missing capability.
- Add abstractions only after a second real use case exists or the 2D reference repo has a matching proven pattern.
- Do not scaffold broad plugin systems, hardware integrations, or CAD editing workflows before the viewer and validation contracts exist.
