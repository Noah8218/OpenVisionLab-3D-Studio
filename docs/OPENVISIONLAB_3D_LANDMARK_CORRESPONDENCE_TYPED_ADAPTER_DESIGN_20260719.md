# OpenVisionLab 3D Landmark Correspondence Typed Adapter v1 Design

Updated: 2026-07-20
Status: **Implemented v1 boundary — real fixture execution remains blocked**

Owner approval recorded 2026-07-19: `ExactlyFour` pairs,
`CurrentPublishedCornerAnchor` inputs, non-degenerate source/reference
tetrahedra, and a distinct later XYZ Affine tool were all approved. No
fixture/nominal reference coordinates or real aligned four-anchor acquisition
were supplied with that approval, so this implementation must remain an
uncalibrated structural-evidence gate.

## Implementation record — 2026-07-20

Implemented scope:

- `Core`: immutable `C3DLandmarkCorrespondenceSet` / pair contract with
  canonical SHA-256 evidence identity;
- `Data`: Teaching Recipe schema `1.2` descriptor persistence and strict
  schema validation, while schema `1.0` / `1.1` recipes remain readable;
- `Tools` and `Runner`: one exact-four, current-Published-CornerAnchor rule
  using source/reference rank and normalized tetrahedron-volume checks;
- `Shell`: authored rows and descriptor are Preview/Publish/stale aware;
  changing a row, descriptor, or upstream anchor clears the displayed
  correspondence evidence and requires a new Preview;
- `Viewer`: four exact source anchors and their tetrahedron edges can be
  overlaid over the immutable source C3D; no transformed surface is drawn;
- `Tool Lab`: a single reusable custom-title window presents the source Viewer,
  reference descriptor/coordinates, typed row table, and existing PropertyGrid
  editor. It is paired with a floatable/hideable **Correspondence Evidence**
  dock pane in both Workbench and Advanced layouts; and
- Tool Lab commands are presentation-only until the operator explicitly
  chooses Preview. Publish promotes the current evidence and never reruns it.

Verification recorded from the current Debug build:

| Gate | Result | Evidence |
| --- | --- | --- |
| Build | Pass, 0 warnings / 0 errors | `dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform='Any CPU'` |
| Correspondence rule golden | Pass, `5/5` | `artifacts/verification/20260719-landmark-correspondence-v1/golden.txt` |
| Recipe selection regression | Pass, `17/17` | `artifacts/verification/20260719-landmark-correspondence-v1/selection-contract.txt` |
| Dock / float / hide regression | Pass after the new ninth pane is included | `artifacts/verification/20260719-landmark-correspondence-v1/docking.txt` |
| Current Tool Lab UI | accepted screenshot quality | `artifacts/ui/20260719-landmark-correspondence-v1/after-tool-lab.png` |

The closest reproducible before baseline is the current-build Line Intersection
Tool Lab capture at
`artifacts/ui/20260719-landmark-correspondence-v1/before-line-intersection-tool-lab.png`.
There was no earlier Correspondence Tool Lab to capture without undoing the
implementation, so it is a visual-family baseline, not a same-screen before.

Remaining external gate:

1. Provide four distinct real current `CornerAnchor` outputs and their four
   named reference XYZ values, unit, frame, provenance, revision, and a
   explicitly chosen normalized-volume threshold.
2. Verify a real, non-coplanar source/reference correspondence set through the
   complete `Runner` chain. Until then, do not claim an affine result,
   calibration, metrology accuracy, or a validated production recipe.

## Decision to make

This is the required gate between the implemented source-side feature chain
and a future full-XYZ affine transform:

```text
Source C3D
  -> Filter
  -> Height Difference Edge
  -> 3D Line Fit
  -> Line Intersection
  -> Published CornerAnchor landmarks
  -> Landmark Correspondence v1
  -> XYZ Affine Transform (later)
  -> Re-grid / Thickness / Warpage (later)
```

The product remains a typed, tree-first inspection workbench. It does not
become a free-form graph editor, an automatic feature matcher, or a generic
registration product.

The existing Teaching Recipe schema `1.1` already stores structural
correspondence rows: a source entity ID, a reference landmark ID, reference
XYZ, and reference frame. That is useful authoring data, but it is not yet an
executable correspondence artifact: it does not bind the row to a current
Published `CornerAnchor`, does not identify the reference coordinate source,
and only warns for fewer than four rows. Landmark Correspondence v1 upgrades
that existing path; it must not add a parallel correspondence editor.

