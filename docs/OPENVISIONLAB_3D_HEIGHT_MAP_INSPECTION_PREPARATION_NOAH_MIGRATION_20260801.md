# Height-map Inspection and Preparation Library-Noah Migration

Date: 2026-08-01

Status: Complete

## Outcome

The reusable height-map calculations used by C3D loading, Source Quality,
Completeness Grid, ROI measurements, and reference-grid reconstruction now
belong to committed Library-Noah source and are consumed through the vendored
`Lib.ThreeD 2.8.4` package. Studio no longer contains a second implementation
of those calculations.

Studio continues to own product-specific C3D byte decoding, source/unit/frame
and recipe identity, canonical artifact hashing, explicit lifecycle routing,
metrics, overlays, report composition, and Viewer-only point projection.

## Scope

Included:

- finite/zero/non-finite height-grid counts, minimum, maximum, mean, histogram,
  bin bounds, peak, and deterministic tie order;
- source-neutral double-precision distribution statistics;
- rectangular height-map finite/missing counts, sum, mean, range, and coverage;
- Completeness Grid cell placement, reference mean, relative mean, finite
  coverage, and typed per-cell/aggregate decisions;
- declared-frame and reference-axis grid-point reconstruction with explicit
  supported coordinate bounds;
- strict Studio adapters and observable pre/post parity;
- updated decreasing ownership ledger and structure guard.

Excluded:

- any change to UI, visible text, layout, theme, Viewer color policy, or
  GoPxL-inspired interaction design;
- recipe execution on load, Preview, Publish, Run, or Validation side effects;
- Dual Surface Thickness and Height Deviation numerical migration;
- Landmark Correspondence, declared mesh-normal quality, repeatability, or
  validation-statistics migration;
- physical calibration, metrology, cross-hardware, acquisition, or production
  claims.

## Library-Noah source and package

Five public sealed source-neutral Tools were added:

| Tool | Numerical responsibility |
| --- | --- |
| `HeightGridSummaryTool` | Raw float-grid statistics and deterministic distribution evidence |
| `HeightDistributionStatisticsTool` | Double-precision finite-value statistics and bins |
| `HeightMapRegionStatisticsTool` | Rectangular finite-value aggregation and coverage |
| `CompletenessGridInspectionTool` | Cell placement, reference-relative measurements, typed decisions |
| `ReferenceGridPointReconstructionTool` | U/V and declared XYZ reconstruction with range validation |

Immutable provenance:

| Item | Value |
| --- | --- |
| Repository/worktree | `C:\Git\Library-Noah-surface-match-kernel` |
| Branch | `codex/surface-match-kernel` |
| Source commit | `a64c31b1024f154e402d258ade4b70470ad50fb2` |
| Package | `Lib.ThreeD 2.8.4` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.8.4.nupkg` |
| SHA-256 | `0F4FB2A1115C0247E03BA85D335BE40241FD02A6F5694FE6E36B872CB3A846F5` |

The Noah commit passes Release build `0 warnings / 0 errors` and full
`Lib.Inspection.Smoke` `86/86`. The package was packed only after that exact
source was committed; source and vendored package hashes agree.

## Studio adaptation boundary

| Studio owner | Retained Studio responsibility | Noah calculation |
| --- | --- | --- |
| `C3DHeightGrid` | Decode bytes, source hash, timing, display-point projection | `HeightGridSummaryTool` |
| `C3DHeightDistribution` | Immutable Studio-facing evidence projection | Summary Tool result |
| `C3DHeightFieldSnapshot` | Byte identity, parsing/encoding, immutable values | `HeightDistributionStatisticsTool` |
| `C3DSourceQualityAnalyzer` | Source/mask identity and report composition | `HeightDistributionStatisticsTool` |
| `C3DCompletenessGridRule` | Recipe identity, canonical hash, metrics, overlays | `CompletenessGridInspectionTool` |
| `ToolRecipeHeightMeasurementExecution` | Explicit recipe routing and evidence hash | ROI statistics and point reconstruction Tools |

The structure guard now explicitly rejects restoration of the former local
count/sum/average, completeness sampling, and reference-coordinate formulas.
The schema-1 ledger decreases from `12` to `8` migration-debt files and grows
from `16` to `22` reviewed Studio boundaries. This migration changes no
backlog classification; inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`.

