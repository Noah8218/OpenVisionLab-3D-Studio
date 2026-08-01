# Dual Surface Thickness and Height Deviation Noah migration

Date: 2026-08-01
Status: Complete

## Outcome

`DualSurfaceThicknessRule` and `HeightDeviationRule` are now strict adapters
over committed, public, sealed Library-Noah Tools. Studio no longer owns the
dual-surface residual/statistical calculation or the height-summary peak
deviation decision.

The operator workflow is unchanged: source and ROI teaching remain explicit,
Preview/Publish/Run remain explicit actions, output creation does not switch
the input, and Viewer evidence remains composed by Studio.

## Ownership boundary

| Concern | Library-Noah owner | Studio owner |
| --- | --- | --- |
| Reference plane and signed measurement residuals | `DualSurfaceThicknessInspectionTool` | Source/unit identity and sample adaptation |
| Mean/min/max/range/RMS and limit counts | `DualSurfaceThicknessInspectionTool` | ToolResult metric ordering and overlays |
| Low/high/peak absolute deviation and typed decision | `HeightDeviationInspectionTool` | Source name, unit, invariant message, metrics and overlays |
| Elapsed time, recipe lifecycle, Preview/Publish/Run | None | Studio |

The Noah Tools know no C3D path, recipe identifier, UI, Viewer, calibration,
or production system. They consume source-neutral samples/statistics and
return typed controlled results.

## Exact package provenance

| Item | Value |
| --- | --- |
| Noah source worktree | `C:\Git\Library-Noah-surface-match-kernel` |
| Source commit | `ec8f1b3db57bea0065cd82735acb08111f88f3c0` |
| Package | `Lib.ThreeD 2.8.5` |
| SHA-256 | `3BE4E7F83CC4A9E3542C6FCA9C38C5F13D2BFEE703F78035CB9082DC0B5EBCDB` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.8.5.nupkg` |
| Target | `netstandard2.0` |

The Noah worktree was clean before packing. The package was built from the
committed source, copied into Studio, and verified against its checksum and
nuspec repository commit.

## Observable compatibility

The current Release was captured before Studio adaptation.

- Generic height-measurement Workbench: baseline/current `56/56` lines,
  `0` normalized differences after replacing only the generated temporary
  capture path.
- Actual `c3d-height-deviation.recipe.json` Runner report: baseline/current
  `24/24` lines, `0` normalized differences after replacing only elapsed time
  and generated timestamp fields.
- The current recipe is a real `Pass`: peak deviation `21.905 raw-height`
  against tolerance `1200.000 raw-height`. Older documentation that expected
  `Fail` is historical and is not used as the current semantic baseline.

## Verification

| Check | Result |
| --- | --- |
| Noah Release build | Pass: `0` warnings, `0` errors |
| Noah full Smoke | Pass: `92/92` |
| Package integrity | Pass |
| Studio Release build | Pass: `0` warnings, `0` errors |
| Expanded package bridge | Pass: `14/14` |
| Generic height-measurement Workbench | Pass: `54/54` |
| Actual Height Deviation recipe | Pass with exact normalized parity |
| Validation Set | Pass: `84/84` |
| C3D map fidelity | Pass: `10/10` |
| Source Quality | Pass: `13/13` |
| Completeness Grid | Pass: `23/23` |
| Existing C3D Thickness | Pass: `5/5` |
| Height distribution | Pass: `25/25` |
| Code structure and decreasing ledger | Pass: `26/26`, `6 debt / 24 boundaries` |
| Refreshed Wide/Compact fixed package | Pass: both `-ValidateOnly` modes |

No UI, UX, theme, layout, visible text, navigation, or responsive behavior
changed, so new screenshots are neither required nor presented as evidence.
Human-owner unaided R0 remains an external acceptance task; automation does
not replace it.

## Evidence and reproduction

Logical evidence path:
`artifacts/current/20260801-noah-dual-thickness-height-deviation-migration/`.

Physical test-data path:
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-dual-thickness-height-deviation-migration`.

Primary commands:

```powershell
dotnet build Lib.Common.sln -c Release --no-incremental
dotnet run --no-build --project Lib.Inspection.Smoke\Lib.Inspection.Smoke.csproj -c Release

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\verify-library-noah-package.ps1
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release `
  -p:Platform="Any CPU" --no-incremental
dotnet src\OpenVisionLab.ThreeD.Runner\bin\Release\net10.0\OpenVisionLab.ThreeD.Runner.dll `
  --verify-library-noah-3d --report <report>
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\verify-code-structure.ps1 -ReportPath <report>
```

## Completion record

Status: Complete

Scope: Dual Surface Thickness residual/statistical evaluation and Height
Deviation peak-decision calculation moved to two committed Library-Noah Tools;
Studio adapters, package provenance, focused parity, regressions, ownership
ledger, and fixed R0 inputs updated.

Acceptance criteria: committed Tool source -> Pass; exact vendored package ->
Pass; no duplicate Studio calculation -> Pass; observable result compatibility
-> Pass; focused and expanded regressions -> Pass; decreasing ledger -> Pass;
fixed package validation -> Pass.

Verification: Noah `0/0`, `92/92`; Studio `0/0`; bridge `14/14`; Workbench
`54/54`; Validation Set `84/84`; focused regressions and structure `26/26`;
both fixed `-ValidateOnly` checks passed.

Evidence:
`artifacts/current/20260801-noah-dual-thickness-height-deviation-migration/`.

Boundary / next dependency: six migration-debt files remain. Next migrate the
geometry-quality pair `ImportedMeshNormalQualityAnalyzer` and
`C3DLandmarkCorrespondenceRule`; then the repeatability pair and the validation
statistics pair before `J-12 Multiple-match`. Human-owner R0 remains external.
