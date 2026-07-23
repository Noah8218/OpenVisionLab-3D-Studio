# Viewer pointer render coalescing checkpoint - 2026-07-23

## Status

`Complete` for the bounded P0-C pointer-render slice.

OpenVisionLab 3D Studio remains a local, rule-based 3D inspection recipe
workbench. This change improves Viewer interaction only. It does not run or
change Preview, Publish, Run, recipe persistence, or inspection algorithms.

## Completed contract

- SharpGL remains the single render scheduler at the existing fixed 30 FPS.
- Orbit, middle-button pan, right-button pan, and profile-endpoint drag update
  camera or selection state without calling synchronous `DoRender()` for every
  `MouseMove` event.
- All pointer changes received between two scheduled frames collapse into the
  next scheduled frame. Mouse-up, wheel, pick, commands, and non-drag explicit
  renders retain their existing behavior.
- The real-input smoke now records mouse-move handler time, next-frame delay,
  scheduled drag requests, forbidden immediate drag renders, active C3D point
  count, and display-list build time.
- The smoke fails closed when the handler exceeds `33.34 ms`, the next observed
  frame exceeds `100 ms`, no scheduled drag request/frame is observed, or a
  synchronous drag render occurs.

No new render engine, dependency, background OpenGL context, or speculative LOD
framework was introduced.

## Current evidence

The fixed current-source scene contains `33,761` sampled C3D render points and
uses Wireframe geometry. The instrumented pre-change baseline and final runs use
the actual Debug Shell EXE at
`src/OpenVisionLab.ThreeD.Shell/bin/Debug/net10.0-windows10.0.19041/`.

| Criterion | Result | Evidence |
|---|---|---|
| Full Debug build | Pass, 0 warnings / 0 errors | `artifacts/current/20260723-viewer-render-coalescing/final-build.txt` |
| Instrumented pre-change C3D drag | Average `22.884 ms`, maximum `108.186 ms`, 6 immediate renders | `instrumented-baseline-pointer.txt` |
| Final actual-EXE C3D pointer repetition | Pass `3/3`; handler averages `0.134-0.184 ms`, maxima `0.963-1.528 ms`; next-frame maxima `36.787-51.914 ms`; 0 immediate renders | `after-pointer-1.txt` through `after-pointer-3.txt` |
| Different-source load followed by pointer smoke in the same EXE | Pass; Thickness -> Warpage, load `4,035 ms`, 85 Dispatcher ticks; handler average `0.123 ms`, maximum `0.889 ms`; next-frame maximum `46.416 ms` | `after-async-load-and-pointer-load.txt`, `after-async-load-and-pointer.txt` |
| First C3D display-list observation | `37.168-41.758 ms` across final runs; the combined post-load run records `37.581 ms` | final pointer reports above |
| Viewer display settings regression | Pass, 83 checks | `display-viewmodel.txt` |
| Docking regression | Pass, 26/26 | `workbench-docking.txt` |
| Recipe teaching regression | Pass, 25/25 | `tool-recipe-teaching.txt` |
| Korean 1920 x 1040 screenshot quality | Before and after accepted on attempt 1 | `before-1920x1040-ko.quality.txt`, `after-1920x1040-ko.quality.txt` |

The full artifact root is
`artifacts/current/20260723-viewer-render-coalescing/`.

## Visual comparison

- Before: `before-1920x1040-ko.png`
- After: `after-1920x1040-ko.png`

The comparison preserves custom chrome, docking layout, Wireframe rendering,
HUD, height distribution, Viewer context commands, and right-drag pan. The
after capture intentionally reflects the Warpage source loaded by the combined
performance smoke; it is not a pixel-identical scene comparison.

## Claim boundary and next dependency

This evidence proves bounded software interaction behavior for the fixed local
C3D sample and desktop session. It does not prove GPU portability, a production
frame-time SLA, very large data LOD behavior, physical calibration, sensor
fidelity, or metrology. Display-list creation remains a synchronous first-frame
operation and was measured rather than moved to another OpenGL context. Its
observed `37.168-41.758 ms` range is retained as a regression baseline.

The next product evidence priority is an unaided owner replay of the first
recipe journey. The next geometric trust gate remains blocked until a distinct
real four-landmark acquisition supplies trusted unit, frame, provenance,
correspondence, and reference-grid evidence.

## Durable completion record

```text
Status: Complete
Scope: Fixed-scene Viewer pointer-render coalescing and actual-EXE latency evidence
Acceptance criteria: no per-MouseMove synchronous render -> pass; left/right/middle drag, pick, zoom, context menu -> pass; handler <= 33.34 ms and next frame <= 100 ms -> pass; current UI/Wireframe preserved -> pass
Verification: Debug build 0/0; actual EXE pointer 3/3; post-load pointer 1/1; display 83; docking 26/26; recipe teaching 25/25; before/after screenshot quality attempt 1
Evidence: artifacts/current/20260723-viewer-render-coalescing/
Boundary / next dependency: fixed local software evidence only; owner first-recipe replay and trusted real four-landmark acquisition remain external
```