## Critical full-XYZ boundary

The owner selected a complete XYZ point-cloud affine map, not a height-map XY
affine correction. A general affine transform has twelve unknowns:

```text
[X' Y' Z']^T = A * [X Y Z]^T + t
```

It needs four source/reference pairs whose source positions are
affine-independent. For a non-singular full-space mapping, the four source
positions **and** the four reference positions must form non-degenerate
tetrahedra. Four corners all lying on one nominal plane do not meet that
requirement, even if small C3D noise makes their numeric rank non-zero.

Therefore the current `UpperLeftCorner` and `LowerRightCorner` are useful
feature evidence but are insufficient. Before the affine tool can run, the
fixture/nominal reference must provide at least one genuinely off-plane
landmark (for example a qualified vertical or raised fixture feature), or the
owner must approve a separate constrained planar transform. Landmark
Correspondence v1 never silently falls back to a planar transform.

This is an input-validity gate only. It does not claim physical calibration,
metrology, or measurement accuracy.

## Proposed v1 scope

### Included after approval

- exactly four explicitly authored source-to-reference pairs;
- source side limited to four current Published `CornerAnchor` outputs from
  the typed Line Intersection adapter;
- one explicit reference frame, reference unit, provenance label, and
  revision for the set;
- deterministic validation of source lineage, duplicate pairs, finite
  coordinates, shared source convention, and affine independence;
- immutable Preview and Published `C3DLandmarkCorrespondenceSet` artifacts;
- a dedicated, reusable, dockable **Landmark Correspondence Tool Lab**;
- Runner replay through the same Tools validation rule; and
- clear hand-off to a later XYZ Affine Tool Lab.

### Explicitly excluded

- calculation, display, or application of an affine matrix;
- least-squares fitting, residual/outlier selection, robust registration, or
  automatic matching;
- accepting five or more pairs in v1;
- using arbitrary grid picks, point-cloud clicks, meshes, planes, or line
  endpoints as landmark sources;
- changing the C3D source, Viewer camera, recipe route, or upstream
  Published artifacts during Preview;
- manufacturing units, physical calibration, Gauge R&R, tolerance decisions,
  or inspection OK/NG; and
- a writable free-form node canvas.

Exactly four is deliberately the smallest safe v1. A later over-constrained
correspondence slice may add five or more pairs only together with an
approved residual, outlier, weighting, and acceptance policy.

## Typed contract

### Recipe teaching data

The current `ToolRecipeSelection` / `ToolRecipeLandmarkCorrespondence` data
remains the authoring source. Schema `1.2` should add a selection-level
correspondence descriptor rather than duplicate each per-row setting:

```text
LandmarkCorrespondenceDescriptor
  referenceFrameId
  referenceUnit
  referenceProvenance        // e.g. approved fixture/CAD/drawing identifier
  referenceRevision
  pairCountPolicy            = ExactlyFour
  minimumNormalizedTetrahedronVolume
  sourceArtifactPolicy       = CurrentPublishedCornerAnchor
```

Each existing row retains only the actual mapping:

```text
sourceEntityId
referenceLandmarkId
referencePosition (X, Y, Z)
```

The per-row `ReferenceFrameId` in schema `1.1` remains readable for backward
compatibility. Saving an approved v1 correspondence selection writes the
single descriptor and requires every legacy row frame to equal its
`referenceFrameId`; it does not guess a unit, provenance, revision, or new
coordinate values.

Proposed closed Tool Recipe step shape:

```json
{
  "id": "step.landmark-correspondence.01",
  "toolId": "landmark-correspondence",
  "toolName": "Landmark Correspondence",
  "minimumInputCount": 1,
  "inputEntityIds": ["selection.landmark-correspondence.01"],
  "outputEntityId": "derived.correspondences.01",
  "parameters": [
    { "name": "PairCountPolicy", "value": "ExactlyFour" },
    { "name": "SourceArtifactPolicy", "value": "CurrentPublishedCornerAnchor" },
    { "name": "AffineIndependencePolicy", "value": "RequireNonDegenerateTetrahedra" }
  ]
}
```

