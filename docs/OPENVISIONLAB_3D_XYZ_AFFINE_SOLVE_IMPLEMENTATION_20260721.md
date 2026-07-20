# OpenVisionLab 3D A1 XYZ Affine Solve Implementation

Updated: 2026-07-21

Status: Complete for the deterministic A1 code slice and its synthetic
verification boundary. This record does not establish a physical fixture,
calibration, measurement, or a transformed C3D surface.

## Delivered scope

`Published CorrespondenceSet -> AffineTransform3D` is now an explicit typed
tool. It accepts exactly four current source/reference pairs and solves the
source-to-reference row mapping with double arithmetic:

```text
P = [sourceX sourceY sourceZ 1]  // 4 x 4
Q = [referenceX referenceY referenceZ] // 4 x 3
B = inverse(P) * Q
```

The implementation uses scaled partial-pivot Gauss-Jordan inversion; it does
not use normal equations, least squares, outlier removal, automatic matching,
or a planar fallback. The affine contract stores the 3x4 matrix, source
determinant, absolute linear determinant, infinity-norm condition estimate,
per-landmark transformed/reference/residual XYZ, RMS/max residual, source and
reference provenance, and a canonical SHA-256.

The Shell adds the `XYZ Affine Solve` catalog entry, typed WPG properties
(`ExactFourPartialPivot`, `MaximumConditionEstimate`,
`ArithmeticResidualWarning`), explicit Preview/Discard/Publish state, stale
handling, and one reusable modeless Tool Lab. The Tool Lab presents the raw
source viewer, source/reference route, matrix/residual evidence, and WPG.
It deliberately presents no transformed surface: application belongs to A2.

## Acceptance evidence

| Criterion | Result |
| --- | --- |
| General 3x4 solve | Pass: synthetic golden verifies an independent general matrix and transformed point. |
| Exact-four, numerical limits, condition breach, five-pair rejection, cancellation | Pass: synthetic golden `4/4`. |
| Deterministic Tools/recipe-adapter hash | Pass: repeated SHA-256 `FD5C0AC49E0D7C3238827F88632D5EA2FF368E9A2F4BD7B66A3E53DE780364BA`. |
| WPG draft/apply/save/reopen without automatic execution | Pass: Recipe Manager/WPG `18/18`. |
| Existing workbench regressions | Pass: teaching `18/18`, docking `25/25`, Artifact Navigator `24/24`. |
| Current source UI | Pass: Korean `1920 x 1080` and `1280 x 760` workbench plus English `1680 x 940` Tool Lab; every screenshot quality check accepted on attempt 1. |

Commands actually run:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform='Any CPU' --no-restore -v:q
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug -- --verify-c3d-affine-solve --report artifacts\verification\20260721-xyz-affine-solve\golden-final.txt
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug -- --verify-tool-recipe-teaching artifacts\verification\20260721-xyz-affine-solve\tool-recipe-teaching-final.txt
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug -- --verify-recipe-manager-wpg artifacts\verification\20260721-xyz-affine-solve\recipe-manager-wpg-final2.txt
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug -- --verify-workbench-docking artifacts\verification\20260721-xyz-affine-solve\docking-rerun.txt
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug -- --verify-artifact-navigator artifacts\verification\20260721-xyz-affine-solve\artifact-navigator-rerun.txt
```

UI artifacts:

- `artifacts/ui/20260721-xyz-affine-solve-a1/before-workbench-affine-1920-ko.png`
- `artifacts/ui/20260721-xyz-affine-solve-a1/after-workbench-affine-solve-1920-ko.png`
- `artifacts/ui/20260721-xyz-affine-solve-a1/after-workbench-affine-solve-1280-ko.png`
- `artifacts/ui/20260721-xyz-affine-solve-a1/after-affine-solve-tool-lab-1920-en.png`

## Boundary and next dependency

The supplied teaching template remains an intentionally readable two-corner
legacy scaffold. It cannot Preview A1 because it has no real four-anchor
Published `CorrespondenceSet`; no coordinate or fixture data was invented.
The next prerequisite is the real four-anchor input package with source and
reference provenance. It enables one real Preview/Publish and headless recipe
replay. Only after that A1 evidence and explicit owner approval may A2 apply
the published matrix to finite C3D points. A1 does not apply points, re-grid,
calculate Thickness/Warpage, calibrate, or claim metrology.

## Durable closure

```text
Status: Complete
Scope: A1 deterministic full-XYZ affine solve code, typed recipe/WPG/Tool Lab wiring, and synthetic Runner evidence.
Acceptance criteria: exact-four matrix and rejection gates -> pass; deterministic hash -> pass; typed UI/WPG and existing regressions -> pass.
Verification: current Debug build 0 warnings/0 errors; A1 golden 4/4; teaching 18/18; WPG 18/18; docking 25/25; Artifact Navigator 24/24; accepted current-source captures.
Evidence: artifacts/verification/20260721-xyz-affine-solve/ and artifacts/ui/20260721-xyz-affine-solve-a1/.
Boundary / next dependency: real fixture correspondence package is required for a real A1 Preview/Publish and Runner replay; A2/A3/A4 are excluded.
```
