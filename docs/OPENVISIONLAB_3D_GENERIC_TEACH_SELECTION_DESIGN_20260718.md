# OpenVisionLab 3D Generic Teach Selection v1 Design

Updated: 2026-07-18

Status: **Complete - design package**
Implementation approval: **Pending owner decision**

## Decision Needed

Teaching needs recipe-owned selections before a generic 3D tool can execute.
The first implementation should support C3D height-grid teaching only:

1. named grid rectangles for edge bands and measurement ROIs;
2. named ordered point sets with exactly two or three C3D grid picks; and
3. named landmark-correspondence rows whose source side is a taught entity and
   whose reference side has an explicit XYZ coordinate and frame.

Do not add freehand polygons, lasso selection, point-cloud/mesh locators,
automatic edge finding, line fitting, affine solving, Preview, or Run in this
slice. They are not needed to make the selection model durable and would hide
unapproved metrology semantics.

## Evidence Behind the Design

- The current generic `ToolRecipeDocument` v1.0 stores source, references,
  ordered steps, entity routing, and string parameters. It has no structured
  geometry or selection provenance.
- The existing Viewer already picks C3D grid points and teaches specialized
  Thickness/Warpage grid ROIs. Those handlers are tool-specific and must not
  be reused as a hidden generic execution path.
- The 2D reference uses a distinct ROI snapshot with stable identity and
  before/after ownership (`RoiSnapshotChangedEventArgs`), while its authoring
  actions remain separate from explicit Preview/Run. The 3D equivalent should
  persist an immutable selection snapshot rather than a transient screen click.
- The supplied XYZ-affine teaching template has two corner anchors only. That
  remains useful teaching context but is insufficient for a general full-XYZ
  affine transform; four affine-independent correspondences, or a separately
  approved fixture-constrained contract, remain mandatory.

## User Flow

```text
Select an authored recipe step
  -> Properties shows only that step's required teaching selections
  -> choose Capture ROI, Capture 2 points, or Capture 3 points
  -> Viewer enters one explicit, temporary selection session
  -> pick the required C3D grid cells
  -> review the captured frame/source/coordinates
  -> Apply selection to recipe
  -> selection becomes a named input entity for the selected step
  -> save/reopen validates the same selection against the source identity
```

Selecting a palette tool never starts a session. Starting, cancelling,
replacing, or applying a selection never runs an algorithm and never changes
the input data entity.

## Teach Workbench UI

The existing Teach layout remains `Project Explorer | 3D View | Properties`.
Only the selected step receives a compact **Teaching selections** section in
Properties; no permanent fourth pane is added.

```text
Properties - Step 02: Height Difference Edge
-------------------------------------------------------
Expected inputs      FilteredHeightField + Grid ROI
Input entities       derived.filtered-height.01

Teaching selections                         [Capture]
  Edge band: upper-left horizontal      Required
  No recipe-owned grid ROI yet.
  [Capture grid rectangle] [Use existing selection]

Edge parameters
  Direction: Horizontal
  Threshold: <explicit value or later approved rule>

Boundary: Capture stores geometry only. It does not find an edge.
```

When capture is active, the Viewer header shows one non-modal ribbon:

```text
Capture: Upper-left horizontal edge band | Grid rectangle | Pick corner 1 of 2
[Cancel]  [Undo last point]  [Apply selection - disabled until valid]
```

- A C3D grid rectangle uses two opposite **grid-cell** picks, not drag. This
  preserves the existing left-drag/orbit interaction and makes the stored
  grid bounds unambiguous.
- A two-point line uses exactly two distinct grid-cell picks; a three-point
  plane uses exactly three distinct, non-collinear picks. The session displays
  `1/2`, `2/2`, or `1/3` progress before Apply is enabled.
- Correspondence is edited as an explicit table in Properties. Its source is
  a named earlier entity (for example `derived.corner-ul.01`); its reference
  side is a named XYZ fixture landmark and explicit reference frame. It is not
  a Viewer click unless that source anchor is itself a point-set selection.
- The Viewer renders only a transient candidate overlay while capturing. A
  recipe-owned overlay appears only after **Apply selection**.
- Escape and **Cancel** discard the transient candidate. **Replace** creates
  a new candidate and requires Apply again. No partial capture is serialized.

## Selection Inputs by Tool

| Taught tool | Recipe-owned selection input | Capture action | No-selection behavior |
| --- | --- | --- | --- |
| ROI / Crop | `GridRectangle` | Two C3D grid corners | Cannot save that step as a bounded crop. |
| Height Difference Edge | `GridRectangle` edge band | Two C3D grid corners | The step has no explicit search band. |
| 2-Point Line | `PointSet` cardinality `2` | Two C3D grid points | The line is incomplete. |
| 3-Point Plane | `PointSet` cardinality `3` | Three C3D grid points | The plane is incomplete; collinear points are rejected. |
| 3D Line Fit | none | Select prior `EdgePointSet` entity | No Viewer capture is implied. |
| Line Intersection | none | Select two prior `LineFeature` entities | No Viewer capture is implied. |
| Landmark Correspondence | `LandmarkCorrespondenceSet` | Add named source/reference rows | Fewer than four rows remains a non-executable warning. |
| Thickness / Warpage | `GridRectangle` measurement ROI | Two C3D grid corners | The measure step lacks a recipe-owned ROI. |