`reference.fixture-landmarks` remains a human-readable recipe reference,
but it is not a substitute for the structured selection. The template must
not be changed to sample XYZ values until the owner supplies the real fixture
reference data.

### Runtime output

`Core` will own immutable `C3DLandmarkCorrespondenceSet` and
`C3DLandmarkCorrespondencePair` contracts. The Preview/Published output must
contain, per pair:

```text
source identity
  sourceEntityId / source OutputRole
  source CornerAnchor XYZ
  source CornerAnchor content SHA-256
  root source entity ID / root source SHA-256
  source unit / frame ID / coordinate convention

reference identity
  referenceLandmarkId / reference XYZ
  reference frame ID / reference unit
  reference provenance / reference revision

set evidence
  exactly four ordered pairs
  pair-count policy / independence policy / taught volume threshold
  source and reference normalized tetrahedron volumes
  source and reference rank result
  canonical content SHA-256
```

The set's SHA-256 is a replay identity, not a calibration certificate. The
output can be consumed only by the later `XYZ Affine Transform` adapter.

### Affine-independence test

For each side of the mapping, let `p1..p4` be the four coordinates. The
validator computes a dimensionless normalized tetrahedron volume:

```text
volume6 = abs(dot(p2 - p1, cross(p3 - p1, p4 - p1)))
span    = maximum pairwise Euclidean distance
normalizedVolume = volume6 / span^3
```

It rejects non-finite values, zero span, a value at or below the explicitly
taught `minimumNormalizedTetrahedronVolume`, or an augmented-coordinate rank
below four. Both source and reference sides must pass. The volume threshold is
dimensionless so that it still detects nearly planar landmark sets when source
raw-height and reference coordinates use different declared units.

The validator reports the values and failing side. It does not calculate an
affine determinant, residual, matrix, or transformed point cloud; those are
the next tool's responsibilities.

## Fail-closed preflight

Preview and Runner emit no correspondence artifact when any rule fails:

- the correspondence selection does not have exactly four rows;
- a row has an empty/duplicate source entity or reference landmark ID;
- a source entity is not an exact current Published `CornerAnchor`, or two
  rows resolve to the same CornerAnchor content identity or XYZ coordinate;
- source root entity/SHA-256, unit, frame, or `column-rawHeight-row`
  convention differs between rows;
- reference frame, unit, provenance, revision, landmark ID, or XYZ is empty
  or invalid, or reference XYZ is non-finite/duplicated;
- the source or reference tetrahedron is rank-deficient or below the taught
  normalized-volume threshold;
- the selection, a source artifact, an upstream Line Intersection input, or
  a reference descriptor changed after Preview; or
- an unknown/duplicated/malformed correspondence parameter is supplied.

The reference unit may differ from the source unit because the future affine
map can express a coordinate conversion. That fact alone is not evidence
that either unit is physically calibrated.

## Tool Lab and Workbench UX

The Tool Lab follows the approved Recipe Navigator -> Tool Lab -> read-first
Flow Map direction. Opening the window only presents the selected step; it
does not run Filter, Edge, Line Fit, Intersection, Correspondence, or Affine.

At the 1920 x 1080 baseline, the reusable dockable/floating window contains:

```text
Published source anchors        Pairing table                  Reference landmarks
3D source Viewer                Source role/entity ->          Reference-frame Viewer
CornerAnchor overlays           reference ID / XYZ             triad + four point markers
source/frame/hash               state and validation            no fabricated surface

typed WPG descriptor            Preview / Discard / Publish     Correspondence evidence
reference frame/unit/provenance current state only              hashes / rank / volumes
```

- The source Viewer shows only the exact root C3D and the four labelled
  published CornerAnchor overlays.
- The reference Viewer draws an empty reference frame, XYZ triad, and the
  four explicit reference-point markers. It must not manufacture a reference
  mesh or transformed source surface.
- The pairing table is the existing correspondence-row editor evolved with
  source role/hash, reference frame/unit, and an explicit status column.
- The WPG card edits the selection descriptor. Icons supplement text,
  tooltips, and accessible names; they never replace them.
- `Preview` validates and creates a temporary immutable set. `Discard`
  removes only that Preview. `Publish` promotes the exact non-stale Preview.
  `Apply Step to Recipe` is separate and never implicit.
