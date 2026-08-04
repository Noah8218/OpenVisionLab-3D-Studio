# Public Sample Recipe Contract Recovery - 2026-08-03

Status: Complete

## Scope

Recover the six tracked Runner recipe contracts that became inconsistent when
the repository switched from the former synthetic Thickness Coupon bytes to the
current public `Thickness Coupon v1` sample.

No numerical, geometric, Library-Noah, Viewer, Runner, or acceptance-policy
implementation changed.

## Root cause

Commit `923be30` replaced the former source with:

```text
3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D
SHA-256 D879FC9E40678762214E8C3FBEA01F5C9A309701DAAEAD448067E563C5B502F8
```

The commit updated each recipe's source path and declared unit, but retained
measurement expectations authored for the previous C3D bytes. The new and old
Git blobs differ, and the new public sample also changed the intended ROI
layout. The mismatch therefore predates the Windows reinstall and is not
evidence of restored binary corruption.

## Recovered contracts

| Recipe | Recovered authored contract | Expected result |
| --- | --- | --- |
| Height Deviation | peak tolerance `20.0 raw-height` | Fail |
| Plane Flatness | tolerance `0.01 model` | Fail |
| Point Pair | expected elevation angle `-0.103 deg` | Pass |
| Gap/Flush | expected flush `5.327 raw-height`, tolerance `0.1` | Pass |
| Volume | expected signed net volume `0.005 model^3` | Pass |
| Cross-section | width `8.757 model`, raw-height range `9.644` | Pass |

The first two remain deliberate rejection examples. The other four preserve
the CI-authored successful replay intent against the current public sample.

## Verification

The six tracked recipes were executed with the current Release Runner and their
existing CI expected statuses:

```text
height-deviation|expected=Fail|exit=0
plane-flatness|expected=Fail|exit=0
point-pair|expected=Pass|exit=0
gap-flush|expected=Pass|exit=0
volume|expected=Pass|exit=0
cross-section|expected=Pass|exit=0
```

Evidence:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-recovery-prerequisites-and-recipe-contracts\recipe-contracts
```

## Boundary

These values are deterministic public-sample regression contracts. They do not
assert physical calibration, metrology accuracy, or suitability for a real
part. A future sample regeneration must update the sample SHA-256, every
dependent recipe expectation, and the expected-status smoke as one reviewed
change.
