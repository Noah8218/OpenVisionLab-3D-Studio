# OpenVisionLab 3D 2-Point Line v1 Design

Updated: 2026-07-21

Status: **Owner-approved v1 design; implementation is phased after the Noah
common-line package boundary.**

## Owner approval — 2026-07-21

The owner approved all five v1 decisions in this document: manual construction,
raw C3D plus `PointSet(2)` input, preserved authored order, a narrow common
line geometry boundary, and a separate later 3-Point Plane slice. Implementation
may proceed only through the documented Noah/Studio ownership boundary.

## Decision context

The real four-anchor source/reference package is absent by owner decision.
Therefore the completed A1 Full XYZ Affine Solve remains synthetically
verified only; it does not authorize A2 point application, re-grid, Thickness,
Warpage, calibration, or a physical claim.

The next independent missing tool from the approved teaching workflow is a
manual **2-Point Line**. It uses the already implemented recipe-owned C3D
point-set capture, and creates a deterministic source-coordinate line feature.
It is useful for fixture/datum teaching and, after a narrow shared-line input
change, can be an explicit input to Line Intersection.

```text
C3D HeightField + recipe-owned PointSet(2)
  -> Published PickedLineFeature
  -> Line Intersection (alongside existing fitted LineFeature)
  -> CornerAnchor
```

## Product boundary

Included in v1:

- exactly two ordered, distinct, source-bound C3D grid-cell picks;
- resolution of current finite height values from the selected C3D height
  field during explicit Preview;
- immutable full-XYZ line/segment evidence and canonical SHA-256;
- explicit Preview, Cancel, stale invalidation, Publish, Runner replay, and
  one reusable 2-Point Line Tool Lab; and
- use as an explicit Line Intersection input through a narrow common line
  geometry contract.

Excluded:

- automatic point/edge/line discovery, filtering, fitting, snapping, or
  interpolation;
- tolerance Pass/Fail, calibrated length, physical unit conversion, or GD&T;
- re-running upstream tools;
- applying the A1 affine matrix, re-gridding, Thickness, Warpage, calibration,
  or metrology; and
- a generic graph executor or untyped feature abstraction.

## Recommended typed contract

The existing `C3DLineFeature` is intentionally a fitted-edge object: it owns
an `EdgePointSet`, fit diagnostics, residual policy, and deterministic TLS
provenance. A manually picked line must not pretend to have these facts.

`Library-Noah` owns the pure two-point geometry construction. Studio owns the
typed C3D locator/source adapter and immutable recipe artifact. Add only the
narrow `IC3DLineGeometry` contract needed by Line Intersection:

```text
output identity/content hash
root source identity/hash, unit, frame, coordinate convention
anchor XYZ, normalized direction XYZ, segment-start XYZ, segment-end XYZ
line origin kind = FittedEdge | PickedPoints
```

`C3DLineFeature` implements this contract without changing its fitted-edge
meaning. The new immutable Studio `C3DTwoPointLineFeature` records the
Library-Noah result plus Studio lineage:

```text
selection identity/content hash and source binding
first/second grid locators and resolved XYZ
segment length in source-coordinate
output role
fixed policy = OrderedPointsDefineSegment
provenance and canonical SHA-256
```

This is the smallest shared contract because Line Intersection already needs
the same source/frame/line/segment data from exactly two concrete line types.
No generic `Feature`, factory, or untyped geometry hierarchy is introduced.
See `docs/OPENVISIONLAB_3D_ALGORITHM_OWNERSHIP_AND_NOAH_MIGRATION_20260721.md`
for the binding ownership and package boundary.

## Exact algorithm

For authored pick order `P1`, `P2` resolved from the current selected C3D
height field:

```text
d       = P2 - P1
length  = norm(d)
anchor  = P1
unitDir = d / length
segment = [P1, P2]
```