- All panes are part of the normal docking system. The menu and tool window
  may be opened once and reused; no viewer window is opened per row.

The Recipe Navigator / Flow Map presents the lineage as:

```text
Published CornerAnchor x4 -> CorrespondenceSet (Declared | Preview | Published)
                         -> XYZ Affine Transform (blocked until implemented)
```

Selection changes, docking, floating, camera movement, overlay visibility,
and pair-table focus are presentation-only operations.

## Explicit lifecycle

| State | Meaning | Allowed action |
| --- | --- | --- |
| `Taught incomplete` | Pair or reference descriptor is incomplete. | Finish teaching. |
| `Waiting for upstream` | One named CornerAnchor is absent, Preview-only, or stale. | Preview/Publish that exact upstream step. |
| `Ready` | Four exact Published anchors and descriptor pass structural preflight. | Preview Correspondence. |
| `Preview running` | The one Tools validation rule is running. | Cancel. |
| `Preview ready` | Immutable correspondence evidence exists. | Review, Discard, or Publish. |
| `Preview stale` | A row, descriptor, or upstream identity changed. | Preview again. |
| `Published` | The current Preview was promoted without recalculation. | Save or teach Affine. |
| `Error` | Provenance, reference, count, or independence check failed. | Correct the named cause. |

```text
Preview Landmark Correspondence
  -> resolve exactly four authored rows
  -> require four exact current Published CornerAnchor artifacts
  -> validate source/reference identity and tetrahedron independence
  -> create temporary C3DLandmarkCorrespondenceSet
  -> do not calculate affine, re-grid, Thickness, Warpage, save, or mutate source

Publish Landmark Correspondence
  -> require current non-stale Preview
  -> promote exactly that output without recalculation
```

## Layer ownership

| Layer | Owns | Does not own |
| --- | --- | --- |
| `Core` | Immutable descriptor/pair/set contracts and closed schema validation. | Viewer state, file dialogs, matrix solving. |
| `Data` | JSON `1.2` compatibility, reference descriptor persistence, and C3D source-byte binding. | Silent frame/unit conversion. |
| `Tools` | Current Published-anchor resolution and correspondence validity rule. | Affine matrix/residual or physical measurement decisions. |
| `Runner` | Headless use of the same Tools rule and evidence report. | Separate correspondence math. |
| Shell ViewModel | Row drafts, stale state, explicit commands, typed registry/tree state. | Geometry/math in code-behind. |
| Viewer / Tool Lab View | Source/reference rendering and selection overlays. | Persistence, validation, or execution. |

## Required verification after implementation

1. Build with zero warnings/errors.
2. Golden/Runner cases: valid exact four, three rows, duplicate source,
   duplicate reference, stale/missing/non-Published source, source mismatch,
   reference metadata failure, non-finite values, coplanar source, coplanar
   reference, below-threshold near-planar set, and stable canonical hash.
3. Workbench/Runner parity: same artifact SHA-256, state, rank, and
   normalized volumes.
4. Lifecycle proof: opening the Tool Lab, changing docking, and editing a
   draft do not run an algorithm; Preview becomes stale after every bound
   input/descriptor change; Publish does not recalculate.
5. Fresh current-build before/after UI captures of main Workbench and Tool
   Lab, including source/reference viewers, no clipped pairing rows, and
   visible non-execution state.
6. Regression of Filter, Edge, Line Fit, Line Intersection, Teaching Recipe,
   dock layout, title views, and BinaryHost boundaries.

## Owner input and approval checkpoint

Implementation remains blocked until all three answers are supplied:

1. **Reference evidence:** provide the four named fixture/nominal landmarks,
   XYZ values, reference frame ID, unit, provenance identifier, and revision.
2. **Full-XYZ geometry:** confirm that those four source/reference landmarks
   are intentionally non-coplanar. If the available physical landmarks are
   planar, choose a separately designed constrained transform instead.
3. **v1 policy:** approve `ExactlyFour` pairs and an explicit
   `MinimumNormalizedTetrahedronVolume` teaching value. The value cannot be
   inferred from the current `3D/Warpage` sample.

Once approved, the next implementation order is: typed Core/Data contract ->
Tools/Runner golden rule -> Shell stale/Preview/Publish -> Tool Lab and fresh
UI evidence. XYZ Affine implementation follows only after this artifact has
passed those gates.
