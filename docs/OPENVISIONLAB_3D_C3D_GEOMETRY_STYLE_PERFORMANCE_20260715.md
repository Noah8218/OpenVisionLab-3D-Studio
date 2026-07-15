# C3D Geometry Style Performance Gate

Updated: 2026-07-15

## Decision

The local deterministic performance gate passes for the fixed C3D Thickness sample in all 12 combinations of:

- Geometry Style: Points, Wireframe, Surface, Surface + Edges
- Render Density: Fast, Balanced, Detailed

This is a local engineering baseline, not a general GPU requirement or a physical-accuracy claim. Windows CI has not yet revalidated this new performance gate.

## Test Contract

Each case forces 31 SharpGL `DoRender` frames, requires finite FPS and draw-time telemetry, verifies the effective style and density, requires an accepted screenshot, and preserves one identical C3D two-point measurement contract per density. Static C3D cases must also report an active `OpenGLDisplayList` cache.

Thresholds were fixed before the final runs:

| Density | Minimum FPS | Maximum mean draw time |
| --- | ---: | ---: |
| Fast | 20 | 50.000 ms |
| Balanced | 12 | 83.333 ms |
| Detailed | 6 | 166.667 ms |

Local environment:

- CPU: AMD Ryzen 5 2600 Six-Core Processor
- GPU: NVIDIA GeForce GTX 1060 3GB, driver `32.0.15.6094`
- OS: Windows 10 Pro `10.0.19045`
- SDK: .NET `10.0.301`
- Configuration: Debug

## Measured Failure And Correction

The first independent repeat exposed a real threshold failure: Balanced Surface + Edges recorded `11.728 FPS / 77.465 ms` against the `12 FPS` minimum. The failure is preserved under `artifacts/c3d_geometry_performance_20260715/repeat2`.

The correction remained inside the Viewer WPF/SharpGL boundary:

1. Wireframe uses complete row/column grid edges rather than triangle diagonals.
2. Surface + Edges uses every fourth source-grid line while preserving outer lines and never bridging missing cells.
3. Static C3D geometry is compiled into an OpenGL display list keyed by source object, transform, Geometry Style, Color Map, and point size.
4. Plane-flatness Deviation coloring bypasses the static cache because its colors are result-owned and dynamic.
5. Picking and inspection measurements continue to use source cells, not display triangles or sampled overlay edges.

The dynamic-color smoke also exposed an MVVM ordering defect: Plane Flatness made Deviation available after attempting to select it, so Height remained effective. `MainWindowViewModel` now refreshes the display capability context before selecting result-owned Deviation. The display ViewModel verification covers this transition.

## Final Evidence

Two independent final runs pass `24/24` total cases:

| Density | Cases | Minimum observed FPS | Maximum observed draw time |
| --- | ---: | ---: | ---: |
| Fast | 8 | 46.786 | 12.244 ms |
| Balanced | 8 | 32.574 | 21.074 ms |
| Detailed | 8 | 18.352 | 42.143 ms |

The worst observed static case was Detailed Surface + Edges at `18.352 FPS / 42.143 ms`. The dynamic Balanced Plane Flatness case correctly selected Deviation, bypassed the display list, and recorded `14.316 FPS / 61.444 ms` without freezing result colors.

Evidence:

- Aggregate: `artifacts/c3d_geometry_performance_20260715/final-performance-evidence.txt`
- Final run 1: `artifacts/c3d_geometry_performance_20260715/final_run1/c3d-geometry-performance-summary.txt`
- Final run 2: `artifacts/c3d_geometry_performance_20260715/final_run2/c3d-geometry-performance-summary.txt`
- Dynamic Deviation: `artifacts/c3d_geometry_performance_20260715/dynamic_deviation_after-contract.txt`
- Hosted Viewer/Shell: `artifacts/c3d_geometry_performance_20260715/regression_shell`

Regression evidence from the same source state:

- Build: zero warnings and zero errors
- Display-settings ViewModel: `64` checks
- Nominal/actual ViewModel: `71` checks
- Fixed Viewer/Shell matrix: `128/128`
- BinaryHost: manifest `13/13`, outputs `12/12`, Host API commands `3/3`
- Viewer and Shell pointer input: routed events `3/12/3/1`, pick/orbit/pan/zoom passed
- Hosted Viewer and full Shell Surface + Edges screenshots: accepted on attempt 1
- Invalid `--smoke-render-frames 15`: controlled process exit code `1`

## Reproduce

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-c3d-geometry-performance.ps1 `
  -Configuration Debug `
  -ArtifactDirectory artifacts\c3d_geometry_performance_refresh `
  -RenderFrames 31 `
  -SkipBuild
```

Run the command twice before changing the local threshold claim. Do not use screenshot acceptance or total process elapsed time as a substitute for the 31-frame contract.

## Limits

- Results apply to this machine, fixed sample, Debug build, and current SharpGL compatibility path.
- The telemetry is a mean SharpGL draw callback measurement. It is not GPU timer-query data, input latency, frame pacing, startup compilation time, or arbitrary-dataset proof.
- The sampled edge overlay is display-only. It must not enter inspection metrics, recipes, fingerprints, or Runner evidence.
- C3D physical pitch, scale, offset, units, origins, and calibration identity remain unavailable, so no physical or metrology accuracy follows from this gate.

## Next Gate

Add C3D Grayscale and Thermal color maps as a separate display-only slice while preserving the static-cache key, dynamic Deviation bypass, measurement invariance, and all fixed Viewer trust regressions.
