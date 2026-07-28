# OpenVisionLab 3D A2 Full-XYZ Affine Apply Design

Updated: 2026-07-21

Status: Owner-approved on 2026-07-21. The seven decisions below authorize the
bounded A2 implementation recorded in the implementation checkpoint below.

## Implementation checkpoint - 2026-07-21

The approved A2 v1 is implemented for deterministic local software evidence.
`Lib.ThreeD` `2.6.1` from committed Library-Noah source
`b3060175c34956001662383adfe57e14abbdd92a` owns the source-neutral,
allocation-bounded full-XYZ point loop. Studio consumes only the vendored
package, reads and validates raw C3D bytes, owns typed recipe lifecycle and
immutable contracts, and renders a separate result view. There is no
cross-repository `ProjectReference`.

Current evidence:

- Studio A2 Runner golden: `4/4` pass (`artifacts/current/a2-affine-apply-golden-final.txt`).
- A1 Runner regression: `4/4` pass (`artifacts/current/a1-affine-solve-regression-final.txt`).
- Vendored package verification passed for `Lib.ThreeD 2.6.1`, the exact
  source commit above, and SHA-256
  `22147F12F43F3B22044A2BBDA50137272C75F6E15408CD076FAEAE596E90B9A8`
  (`artifacts/current/a2-library-noah-package-final.txt`).
- Current `Any CPU` Shell build passed with `0` warnings and `0` errors.
- The current-build A2 Tool Lab waiting-state capture passed screenshot quality
  on attempt one at `1600 x 960`:
  `artifacts/current/a2-affine-apply-tool-lab-after.png`.

The Tool Lab evidence intentionally stops at `A1 Publish required`: the real
four-anchor source/reference package and its Published A1 matrix remain absent.
Therefore, the actual transformed line-cloud UI Preview/Publish and a real
headless replay are **unverified**, not implied by the synthetic golden suite.

## Purpose and narrow scope

A1 has already solved and published an immutable source-to-reference
`C3DAffineTransform3D`. A2 applies that already-published matrix to every
finite point from one verified raw C3D source:

```text
verified raw SourceC3D + current Published AffineTransform3D
    -> TransformedPointCloud
```

This is the owner's chosen full XYZ point-cloud affine path. It is not an XY
height-map correction and it must not use Viewer-normalized coordinates.

| Included in A2 | Explicitly excluded from A2 |
| --- | --- |
| One full-XYZ matrix application to every finite raw C3D cell | A new solve, best fit, least squares, automatic matching, or planar fallback |
| Source identity, transform identity, result hash, Preview/Discard/Publish, stale state, and Runner parity | C3D mutation, C3D file output, re-grid, interpolation, collision policy, fill, smoothing, triangulation, or mesh creation |
| Source-versus-result 3D comparison in a dedicated Tool Lab | Thickness, Warpage, calibration, Gauge R&R, physical units, or metrology claims |
| Source-to-reference frame provenance and raw-grid locator retention | Filtered-height-field application in v1, generic graph editing, device, PLC, robot, cloud, or production control |

`RegriddedHeightField` remains A3. A transformed cloud is not automatically a
height map and is therefore not an eligible Thickness or Warpage input.

## Coordinate and missing-value contract

The verified C3D source contract is already fixed:

```text
source point p = (column, rawHeight, row)
source convention = column-rawHeight-row
source frame      = recipe Source.FrameId
source unit       = recipe Source.Unit
```

For each finite, non-zero C3D sample at row `r`, column `c`, raw height `h`:

```text
p = (c, h, r)
q = PublishedAffineTransform.Transform(c, h, r)
```

`q` is stored in the transform's declared reference frame. The existing
`C3DHeightGrid` centered/scaled Viewer coordinates are presentation-only and
must never be passed into A2 math. A2 uses double arithmetic end to end.

Zero and non-finite C3D samples remain missing: no output point is created for
them. The output retains source grid width, height, finite count, and missing
count so the absence is visible and deterministic. It never invents a point,
height, color, triangle, or neighbour.

## Inputs, route, and closed v1 policy

A2 has exactly two required inputs:

1. The recipe's verified raw `SourceC3D` identity.
2. One current Published `AffineTransform3D` routed as the exact upstream
   entity.

The transform must have the same root source entity ID, source SHA-256,
source frame, source unit, and `column-rawHeight-row` convention as the raw
C3D input. A stale, preview-only, foreign-source, unit/frame/convention
mismatch, non-finite, or non-invertible transform is rejected before point
iteration.

