# OpenVisionLab 3D Line Intersection Typed Adapter v1 Design

Updated: 2026-07-19
Status: **Complete (approval design; implementation intentionally blocked)**

## Purpose and product boundary

This document defines the next typed feature-extraction slice after the
implemented 3D Line Fit v1 adapter:

```text
Published FilteredHeightField
  -> Published EdgePointSet
  -> Published LineFeature A + Published LineFeature B
  -> CornerAnchor evidence
  -> Landmark Correspondence (later)
  -> XYZ affine / re-grid / measurement (later)
```

It serves the owner's intended full-XYZ workflow: derive two fitted 3D edge
lines around a physical corner, determine their supported closest approach,
and create one named source-coordinate landmark for later correspondence.

`Line Intersection` is a user-facing tool name. In three dimensions, two
non-parallel infinite lines can be **skew** and therefore have no exact common
point. v1 must never silently invent a geometric intersection. It calculates
the two closest points first and emits a `CornerAnchor` only when every taught
quality gate passes.

This is a source-coordinate feature extractor, not a physical measurement,
calibration, datum, GD&T, tolerance decision, or inspection OK/NG result.
The current C3D convention remains `X=column`, `Y=raw-height`, `Z=row`.

## Scope

### Included after owner approval

- exactly two current Published `C3DLineFeature` inputs;
- deterministic full-XYZ closest-points calculation on their infinite lines;
- explicit maximum closest-approach distance, minimum acute angle, and
  maximum inlier-support extension;
- a typed immutable `C3DLineIntersectionFeature` whose semantic output is a
  `CornerAnchor`;
- explicit Workbench Preview, Cancel, stale invalidation, and Publish;
- one reusable, dockable **Intersection Tool Lab** for source/candidate
  comparison and parameter teaching;
- one dockable **Intersection Evidence** pane with linked Viewer evidence;
- Runner replay through the same Tools rule; and
- numerical, lifecycle, Viewer, Tool Lab, and regression verification.

### Explicitly excluded

- rerunning Filter, Edge, or Line Fit from this tool;
- arbitrary point-cloud/mesh intersection;
- automatic corner, edge, or line discovery;
- an implicit fallback to a line endpoint, a projected grid cell, or a
  nearest rendered point;
- accepting parallel or near-parallel lines;
- creating a measurement Pass/Fail, physical unit, or calibrated landmark;
- executing Landmark Correspondence, XYZ affine, re-grid, Thickness, or
  Warpage; and
- a free-form graph editor or automatic creation of extra Viewer windows.

## Why closest approach is the required mathematical contract

For two normalized fitted lines,

```text
L1(t) = A + t * u
L2(s) = B + s * v
```

the adapter preserves the authored input order and evaluates:

```text
w       = A - B
a, b, c = dot(u, u), dot(u, v), dot(v, v)
d, e    = dot(u, w), dot(v, w)
D       = a*c - b*b
t       = (b*e - c*d) / D
s       = (a*e - b*d) / D
C1      = L1(t)
C2      = L2(s)
gap     = length(C1 - C2)
anchor  = (C1 + C2) / 2
angle   = acos(abs(dot(u, v))) in degrees
```

The directions already stored by Line Fit are unit vectors, but the adapter
still validates their finite non-zero length before this calculation. It must
reject `D` at or below the taught angle policy instead of allowing an unstable
division for parallel or near-parallel lines.

The emitted `CornerAnchor` is the midpoint of `C1` and `C2`; the two distinct
closest points and the gap remain part of immutable evidence. The midpoint is
therefore a documented representative anchor, not a claim that the measured
surfaces meet at exactly that coordinate.

## Required typed input and output

### Input preconditions

Both inputs must be independently current Published `C3DLineFeature` objects
and all of these values must agree exactly:

- root source entity ID;
- root source SHA-256;
- `unit`;
- `frameId`; and
- `column-rawHeight-row` coordinate convention.

The tool rejects two references to the same entity ID or the same LineFeature
content SHA-256. It accepts only the two IDs authored on the selected recipe
row, in that order. A Preview object, stale published object, a line from a
different source/frame, or an undeclared entity cannot be substituted.

### Proposed Core contract

`Core` will own one immutable `C3DLineIntersectionFeature` with canonical
content SHA-256. Its minimum evidence is:

