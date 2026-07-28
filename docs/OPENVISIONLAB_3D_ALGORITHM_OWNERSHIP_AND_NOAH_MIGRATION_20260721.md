# OpenVisionLab 3D Algorithm Ownership and Library-Noah Migration

Updated: 2026-07-21

Status: **Owner-approved architecture direction; migration is phased and
evidence-gated.**

> 2026-07-22 current package update: `Lib.ThreeD 2.7.9` at
> `e36d9c07baab967fd4252e7052345563f29872a3` additionally owns pure
> Gap/Flush, Volume, and Cross-section Dimensions inspection arithmetic. The
> vendored package SHA-256 is
> `B21A6266AFD470B7EE8A4C857496E53561F4D399F2460FEE2939AAE85AD0FF92`.
> Older package sections below remain migration history.

## Binding ownership rule

OpenVisionLab 3D Studio is a typed inspection-tool workbench. It owns recipe
identity, source binding, operator teaching, WPG, Tool Labs, explicit
Preview/Publish state, Viewer overlays, Runner reports, and replay evidence.

`Library-Noah` (`Lib.ThreeD`) owns reusable, pure numerical 3D algorithms.
It must not reference Studio `Core`, `Data`, WPF, Viewer, recipe JSON, or
Shell state. Studio `Tools` converts its own immutable contracts to/from Noah
inputs and results; it must not carry a second numerical implementation.

```text
Studio recipe / source / current Published artifact
  -> Studio typed adapter and identity validation
  -> Lib.ThreeD pure input
  -> Lib.ThreeD algorithm
  -> Studio immutable artifact, hash, metrics, overlay, lifecycle evidence
```

This preserves the Tool-first product workflow while making calculations
reusable by other OpenVisionLab products.

## Current audited state

| Algorithm family | Current numerical owner | Required direction |
| --- | --- | --- |
| Thickness / Warpage | `Lib.ThreeD` through the Studio bridge | Retain; Studio remains an adapter. |
| Full XYZ Affine Solve | `Lib.ThreeD` | Studio is a typed adapter; preserve Studio artifact/hash/UI. |
| 2-Point Line | `Lib.ThreeD` pure construction | Complete: Studio is the strict raw-C3D/PointSet(2) adapter and owns lifecycle/evidence only. |
| 3-Point Plane | `Lib.ThreeD` pure construction | Complete: Studio is the strict raw-C3D/PointSet(3) datum-plane adapter and owns lifecycle/evidence only. |
| Filter | `Lib.ThreeD` pure finite/NaN median filter | Complete: Studio retains the C3D-zero/derived-finite-zero boundary and typed lifecycle/evidence only. |
| Height Difference Edge | `Lib.ThreeD` pure adjacent-pair scan/selection | Complete: Studio is the strict C3D lineage/artifact adapter and owns lifecycle/evidence only. |
| 3D Line Fit | `Lib.ThreeD` pure deterministic consensus/TLS | Complete: Studio is the strict C3D lineage/artifact adapter and owns lifecycle/evidence only. |
| Line Intersection | `Lib.ThreeD` | Studio is a typed C3D lineage/artifact adapter; Noah owns closest-approach geometry. |
| Plane Flatness / Point Pair / Gap-Flush / Volume / Cross-section Dimensions | `Lib.ThreeD` pure inspection tools | Complete for deterministic software evidence: Studio owns A3 identity, ROI/WPG/UI, metrics, overlays, hashes, and replay. |
| Landmark Correspondence | Studio structural gate | Retain Studio identity/recipe ownership; extract only reusable rank/volume math if repeated external consumers require it. |

No migration is a claim of physical calibration, metrology, or a real
four-anchor fixture result.

## Active package migration: Lib.ThreeD 2.7.4

The active package retains the source-neutral affine, common-line, plane,
point-cloud-apply, and reference-grid tools, deterministic full-XYZ
consensus/TLS line fitting, deterministic height-difference edge selection,
and deterministic median filtering:

1. `FullXyzAffineSolveTool` — exact four-pair source-to-reference solve using
   scaled partial-pivot arithmetic, determinant/condition evidence, and
   residuals.
2. `TwoPointLineTool` — ordered full-XYZ segment construction from two finite
   points with controlled invalid-input results.

3. `LineIntersectionTool` evaluates full-XYZ closest approach, acute angle,
   and finite-segment support for two normalized source-neutral lines.

4. `ThreePointPlaneTool` evaluates an ordered full-XYZ support triangle,
   oriented unit normal, and plane offset from three finite non-collinear
   points. It has no C3D, recipe, WPF, or measurement dependency.

