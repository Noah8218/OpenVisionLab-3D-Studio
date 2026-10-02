# Public sample data

Only redistributable samples and procedurally generated fixtures belong in this
repository. Keep attribution and licensing with each source.

## Thickness Coupon

[ThicknessCouponV1](../3D/Samples/ThicknessCouponV1/README.md) includes the C3D
input, inspection recipe, preview, and ground truth. Exact identities and
expected values are owned by its
[ground-truth manifest](../3D/Samples/ThicknessCouponV1/ground-truth.json)
and [generator](../scripts/generate-thickness-coupon-sample.py).

Copy the complete folder to preserve its relative recipe/source paths.
The `raw-height` unit is deliberately non-physical.

## Other inputs

- [Public format samples](../3D/PublicSamples/README.md) include redistributable
  glTF, STL, LAS, LAZ, and intentionally invalid parser fixtures.
- [Affine inspection plate](../3D/SyntheticValidation/AffineInspectionPlateV1/README.md)
  provides generated geometric validation input.

Do not commit customer scans, production captures without redistribution rights,
or renamed or transformed copies of private sources. Generated input needs a
reproducible generator, declared expected values, and a stated measurement boundary.