This keeps the user's intended chain explicit:

```text
Filter
  -> named upper-left/lower-right edge bands
  -> height-difference edge entities
  -> fitted line entities
  -> intersection/corner entities
  -> four or more source/reference correspondence rows
  -> XYZ affine (still taught only)
  -> re-grid
  -> Thickness / Warpage and review
```

## Persistence Contract: Proposed Schema 1.1

Schema `1.1` adds a root `selections` collection to `ToolRecipeDocument`.
Selections are addressable input entities, so existing `inputEntityIds` remains
the only graph-routing field. A step consumes a selection by ID just as it
consumes a source, reference, or earlier output.

```json
{
  "schemaVersion": "1.1",
  "name": "C3D affine teaching",
  "source": { "id": "source.c3d.height-map", "frameId": "frame.c3d-grid-index" },
  "selections": [
    {
      "id": "selection.edge-band.ul.horizontal",
      "name": "Upper-left horizontal edge band",
      "kind": "grid-rectangle",
      "rootSourceId": "source.c3d.height-map",
      "frameId": "frame.c3d-grid-index",
      "sourceBinding": {
        "format": "C3D",
        "contentSha256": "<captured-source-bytes-sha256>",
        "gridWidth": 1967,
        "gridHeight": 1301
      },
      "gridRectangle": { "row": 40, "column": 30, "rowCount": 90, "columnCount": 420 }
    },
    {
      "id": "selection.datum-plane.01",
      "name": "Datum plane points",
      "kind": "point-set",
      "rootSourceId": "source.c3d.height-map",
      "frameId": "frame.c3d-grid-index",
      "sourceBinding": { "format": "C3D", "contentSha256": "<same-source-hash>" },
      "points": [
        { "locator": { "kind": "grid-cell", "row": 100, "column": 100 }, "capturedPosition": { "x": 0.0, "y": 0.0, "z": 0.0 }, "rawHeight": 0.0 },
        { "locator": { "kind": "grid-cell", "row": 100, "column": 300 }, "capturedPosition": { "x": 1.0, "y": 0.0, "z": 0.0 }, "rawHeight": 0.0 },
        { "locator": { "kind": "grid-cell", "row": 300, "column": 100 }, "capturedPosition": { "x": 0.0, "y": 0.0, "z": 1.0 }, "rawHeight": 0.0 }
      ]
    },
    {
      "id": "selection.fixture-correspondence.01",
      "name": "Fixture XYZ correspondences",
      "kind": "landmark-correspondence-set",
      "rootSourceId": "source.c3d.height-map",
      "frameId": "frame.fixture",
      "rows": [
        {
          "sourceEntityId": "derived.corner-ul.01",
          "referenceLandmarkId": "fixture.corner-ul",
          "referencePosition": { "x": 0.0, "y": 0.0, "z": 0.0 },
          "referenceFrameId": "frame.fixture"
        }
      ]
    }
  ],
  "steps": [
    {
      "id": "step.edge.ul.horizontal.01",
      "inputEntityIds": [
        "derived.filtered-height.01",
        "selection.edge-band.ul.horizontal"
      ]
    }
  ]
}
```

The example is structural only. Placeholder XYZ values and a single
correspondence row are intentionally not executable data.

### Required Selection Fields

| Field | Why it is required |
| --- | --- |
| `id`, `name`, `kind` | Stable, human-readable recipe entity identity. |
| `rootSourceId`, `frameId` | Stops a selection from silently crossing source/frame boundaries. |
| `sourceBinding.contentSha256` | Detects replacement of a same-path source file. |
| C3D grid size | Makes invalid row/column geometry detectable before execution. |
| Grid locator plus captured XYZ/raw value | Keeps the stable C3D address and the visible teaching evidence together. |
| Reference landmark ID, XYZ, and frame | Makes correspondence intent auditable without inventing a transform. |

### Validation and Migration Rules

1. Selection IDs must be unique across source, references, selections, step
   IDs, and step outputs.
2. Every selection must resolve to the declared root C3D source and its exact
   frame; a changed source hash marks it **stale** and blocks saving changes
   that claim a valid selection until it is recaptured or explicitly replaced.
3. A grid rectangle must have positive dimensions within recorded grid bounds.
4. A two-point set requires exactly two distinct cells; a three-point set
   requires exactly three distinct, non-collinear captured positions.
5. A correspondence set requires unique source entities and unique reference
   landmark IDs. It warns below four rows; it does not calculate rank,
   residual, determinant, or a transform.
