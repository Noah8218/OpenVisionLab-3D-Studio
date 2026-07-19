# OpenVisionLab 3D Tool Recipe Teaching v1

Updated: 2026-07-18

## Decision

> **Superseded UI direction (2026-07-19):** The former single-left-surface teaching layout is replaced by the approved Recipe Manager window + Recipe Navigator + Tool Lab + Compare Workspace direction in [GoPxL-Informed Tool Lab Direction](OPENVISIONLAB_3D_GOPXL_TOOL_LAB_DIRECTION_20260719.md). This document retains the teaching persistence and explicit-execution contracts until those contracts are revised explicitly.

Finish generic **teaching/authoring** before implementing a generic inspection algorithm executor.

Teaching Recipe v1 is a non-executing, local recipe-authoring workflow. It lets an engineer load a C3D source, declare references, assemble ordered tools, set entity routing and parameters, validate the graph, save JSON, and reopen it for further editing. It is intentionally separate from the existing executable, tool-specific C3D recipes.

## Engineer Workflow

```text
Load C3D source
  -> declare source unit/frame and fixture/reference entities
  -> select a Toolbox tool and add it to the ordered recipe
  -> name the output and route one or more earlier entities into the next step
  -> set the teach-time parameters
  -> validate the graph
  -> save or reopen *.ov3d-teach.json
```

For the intended feature-first workflow, the provided template records:

```text
C3D source
  -> Filter
  -> upper-left and lower-right height-difference edges
  -> four 3D line fits
  -> two line intersections (corner anchors)
  -> landmark correspondence declaration
  -> XYZ Affine Transform (taught only)
  -> Re-grid Height Map (taught only)
  -> Thickness and Warpage
  -> Overlay / Control Review
```

The template is [c3d-xyz-affine-teaching-template.ov3d-teach.json](../recipes/c3d-xyz-affine-teaching-template.ov3d-teach.json). Its relative source path resolves to the user-designated `3D/Warpage` C3D file when opened from the recipe.

## User Interface Contract

- **Project Explorer** is the single left-side teaching surface. Its Recipe section owns the recipe name plus New, Open, and Save; its Input data section owns Load C3D and source unit/frame; its References & frame section owns declared reference entities.
- **Add inspection step** is a palette inside Project Explorer. Selecting a catalog item only describes the candidate tool; an ordered recipe row is created only by the explicit **Add selected step** command. Neither action runs an algorithm.
- **Properties** edits only an actual selected taught step: input/output entity IDs, parameters, and step ordering/removal. When no taught step is selected, it presents a clear empty state rather than making a catalog item look like an authored step.
- **3D View** stays central and keeps the existing Viewer as its rendering/camera/selection owner. Its default Teach HUD is compact; detailed diagnostics remain available in Advanced layout.
- **Recipe details** is a collapsed-by-default bottom disclosure containing the ordered Steps & validation review and the authoring-only log. It does not represent an inspection run.
- **Advanced layout** remains an explicit opt-in diagnostic workspace; it retains the existing docked composition and does not become the default authoring route.

The Shell only bridges file dialogs and C3D source loading. The Viewer remains the owner of rendering, camera, and existing selection state; no normal Viewer interaction is repurposed to execute a generic tool.

## Persistence and Validation Contract

`ToolRecipeDocument` schema `1.0` is owned by `Core`; `ToolRecipeDocumentStore` in `Data` performs JSON load/save. A valid document requires:

- a non-empty recipe name and source ID/name/format/unit/frame/path;
- at least one ordered taught step;
- unique reference, step, and output entity IDs;
- every input entity to be the source, a declared reference, or an earlier step output;
- no repeated input entity in a step;
- each tool's declared minimum input count; and
- named parameters.

Opening resolves a relative source path against the teaching JSON file, so a template can remain portable within the repository. Saving writes a separate `*.ov3d-teach.json` document; it does not overwrite or masquerade as an executable C3D recipe.

## XYZ Affine Boundary

The `XYZ Affine Transform` row intentionally produces a validation warning, not a calculated transform. Two corner anchors do not establish a general full-XYZ affine map. Execution remains blocked until the recipe carries four affine-independent source/reference correspondences, or a separately approved fixture-constrained transform contract, with rank/residual/determinant and source-provenance evidence.

The same applies to edge extraction, line fit, intersection, re-grid, and generic Thickness/Warpage execution: their rows are teach-time declarations only. Existing dedicated C3D Thickness and Warpage workflows remain available and unchanged.

## Verification

Current-source verification after this change:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform='Any CPU'
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug -- --verify-tool-recipe-teaching artifacts\ui\20260718-modern-teach-workbench\tool-recipe-teaching-verification.txt
```

The verification creates a graph through the ViewModel, saves and reloads it, confirms parameter/reference persistence, confirms an invalid entity route blocks saving, and confirms the shipped template resolves its relative `3D/Warpage` C3D source.

The current UI evidence is [after-template-open.png](../artifacts/ui/20260718-modern-teach-workbench/after-template-open.png). It is a current-build Shell capture with the template open, the teaching-only boundary visible, and the Affine warning preserved.

## Next Gate

Do not implement a numerical algorithm as the next response to this document alone. The next engineering gate is to turn one approved taught row into a typed execution adapter only after its required inputs, selections/ROI semantics, golden data, and acceptance rules are confirmed. The first candidate must be chosen by the owner; no automatic default is implied by this template.

The proposed C3D-first selection UI and schema boundary is recorded in [OpenVisionLab 3D Generic Teach Selection v1 Design](OPENVISIONLAB_3D_GENERIC_TEACH_SELECTION_DESIGN_20260718.md). It must be approved before any selection UI, schema `1.1`, or Viewer capture-session implementation begins.
