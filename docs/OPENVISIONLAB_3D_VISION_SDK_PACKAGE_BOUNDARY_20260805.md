# OpenVisionLab Vision SDK 3D Package Boundary

Date: 2026-08-05

Status: Active package boundary

## Purpose

OpenVisionLab 3D Studio consumes the UI-free 3D algorithms from the fixed,
vendored `OpenVisionLab.Vision3D` NuGet package. A clean clone therefore does
not require an adjacent `OpenVisionLab-Vision-SDK` checkout.

## Fixed input

| Item | Value |
| --- | --- |
| Package ID | `OpenVisionLab.Vision3D` |
| Version | `3.0.0` |
| Source repository | `C:\Git\OpenVisionLab-Vision-SDK` |
| Source commit | `f34fdf912ff38fe20f36dbb063837e14b4f922b3` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/OpenVisionLabVisionSdk/OpenVisionLab.Vision3D.3.0.0.nupkg` |
| SHA-256 | `F7324DC43ABF8E130D6F88C034287C192CFEA89E16A8A906A60F52DE341045B4` |

`NuGet.Config` uses only the repository-relative vendored feed, the existing
WPF PropertyGrid feed, and NuGet.org. No Studio project may point at a local SDK
checkout.

## Namespace migration

| Former | Current |
| --- | --- |
| `Lib.ThreeD.Geometry` | `OpenVisionLab.Vision3D.Geometry` |
| `Lib.ThreeD.FeatureExtraction` | `OpenVisionLab.Vision3D.FeatureExtraction` |
| `Lib.ThreeD.Inspection` | `OpenVisionLab.Vision3D.Inspection` |

This is a package and namespace identity migration. The SDK's published
migration guide states that formulas and behavioral contracts are unchanged.

## Responsibility split

- SDK: reusable numerical algorithms and typed controlled results.
- Studio Tools/Data: strict conversion, identity/unit/frame checks, recipe
  policy, result/evidence composition, and persistence.
- Runner: direct package and product regression verification.
- Shell/Viewer: explicit workflow and presentation only.

## Integrity command

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-vision-sdk-package.ps1
```

The verifier checks the package SHA-256, nuspec ID/version/repository commit,
license and notice, package documentation, and the `netstandard2.0` assembly
and XML documentation.

## Update checklist

1. Commit and verify SDK source.
2. Pack the exact clean commit.
3. Copy the package and checksum into `third_party/OpenVisionLabVisionSdk`.
4. Update package references and `VisionSdkHeightMapInspection` provenance.
5. Run package, restore, build, Runner, Workbench, and structure verification.
6. Record the exact source commit, package hash, commands, and regression result.

## Boundary

The fixed package provides reproducible software behavior. It does not infer
physical units or coordinate frames, calibrate C3D height data, reconstruct
missing values, or certify metrology or production readiness.