6. Version `1.0` recipes continue to open. Existing free-text `ROI` and point
   parameters are never auto-converted because their geometry/provenance is
   unknowable. A selection is added only by explicit recapture, then the save
   writes schema `1.1`.

## Ownership Boundary

| Layer | Owns | Does not own |
| --- | --- | --- |
| `Core` | Immutable selection contracts and structural validation. | Viewer hit testing, geometry algorithms, or affine solving. |
| `Data` | JSON schema `1.1`, source-byte SHA-256 binding, load/save/stale detection. | Silent coordinate remapping. |
| Shell ViewModel | Selected step, requirement progress, explicit Capture/Apply/Cancel commands, authored state. | Pointer-ray math or rendering state. |
| Viewer ViewModel / Viewer | Temporary capture session, C3D hit result, transient and applied selection overlays. | Recipe persistence and generic tool execution. |
| Tools / Runner | Nothing in this phase. | Any generic selection-based algorithm adapter. |

## Approval Checkpoint and Implementation Acceptance Criteria

Implementation may begin only after the owner accepts all of the following:

1. **C3D-first selection scope:** grid rectangles, two/three-point sets, and
   correspondence rows are sufficient for Selection v1; freehand, cloud, and
   mesh selection wait for a real consuming tool.
2. **Reference-landmark source:** reference XYZ values are supplied as known
   fixture/nominal coordinates in an explicit frame. If they instead come from
   a nominal file or another sensor, the import/provenance contract must be
   specified first.
3. **Source identity:** SHA-256 of the C3D bytes is required when a selection
   is applied; a same-path file replacement must never silently reuse picks.

After approval, the smallest complete implementation must pass:

- Core validation tests for valid/invalid bounds, cardinality, duplicate IDs,
  source/hash/frame mismatch, correspondence count, and `1.0` migration;
- current-source save/reopen proof that structured selections survive JSON;
- a Shell UI smoke showing an inactive, capture-in-progress, and applied
  selection state at `1280 x 760`;
- a Viewer interaction smoke proving Capture/Cancel/Apply changes authored
  selection state only and does not invoke Preview, Run, or an algorithm; and
- before/after UI captures and a quality report in a new artifact folder.

## Explicit Boundary

This design completes an approval-ready selection contract, not an algorithm
implementation. It does not prove edge accuracy, line-fit robustness,
intersection tolerance, XYZ affine validity, re-grid fidelity, Thickness,
Warpage, calibration, or physical/metrology accuracy.

## Completion Record

```text
Status: Complete
Scope: Generic C3D teaching-selection UI and persistence design only.
Acceptance criteria:
  - UI flow, selection types, ownership, validation, migration, and explicit
    non-execution boundary are recorded: Pass.
  - Concrete C3D grid-rectangle, point-set, and correspondence JSON example is
    recorded: Pass.
  - Approval decisions and post-approval verification gates are recorded: Pass.
Verification:
  - Current Studio source reviewed: ToolRecipeDocument, ToolRecipeValidator,
    ToolRecipeDocumentStore, ToolWorkbenchViewModel, Viewer picking/viewport.
  - Current 2D reference reviewed: RoiSnapshotChangedEventArgs and
    RoiImageCanvasViewModel teaching/selection ownership.
Evidence: docs/OPENVISIONLAB_3D_GENERIC_TEACH_SELECTION_DESIGN_20260718.md
Boundary / next dependency: Historical design checkpoint; the implementation
closure immediately below supersedes this approval dependency.
```

## Implementation Closure — 2026-07-18

The owner approved the C3D-first teaching direction after this design was written. The bounded implementation now persists source-bound grid-rectangle and point teaching evidence in recipe schema `1.1`, keeps schema `1.0` readable, and proves Capture/Cancel/Apply without invoking Preview, Run, Publish, or a numerical algorithm. The separate Viewer Profile mode reuses C3D cell picking only for live display; its P1/P2 state never mutates a recipe selection or the existing point-pair tool contract.

Current evidence under `artifacts/ui/20260718-generic-teach-selection-v1` passes selection `17/17`, teaching document `14/14`, capture ViewModel `14/14`, C3D profile sampling `10/10`, Profile ViewModel `8/8`, interactive Profile `6/6`, and the current solution build with zero warnings/errors. The accepted `final-profile.png` records the Viewer endpoints/line and docked height trace.

```text
Status: Complete
Scope: The approved C3D-first generic teaching-selection slice plus a deliberately separate display-only P1/P2 Profile interaction.
Acceptance criteria: source/hash binding -> pass; recipe save/reopen and schema migration -> pass; Capture/Cancel/Apply non-execution boundary -> pass; current-build UI/pointer evidence -> pass.
Verification: focused contracts 78/78; Profile pointer 6/6; current solution build 0 warnings/0 errors.
Evidence: artifacts/ui/20260718-generic-teach-selection-v1 and this document.
Boundary / next dependency: line/plane fitting, edge/intersection execution, XYZ affine solving, physical mapping, and metrology remain outside this completed teaching/display slice.
```