```text
identity and lineage
  outputEntityId
  firstLineEntityId / firstLineContentSha256
  secondLineEntityId / secondLineContentSha256
  rootSourceEntityId / rootSourceSha256
  unit / frameId / coordinateConvention

taught contract
  maximumClosestApproachDistance
  minimumAcuteAngleDegrees
  maximumSupportExtension
  outputRole
  closestApproachPolicy = MidpointOfClosestPoints
  parallelPolicy        = RejectBelowMinimumAcuteAngle
  supportPolicy         = WithinInlierProjectionExtentsWithMaximumExtension

geometric evidence
  cornerAnchorX / Y / Z
  firstClosestX / Y / Z
  secondClosestX / Y / Z
  firstLineParameter / secondLineParameter
  acuteAngleDegrees
  closestApproachDistance
  firstSupportMinimum / firstSupportMaximum / firstSupportExtension
  secondSupportMinimum / secondSupportMaximum / secondSupportExtension

provenance and replay evidence
  provenance
  canonical contentSha256
```

The canonical hash includes the fixed policies, both ordered input identities,
all taught values, and all finite output values as IEEE-754 values. It is
replay evidence only.

`OutputRole` is operator-authored descriptive data such as
`UpperLeftCorner` or `LowerRightCorner`. It must be non-empty and unique in a
recipe; it has no effect on the geometry.

### Support is a gate, not a substitute line definition

Line Fit's `SegmentStart/End` values remain finite inlier-projection support
evidence. The infinite lines define closest approach. Separately, v1 checks
whether `t` and `s` lie within each line's inlier projection range expanded by
the explicit `MaximumSupportExtension`.

This distinction prevents the common failure where a mathematically valid
intersection lies far outside the edge observations and is accidentally used
as a fixture landmark. It still permits a small, deliberately taught extension
when the two edge bands stop just before the physical corner.

No `CornerAnchor` is emitted if either closest point is outside this bounded
support gate.

## Strict recipe shape

The current template placeholders remain unchanged until approval. The
approved v1 rows would use this closed shape; no smoke values are to be saved.

```json
{
  "id": "step.corner.ul.01",
  "toolId": "line-intersection",
  "toolName": "Line Intersection",
  "minimumInputCount": 2,
  "inputEntityIds": [
    "derived.line-ul-horizontal.01",
    "derived.line-ul-vertical.01"
  ],
  "outputEntityId": "derived.corner-ul.01",
  "parameters": [
    { "name": "MaximumClosestApproachDistance", "value": "Set explicitly" },
    { "name": "MinimumAcuteAngleDegrees", "value": "Set explicitly" },
    { "name": "MaximumSupportExtension", "value": "Set explicitly" },
    { "name": "OutputRole", "value": "UpperLeftCorner" },
    { "name": "ClosestApproachPolicy", "value": "MidpointOfClosestPoints" },
    { "name": "ParallelPolicy", "value": "RejectBelowMinimumAcuteAngle" },
    { "name": "SupportPolicy", "value": "WithinInlierProjectionExtentsWithMaximumExtension" }
  ]
}
```

The parser requires exactly these seven names once each and rejects unknown or
duplicated names, placeholders, locale-dependent numbers, unit suffixes,
non-finite numbers, negative extension, non-positive gap, angles outside
`(0, 90]`, an empty output role, an output ID collision, or an input/output ID
match. `MaximumSupportExtension` may be exactly zero.

The `LowerRightCorner` row follows the same contract with its two explicit
line IDs and output role. The operator teaches all three numeric limits for
each actual corner; no value is inferred from the Line Fit smoke fixture or
copied automatically between the two rows.

## Workbench and Tool Lab UX

The product retains its bounded, GoPxL-inspired chain rather than a generic
node canvas. The Recipe Navigator and read-only Flow Map show the two incoming
named `LineFeature` entities and one outgoing `CornerAnchor` so the user can
trace source and result without guessing the connection.

### Workbench authoring card

When a `Line Intersection` row is selected, the typed WPG card shows:

```text
Step 05: Line Intersection
Inputs                 Line A / Line B (Published identity and hash)
Maximum closest gap    [explicit source-coordinate value]
Minimum acute angle    [explicit degrees]
Support extension      [explicit source-coordinate value]
Output role            [UpperLeftCorner]
Fixed policy           closest midpoint / reject near-parallel / bounded support
Preview selected Line Intersection     Publish selected Line Intersection
```

The card labels all geometric distances as **source-coordinate**, not mm.
Editing an input route, parameter, output ID, or upstream published identity
clears the candidate and marks Preview stale. Editing does not run any tool.

### Intersection Tool Lab

Selecting an existing Line Intersection step opens one reusable custom-chrome
`Intersection Tool Lab` window. It never executes Preview or Publish on open.
At the 1920 x 1080 baseline it presents:

