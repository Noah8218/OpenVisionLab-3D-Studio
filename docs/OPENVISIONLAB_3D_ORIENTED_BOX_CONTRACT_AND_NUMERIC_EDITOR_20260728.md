# OpenVisionLab 3D OrientedBox3D Contract and Numeric Editor

Date: 2026-07-28

Status: Complete for E-07/E-08 software scope

## Outcome

OpenVisionLab 3D Studio now owns a persisted volumetric ROI contract that is
separate from the existing surface-footprint `GridRectangle`.

- recipe schema `1.4` adds selection kind `oriented-box-3d`;
- schema `1.3` recipes remain valid and executable;
- one box stores center XYZ, three right-handed orthonormal axes, and positive
  half-extents XYZ in the declared source frame;
- the normal Selected Tool Regions surface exposes a numeric MVVM editor;
- New and Cancel remain transient;
- Apply is explicit, preserves the selection identity on later edits, marks
  only the recipe dirty, and does not Preview, Publish, or Run;
- Delete removes only an unconsumed box and does not execute inspection;
- save/reopen preserves the exact typed geometry.

This closes master-backlog items `E-07` and `E-08`.

## Why this is a new ROI type

`GridRectangle` remains an X/Z native-grid footprint. Its Viewer overlay Y
offset is presentation-only and cannot represent a saved volume.

`OrientedBox3D` is a persisted 3D volume:

```text
Center      = (cx, cy, cz)
Axis X      = normalized local X direction
Axis Y      = normalized local Y direction
Axis Z      = normalized local Z direction
HalfExtents = positive distances along the three local axes
```

The numeric values are stored in the source's declared frame and unit. For the
current owner C3D, that evidence is `frame.c3d-grid-index` and `raw-height`;
it is not a calibrated physical coordinate or metrology claim.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `ToolRecipeDocument` | Schema `1.4`, typed payload, and selection kind |
| `ToolRecipeOrientedBox3DGeometry` | WPF-neutral finite/positive/orthonormal/right-handed validation |
| `ToolRecipeValidator` | Schema gating, payload exclusivity, source identity, and route compatibility |
| `OrientedBox3DEditorViewModel` | Numeric draft, validation, selection, and explicit command state |
| `ToolWorkbenchViewModel.OrientedBox3D` | Recipe mutation, same-ID update, delete protection, logging, and non-execution boundary |
| `SelectedToolWorkspaceView` | Numeric Regions UI only |

The editor is not implemented as View code-behind. The Workbench remains the
recipe owner; the View binds to the independent editor ViewModel.

## Validation contract

Apply and save fail closed unless:

- center XYZ and all axis/extent values are finite;
- each axis has unit length within `1e-5`;
- the three axes are mutually orthogonal within `1e-5`;
- `cross(AxisX, AxisY)` agrees with `AxisZ`, producing a right-handed basis;
- every half-extent is greater than zero;
- the selection contains no rectangle, point-set, or correspondence payload;
- source ID, frame, grid dimensions, and C3D content identity match.

Schema `1.3` rejects an `OrientedBox3D` payload, while all existing
artifact-owned selection and affine adapters accept both `1.3` and `1.4`.

## Operator workflow

1. Load an identified C3D source.
2. Open Selected Tool -> Regions -> `3D Box Regions`.
3. Choose `New box`.
4. Enter the center, local axes, and half sizes.
5. Resolve every validation message.
6. Choose `Apply box`.
7. Save and reopen the recipe to verify persistence.
8. Select the same box to edit it without changing its identity, or delete it
   while no recipe step consumes it.

No inspection step is required merely to author the typed region. The UI
fixture uses an imported authoring placeholder only to keep the Selected Tool
surface visible for screenshot evidence; it is not a new inspection adapter.

## Current owner-source evidence

Source:

`3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D`

