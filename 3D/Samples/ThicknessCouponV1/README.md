# Thickness Coupon v1

This package provides a ready-to-run Thickness workflow for OpenVisionLab 3D
Studio.

## Files

- `thickness-coupon-v1.C3D`: `1280 x 840` row-major C3D
  height grid.
- `inspection-recipe.ov3d-recipe.json`: schema `1.5` recipe with eight
  independently editable Thickness steps and 16 artifact-owned
  `GridRectangle` selections.
- `oriented-box-demo.ov3d-recipe.json`: numeric `OrientedBox3D` UI example.
- `ground-truth.json`: source identity, ROI coordinates,
  and expected signed height separation for every pad.
- `source-height-preview.png`: height-map preview with cyan
  reference ROIs and orange measurement ROIs.

## Known values

Each visible pad contains its own datum strip and raised measurement plateau.
Both ROIs stay inside the same pad: cyan identifies the reference surface and
orange identifies the surface whose height separation is measured.

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

The declared unit is `raw-height`.

## Reproduce

```powershell
python scripts/generate-thickness-coupon-sample.py `
  --output 3D/Samples/ThicknessCouponV1
```

The C3D SHA-256 must match the value recorded in
`ground-truth.json` and the recipe source binding.
