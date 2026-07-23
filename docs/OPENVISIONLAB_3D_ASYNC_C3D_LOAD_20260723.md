# Asynchronous C3D load checkpoint — 2026-07-23

## Status

`Complete` for the bounded P0-B source-load slice.

OpenVisionLab 3D Studio remains a local, rule-based 3D inspection recipe
workbench. Source loading changes Viewer and recipe input state only; it never
runs Preview, Publish, or Run.

## Completed contract

- The existing Viewer `Open 3D Map` action now decodes C3D data, calculates its
  full-source distribution, and prepares sampled render topology on a worker
  thread.
- The Viewer commits the prepared grid only after all load and preparation
  work succeeds. Cancellation or failure keeps the current Viewer source and
  authored recipe source unchanged.
- The command bar shows a localized determinate progress ribbon and Cancel
  action. Open is disabled while one load is active.
- Cancellation is checked during source sampling, distribution construction,
  render-point creation, and render-topology preparation.
- Progress reports are bounded rather than posted for every source row.
- Successful load still resets a newly loaded C3D to Wireframe. Same-source
  reuse and explicit Preview/Publish/Run contracts remain unchanged.

## Current evidence

| Criterion | Result | Evidence |
|---|---|---|
| Full Debug build | Pass, 0 warnings / 0 errors | `artifacts/current/20260723-async-c3d-load/final-build.txt` |
| Loader progress and cancellation arithmetic | Pass, 22 checks | `artifacts/current/20260723-async-c3d-load/c3d-load-progress-cancel.txt` |
| Workbench load state, command enablement, cancellation routing, recipe preservation | Pass, 25/25 | `artifacts/current/20260723-async-c3d-load/tool-recipe-teaching.txt` |
| Docking/UI regression | Pass, 26/26 | `artifacts/current/20260723-async-c3d-load/workbench-docking.txt` |
| Actual EXE different-source completion | Pass; Thickness -> Warpage; 3,814 ms; 77 input-priority Dispatcher ticks; progress 100% | `artifacts/current/20260723-async-c3d-load/actual-exe-async-load-complete.txt` |
| Actual EXE cancellation | Pass; cancel at 10%; 327 ms; original Thickness source retained | `artifacts/current/20260723-async-c3d-load/actual-exe-async-load-cancel.txt` |
| Actual EXE missing-source failure | Pass; 51 ms; original Thickness source retained | `artifacts/current/20260723-async-c3d-load/actual-exe-async-load-failure.txt` |
| Korean 1920 x 1040 loading UI | Accepted on attempt 1 | `artifacts/current/20260723-async-c3d-load/after-loading-1920x1040-ko.png` |
| English 1280 x 720 loading UI | Accepted on attempt 1 | `artifacts/current/20260723-async-c3d-load/after-loading-1280x720-en.png` |

The pre-change current-source capture is
`artifacts/current/20260723-async-c3d-load/before-1920x1040-ko.png`.

## Claim boundary and next gate

The fixed different-source EXE load took 3.814 seconds. Dispatcher activity
proves that the UI message pump continued to process input-priority work; it
does not prove a maximum input-latency bound or smooth rendering throughout
the operation. P0-C must measure and reduce first-render/display-list cost,
coalesce pointer-driven renders, and record drag input latency and frame rate.
No physical calibration, sensor fidelity, or metrology claim follows from this
checkpoint.