V1 has no editable numerical parameter and no surface selector. It always
applies to the raw source. The existing filtered field can remain upstream
evidence for teaching anchors, but it cannot silently become the inspection
surface. A later filtered-surface variant requires its own source/meaning and
comparison contract.

## Output contract

Core adds a source-neutral immutable `C3DTransformedPointCloud` contract only
after approval. Its v1 shape is deliberately small and ordered:

```text
C3DTransformedPointCloud
  ContractVersion
  OutputEntityId
  RootSourceEntityId / RootSourceSha256 / SourceContentSha256
  SourceFrameId / SourceUnit / SourceCoordinateConvention
  ReferenceFrameId / ReferenceUnit / ReferenceProvenance / ReferenceRevision
  AffineTransformEntityId / AffineTransformContentSha256
  SourceGridWidth / SourceGridHeight / FinitePointCount / MissingPointCount
  Ordered C3DTransformedPoint[] (row-major source locator order)
  Provenance / ContentSha256

C3DTransformedPoint
  Row / Column / RawHeight
  X / Y / Z                         // source-to-reference transformed double XYZ
```

The result is an in-memory, immutable execution artifact. Recipes persist the
step route and no transformed data cache. Viewer and Runner recreate the
output only from the verified source bytes plus the published transform. This
avoids a second mutable C3D file and keeps result lineage auditable.

The canonical SHA-256 covers the closed metadata above and every transformed
point in row-major locator order using invariant, bit-stable double encoding.
The original source is not rewritten, and neither source XYZ nor display RGB
is duplicated beyond the necessary raw height and locator evidence.

## Execution and responsibility boundaries

| Layer | A2 responsibility | Must not do |
| --- | --- | --- |
| Library-Noah `Lib.ThreeD` | Source-neutral finite-point transform loop, input validation, output ordering, and canonical result construction | Read C3D files, hold WPF state, re-grid, or measure |
| Studio Data | Read and verify exact raw C3D bytes; expose raw grid samples to the adapter | Apply Viewer scaling or infer physical units |
| Studio Tools | Adapt verified C3D points and a published affine contract into the Library-Noah call; enforce recipe routing and source lineage | Reimplement affine math or create WPF state |
| Core | Immutable typed transformed-cloud contracts | Source paths, dialogs, mutable caches, or rendering state |
| Runner | Recreate the same output and report the same identity/hash | Use alternate transform math |
| Shell ViewModel | Explicit Preview/Discard/Publish/cancel/stale lifecycle and artifact registration | Run on Tool Lab open or parameter selection |
| Viewer / Tool Lab | Render raw source and transformed cloud as separate evidence views | Execute, save, re-grid, or imply calibrated scale |

Library-Noah is the algorithm owner, in accordance with the product boundary.
Studio may update the vendored `Lib.ThreeD` package only after the Library-Noah
implementation and its independent package verification are complete.

## Preview, Publish, stale, and source lifecycle

```text
Ready
  -> Preview (temporary TransformedPointCloud)
  -> Discard (remove only temporary output)
  -> Publish (promote the exact Preview, without recomputation)
  -> Stale (route/source/transform identity changes)
```

- Opening, docking, floating, camera movement, overlay visibility, Tree/Flow
  Map selection, or Tool Lab opening never applies the transform.
- Preview is permitted only when the raw C3D is verified and the exact routed
  affine output is current and Published.
- Publish stores the exact Preview reference and records its canonical hash;
  it never reruns A2.
- Changing source bytes, source route, affine route, affine content hash,
  affine publication state, source frame/unit/convention, or replacing the
  selected recipe step clears the A2 Preview and any Published A2 output.
- Cancel preserves the source, published transform, recipe, and prior
  published artifact. It does not write a partial result.

No WPG draft is shown in v1 because there is no authored parameter. Step
Parameters instead presents a read-only typed route card with source,
transform, reference frame, finite/missing counts, and execution state.

## UI and Viewer evidence

`Apply XYZ Affine Tool Lab` is a single reusable modeless custom-title window
using the established light/navy/teal contract. At 1920 x 1080 it has three
clear regions:

```text
Input route / identity       Raw Source Viewer       Transformed Result Viewer
source + affine evidence     raw grid lines          transformed grid-edge lines
Preview / Discard / Publish  source triad/frame      reference triad/frame
```

The transformed Viewer defaults to the product's line/grid presentation.
It joins only adjacent source grid locators when both endpoint points exist;
it does not infer faces or bridge missing cells. Point markers can assist
selection, but are not the default geometry. The original Workbench Viewer
continues to show the active source/inspection context until the operator
explicitly selects the published A2 artifact.

