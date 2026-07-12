# OpenVisionLab 3D Studio .NET 10 Migration

## Decision

OpenVisionLab 3D Studio migrated from .NET 8 to .NET 10 on 2026-07-12.

- Core, Data, Tools, Runner: `net10.0`
- Viewer, Docking.Controls, Shell, app host: `net10.0-windows`
- SDK baseline: `10.0.300` with `latestFeature` roll-forward in `global.json`
- CI: `actions/setup-dotnet` with `10.0.x`

This is a repository compatibility baseline, not a claim that every dependency ships a native .NET 10 build.

## Dependency Boundary

| Boundary | Version | .NET 10 evidence | Residual risk |
| --- | --- | --- | --- |
| WPF / Windows Desktop | .NET 10 | Current restore/build and Viewer/Shell runtime smoke pass. | Recheck future WPF breaking changes when SDK feature bands change. |
| WPF-UI | 4.3.0 | Package contains `net10.0-windows7.0`; Shell smoke passes. | Theme/control changes remain package-version dependent. |
| Dirkster.AvalonDock | 4.74.1 | Package contains `net10.0-windows7.0`; docking Shell smoke passes. | Dock layout persistence beyond current smoke remains untested. |
| SharpGL.WPF | 3.1.1 | NuGet selects its compatible `netcoreapp3.1` asset; C3D, textured GLB, selection and result overlays render under .NET 10. | Package was last updated in 2020 and does not ship a direct `net10.0-windows` asset. Preserve screenshot regression coverage. |
| Unofficial.laszip.netstandard | 5.6.2 | `netstandard2.0` package; 2,155,617-point compressed LAZ decode and bounds check pass. | Native/runtime behavior still needs Windows smoke coverage on CI/release machines. |

NuGet vulnerability and deprecated-package checks reported no findings for the current direct/transitive graph on 2026-07-12.

## Verification Evidence

- Restore/build: warning `0`, error `0`, all outputs under `net10.0` or `net10.0-windows`.
- Mapping golden: `10/10`.
- Plane flatness: `9/9`.
- Point-pair dimensions: `9/9`.
- Gap/Flush: `8/8`.
- Volume: `9/9`.
- Cross-section Dimensions: `9/9`.
- Fixed Viewer/Shell matrix: `128` pass, `0` fail in `artifacts/net10_matrix_20260712/matrix_smoke_summary_after.txt`.
- SharpGL C3D screenshot: `artifacts/net10_validation_20260712/viewer_cross_section.png`.
- SharpGL textured GLB screenshot: `artifacts/net10_validation_20260712/viewer_glb_textured.png`.
- WPF-UI/AvalonDock Shell screenshot: `artifacts/net10_validation_20260712/shell_cross_section.png`.
- LASzip probe: `artifacts/net10_validation_20260712/laz_probe.txt`.

## Reproduction

```powershell
dotnet --version
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1 -ArtifactDir artifacts\net10_matrix_after -SkipBuild
dotnet list OpenVisionLab.ThreeDStudio.slnx package --vulnerable --include-transitive
dotnet list OpenVisionLab.ThreeDStudio.slnx package --deprecated
```

## .NET 10 WPF Watch List

- Do not introduce empty `ColumnDefinitions` or `RowDefinitions` collections.
- Treat invalid `DynamicResource` usage as a runtime crash risk and keep current-source Shell smoke coverage.
- Keep SharpGL as an explicit compatibility boundary until a replacement or maintained fork is proven with the same Viewer gate.
- Record the runtime/framework identity in durable run evidence before results are used across mixed application versions.

## Checked Primary References

- Microsoft WPF .NET 10 overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- Microsoft .NET 10 breaking changes: https://learn.microsoft.com/en-us/dotnet/core/compatibility/10
- Microsoft target framework guidance: https://learn.microsoft.com/en-us/dotnet/standard/frameworks
- NuGet SharpGL.WPF 3.1.1: https://www.nuget.org/packages/SharpGL.WPF/3.1.1
- NuGet WPF-UI 4.3.0: https://www.nuget.org/packages/WPF-UI/4.3.0
- NuGet Unofficial.laszip.netstandard 5.6.2: https://www.nuget.org/packages/Unofficial.laszip.netstandard/5.6.2