```text
┌ Source geometry (left 3D viewer) ─────┬ Candidate evidence (right 3D viewer) ┐
│ root C3D + both published line overlays│ same exact root C3D + closest pair   │
│ Line A teal / Line B violet            │ amber connector + magenta anchor     │
├────────────────────────────────────────┴──────────────────────────────────────┤
│ typed PropertyGrid • preflight • angle/gap/support evidence • Preview/Publish │
└───────────────────────────────────────────────────────────────────────────────┘
```

Both Viewers use the exact root source associated with the two published
lines. The right Viewer never fabricates a transformed surface; it overlays
only the candidate evidence on the same source. All views remain dockable or
floatable under the existing workspace policy. A later comparison workspace
may pin these views, but the tool must not automatically create unbounded
Viewer windows.

### Viewer and diagnostics

After Preview, the shared Workbench Viewer and Tool Lab show:

- first fitted support segment in teal;
- second fitted support segment in violet;
- closest-points connector in amber;
- midpoint CornerAnchor marker in magenta;
- a compact HUD with Preview/Published state, gap, acute angle, and support
  extensions; and
- a dockable `Intersection Evidence` pane containing both input IDs/hashes,
  closest-pair coordinates, gap, angle, support checks, output identity, and
  source/frame labels.

The top `View` menu and short-right-click canvas menu expose the same
display-only `Intersection overlays` toggles. Right-button drag continues to
pan; only a short right-click opens the menu. Selecting an evidence row or an
overlay marker changes selection/highlighting only and never recalculates.

## Explicit state and execution contract

| State | Meaning | Allowed action |
| --- | --- | --- |
| `Taught incomplete` | A route, output role, or explicit limit is missing. | Complete teaching. |
| `Waiting for upstream` | One or both LineFeature outputs are absent, stale, Preview-only, or unpublished. | Preview and Publish the named line steps. |
| `Ready` | Both exact Published lines and all parameters pass preflight. | Preview intersection. |
| `Preview running` | The single Tools rule is calculating off the UI thread. | Cancel. |
| `Preview ready` | One immutable corner candidate and evidence exist. | Review or Publish. |
| `Preview stale` | An input identity or authored value changed. | Preview again. |
| `Published` | The exact non-stale Preview object was promoted. | Save or teach correspondence. |
| `Error` | A lineage, geometry, quality, support, or parameter rule failed closed. | Correct the named cause. |

```text
Preview selected Line Intersection
  -> require the two exact current Published LineFeature inputs
  -> call one Tools rule
  -> create temporary C3DLineIntersectionFeature + evidence
  -> do not run Filter, Edge, Line Fit, correspondence, affine, or save

Publish selected Line Intersection
  -> require the current non-stale Preview
  -> promote the exact same object without recalculation

show / hide / dock / float / select evidence / change overlay visibility
  -> presentation state only
```

Whole-recipe Run remains blocked until this and all later typed adapters exist.

## Fail-closed rules

Preview and Runner produce no output for any of the following:

- anything other than exactly two named current Published LineFeature inputs;
- source ID/SHA-256, unit, frame, or coordinate-convention mismatch;
- duplicate line entity/content identity;
- empty, colliding, or input-equal output ID;
- missing, duplicate, malformed, placeholder, or unknown parameters;
- non-finite line/endpoint values or non-unit/degenerate directions;
- an angle at or below the taught minimum;
- a non-finite closest parameter, closest coordinate, gap, angle, or support
  calculation;
- closest gap greater than the taught maximum;
- either closest parameter outside its line's bounded support interval;
- cancellation before immutable candidate completion; or
- stale upstream output at Preview or Publish time.

No partial output, automatic snapping, or persisted candidate survives an
error or cancellation.

## Ownership boundary

| Layer | Owns | Does not own |
| --- | --- | --- |
| `Core` | Immutable intersection contract, ordered lineage, geometric evidence, canonical hash. | Calculation, WPF, C3D loading. |
| `Data` | Existing published-artifact/source identity lookup. | Line math or parameter decisions. |
| `Tools` | Strict parser, closest-points math, angle/gap/support gates, output creation. | Recipe mutation, View transforms, file dialogs. |
| `Shell ViewModel` | Selected row, explicit lifecycle, stale propagation, WPG state, Tool Lab presentation state. | A second intersection algorithm. |
| `Viewer` | Overlay rendering, picking, HUD, presentation-only visibility state. | Geometry calculation or persistence. |
| `Docking View` | Intersection Evidence presentation and linked selection. | Acceptance reclassification. |
| `Runner` | Calls the same Tools rule and records the same output SHA-256. | Separate headless math. |

