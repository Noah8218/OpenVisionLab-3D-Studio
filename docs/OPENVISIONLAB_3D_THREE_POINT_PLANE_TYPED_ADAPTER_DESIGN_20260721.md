# OpenVisionLab 3D 3-Point Plane v1 Design

Updated: 2026-07-21

Status: **Complete for deterministic local software evidence.** Owner approval
was recorded before implementation; the exact Noah tool, Studio adapter, UI,
Runner replay, and current-build C3D Tool Lab evidence are now recorded below.

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

Completion evidence (2026-07-21):

1. Library-Noah commit `f62345c137b0c0d5e8b671c92f448e0c87f3e88a` builds
   `0/0`; `Lib.Inspection.Smoke` passes `23/23`, including authored normal
   reversal plus collinear and near-collinear rejection. The vendored
   `Lib.ThreeD` 2.4.0 SHA-256 is
   `D128C08B27A1FFF43EE32EFB11675EA067656711E7C13B545EEDEDF9238060E0`.
2. Studio `C3DThreePointPlaneGoldenVerification` passes `7/7`: current-source
   binding, order/hash, degeneracy, strict shape, tamper rejection, Runner,
   and cancellation. The actual Thickness-C3D Runner replay is `Pass` with
   output SHA-256 `A9AEE8119424035DEDF640908B014443346F2AD0E4B688AF2B4B9BB697DFDAC9`.
3. The Workbench verification passes `11/11`: typed WPG, explicit
   Preview/Publish, ordered normal/equation, headless identity parity, stale
   clearing, source replacement, and `PlaneFeature` artifact state. There is
   intentionally no Plane measurement or Warpage consumer in v1.
4. Current-source regressions pass: 2-Point Line `16/16`, Line Intersection
   `23/23`, recipe teaching `18/18`, Recipe Manager/WPG `18/18`, Artifact
   Navigator `24/24`, and docking `25/25`; the Debug solution build is `0/0`.
5. Current-build actual-Thickness C3D Tool Lab capture passes screenshot
   quality on the first attempt and visibly shows all three ordered markers,
   support triangle boundary, normal arrow, typed `PlaneFeature`, and the
   no-OK/NG boundary. Evidence is under
   `artifacts/three-point-plane-adapter-20260721/`.

## Explicit exclusions

v1 does not:

- fit a best plane to a region or report residuals;
- calculate flatness, warpage, thickness, angle, distance, or acceptance;
- infer a physical datum, edge, normal direction, unit, or calibration;
- apply the plane to a point cloud, transform/re-grid a C3D map, or trigger
  any upstream/downstream execution; or
- reopen A1's absent real fixture, A2 Apply, A3 Re-grid, or metrology claims.

## Owner decisions resolved before implementation

1. The authored `P1 -> P2 -> P3` right-hand-rule normal is retained; it is
   never silently canonicalized.
2. v1 remains a raw-C3D manual datum `PlaneFeature` with only `OutputRole`;
   it has no immediate measurement, affine-apply, Thickness, or Warpage
   consumer.
3. Viewer evidence is the translucent magenta support triangle, contrast
   boundary, ordered point markers, and magenta normal arrow on the current
   source surface rather than a fitted ROI plane.
