# OpenVisionLab 3D Algorithm Ownership and Library-Noah Migration

Updated: 2026-07-21

Status: **Owner-approved architecture direction; migration is phased and
evidence-gated.**

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
| 2-Point Line | `Lib.ThreeD` pure construction | Connect the existing packaged construction to a later Studio Tool adapter. |
| Filter | Studio | Later migrate after the C3D-zero/missing-mask boundary is reproduced in Noah. |
| Height Difference Edge | Studio | Later migrate after the source-grid/selection adapter is fixed. |
| 3D Line Fit | Studio | Later migrate after fitted-edge diagnostics are independently preserved. |
| Line Intersection | `Lib.ThreeD` | Studio is a typed C3D lineage/artifact adapter; Noah owns closest-approach geometry. |
| Landmark Correspondence | Studio structural gate | Retain Studio identity/recipe ownership; extract only reusable rank/volume math if repeated external consumers require it. |

No migration is a claim of physical calibration, metrology, or a real
four-anchor fixture result.

## Active package migration: Lib.ThreeD 2.3.0

Noah 2.2.0 introduced two source-neutral algorithms. The active 2.3.0 package
retains them and adds the first reusable common-line geometry calculation:

1. `FullXyzAffineSolveTool` — exact four-pair source-to-reference solve using
   scaled partial-pivot arithmetic, determinant/condition evidence, and
   residuals.
2. `TwoPointLineTool` — ordered full-XYZ segment construction from two finite
   points with controlled invalid-input results.

3. `LineIntersectionTool` evaluates full-XYZ closest approach, acute angle,
   and finite-segment support for two normalized source-neutral lines.

Studio continues to own C3D locator resolution, source SHA/frame validation,
recipe parameter parsing, canonical Studio output hashes, and the WPF lifecycle.
The A1 and Line Intersection Studio rules call Noah rather than retaining
matrix/pivot or closest-approach/angle/support numerical implementations. The
planned 2-Point Line Tool will call the Noah construction tool and will not
duplicate subtraction, normalization, or zero-length checks.

The Studio package reference is pinned to the locally vendored `Lib.ThreeD`
2.3.0 artifact. Development uses the packaged output, not a cross-repository
`ProjectReference`, so the same package boundary is tested locally and in CI.

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

## Explicit boundaries

- A1's real four-anchor fixture Preview/Publish/Runner replay remains
  unverified because no real source/reference package exists.
- A2 affine application, re-grid, Thickness/Warpage after alignment,
  calibration, and metrology are not included in this migration.
- Studio does not create a generic graph executor, a plugin factory, or a
  second algorithm API. Each typed adapter remains explicit.
