# Declared-normal quality and Landmark Correspondence Noah migration

Date: 2026-08-01

## Outcome

`ImportedMeshNormalQualityAnalyzer` and
`C3DLandmarkCorrespondenceRule` are now strict product adapters over two
committed public Library-Noah Tools. Studio no longer owns declared-normal
length/topology/alignment arithmetic or four-point rank/normalized-volume
arithmetic.

The operator workflow is unchanged. Source loading, recipe teaching,
Preview/Publish/Run, output identity, Viewer overlays, and validation remain
explicit Studio responsibilities. The migration does not generate or repair
normals, solve an affine transform, change a source, or claim physical
calibration or metrology.

## Ownership boundary

| Concern | Library-Noah owner | Studio owner |
| --- | --- | --- |
| Finite, non-zero, and unit normal length | `DeclaredMeshNormalQualityTool` | Source/format identity and report policy |
| Triangle indices, degeneracy, and corner alignment | `DeclaredMeshNormalQualityTool` | Admission evidence and immutable `SourceNormalQualityReport` |
| Four-point augmented rank | `LandmarkCorrespondenceValidationTool` | Landmark identity, order, lineage, unit, and frame validation |
| Span-normalized tetrahedral volume and taught gate | `LandmarkCorrespondenceValidationTool` | Recipe parameter policy, output artifact/hash, metrics, overlays, and lifecycle |

Both Noah Tools consume source-neutral coordinates and return typed results.
They know no C3D/GLB/STL path, recipe ID, WPF, Viewer, operator session,
calibration fixture, or production system.

## Exact package provenance

| Item | Value |
| --- | --- |
| Noah source worktree | `C:\Git\Library-Noah-surface-match-kernel` |
| Source commit | `3ef2f52546a9187df465bf8973e26426c30f7634` |
| Package | `Lib.ThreeD 2.8.6` |
| SHA-256 | `02E0D0B69F9D7CECBA958BF4BDC7F2999D0902539C33CD0F133C48C08C3A25B0` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.8.6.nupkg` |
| Target | `netstandard2.0` |

Noah Release and six new Tool Smoke cases passed before commit. The exact
committed source was then packed from a clean worktree. The vendored package,
sidecar checksum, nuspec version, repository commit, and target framework all
pass the Studio package-integrity gate.

## Observable compatibility

The current Release was captured before Studio adaptation and repeated after
the package migration.

- Source-channel and dense-normal quality: baseline/current `26/26`; after
  removing only the generated timestamp line, `0` differences.
- Landmark Correspondence: baseline/current `5/5`; the full reports are
  byte-equivalent, including independent/coplanar decisions, ranks,
  normalized volumes, hashes, and controlled error messages.

This proves compatibility for the fixed software fixtures. It is not evidence
of sensor accuracy, uncertainty, traceability, GR&R, or a physical four-anchor
fixture.

## Verification

| Check | Result |
| --- | --- |
| Noah Release build | Pass: `0` warnings, `0` errors |
| Noah full Smoke | Pass: `98/98` |
| Package integrity and provenance | Pass |
| Studio Release build | Pass: `0` warnings, `0` errors |
| Expanded direct package bridge | Pass: `16/16` |
| Source-channel and normal quality | Pass: `26/26` |
| Landmark Correspondence golden | Pass: `5/5` |
| Baseline/current normalized parity | Pass: `2/2` |
| Source Quality workspace | Pass: `18/18` |
| Tool Recipe teaching | Pass: `28/28` |
| Inspection Workspace | Pass: `63/63` |
| Validation Set | Pass: `84/84` |
| Data-loading/Viewer/Shell matrix | Pass: `128/128`, `0` failures; `44/44` EXE windows intersect leftmost `DISPLAY2` |
| Code structure and decreasing ledger | Pass: `27/27`, `4 debt / 26 boundaries` |
| NuGet vulnerable/deprecated audit | Pass: `0/0` |
| Refreshed Wide/Compact fixed package | Pass: both `-ValidateOnly` modes |

The accepted data-loading matrix is `after/data-loading-matrix-leftmost-final`.
It launched current Release desktop EXEs, dynamically selected leftmost
`DISPLAY2` at `-1920,360,1920 x 1080`, and recorded `44` valid window
rectangles with `0` outside that display. Screenshots/reports are physically
on `D:` through the verified repository junction. This slice changed no UI,
UX, theme, layout, visible text,
navigation, or responsive behavior; no new before/after UI comparison is
claimed. Human-owner unaided R0 remains external and is not replaced by these
automated checks.

## Evidence and reproduction

Logical evidence path:
`artifacts/current/20260801-noah-normal-quality-landmark-migration/`.

Physical test-data path:
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-normal-quality-landmark-migration`.

Primary commands:

```powershell
dotnet build Lib.Common.sln -c Release --no-incremental
dotnet run --no-build --project Lib.Inspection.Smoke\Lib.Inspection.Smoke.csproj -c Release

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\verify-library-noah-package.ps1 -ReportPath <report>
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release --no-incremental
dotnet src\OpenVisionLab.ThreeD.Runner\bin\Release\net10.0\OpenVisionLab.ThreeD.Runner.dll `
  --verify-library-noah-3d --report <report>
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\verify-code-structure.ps1 -ReportPath <report>
```

## Completion record

Status: Complete

Scope: Declared mesh-normal quality and four-point Landmark Correspondence
independence calculation moved to two committed Library-Noah Tools; Studio
adapters, package provenance, focused parity, regressions, ownership ledger,
and fixed R0 inputs updated.

Acceptance criteria: committed Tool source -> Pass; exact vendored package ->
Pass; no duplicate Studio geometry calculation -> Pass; observable result
compatibility -> Pass; focused and expanded regressions -> Pass; decreasing
ledger -> Pass; fixed package validation -> Pass.

Verification: Noah `0/0`, `98/98`; Studio `0/0`; bridge `16/16`; normal
quality `26/26`; Landmark Correspondence `5/5`; normalized parity `2/2`;
expanded regressions; structure `27/27`; both fixed `-ValidateOnly` checks.

Evidence:
`artifacts/current/20260801-noah-normal-quality-landmark-migration/`.

Boundary / next dependency: four migration-debt files remain. Next migrate
`AlignedPointRepeatabilityRule` and `ThicknessRepeatabilityRule` to a committed
Noah repeatability-statistics Tool, then migrate the two validation-statistics
analyzers before `J-12 Multiple-match`. Human-owner R0 remains external.