Preview rejects a missing/stale source binding, missing-height cell,
out-of-range locator, non-finite coordinate, duplicate locator, zero-length
segment, output-ID collision, or cancellation. It never substitutes a nearby
rendered point or a captured historical height.

The authored order is preserved. Reversing the two picks describes the same
geometric support but produces a separate ordered artifact and SHA-256. This
is intentional: the operator can see direction, while Line Intersection's
closest-approach geometry remains order-neutral apart from its explicit
first/second evidence.

## Strict recipe shape

The row uses exactly one current C3D height-field entity and exactly one
recipe-owned point-set selection with cardinality two. Its only authored
parameter is a descriptive non-empty `OutputRole`; all geometry comes from
the selection and current source, not text values.

```json
{
  "id": "step.fixture-line.01",
  "toolId": "two-point-line",
  "inputEntityIds": [
    "source.c3d.height-map",
    "selection.fixture-line.01"
  ],
  "outputEntityId": "derived.fixture-line.01",
  "parameters": [
    { "name": "OutputRole", "value": "FixtureEdgeA" },
    { "name": "ConstructionPolicy", "value": "OrderedPointsDefineSegment" }
  ]
}
```

`ConstructionPolicy` is fixed and validates exactly. The selected height field
must initially be the recipe-bound C3D source. Supporting a saved derived
filtered height field is deliberately deferred until its persisted source and
selection identity contract is separately approved.

## Workbench and Tool Lab UX

The selected-step Teaching Selections card uses the existing explicit
`Capture → Apply` two-cell flow. Selecting the catalog item or opening the
Tool Lab never starts capture or computation.

```text
Step: 2-Point Line
Input: Source C3D HeightField + PointSet(2)
P1 / P2: grid-cell IDs, resolved source-coordinate XYZ, source hash
Role: [FixtureEdgeA]
Policy: Ordered points define segment (fixed)
[Preview] [Cancel] [Publish]
```

The reusable Tool Lab displays the source C3D with P1/P2 at left and the same
source with the picked segment at right. The standard viewer conventions stay
unchanged: line is cyan, P1/P2 are labeled markers, right-drag pans, and a
short right-click opens only display commands. The Workbench Viewer and a
dockable evidence pane expose the same display-only overlay and selected
feature state. No transformed map is shown.

## Lifecycle and validation evidence

```text
Taught incomplete -> capture/apply exactly two points
Ready -> explicit Preview -> Preview ready -> explicit Publish
input/selection/parameter/source change -> Preview stale
```

Runner and Workbench call the same Tools rule. Required synthetic evidence:

1. exact two-point full-XYZ segment and deterministic SHA-256;
2. reversed picks preserve support but retain ordered output/hash;
3. duplicate, missing, stale, missing-height, non-finite, zero-length,
   wrong source/frame, malformed policy, collision, and cancellation reject;
4. Line Intersection accepts a fitted/picked pair only when common lineage,
   closest-approach, angle, gap, and support gates pass; and
5. Workbench Preview/Publish/stale/Runner parity plus current-build Tool Lab
   capture at `1920 x 1080` and `1280 x 760`.

## Approved v1 decisions

The following exact v1 decisions are owner-approved:

1. `2-Point Line` is manual source-coordinate construction, not fitting or a
   measurement result.
2. The initial input is only the recipe-bound raw C3D height field plus one
   source-bound `PointSet(2)`; filtered/derived fields are deferred.
3. P1 → P2 authored order is preserved and replay-hashed.
4. A narrow `IC3DLineGeometry` allows one explicit fitted/picked pair in the
   existing Line Intersection tool; no generic geometry graph is added.
5. `3-Point Plane` is a separate later datum-plane slice, not bundled into
   this implementation.

## Durable boundary

This design does not change the status of A1's absent real fixture. Real
four-anchor correspondence evidence remains the prerequisite for actual A1
Preview/Publish replay and for every affine application or transformed-surface
algorithm.