5. `DeterministicLineFitTool` evaluates ordered finite XYZ points using the
   fixed SHA-256 pair schedule, consensus priority, orthogonal TLS,
   source-scanline direction, inlier support gates, and diagnostics. It has no
   C3D, recipe, WPF, source identity, or measurement dependency.

6. `DeterministicHeightDifferenceEdgeTool` evaluates a source-neutral
   row-major scalar grid, explicit rectangular selection, axis, polarity, and
   minimum delta. It owns finite-pair filtering, adjacent-pair deltas,
   strongest-per-scanline selection, exact-tie ordering, and diagnostics. It
   has no C3D, recipe, WPF, source identity, or measurement dependency.

7. `DeterministicMedianFilterTool` evaluates source-neutral row-major scalar
   grids where finite values are valid and non-finite values are missing. It
   owns bounded `3/5/7` median-window arithmetic, finite-neighbor selection,
   available-neighbor borders, missing-mask preservation, and changed-cell
   count. It has no C3D, recipe, WPF, source identity, or measurement
   dependency.

Studio continues to own C3D locator resolution, source SHA/frame validation,
recipe parameter parsing, canonical Studio output hashes, and the WPF lifecycle.
The A1 and Line Intersection Studio rules call Noah rather than retaining
matrix/pivot or closest-approach/angle/support numerical implementations. The
completed 2-Point Line Tool calls the Noah construction tool and does not
duplicate subtraction, normalization, or zero-length checks.

The Studio package reference is pinned to the locally vendored `Lib.ThreeD`
2.7.4 artifact from Library-Noah commit
`5d06460c14b1edf390241b28511ce4997f70dc28`; its SHA-256 is
`BB44D30F8D3AB9C1CF528482CFA2A5A804D9222FFBAE258C765CEF2696EB2573`.
Development uses the packaged output, not a cross-repository `ProjectReference`,
so the same package boundary is tested locally and in CI.

## Migration acceptance gates

- Library-Noah build and deterministic smoke prove the pure result and error
  paths.
- Studio A1 Golden continues to pass without a local inverse/matrix-solve
  implementation.
- Studio Line Intersection Golden continues to pass without a local
  closest-approach/angle/support implementation.
- Search proves Studio calls `Lib.ThreeD.FeatureExtraction` for migrated math
  and no old private numerical helper remains.
- Vendored package ID/version/hash match the Library-Noah package output.
- Existing Studio Tool/Runner checks continue to pass.
- The 2-Point Line Studio adapter proves strict raw-C3D/PointSet(2) binding,
  ordered replay identity, explicit lifecycle, source-change clearing, Tool
  Lab, and Runner behavior without copying Noah geometry math.
- The 3-Point Plane Studio adapter proves strict raw-C3D/PointSet(3) binding,
  ordered-normal replay identity, explicit lifecycle, source-change clearing,
  Tool Lab support-triangle/normal evidence, and Runner behavior without
  copying Noah cross-product, normalization, or plane-equation math.
- The 3D Line Fit Studio adapter proves strict Published EdgePointSet binding,
  unchanged canonical artifact hash, explicit lifecycle, and Runner behavior
  while the Noah tool owns pair scheduling, TLS, residual classification, and
  support diagnostics.
- The Height Difference Edge Studio adapter proves strict raw-height derived
  C3D source/selection binding, unchanged canonical artifact hash, explicit
  lifecycle, and Runner behavior while the Noah tool owns all pair scanning,
  missing-pair handling, candidate ordering, and numerical diagnostics.
- The Filter Studio adapter proves strict raw-height C3D source binding,
  unchanged finite-zero derived-output rejection, canonical artifact hash,
  explicit lifecycle, and Runner behavior while the Noah tool owns all median
  windows, neighbor selection, missing-mask preservation, and changed-count
  arithmetic.

## Explicit boundaries

- A1's real four-anchor fixture Preview/Publish/Runner replay remains
  unverified because no real source/reference package exists.
- A2 affine application and A3 re-grid have deterministic synthetic evidence
  only; real aligned fixture validation, Thickness/Warpage after alignment,
  calibration, and metrology are not included in this migration.
- Studio does not create a generic graph executor, a plugin factory, or a
  second algorithm API. Each typed adapter remains explicit.
- 2-Point Line is construction evidence only. It does not find a physical
  edge, establish a calibrated length, or authorize affine application,
  re-grid, Thickness, Warpage, calibration, or metrology.
