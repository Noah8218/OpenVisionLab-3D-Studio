# OpenVisionLab 3D 3-Point Plane v1 Design

Updated: 2026-07-21

Status: **Draft design only. No Studio or Library-Noah implementation is
authorized by this document.**

## Purpose

The next small typed feature is a manual datum-plane construction:

```text
raw C3D HeightField + recipe-owned PointSet(3)
  -> OrientedPlaneFeature
```

It completes no inspection by itself. Its purpose is to preserve three
operator-selected full-XYZ points as a deterministic plane feature that a
later separately approved reference/deviation, plane intersection, or datum
workflow can consume explicitly.

This is deliberately separate from the completed 2-Point Line and from the
unavailable A1 real-fixture/A2 Apply/Re-grid/Thickness/Warpage path.

## v1 contract

Input is exactly:

- the recipe-bound raw C3D height field; and
- one current source-bound `PointSet(3)` with exactly three distinct C3D
  grid-cell locators.

Each Preview resolves `P1`, `P2`, and `P3` from the current C3D bytes as:

```text
P = (column, raw-height, row)
```

Captured historic height values are selection evidence only. They are never
used as executable geometry. Source length/SHA-256/grid/frame identity must
match before locator resolution.

The deterministic construction is:

```text
u = P2 - P1
v = P3 - P1
n = normalize(cross(u, v))
d = -dot(n, P1)
plane = n.x * X + n.y * Y + n.z * Z + d = 0
support = triangle(P1, P2, P3)
```

`n` is an **oriented** normal. Its direction follows the authored `P1 -> P2
-> P3` order under the existing right-handed Viewer convention (`Y` is raw
height). Reversing two picks produces the same geometric plane but the opposite
normal and a different ordered artifact hash. This makes later signed distance
semantics explicit instead of silently canonicalizing the normal.

The pure tool rejects null/non-finite points, duplicate points, zero-length
edges, and collinear or numerically degenerate triples. The proposed fixed
degeneracy test is a dimensionless normalized cross-product magnitude:

```text
norm(cross(u, v)) / (norm(u) * norm(v)) > 1e-12
```

It is a fixed v1 validity boundary, not a taught measurement tolerance.

## Typed ownership boundary

`Library-Noah` owns only the source-neutral calculation:

```text
ThreePointPlaneInput(P1, P2, P3)
  -> ThreePointPlaneResult(anchor=P1, normal, d, support triangle)
```

It has no C3D, recipe, WPF, Viewer, hash, tolerance, or inspection dependency.
It must live next to `TwoPointLineTool` in `Lib.ThreeD.FeatureExtraction` and
be independently tested there before Studio consumes a versioned package.

Studio owns:

- strict raw C3D plus `PointSet(3)` recipe preparation and current source
  binding verification;
- C3D locator-to-XYZ resolution, immutable `C3DThreePointPlaneFeature`,
  canonical Studio content hash, output ID, role, provenance, and runner
  report;
- explicit Preview, Cancel, Publish, branch-local stale state, source-change
  clearing, Artifact Registry state, WPG, Viewer overlay, and Tool Lab; and
- conversion to/from the Noah input/result without duplicating cross product,
  normalization, plane-equation, or degeneracy numerical code.

No generic `Feature`, factory, graph executor, or broad surface abstraction is
introduced. A later plane consumer gets a separate narrow interface only when
there are at least two concrete plane producers.

## Recipe and UI

The recipe step has only one editable semantic parameter in v1:

```json
{
  "toolId": "three-point-plane",
  "inputEntityIds": [
    "source.c3d.height-map",
    "selection.datum-plane.01"
  ],
  "outputEntityId": "derived.datum-plane.01",
  "parameters": [
    { "name": "OutputRole", "value": "FixtureDatum" },
    { "name": "ConstructionPolicy", "value": "OrderedPointsDefineOrientedPlane" }
  ]
}
```

`ConstructionPolicy` is fixed/read-only. There is no fit residual, ROI,
tolerance, automatic point search, acceptance state, calibrated unit, or
reference transformation parameter.

The Tool Lab follows the existing dual-viewer pattern while retaining its own
OpenVisionLab layout:

```text
Input viewer: raw C3D + numbered P1/P2/P3 markers
Output viewer: same source + translucent support triangle + normal arrow
Review: point locators/current XYZ/source hash/normal/equation/published state
```

Opening a Tool Lab or changing a View never captures, computes, publishes, or
changes the recipe. The standard Viewer gesture boundary remains intact:
right-drag pans and a short right-click opens display commands only.

## Lifecycle and implementation evidence

```text
Taught incomplete -> capture/apply exactly three points
Ready -> explicit Preview -> Preview ready -> explicit Publish
input/selection/role/source change -> only this branch and its explicit
downstream consumers become stale
```

Implementation is complete only when all are current-source checks:

1. Library-Noah deterministic construction/error suite, including authored
   normal reversal and a near-collinear rejection;
2. Studio Golden/Runner suite proving C3D source identity, point order,
   selection hash, actual source locator resolution, strict parameter shape,
   output identity, malformed input rejection, and cancellation;
3. Workbench suite proving WPG apply/discard, explicit Preview/Publish,
   independent branch preservation, stale/source replacement clearing, and
   Artifact Registry state;
4. existing 2-Point Line, Line Intersection, recipe teaching, WPG, Artifact
   Navigator, and docking regressions; and
5. a current-build actual C3D Tool Lab capture with all three points, plane
   support, normal direction, and non-OK/NG boundary visible.

## Explicit exclusions

v1 does not:

- fit a best plane to a region or report residuals;
- calculate flatness, warpage, thickness, angle, distance, or acceptance;
- infer a physical datum, edge, normal direction, unit, or calibration;
- apply the plane to a point cloud, transform/re-grid a C3D map, or trigger
  any upstream/downstream execution; or
- reopen A1's absent real fixture, A2 Apply, A3 Re-grid, or metrology claims.

## Owner decisions required before implementation

1. Confirm the recommended authored-order normal policy (`P1 -> P2 -> P3`)
   rather than canonicalizing to a frame-positive normal.
2. Confirm that v1 remains a raw-C3D manual datum feature with one `OutputRole`
   only, and has no immediate measurement/warpage consumer.
3. Confirm that a small translucent support triangle plus normal arrow is the
   desired Viewer evidence rather than a fitted ROI plane.

On approval, first add and verify the pure Noah tool/package; only then add
the typed Studio adapter. The design does not authorize changing either
repository yet.
