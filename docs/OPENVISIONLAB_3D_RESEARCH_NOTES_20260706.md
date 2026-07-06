# OpenVisionLab 3D Research Notes

Checked: 2026-07-06

This note combines local reference review and web research for the initial 3D Studio direction.

## 1. Research Questions

1. What should be built first?
2. Which 2D OpenVisionLab contracts transfer to 3D?
3. Which 3D technology families are plausible for the first viewer and later validation work?
4. What should be documented before code starts?

## 2. Local Reference Findings

Reference repository: `C:\Git\OpenVisionLab_Dev`

Documents checked:

- `AGENTS.md`
- `docs/CODEBASE_STRUCTURE.md`
- `docs/OPENVISIONLAB_PLATFORM_DIRECTION.md`
- `docs/OPENVISIONLAB_PRODUCT_IDENTITY_AND_ROADMAP.md`
- `docs/OPENVISIONLAB_STATUS_AND_NEXT_STEPS.md`

Key contracts to carry forward:

- Explicit layer/entity flow.
- Preview and publish separation.
- Property/model-driven tools where practical.
- Tool result status, metrics, overlays, elapsed time, and message.
- Recipe/runner direction so UI-created rules can run outside the UI.
- WPF/MVVM direction with thin view code-behind.
- Screenshot smoke evidence for UI/UX changes.

Inference from the 2D reference: the 3D product should not begin as a CAD editor. It should begin as an inspection workbench where the viewer makes rule results visible and testable.

## 3. Web Findings

### Viewer And .NET UI

- [SharpGL](https://github.com/dwmkerr/sharpgl) documents WPF controls, OpenGL bindings, a scene graph package, and serialization utilities for several 3D file formats.
- [Helix Toolkit](https://helix-toolkit.github.io/) presents itself as 3D graphics for .NET with WPF, SharpDX, Avalonia, and WinUI support.
- [Helix Toolkit GitHub](https://github.com/helix-toolkit/helix-toolkit) lists WPF SharpDX scene graph support and Assimp-based model import/export support.
- [ActiViz 9.6 Release](https://www.kitware.com/activiz-9-6-release/) says ActiViz is Kitware's .NET/C# wrapper for VTK and includes WPF/Avalonia-oriented improvements.

Original conclusion: HelixToolkit looked like the shortest WPF/MVVM candidate from web research alone.

Updated project decision: use SharpGL first because the project owner is already familiar with it and can analyze the source confidently. This reduces debugging risk for the first viewer MVP. Keep HelixToolkit, AB4D, ActiViz/VTK, Eyeshot, HOOPS, and Open CASCADE as later alternatives for specific failures or requirements.

### Asset And CAD Formats

- [glTF 2.0 Specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html) defines glTF as an API-neutral runtime asset delivery format for modern graphics applications.
- [Open CASCADE Technology overview](https://dev.opencascade.org/doc/overview/html/) documents BRep modeling data and CAD data exchange including STEP, IGES, glTF, OBJ, and STL paths.

Conclusion: use glTF/GLB or generated mesh data for the first viewer path. Treat STEP/IGES/BRep as a later CAD-kernel phase.

### 3D Processing And Validation

- [Open3D documentation](https://www.open3d.org/docs/release/) covers point clouds, meshes, KDTree, octree, voxelization, ray casting, distance queries, ICP, and registration workflows.
- [Point Cloud Library documentation](https://pointclouds.org/documentation/) describes filtering, feature estimation, surface reconstruction, registration, model fitting, and segmentation.
- [CloudCompare presentation](https://www.cloudcompare.org/presentation.html) describes comparison between dense point clouds and point-cloud/mesh data, using octree-backed distance computation.
- [CGAL Polygon Mesh Processing](https://doc.cgal.org/latest/Polygon_mesh_processing/index.html) covers mesh processing, Boolean operations, remeshing, repair, clipping, slicing, and related geometry algorithms.

Conclusion: the later validation stack should be chosen per rule family. Do not pull in Open3D, PCL, CGAL, VTK, and OCCT at once.

## 4. Direction Decision

Recommended order:

1. Documentation foundation.
2. WPF 3D Viewer MVP.
3. Core 3D entity/result/metric/overlay contracts.
4. One sample-backed rule-based validation tool.
5. Recipe and runner.
6. Format and algorithm expansion.

The first viewer prototype should prove:

- render,
- camera,
- picking,
- measurement overlay,
- source/result separation,
- screenshot smoke.

## 5. Technology Decision Rule

Use the smallest dependency that proves the current milestone.

| Need | Candidate |
| --- | --- |
| WPF viewer MVP | SharpGL WPF |
| WPF framework fallback | HelixToolkit WPF/SharpDX |
| Scientific visualization and point-cloud rendering | ActiViz/VTK |
| Point-cloud algorithms | Open3D or PCL |
| Mesh repair/Boolean/remeshing | CGAL |
| CAD topology and STEP/IGES | Open CASCADE Technology |
| Runtime mesh interchange | glTF/GLB |

SharpGL is selected for the first prototype, but the MVP still must pass render, camera, picking, overlay, and screenshot checks before feature work expands.