Do not introduce a generic graph executor or adapter factory in this slice.
Direct typed dispatch remains the simpler verified choice until repetitive
behavior proves a stable abstraction.

## Verification gates after approval

### Numerical Golden

1. Exact perpendicular 3D lines yield identical closest points, zero gap, the
   analytic midpoint, and the expected acute angle.
2. An oblique exact 3D intersection is recovered with fixed tolerance.
3. A skew pair below and exactly at maximum gap emits the documented midpoint;
   one immediately above it rejects.
4. Minimum acute angle passes at equality and rejects immediately below it;
   parallel and anti-parallel lines reject.
5. Both finite support boundaries pass at equality; one distance immediately
   beyond maximum extension rejects.
6. Swapping authored line order preserves geometric midpoint/gap but retains
   the ordered first/second evidence and therefore its own replay hash.
7. Same-ID, same-hash, root/frame/unit mismatch, malformed policy, non-finite
   geometry, output collision, stale identity, and cancellation fail closed.
8. Repeated calculation is byte-identical, and Workbench/Runner produce the
   same output SHA-256.

### Workbench, Tool Lab, and Viewer

- all eight lifecycle states are ViewModel verified;
- Preview refuses unpublished/stale Line Features and never invokes Line Fit;
- parameter/upstream edits clear candidate overlays and evidence without
  calculation;
- Publish promotes the exact Preview object without rerunning the rule;
- Tool Lab opening selects the row and source views but never executes;
- both Viewers show the same source identity and their intended overlays;
- menu/docking/select actions perform zero tool calls;
- right-drag pan, short-right-click, top View menu, orbit, zoom, pick, and
  Profile remain green; and
- current-source captures show the typed card, source/candidate comparison,
  HUD, and dockable evidence pane.

### Regression and delivery

- solution build with zero warnings/errors;
- Filter, Edge, Line Fit Golden and Workbench parity regressions;
- recipe teaching/routing/source-binding checks;
- docking, profile, height-distribution, pointer, and BinaryHost boundaries;
- Runner report evidence and CI invocation/assertions for the new Golden,
  Workbench, Tool Lab, and current-source smoke gates; and
- owner review of both real upper-left/lower-right line pairs with explicitly
  taught gap, angle, and support-extension limits.

## Approval checkpoint

Implementation may begin only after the owner approves or changes all nine
decisions below:

1. Consume exactly two current Published `C3DLineFeature` inputs in authored
   order and never rerun upstream tools.
2. Use full-XYZ infinite-line closest approach, retaining both closest points
   and their midpoint as evidence; do not fabricate an exact intersection.
3. Emit the midpoint only as a named `CornerAnchor` when `gap` is at or below
   an explicit source-coordinate maximum.
4. Reject parallel/near-parallel lines using an explicit minimum acute angle
   in degrees; this is a feature-quality gate, not inspection OK/NG.
5. Require each closest point to remain within its inlier support extent plus
   an explicit maximum extension; never accept unlimited extrapolation.
6. Preserve root source/frame/unit/coordinate identity and reject duplicate or
   incompatible line evidence before geometry calculation.
7. Use the exact seven-parameter recipe schema and retain real row limits as
   `Set explicitly` until the owner teaches them.
8. Provide the separate reusable Intersection Tool Lab, identical source and
   candidate 3D views, dockable Intersection Evidence, and mirrored
   display-only overlay controls.
9. Keep Preview/Publish explicit, Runner shared, whole Run blocked downstream,
   and success as feature extraction rather than a metrology or OK/NG claim.

## Completion record

```text
Status: Complete
Scope: Line Intersection v1 design and approval contract only. No Core, Data,
       Tools, Shell, Viewer, Runner, recipe, or CI implementation was changed.
Acceptance criteria:
  - Full-XYZ skew-line math and no-fabricated-intersection rule: Pass.
  - Typed lineage/output, strict recipe, UX, states, failures, ownership, and
    verification gates: Pass.
  - Nine owner decisions made explicit before implementation: Pass.
Verification:
  - Implemented Line Fit contracts/rule/adapter, teaching template, current
    Workbench catalog, Viewer/diagnostics contracts, product target, handoff,
    and generic selection contract were inspected read-only.
Evidence: docs/OPENVISIONLAB_3D_LINE_INTERSECTION_TYPED_ADAPTER_DESIGN_20260719.md
Boundary / next dependency: explicit owner approval or requested changes to
  all nine decisions. Do not implement the adapter before that approval.
```
