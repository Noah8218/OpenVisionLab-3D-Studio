# Synthetic Thickness Coupon v1

This package is the public, deterministic Thickness workflow sample for
OpenVisionLab 3D Studio. It is fictional and is not derived from a captured
part, company fixture, customer data, or a previous C3D file.

## Files

- `synthetic-thickness-coupon-v1.C3D`: generated `1280 x 840` row-major C3D
  height grid.
- `inspection-recipe.ov3d-recipe.json`: schema `1.5` recipe with eight
  independently editable Thickness steps and 16 artifact-owned
  `GridRectangle` selections.
- `oriented-box-demo.ov3d-recipe.json`: synthetic-only numeric
  `OrientedBox3D` UI replay fixture.
- `ground-truth.json`: generator provenance, source identity, ROI coordinates,
  and expected signed height separation for every pad.
- `source-height-preview.png`: deterministic height-map preview with cyan
  reference ROIs and orange measurement ROIs.
- `ai-concept.png`: visual ideation only. It influenced the fictional layout
  but contains no measurement values and is not used by inspection.

## Known values

The base is an affine datum surface. Each narrow reference ROI lies exactly on
that datum and each measurement ROI lies on a parallel plateau.

| Pad | Expected signed separation |
| --- | ---: |
| 1 | 8 |
| 2 | 12 |
| 3 | 16 |
| 4 | 20 |
| 5 | 10 |
| 6 | 14 |
| 7 | 18 |
| 8 | 22 |

The declared unit is `synthetic-height-unit`. These values prove software
routing, ROI persistence, calculation, and Runner replay. They are not
calibrated physical metrology.

## Reproduce

```powershell
python scripts/generate-synthetic-thickness-coupon.py `
  --output 3D/SyntheticValidation/ThicknessCouponV1
```

The generated C3D SHA-256 must match the value recorded in
`ground-truth.json` and the recipe source binding.