## Acceptance criteria and evidence

| Criterion | Result |
| --- | --- |
| Exact committed Noah source, package version, and hash | Pass |
| Noah build and full Smoke | Pass: `0/0`, `86/86` |
| Studio final Release build | Pass: `0/0` |
| Package integrity and expanded bridge | Pass: integrity, `12/12` |
| C3D map fidelity | Pass: `10/10` |
| Source Quality report | Pass: `13/13` |
| Completeness Grid | Pass: `23/23` |
| Height distribution | Pass: `25/25` |
| Generic height-measurement Workbench | Pass: `54/54` |
| Normalized pre/post observable parity | Pass: `5/5` reports |
| Final Cross-section compatibility seal | Pass: baseline/current `56/56` lines, `0` normalized differences |
| C3D Height Image regression | Pass: `25/25` |
| Artifact-owned ROI Runner regression | Pass: `18/18` |
| Validation Set regression | Pass: `84/84` |
| Code structure and decreasing ledger | Pass: `25/25`, `8 debt / 22 boundaries` |
| Refreshed fixed R0 package | Pass: Wide and Compact `-ValidateOnly` |

The five parity reports differ before normalization only by generated
timestamps, phase-specific evidence paths, or per-run temporary GUIDs. After
those non-behavioral fields are normalized, every line and line count matches.
The final adapter keeps the pre-migration Cross-section `double` value range
while the other reconstruction routes retain their former finite `float`
range. A sealed rerun after that compatibility adjustment passes `54/54`; its
56-line report has zero normalized differences from the baseline after only
the generated temporary capture path is replaced.

No UI screenshot was captured because this slice changes no UI, UX, layout,
visible text, theme, navigation, or responsive behavior. The existing human
owner Wide/Compact R0 remains external; automated evidence does not close it.

## Reproduction checklist

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release `
  -p:Platform="Any CPU" --no-incremental

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\verify-library-noah-package.ps1

dotnet src\OpenVisionLab.ThreeD.Runner\bin\Release\net10.0\OpenVisionLab.ThreeD.Runner.dll `
  --verify-library-noah-3d --report <report-path>

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\verify-code-structure.ps1 -ReportPath <report-path>

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\start-human-owner-r0.ps1 -Layout Wide -ValidateOnly
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\start-human-owner-r0.ps1 -Layout Compact -ValidateOnly
```

## Completion record

Status: Complete

Scope: Height-grid summary/distribution, region statistics, Completeness Grid,
and reference-grid reconstruction calculations migrated to five committed
Library-Noah Tools; Studio adapters, package provenance, parity, regressions,
ledger, and fixed R0 hashes updated.

Acceptance criteria: exact committed Noah package -> Pass; no duplicate Studio
calculation -> Pass; observable parity -> Pass; focused and expanded regression
matrix -> Pass; decreasing ownership ledger -> Pass; fixed package validation
-> Pass.

Verification: Noah Release `0/0`, Smoke `86/86`; Studio Release `0/0`;
package/bridge, focused, expanded, structure, parity, and both `-ValidateOnly`
checks all passed as listed above. The final Cross-section compatibility seal
is recorded in `after/sealed-height-measurement-parity.txt`.

Evidence:
`artifacts/current/20260801-noah-height-map-inspection-preparation-migration/`.
The physical test-data location is
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-height-map-inspection-preparation-migration`.

Boundary / next dependency: the eight remaining ledger items are not closed.
The next dependency-ready slice is `DualSurfaceThicknessRule` plus
`HeightDeviationRule`, implemented as committed Library-Noah Tools before
Studio adaptation. Human-owner R0 remains a separate external task.
