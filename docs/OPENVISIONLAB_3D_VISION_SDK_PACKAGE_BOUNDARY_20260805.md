# OpenVisionLab Vision SDK 3D Package Boundary

Date: 2026-08-05

Updated: 2026-08-26 for the deterministic connected-region SDK addition

Status: Active package boundary

## Purpose

OpenVisionLab 3D Studio consumes the UI-free 3D algorithms from the fixed,
vendored `OpenVisionLab.Vision3D` NuGet package. A clean clone therefore does
not require an adjacent `OpenVisionLab-Vision-SDK` checkout.

## Fixed input

| Item | Value |
| --- | --- |
| Package ID | `OpenVisionLab.Vision3D` |
| Version | `3.0.1-dev.20260826.domain-mask.1` |
| Source repository | `C:\Git\OpenVisionLab-Vision-SDK` |
| Source commit | `db8b8a281dd028c62fabfc49febcde9b4d345d37` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/OpenVisionLabVisionSdk/OpenVisionLab.Vision3D.3.0.1-dev.20260826.domain-mask.1.nupkg` |
| SHA-256 | `D87570212D4C8913360CB01D20D9669720EDB6424B42C7FB790909EC8766D1CB` |

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
The current development package additionally owns exact height-map ROI copying
and source-grid output-origin arithmetic through `HeightMapCropTool`, plus
source-neutral grid topology, locator-order, duplicate-locator, and coordinate-
finiteness diagnostics through `GridDiagnosticsTool`.

The same package also owns source-neutral `ConnectedRegionTool` labeling and
`ConnectedRegionMetricsTool` region arithmetic. Studio consumes those tools
through the bounded source-bound `C3DConnectedRegionRule` adapter; an explicit
mask is required and no shape rasterization or persisted region artifact is
implied.

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
4. Update package references and all Studio SDK provenance constants.
5. Run package, restore, build, Runner, Workbench, and structure verification.
6. Record the exact source commit, package hash, commands, and regression result.

## Boundary

The fixed package provides reproducible software behavior. It does not infer
physical units or coordinate frames, calibrate C3D height data, reconstruct
missing values, or certify metrology or production readiness.
