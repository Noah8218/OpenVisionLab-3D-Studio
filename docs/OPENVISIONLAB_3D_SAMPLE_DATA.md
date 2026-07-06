# OpenVisionLab 3D Sample Data

Checked: 2026-07-06

Sample data is currently stored under `C:\Git\OpenVisionLab-3D-Studio\3D`.

## 1. Inventory

| Path | Size | Observed role |
| --- | ---: | --- |
| `3D/Thickness/Ori_20240116_094414.C3D` | 10,236,276 bytes | Candidate raw 3D height/depth grid. |
| `3D/Thickness/Ori_20240116_094414.png` | 539,528 bytes | 2D preview/reference image, 1301 x 1967. |
| `3D/Warpage/Ori_20240116_094430.C3D` | 10,236,276 bytes | Candidate raw 3D height/depth grid. |
| `3D/Warpage/Ori_20240116_094430.png` | 539,528 bytes | 2D preview/reference image, 1301 x 1967. |

Current SHA256 check shows the `Thickness` and `Warpage` C3D files are byte-identical. The PNG files are also byte-identical. Treat this as a current data fact, not a product assumption.

## 2. C3D Format Observation

The C3D files appear to use a simple raster height/depth layout:

```text
int32 width   = 1301
int32 height  = 1967
float32 data  = width * height samples
```

Evidence:

- First 8 bytes decode as `1301` and `1967`.
- File size equals `8 + 1301 * 1967 * 4`.
- Float sample count is `2,559,067`.
- All samples are finite in the checked file.

Observed value stats for `3D/Thickness/Ori_20240116_094414.C3D`:

| Metric | Value |
| --- | ---: |
| Width | 1301 |
| Height | 1967 |
| Float samples | 2,559,067 |
| Zero samples | 905,505 |
| NaN samples | 0 |
| Min | -525.416 |
| Max | 5359.896 |
| Mean | 945.281689495367 |

This is an inferred format until an official C3D writer/spec is found.

## 3. Viewer Usage Plan

Current viewer implementation:

- `3D/Thickness/Ori_20240116_094414.C3D` is loaded by the SharpGL app as an inferred-layout C3D height grid.
- Non-zero finite samples are downsampled into viewer points.
- The viewer displays height/deviation color modes, camera controls, C3D point picking, raw height status, and screenshot smoke.
- This is still viewer-only. It is not a production C3D import contract and does not run Thickness/Warpage algorithms.

Use the samples in this order:

1. Use the PNG files as visual references for expected shape, crop, invalid regions, and color-map style.
2. Keep the first C3D use viewer-only: render, color mode, camera, picking, and screenshot smoke.
3. Define source/result entity contracts before any C3D rule result mutates or publishes geometry.
4. Start Thickness/Warpage algorithm work only after the viewer completion gate passes.

## 4. Guardrails

- Do not mutate sample files in place.
- Do not assume `Thickness` and `Warpage` represent different measurements until non-identical samples are available or the source format is confirmed.
- Do not build production C3D import around the inferred layout without a small validation command.
- Keep these files local sample data; do not copy them into `C:\Git\OpenVisionLab_Dev`.
