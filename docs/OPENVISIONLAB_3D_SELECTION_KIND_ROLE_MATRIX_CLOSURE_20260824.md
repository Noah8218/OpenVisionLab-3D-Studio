# Selection Kind/Role Compatibility Matrix Closure

Date: 2026-08-24
Status: Complete
Scope: `PL-0047 / E-13`

## Operator problem

A structured selection could be geometrically valid while still being routed
to a tool that did not support its kind or semantic purpose. Selection rules
were distributed across Workbench teaching code and tool-specific validators,
so a newly added kind could otherwise appear compatible before failing later.

## Product contract

`ToolRecipeSelectionContract` is now the single declaration of supported
selection-consuming tool routes. A declaration owns the stable Tool ID,
semantic role, selection kind, input position or range, multiplicity, and any
exact `PointSet` count.

Roles belong to the relationship between a recipe step and a selection. The
selection object therefore retains its reusable geometry and identity without
a duplicated role field or recipe schema change. Existing
`ToolRecipeDualRoiRouting` remains the persistence owner for incomplete and
complete dual-region role assignment.

## Current matrix

| Tool | Role | Kind | Route/count |
| --- | --- | --- | --- |
| Level Surface | Reference region | `GridRectangle` | input 2 onward, one or more |
| ROI / Crop | Region | `GridRectangle` | input 2, exactly one |
| Height Difference Edge | Search region | `GridRectangle` | input 2, exactly one |
| 2-Point Line | Line points | `PointSet` | input 2, exactly two points |
| 3-Point Plane | Plane points | `PointSet` | input 2, exactly three points |
| Datum Plane Raw-Height Deviation | Measurement region | `GridRectangle` | input 3, exactly one |
| Landmark Correspondence | Correspondences | `LandmarkCorrespondenceSet` | input 1, exactly one |
| Thickness | Reference region | `GridRectangle` | input 2, exactly one |
| Thickness | Measurement region | `GridRectangle` | input 3, exactly one |
| Warpage | Measurement region | `GridRectangle` | input 2, exactly one |
| Plane Flatness | Reference region | `GridRectangle` | input 2, exactly one |
| Plane Flatness | Measurement region | `GridRectangle` | input 3, exactly one |
| Point Pair Dimensions | Measurement points | `PointSet` | input 2, exactly two points |
| Gap / Flush | First region | `GridRectangle` | input 2, exactly one |
| Gap / Flush | Second region | `GridRectangle` | input 3, exactly one |
| Volume | Reference region | `GridRectangle` | input 2, exactly one |
| Volume | Measurement region | `GridRectangle` | input 3, exactly one |
| Cross-section Dimensions | Measurement region | `GridRectangle` | input 2, exactly one |
| Completeness Grid | Reference region | `GridRectangle` | input 2, exactly one |
| Completeness Grid | Inspection region | `GridRectangle` | input 3, exactly one |

The matrix has `20` rows across `15` current selection-consuming tools.
`OrientedBox3D` remains authorable and persistable, but no current inspection
tool silently consumes it. A consuming tool must add an explicit declaration
before routing it.

## Fail-closed behavior

Strict recipe validation now rejects:

- a routed selection on a tool with no declared selection role;
- a selection injected into a selectionless tool;
- a selection at an unsupported input position;
- a kind that differs from the declared role kind;
- a `PointSet` with the wrong exact point count;
- a missing required role or too many selections for a role.

Geometry, source binding, artifact ownership, frame, primary input, and
tool-specific parameter checks remain with their existing owners and compose
with this matrix.

Storage validation deliberately distinguishes an incompatible route from an
unfinished route. Undeclared or wrong-kind routed selections still fail, while
a missing role may be saved and reopened as an incomplete draft for explicit
repair. Preview, Publish, Run, source, selection, and result state are not
mutated by either validation path.

## Implementation

- `src/OpenVisionLab.ThreeD.Core/Contracts/ToolRecipes/ToolRecipeSelectionContract.cs`
  owns roles, declarations, lookup, and route validation.
- `ToolRecipeValidator` applies the matrix with strict execution and
  repairable-storage policies.
- `ToolWorkbenchViewModel` derives teaching kind and point count from the same
  declaration while retaining existing operator guidance.
- `ToolWorkbenchViewModel.CompatibleToolCatalog` uses the declaration when it
  searches for a compatible Height Difference Edge region.
- `ToolRecipeSelectionContractVerification` owns the named positive and
  negative matrix cases, including default rejection of an undeclared
  `OrientedBox3D` consumer.

No dependency, UI framework, numerical algorithm, Vision SDK package, recipe
schema, visible layout, style, or control template changed.

## Verification

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-e13-selection-matrix`

| Check | Result |
| --- | --- |
| 15-project Release solution build | Pass, 0 warnings / 0 errors |
| Selection contract through Shell | Pass, `40/40` |
| Selection contract through Runner | Pass, `40/40`; OrientedBox subset `11/11` |
| Tool Recipe teaching/save/reopen | Pass, `51/51` |
| Height Measurement Workbench | Pass, `56/56` |
| Inspection Workspace selection | Pass, `67/67` |
| Ordered Run | Pass, `16/16` |
| Validation Set | Pass, `87/87` |
| Standard .NET test facade | Pass, `2/2` |
| Code structure | Pass, `68/68` |

Windows reported two independent monitors. The smaller left test monitor was
`DISPLAY2`, bounds `-1920,365,1920,1080`; the headless verification commands
did not create a visible product window. No visible XAML or UI text changed,
so no new screenshot baseline was created.

## Boundary

This closes only E-13's software declaration and fail-closed routing matrix.
It does not implement `GridCircle`, `GridPolygon`, a downstream
`OrientedBox3D` inspection consumer, region artifacts, physical metrology,
owner R0, publication, deployment, commit, or push.