| Fact | Value |
| --- | --- |
| Native grid | `1280 x 840` |
| Source frame | `frame.c3d-grid-index` |
| Declared unit | `raw-height` |
| Source SHA-256 | `5D3625B1A5A65EF8BEAB366FF7A007918D28FB614136414BBD30A441E85C8937` |
| Fixture selection | `selection.oriented-box.01` |
| Fixture center | `(732.5, 664.5, 1134)` |
| Fixture axes | identity/right-handed |
| Fixture half-extents | `(250, 500, 500)` |

The fixture is:

`artifacts/current/20260728-oriented-box-contract/oriented-box-ui-fixture.ov3d-recipe.json`

## UI evidence

Fresh pre-change Release captures:

- `artifacts/current/20260728-oriented-box-contract/before-wide-no-oriented-box-editor.png`;
- `artifacts/current/20260728-oriented-box-contract/before-compact-no-oriented-box-editor.png`.

Current Release captures:

- `artifacts/current/20260728-oriented-box-contract/after-wide-numeric-editor.png`;
- `artifacts/current/20260728-oriented-box-contract/after-compact-numeric-editor.png`.

Both after captures pass screenshot quality on attempt 1. The editor remains
visible and usable at `1920 x 1080` and `1280 x 760` without taking ownership
away from the dominant 3D Viewer.

## Verification

| Gate | Result |
| --- | --- |
| Release build | `0 warnings / 0 errors` |
| Selection schema/validation/save-reopen | `25/25` |
| Inspection Workspace editor/non-execution | `60/60` |
| Recipe teaching regression | `28/28` |
| Height measurement regression | `45/45` |
| Artifact Navigator | `31/31` |
| Workbench docking/composition | `33/33` |
| Recipe Manager/WPG | `37/37` |
| Artifact-owned ROI Runner compatibility | `18/18` |
| Synthetic affine schema compatibility | `18/18` |
| Schema 1.3 XYZ Affine adapter | `4/4` |
| Schema 1.3 Landmark Correspondence adapter | `5/5` |
| Shell smoke options | `21/21` |
| Code structure | `17/17` |
| Wide/Compact screenshot quality | Pass on attempt 1 |

Reusable evidence is under:

`artifacts/current/20260728-oriented-box-contract/`

## Boundaries

This slice does not add:

- a rendered 3D box outline;
- move, resize, rotate, or height pointer handles;
- linked Height Image box manipulation;
- a tool input/output route that consumes the box;
- clipping, cropping, completeness, or presence execution;
- calibration, physical units, uncertainty, or metrology evidence.

Those boundaries are deliberate. `E-09` is the next dependency-correct slice:
render the typed box and add projection-correct Viewer pointer handles without
changing the persisted contract.

## Completion record

```text
Status: Complete
Scope: schema 1.4 OrientedBox3D selection, fail-closed geometry validation, Selected Tool numeric MVVM authoring, explicit Apply/Delete, and exact save/reopen
Acceptance criteria: typed volume is distinct from GridRectangle -> pass; invalid axes/extents/mixed payloads fail closed -> pass; 1.3 compatibility remains -> pass; Apply preserves identity and does not execute -> pass; save/reopen preserves exact geometry -> pass; Wide/Compact editor remains usable -> pass
Verification: Release build 0/0; selection 25/25; Workspace 60/60; teaching 28/28; height measurement 45/45; Artifact Navigator 31/31; docking 33/33; Recipe Manager/WPG 37/37; artifact-owned Runner 18/18; synthetic affine 18/18; schema 1.3 affine 4/4; schema 1.3 correspondence 5/5; shell options 21/21; structure 17/17; screenshot quality pass
Evidence: docs/OPENVISIONLAB_3D_ORIENTED_BOX_CONTRACT_AND_NUMERIC_EDITOR_20260728.md and artifacts/current/20260728-oriented-box-contract/
Boundary / next dependency: no rendered box, pointer handles, linked 2D editing, downstream consumer, calibration, or metrology claim; E-09 is next
```
