# Synthetic Affine Inspection Plate v1

Date: 2026-07-22  
Status: Complete for deterministic synthetic display-frame verification

## Purpose

This package is the first reusable whole-chain synthetic golden for the
approved generic 3D inspection direction. It is intentionally a C3D dataset
with known numerical truth rather than a decorative screenshot.

```text
source C3D
  -> Median Filter
  -> 8 Height Difference Edge bands
  -> 8 full-XYZ Line Fits
  -> 4 Line Intersections / CornerAnchors
  -> 4-pair Landmark Correspondence
  -> A1 XYZ Affine Solve
  -> A2 Apply XYZ Affine
  -> A3 Re-grid Height Map
  -> Thickness ROI
  -> Warpage ROI
```

Thickness and Warpage remain ordinary Measure tools in a 27-step generic
schema `1.3` recipe. The package does not introduce a product mode named after
either measurement.

## Dataset design

- Grid: `240 x 160` C3D raw-height cells.
- Four raised rectangular pads expose L-shaped step boundaries.
- Pad heights: `20`, `35`, `50`, and `85` over a base height of `10`.
- Each landmark uses one AcrossColumns and one AcrossRows edge band. The two
  fitted lines intersect at a recipe-owned CornerAnchor.
- The four source and reference anchors are non-coplanar.
- The intended full-XYZ affine contains translation, a rotated orthonormal
  reference basis, anisotropic planar pitch (`1.25`, `0.8`), and height scale
  `0.5`.
- Thickness ROI has known transformed truth: mean `20`, minimum `19.5`,
  maximum `20.5`, range `1.0`, and `800` valid samples.
- Warpage ROI combines a smooth dome and local depression. Independent
  best-fit-plane truth is P2V `2.506350526020297`, RMS
  `0.48084410715300302`, and `2,400` valid samples.
- `52` deterministic missing cells test mask/hole preservation.
- Four deterministic impulses test Median Filter behavior without affecting
  the two measurement ROIs.

## Verified results

The current-source verification passes `16/16`:

- all four Edge -> Line Fit -> Intersection anchors have source-coordinate
  error `0` against the authored truth;
- source and reference landmark sets both have affine rank `4`;
- A1 maximum matrix error is `1.7053025658242404E-13`;
- A2 maximum point error is `1.0306789158764915E-12` across `38,348` finite
  points;
- A3 is `240 x 160`, populated `38,348`, missing `52`, collision `0`, coverage
  `0.99864583333333334`, and maximum height error
  `1.7053025658242404E-13`;
- ordered execution preserves `Thickness -> Warpage` recipe order;
- independent Thickness metric maximum error is
  `1.4210854715202004E-14`;
- independent Warpage metric maximum error is
  `6.7501559897209518E-14`;
- recipe save/reopen preserves schema `1.3`, `27` steps, and `11` selections.

## Reproduction

```powershell
dotnet build "src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj" -c Debug

dotnet run --no-build --project "src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj" -c Debug -- `
  --verify-synthetic-affine-inspection-plate `
  --synthetic-affine-package "3D/SyntheticValidation/AffineInspectionPlateV1" `
  --report "artifacts/current/20260722-synthetic-affine-inspection-plate/verification.txt"

python "scripts/render-synthetic-affine-inspection-plate.py" `
  "3D/SyntheticValidation/AffineInspectionPlateV1"
```

## Evidence

- `3D/SyntheticValidation/AffineInspectionPlateV1/source-affine-inspection-plate-v1.C3D`
- `3D/SyntheticValidation/AffineInspectionPlateV1/inspection-recipe.ov3d-recipe.json`
- `3D/SyntheticValidation/AffineInspectionPlateV1/ground-truth.json`
- `3D/SyntheticValidation/AffineInspectionPlateV1/source-height-preview.png`
- `3D/SyntheticValidation/AffineInspectionPlateV1/reference-height-preview.png`
- `artifacts/current/20260722-synthetic-affine-inspection-plate/verification.txt`

The PNG renderer verifies the C3D dimensions and SHA-256 against
`ground-truth.json` before creating either preview.

## Claim boundary and next gate

This closes a deterministic synthetic software gate only. It does not prove
physical units, sensor fidelity, calibration, Gauge R&R, uncertainty, or
metrology accuracy. The next gate is to acquire four distinct real
CornerAnchor source/reference pairs plus trusted frame, unit, provenance,
revision, and ReferenceGridProfile evidence, then replay the same
A1 -> A2 -> A3 -> measurement lifecycle without changing the contracts.

## Completion record

```text
Status: Complete
Scope: deterministic 240 x 160 C3D package and full synthetic Filter -> four-corner feature chain -> A1/A2/A3 -> Thickness/Warpage verification
Acceptance criteria: exact C3D/recipe/truth package -> pass; four real algorithmic intersections -> pass; intended affine recovery -> pass; A3 hole/locator fidelity -> pass; independent measurement truth -> pass; save/reopen -> pass; human-review previews -> pass
Verification: full solution build 0 warnings/0 errors; SyntheticAffineInspectionPlateVerification 16/16; 11 related golden/regression suites pass; independent Python Thickness/Warpage truth plus preview renderer SHA/dimension checks pass; both generated PNGs visually inspected
Evidence: 3D/SyntheticValidation/AffineInspectionPlateV1 and artifacts/current/20260722-synthetic-affine-inspection-plate/verification.txt
Boundary / next dependency: real four-landmark acquisition and trusted physical/reference provenance remain required; no calibration or metrology claim
```