The output HUD and Artifact Registry expose: source ID/hash, affine ID/hash,
source/reference frames, finite/missing counts, result SHA-256, and
`Preview`, `Published`, or `Stale` status. Text remains visible alongside
color/icon, with tooltip and AutomationName for icon controls.

## Acceptance evidence required for implementation

| Gate | Required proof |
| --- | --- |
| Independent point math | A small synthetic C3D with identity, translation, anisotropic scale, shear, and rotation has every finite output point checked against an independently calculated double result. |
| Missing contract | Zero, NaN, and Infinity source samples yield no points; exact grid dimensions, finite count, missing count, and locator ordering are preserved. |
| Provenance rejection | Wrong source hash/entity/frame/unit/convention, preview-only/stale transform, non-finite matrix, and route mismatch fail closed before iteration. |
| Determinism | Repeated Library-Noah, Studio Tools, and Runner execution has one canonical output SHA-256. |
| Explicit lifecycle | Preview/Discard/Publish/stale/cancel behavior is verified in the Workbench and Runner without automatic upstream execution. |
| UI evidence | Fresh current-build Korean 1920 x 1080 / 1280 x 760 and English 1920 x 1080 captures show source/result comparison, line-based output, triads, labels, disabled states, and no meaningful clipping. |
| Regression | Existing Viewer pointer, recipe teaching, WPG, Artifact Navigator, docking, Filter, Edge, Line Fit, Intersection, Correspondence, A1 Solve, and Datum gates remain green. |
| Real-data boundary | A real four-anchor correspondence package enables one actual A1 solve, A2 Preview/Publish, and Runner replay. Until then, only deterministic synthetic evidence is claimed. |

## Owner decisions recorded as accepted

Approve or revise each decision explicitly:

1. **A2 is separate from A1 and A3:** solve, apply, and re-grid remain three
   explicit tools with separate Preview/Publish states.
2. **Raw-coordinate transform:** A2 uses `(column, raw-height, row)` doubles,
   never Viewer scale/centering or a silent XY fallback.
3. **Raw-source-only v1:** the recipe's verified raw C3D is the sole A2
   surface input; filtered application is deferred.
4. **Typed two-input route:** verified raw source plus one current Published
   matching `AffineTransform3D` are required; every provenance mismatch fails
   closed.
5. **No re-grid/mutation:** output is an immutable, in-memory,
   row-major `TransformedPointCloud`; no C3D file, triangles, fill, or
   measurement is produced.
6. **Line-based comparison UI:** the Tool Lab shows source and transformed
   grid-edge views, frame triads, evidence cards, and explicit lifecycle
   commands; opening a view does not execute.
7. **Library-Noah algorithm ownership:** pure transform computation is added
   and tested in `Lib.ThreeD`; Studio remains the typed recipe/UI adapter.

## Preconditions and non-claims

The current project lacks the real four-anchor source/reference package,
source unit/frame/alignment provenance, and a trusted mapping profile. They
remain unverified. The user previously directed that missing data stay marked
unverified rather than block architecture progress; this design honors that
boundary but does not convert it into a physical-validation claim.

A2 code may begin only after the seven owner decisions are approved. A3
re-grid requires a new owner-approved output-frame, origin, spacing, bounds,
collision, interpolation, missing-value, and mapping-profile design. A4
Thickness/Warpage requires a Published A3 output plus separate measurement
contracts and evidence.

## Durable closure for this design task

```text
Status: Complete
Scope: owner-approved A2 design plus deterministic raw-C3D to immutable
  transformed-cloud implementation; no A3 re-grid or measurement scope.
Acceptance criteria: decisions 1-7 recorded -> accepted; source/transform
  contract, ownership, no-mutation boundary, package identity, synthetic
  Runner output, and waiting-state Tool Lab evidence -> passed.
Verification: current `Any CPU` build `0/0`; A2 Runner Golden `4/4`; A1
  regression `4/4`; vendored package verification passed; current-build Tool
  Lab screenshot quality passed on attempt one.
Evidence: this document, the A2 current artifacts, updated handoff, and
  product-target record.
Boundary / next dependency: real four-anchor source/reference data and a
  trusted Published A1 matrix are required for actual A2 UI Preview/Publish
  and Runner replay. A3 re-grid still requires separate owner-approved frame,
  sampling, collision, interpolation, and missing-value contracts.
```
