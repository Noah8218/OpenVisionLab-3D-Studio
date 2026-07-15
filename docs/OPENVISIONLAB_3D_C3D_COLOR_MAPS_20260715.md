# C3D Grayscale And Thermal Color Maps

Date: 2026-07-15

## Decision

The fixed C3D Viewer now supports `Solid`, `Grayscale`, `Height`, and `Thermal` display color maps. `Deviation` remains available only while a result supplies deviation data. The default remains `Height`.

This is a display-only checkpoint. It does not change source cells, coordinates, picking, measurement, Preview, Publish, recipes, result fingerprints, or Host API v1.0.

## MVVM Delivery Order

1. View: the existing Viewer and Shell `Color Map` ComboBoxes already bind to the Viewer-owned display surface, so no new XAML or Shell state was required.
2. ViewModel: `ViewerDisplaySettingsViewModel` exposes the C3D capability order `Solid, Grayscale, Height, Thermal`, with result-owned `Deviation` appended only when available.
3. Model: `ViewerColorMapPalette` deterministically maps normalized height scalars to RGB and clamps non-finite or out-of-range input.
4. SharpGL boundary: the renderer consumes the effective ViewModel choice. The existing display-list key includes the typed color-map value, so a color change rebuilds static display geometry without changing inspection geometry.

## Palette Contract

| Map | Normalized scalar contract |
| --- | --- |
| Grayscale | `0 -> black`, `0.5 -> mid gray`, `1 -> white` |
| Thermal | `0 -> black`, `1/3 -> red`, `2/3 -> yellow`, `1 -> white` |

The scalar is the existing normalized C3D `HeightScalar`. It is not a calibrated physical height, and this checkpoint does not add automatic/manual display range controls. Non-finite input maps to the low endpoint; finite input is clamped to `[0, 1]`.

## Evidence

- True pre-edit current-build baseline: `artifacts/c3d_color_maps_20260715/before/viewer_height.png` and `shell_height.png`.
- Current-build Grayscale: `artifacts/c3d_color_maps_20260715/after/viewer_grayscale_performance.png`, its quality report, and `viewer_grayscale_performance_contract.txt`.
- Current-build Thermal: `artifacts/c3d_color_maps_20260715/after/viewer_thermal_performance.png`, its quality report, and `viewer_thermal_performance_contract.txt`.
- Display ViewModel verification: `71` checks pass, including exact C3D capability order, typed selections, render notifications, palette stops/clamping, source fallback, and no Preview/Publish side effect.
- Solution build: eight projects, zero warnings, zero errors.
- Balanced fixed C3D sample, 33,761 displayed points, 90 completed SharpGL frames:
  - Grayscale: `75.303 FPS`, `5.272 ms` mean draw.
  - Thermal: `37.049 FPS`, `10.438 ms` mean draw.
- Fixed Viewer/Shell matrix: `128/128` passes under `artifacts/c3d_color_maps_20260715/regression`.
- Binary-only host: zero `ProjectReference`, manifest `13/13`, required outputs `12/12`, Host API commands `3/3`, direct C3D render/pick pass.
- Pointer input: Shell passes `3/12/3/1` routed events with established SHA-256 `2F2CBB688D8C3293C3176100CC6AE2D985BFF1A8F19DE840E77D98D72CCEA2A0`. A standalone first attempt retained under `pointer/viewer_pointer.txt` lost foreground input before pan/zoom (`2/8/2/0`) and failed. Two immediate standalone confirmations then passed `3/12/3/1` and are byte-identical to the established Viewer SHA-256 `4D6C926DA834ED6AE017D98FEB84BCB043C1FA77AD3364A36D1B1EB842C7CF4E`. The failed attempt is retained as focus-sensitivity evidence rather than hidden.

## Acceptance Checklist

- [x] Viewer and Shell expose the same Viewer-owned choices.
- [x] Unsupported source/color combinations still fall back explicitly.
- [x] Grayscale and Thermal are deterministic for endpoints, midpoint/stops, clamping, and non-finite input.
- [x] Screenshot contracts record typed and readable effective color-map values.
- [x] Static C3D caching and dynamic result-owned Deviation behavior are preserved.
- [x] Fixed loading, picking, measurement, controlled-failure, and binary-host gates remain green.
- [ ] Windows CI revalidation after the next explicit commit/push.

## Limitations And Next Step

- These maps are validated only for the fixed C3D display frame and current machine; they are not physical-height or cross-hardware color calibration claims.
- No legend, editable display range, inversion, contouring, or LUT import is included.
- The next display checkpoint is GLB/STL Geometry Style switching while preserving texture and vertex-color behavior. LAS/LAZ remains point-only.
