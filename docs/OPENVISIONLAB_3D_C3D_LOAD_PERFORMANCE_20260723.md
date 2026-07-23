# OpenVisionLab 3D C3D Load Performance

Date: 2026-07-23
Status: Complete

## Scope

This checkpoint optimizes the fixed actual-EXE C3D source transition from
`3D/Thickness/Ori_20240116_094414.C3D` to
`3D/Warpage/Ori_20240116_094430.C3D` without changing sampling density,
triangle order, wire/grid/surface-edge order, camera behavior, recipe state, or
the explicit Preview/Publish/Run contracts.

The instrumented baseline showed that C3D decode and distribution were not the
largest cost. `C3DHeightGridRenderProxy.Create` spent about 2.8 seconds building
sampled render topology with tuple dictionary keys, four separate min/max
passes, and three edge-deduplication hash sets. The implementation now uses a
packed 64-bit row/column key, combines bounds calculation with cell validation,
and uses direction-owned Boolean arrays for orthogonal edge uniqueness. Each
quad diagonal is emitted once at its original position.

## Fixed three-run comparison

All values are milliseconds from the current Debug Shell EXE. The comparison
uses the median of three identical actual-EXE runs before and after the topology
change.

| Stage | Instrumented baseline median | Optimized median | Change |
| --- | ---: | ---: | ---: |
| Whole source transition | 5,447 | 2,891 | -46.9% |
| Grid load total | 328.341 | 272.581 | -17.0% |
| Render topology | 2,821.555 | 48.088 | -98.3% |
| Worker total | 3,153.213 | 362.005 | -88.5% |
| UI apply and first render | 974.070 | 1,088.372 | +11.7% |

The three optimized total times are `2,853`, `3,284`, and `2,891`. The
instrumented baseline totals are `5,494`, `5,271`, and `5,447`. These are local
Debug measurements, not a release-build, large-data, GPU-portability, or
production latency claim. The UI-apply figure is now the largest measured
stage and remains the next bounded performance target; this checkpoint does
not hide that residual cost.

## Preserved contracts

- The exact 2 x 2 quad triangle, wire edge, grid edge, and surface edge index
  arrays are verified, not only their counts.
- Missing cells remain holes and are not bridged.
- Non-unit point stride, surface-edge sampling, duplicate-cell rejection, and
  invalid-stride rejection remain verified.
- Cancellation at 10% retains the previously displayed Thickness source.
- A missing target file retains the previously displayed Thickness source.
- A successful load replaces the Viewer and recipe source atomically with the
  Warpage source.
- Pointer selection, orbit, middle/right pan, wheel zoom, context menu, and the
  30 FPS render scheduler remain active after the different-source load.

## Verification

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform="Any CPU"`
  -> 0 warnings, 0 errors.
- Display/topology verifier -> `83` checks passed.
- C3D height distribution/loader verifier -> `22` checks passed.
- Recipe teaching verifier -> `25/25` passed.
- Workbench docking verifier -> `26/26` passed.
- Actual EXE completion -> three optimized runs passed.
- Actual EXE cancellation -> passed; current source remained Thickness.
- Actual EXE missing-source failure -> passed; current source remained
  Thickness.
- Actual EXE post-load pointer regression -> passed; handler maximum
  `1.175 ms`, next-frame maximum `41.331 ms`, and zero immediate MouseMove
  renders.
- Korean `1920 x 1040` completion screenshot -> quality accepted on attempt 1.

Evidence is under
`artifacts/current/20260723-c3d-load-performance/`. The reproducible visual pair
is `before-loading-1920x1040-ko.png` and
`after-loading-1920x1040-ko.png`; the former is the live progress state and the
latter is the successfully replaced Warpage source.

## Completion record

Status: Complete
Scope: Fixed-sample C3D render-topology generation and measured actual-EXE
source-transition performance.
Acceptance criteria: Exact topology contract passed; actual EXE median improved
from `5,447 ms` to `2,891 ms`; success/cancel/failure retention and post-load
pointer regression passed.
Verification: Build `0/0`, display `83`, loader `22`, recipe teaching `25/25`,
docking `26/26`, three completion runs, cancellation, failure, pointer, and
screenshot-quality checks.
Evidence: `artifacts/current/20260723-c3d-load-performance/`.
Boundary / next dependency: UI apply and first render remain about `1.09 s` and
need a separate measured checkpoint. Owner first-recipe usability and real
four-landmark physical evidence remain separate external gates. No calibration
or metrology claim is made.
