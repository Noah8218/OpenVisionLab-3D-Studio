# OpenVisionLab 3D LAS/LAZ Responsive Load Closure

Date: 2026-08-22

Status: Complete for `PL-0034`

## Operator Problem

Opening or changing the display density of a large LAS/LAZ source decoded
every point synchronously. The retained display sample was bounded, but the
full scan could block the WPF UI, offered no visible progress, could not be
cancelled, and repeated an equivalent source-and-budget scan.

This slice keeps LAS/LAZ as a local Viewer/recipe capability. It does not add
a general Import surface or broaden the advertised format set.

## Product Contract

- Interactive LAS/LAZ decode runs outside the WPF UI thread.
- The existing point cloud remains active until a replacement completes.
- A newer request cancels an older request and only the latest generation may
  update Viewer state.
- Progress is monotonic, bounded to `0..100`, localized, and visible in the
  existing semantic Viewer toolbar.
- Progress reporting is reduced to integer-percentage changes so a
  2,155,617-point source produces 100 UI updates instead of about 530.
- A completed sample is cached only for the exact normalized source path and
  sample budget. An equivalent request reuses it without another decode.
- Progress, cancellation, cache reuse, and display-density changes do not run
  or mutate recipe, Preview, Publish, Run, or result state.
- The synchronous Data API remains available for Runner and compatibility
  callers and delegates to the same deterministic loader contract.

## Root Cause And Correction

`LazPointCloud.Load` owned one synchronous full-point loop without a
`CancellationToken` or progress callback. Viewer recipe and density paths
called that loop directly, and every density change replaced state only after
another complete scan.

The Data loader now has a compatible cancellable/progress overload and checks
cancellation every 4,096 decoded points. Viewer interactive paths use
`Task.Run`, a request generation, and a source-scoped sample cache. Only a
successful latest request replaces the active sample and clears selection.
Cancellation and supported decode failures retain the prior point cloud.

Runtime verification exposed a second WPF-specific cause: Shell layout setup
can temporarily raise `Unloaded` while rehosting the Viewer. Immediate
cancellation treated that transient layout event as final removal. Cancellation
is now deferred to the next Dispatcher background turn and occurs only when
the Viewer is still unloaded.

## Changed Owners

- `OpenVisionLab.ThreeD.Data/PointClouds/LazPointCloud.cs` — compatible
  cancellation and progress contract.
- Viewer `MainWindowViewModel` and localization — loading state, bounded
  percentage, completion/cancel/failure status.
- Viewer control Data/Host/Recipes/Smoke/Contracts — asynchronous latest-wins
  load, exact source-and-budget cache, transient-unload handling, runtime
  evidence, and deterministic race/cache smoke.
- Viewer XAML — passive localized toolbar progress text with automation live
  status; no new framework or action surface.
- Shell smoke orchestration — awaits the asynchronous density transition.
- Source-channel verification — synchronous parity, monotonic progress, and
  compressed-LAZ cancellation checks.

## Verification

### Headless and build checks

- Full Release solution build: `0` warnings, `0` errors.
- Source channel and dense normal quality: `29/29`, including loader parity,
  monotonic bounded progress, and cancellation during compressed LAZ decode.
- Viewer display/runtime localization: `111/111`.
- Shell smoke command-line routing: `42/42`.
- Code structure guard: `67/67`.

### Actual EXE behavior

The current public `xyzrgb_manuscript.laz` fixture contains `2,155,617`
decoded points.

- Normal Fast-to-Balanced transition: final `50,000` sampled points,
  progress `100`, `100` UI progress updates, no cancellation, exit `0`.
- Overlapping Detailed-to-Balanced transition: one superseded request
  cancelled, final `Balanced` sample retained at `50,000` points, no stale
  apply, exit `0`.
- Exact Balanced reload: one cache hit and no additional full-file decode.
- Contract state retained `6` source entities, `0` Preview layers, and `0`
  published results.
- The in-flight Korean status `LAZ/LAS 불러오는 중 · 1% ·
  xyzrgb_manuscript.laz` is complete and unobscured in the Compact embedded
  Viewer. It disappears after completion.
- Current Release Shell Wide `1920 x 1040` and Compact `1280 x 760` captures
  passed screenshot quality on attempt 1 and intersected the dynamically
  selected leftmost monitor.

The available runtime monitor scale was 125%. The progress state, complete
state, Compact/Wide layouts, Korean runtime text, current graphite theme,
latest-request cancellation, cache reuse, and passive accessibility live
status were exercised. A separate hover/pressed/disabled/validation state is
not applicable to the passive progress text. DPI 100%, 150%, 175%, and 200%
were unavailable and remain unverified.

## Evidence

- Proofline: `../.proofline/issues/PL-0034.json`.
- D-backed root:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260822-pl0034-laz-responsive-load\`.
- Before: `before/laz-fast-baseline.png` and baseline contracts/quality.
- Current Wide/Compact and in-flight state: `shell-final/`.
- Cancellation/cache race: `race-current/`.
- Build and focused reports: `verification/`.

## Boundary

This is a software responsiveness and deterministic sampling closure, not a
maximum-input performance SLA. Large-C3D qualification remains blocked on a
representative maximum input and accepted memory/load-time limits. R0 was
explicitly deferred by the product owner for this work sequence. No physical
calibration, Gauge R&R, production approval, general Import workflow, camera,
PLC, robot, cloud, version, package, commit, push, or release claim is made.

## Completion Record

```text
Status: Complete
Scope: PL-0034 asynchronous latest-wins LAS/LAZ Viewer loading, localized progress, cancellation, exact source-and-budget sample reuse, and transient WPF unload correction
Acceptance criteria: loader parity/cancellation/progress -> pass; interactive off-UI-thread load and visible progress -> pass; cancellation/failure retain current state and no inspection execution -> pass; equivalent completed sample reuse -> pass; focused/build/runtime/UI/structure gates -> pass
Verification: Release solution 0 warnings/0 errors; source-channel 29/29; Viewer display 111/111; Shell options 42/42; structure 67/67; current actual EXE Wide/Compact quality and leftmost-monitor intersection pass; density race cancellation=1; cacheHits=1; final Balanced sampledPoints=50000; smokeExitCode=0
Evidence: docs/OPENVISIONLAB_3D_LAZ_RESPONSIVE_LOAD_CLOSURE_20260822.md; .proofline/issues/PL-0034.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260822-pl0034-laz-responsive-load/
Boundary / next dependency: owner R0 is deferred, not completed; large-C3D remains blocked on representative maximum input and accepted budgets; 100/150/175/200% DPI remain unverified
```
