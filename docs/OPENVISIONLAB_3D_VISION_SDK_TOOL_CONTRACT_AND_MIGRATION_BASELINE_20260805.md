# OpenVisionLab 3D Vision SDK Tool Contract and Migration Baseline

Date: 2026-08-05

Updated: 2026-08-23 for the deterministic grid-diagnostics SDK addition

Status: Active architecture contract

## Decision

Reusable numerical, geometric, filtering, feature-extraction, matching,
measurement, inspection, and statistical algorithms belong to
`OpenVisionLab-Vision-SDK`. Studio consumes the committed, vendored
`OpenVisionLab.Vision3D` package and must not contain a second implementation.

The original SDK migration was an identity and ownership change from
`Lib.ThreeD 2.9.1` to `OpenVisionLab.Vision3D 3.0.0`. The current fixed
development package is
`OpenVisionLab.Vision3D 3.0.1-dev.20260823.grid-diagnostics.1`.
The SDK migration guide is authoritative for
the namespace mapping. Existing formulas, tolerances, units, coordinate-frame
requirements, missing-value behavior, coverage gates, result ordering, and
controlled-failure semantics remain unchanged. The fixed SDK commit also uses
overflow/underflow-safe distance and scaled-RMSE evaluation; a mathematically
equivalent result may move by one representable `double` value and therefore
requires an explicit content-hash rebaseline when proven by before/after data.

## Tool form

An SDK algorithm uses:

- a public sealed `XxxTool` entry type;
- source-neutral typed input and optional `XxxOptions`;
- a typed result that distinguishes success, controlled invalid input, and
  unavailable measurement;
- one explicit `Execute(...)` entry point;
- deterministic ordering and tie-breaking where results are replayed;
- no dependency on Studio, WPF, Viewer, recipe JSON, or product lifecycle state.

The existing `IThreeDInspectionTool` remains valid for its height-map
compatibility contract. Other SDK Tool families use their typed public API
without being forced into that interface.

## Studio boundary

Studio may own:

- product/source/unit/frame/artifact identity validation;
- recipe parsing, ROI binding, persistence, and pipeline routing;
- strict input/result adaptation;
- acceptance policy over SDK-owned metrics;
- explicit Preview, Publish, Run, and Validation orchestration;
- evidence composition, presentation-only overlays, and localized UI text.

Studio must not calculate a fit, distance, transform, correspondence,
neighborhood result, distribution, measurement, candidate ranking, or
statistical estimate that belongs in the SDK.

### Current grid-diagnostics boundary

`GridDiagnosticsTool` owns declared/observed/unique grid counts, locator-order
analysis, duplicate-locator analysis, coordinate-finiteness analysis, and
typed deterministic results for implicit and explicit grids. Studio's
`SourceQualityGridDiagnosticsAnalyzer` is a zero-signal Data adapter: it maps
source-neutral samples and typed SDK results, applies the supported C3D
implicit-row-major format policy, validates the Core report contract, and
composes product evidence. The dependency direction is Studio Data -> fixed
vendored SDK; the SDK has no Studio, WPF, Viewer, recipe, or lifecycle
dependency.

## Current fixed SDK input

| Item | Value |
| --- | --- |
| Repository | `C:\Git\OpenVisionLab-Vision-SDK` |
| Source commit | `8be38403d0d00698431d7ffa4de60a63289672c6` |
| Package | `OpenVisionLab.Vision3D 3.0.1-dev.20260823.grid-diagnostics.1` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/OpenVisionLabVisionSdk/OpenVisionLab.Vision3D.3.0.1-dev.20260823.grid-diagnostics.1.nupkg` |
| SHA-256 | `964A543C007687ED93F2AFEC682245A76C61DA2AE42EC9B786FB8CC27BED976C` |

Do not add a cross-repository `ProjectReference`, do not mix a package and a
project reference, and do not package an uncommitted SDK worktree.

## Decreasing migration baseline

The machine-readable decreasing migration baseline is
`docs/OPENVISIONLAB_3D_VISION_SDK_TOOL_MIGRATION_BASELINE_20260805.json`.
It records zero Studio migration-debt files and 35 reviewed Studio-boundary
files. A ceiling may not be raised merely to make the structure guard pass.

`scripts/verify-code-structure.ps1` verifies that:

1. this contract and its schema-1 baseline exist and agree;
2. every inventoried file exists and has a non-increasing numerical signal count;
3. no unclassified Studio numerical owner appears;
4. the reviewed adapters still call SDK Tools without restoring former arithmetic;
5. Shell and Runner remain thin routing/composition boundaries.

## SDK update checklist

1. Verify and commit the exact SDK source.
2. Build, run SDK smoke tests, and pack from that commit.
3. Verify package ID, version, target, repository commit, license, notice,
   documentation, DLL, and XML documentation.
4. Vendor the package and SHA-256 sidecar together.
5. Update the Studio package references and provenance constants together.
6. Run package verification, isolated restore, Release build, focused Tool
   tests, Runner and Workbench regression matrices, and the structure guard.
7. Compare behavior with the preceding fixed package and record intentional
   contract differences explicitly.

Historical Library-Noah contracts and migration records remain evidence for
their recorded source versions. They do not override this active contract.

## Boundary

This contract proves software ownership and deterministic package consumption.
It does not establish physical calibration, certified metrology, production
approval, or human usability. Product-owner unaided Wide/Compact R0 remains a
separate acceptance prerequisite.
