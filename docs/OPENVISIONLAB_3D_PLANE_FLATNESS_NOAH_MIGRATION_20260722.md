# Plane Fit / Flatness Library-Noah Migration

Updated: 2026-07-22  
Status: **Complete for deterministic software ownership; physical/metrology validation unverified**

## Responsibility boundary

The reusable numerical implementation now lives in committed Library-Noah
source and the vendored `Lib.ThreeD 2.7.5` package:

```text
Studio C3D/A3 samples
  -> Studio typed adapter
  -> Lib.ThreeD.LeastSquaresHeightFieldPlaneFitTool
  -> Lib.ThreeD.PlaneFlatnessInspectionTool
  -> Studio ToolResult / metrics / overlays / recipe evidence
```

`LeastSquaresHeightFieldPlaneFitTool` owns finite-sample validation,
least-squares `Y = aX + bZ + c`, normalized normal/offset, raw-height reference
fit, RMS, maximum-deviation target, and target projection.
`PlaneFlatnessInspectionTool` owns reference-plane execution, signed orthogonal
distances, minimum/maximum extrema, peak-to-valley Flatness, surface RMS,
projections, and tolerance evaluation.

Studio continues to own C3D/A3 decoding, source/unit/frame/ROI identity,
PropertyGrid parameters, explicit Preview/Publish, metric names, overlays,
recipe persistence, and Runner evidence. `HeightFieldPlaneFit` is retained as a
compatibility adapter for existing Viewer and Volume callers; it contains no
least-squares or signed-distance arithmetic. `PlaneFlatnessRule` contains only
Studio validation and result mapping.

## Immutable package provenance

- Package: `Lib.ThreeD 2.7.5`
- Library-Noah commit: `e869afdafd78e3769cb66e6a862e381684c22e3d`
- Package SHA-256: `CD53523885064AFBD0B24275391888A591F684DE100FDC30D7751F3B5A8AD2D5`
- Target: `netstandard2.0`
- Studio dependency direction: Studio -> vendored package; no adjacent
  `ProjectReference` and no Noah -> Studio reference.

## Acceptance and evidence

- Library-Noah solution build: `0` warnings, `0` errors.
- Library-Noah independent Smoke: `42/42`.
- Package metadata/hash verification: pass.
- Studio solution build: `0` warnings, `0` errors.
- Studio Library-Noah bridge: `7/7`.
- Plane Flatness Golden: `9/9`.
- Artifact-owned ordered Runner: `14/14`.
- Generic height measurement Workbench: `23/23`, including the sequential
  Reference/Measurement ROI teaching contract.
- Synthetic Affine Inspection Plate: `16/16`.

Current Studio reports are under
`artifacts/verification/20260722-plane-flatness-noah/`. The Library-Noah
source commit is local and was not pushed by this task.

## Completion record

```text
Status: Complete
Scope: Move reusable Plane Fit and Plane Flatness numerical ownership from Studio to Library-Noah while preserving Studio recipe/result contracts.
Acceptance criteria: Noah owns the arithmetic -> pass; Studio old arithmetic removed -> pass; committed package provenance -> pass; focused behavior gates -> pass.
Verification: Noah build 0/0 and Smoke 42/42; package verification pass; Studio build 0/0; bridge 7/7; Flatness 9/9; ordered Runner 14/14; Workbench 10/10; synthetic plate 16/16.
Evidence: Library-Noah commit e869afdafd78e3769cb66e6a862e381684c22e3d; vendored package SHA-256 CD53523885064AFBD0B24275391888A591F684DE100FDC30D7751F3B5A8AD2D5; artifacts/verification/20260722-plane-flatness-noah/.
Boundary / next dependency: This proves deterministic software behavior only. Real A1-to-A3 acquisition provenance and physical/metrology validation remain unverified.
```
