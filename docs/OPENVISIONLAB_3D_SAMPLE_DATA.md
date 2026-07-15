# OpenVisionLab 3D Sample Data

Checked: 2026-07-15

Sample data is currently stored under `C:\Git\OpenVisionLab-3D-Studio\3D`.

## 1. Inventory

| Path | Size | Observed role |
| --- | ---: | --- |
| `3D/Thickness/Ori_20240116_094414.C3D` | 10,236,276 bytes | Candidate raw 3D height/depth grid. |
| `3D/Thickness/Ori_20240116_094414.png` | 539,528 bytes | 2D preview/reference image, 1301 x 1967. |
| `3D/Warpage/Ori_20240116_094430.C3D` | 10,236,276 bytes | Candidate raw 3D height/depth grid. |
| `3D/Warpage/Ori_20240116_094430.png` | 539,528 bytes | 2D preview/reference image, 1301 x 1967. |
| `3D/PublicSamples/glTF/Box.glb` | 1,664 bytes | Public GLB mesh import baseline. |
| `3D/PublicSamples/glTF/BoxTextured.glb` | 6,540 bytes | Public GLB texture/material import baseline. |
| `3D/PublicSamples/glTF/BoxVertexColors.glb` | 1,924 bytes | Public GLB vertex-color import baseline. |
| `3D/PublicSamples/glTF/Avocado.glb` | 8,110,040 bytes | Public GLB realistic non-box textured mesh import baseline. |
| `3D/PublicSamples/STL/Tetrahedron.stl` | 534 bytes | Local generated STL triangle-mesh import baseline. |
| `3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz` | 5,351,794 bytes | Public LAZ metadata/bounds and sampled XYZ/RGB point-cloud render baseline. |
| `3D/PublicSamples/PointCloud/interesting.las` | 37,698 bytes | Public LAS small uncompressed XYZ/RGB point-cloud render baseline. |

Current SHA256 check shows the `Thickness` and `Warpage` C3D files are byte-identical. The PNG files are also byte-identical. Treat this as a current data fact, not a product assumption.

Public sample source and license details are recorded in `3D/PublicSamples/README.md`.

Loader/viewer acceptance coverage for these samples is tracked in `docs/OPENVISIONLAB_3D_DATA_LOADING_TEST_MATRIX_20260707.md`.

The first distinct external measured/nominal candidate is NIST AMMT `Overhang Part X4`. It remains under ignored `artifacts/research_samples/nist_overhang_x4`, not under `3D/PublicSamples`. The nominal STL loads with 2,904 triangles and bounds `(0,0,0)..(9,5,5)`; the Part 1 XCT surface has 8,560,096 triangles and is controlled-rejected by the current 1,000,000-triangle Viewer limit. CloudCompare 2.13.2 supplies an independently verified signed/unsigned full-vertex deviation baseline in the documented NIST 3-2-1 part frame. OpenVisionLab reproduces the original measured SHA-256, triangle count, and bounds without render-density dependency; its controlled stream/PLY/distance/robust-sign golden passes `17/17`; and all 4,223,524 ordered validation vertices pass unsigned and robust-signed CloudCompare parity within `1e-6 mm` with zero sign mismatches. The fixed Part 1 Viewer/Runner workflow passes; generalization beyond this identity-frame sample and redistribution approval remain open. Source, hashes, licensing caution, local bounds, baseline evidence, and next acceptance gates are recorded in `docs/OPENVISIONLAB_3D_MEASURED_NOMINAL_SAMPLE_REVIEW_20260714.md` and `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md`.

The ignored `artifacts/research_samples/nist_overhang_x4_part2` folder contains the second physical-instance candidate. Its source archive is `197,482,785` bytes with SHA-256 `BDA2BC07B0F2E2920E3F5AE378849319D75B22F36AE078FCAF6ED5CB12AC96F9`; the extracted STL is `402,032,984` bytes with SHA-256 `0F74D3A949488C161DAC71681420A171B1EDA3E478ED24D492D33AA6C9F7F032`. The current Runner streams all `8,040,658` triangles and records bounds `(-0.081858255,-0.114424519,-0.150348008)..(8.97986984,5.03950977,4.82653236) mm`. CloudCompare 2.13.2 and OpenVisionLab pass the fixed `3,965,430`-vertex external/non-visual identity-frame parity gate; the visible Viewer/Shell, selected-point, Preview/Publish, typed recipe save/reopen, schema `1.2`, and Viewer/Runner `Matched` gates also pass locally. See `docs/OPENVISIONLAB_3D_NIST_PART2_CLOUDCOMPARE_DEVIATION_BASELINE_20260715.md` and `docs/OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md`. The source remains outside `3D/PublicSamples` and CI. Stanford Drill remains ignored under `artifacts/research_samples/stanford_drill`; its local `12`-scan, `50,643`-point non-identity transform gate passes with independent point/aggregate parity and tamper rejection. It must not ship in a commercial product without Stanford permission. See `docs/OPENVISIONLAB_3D_STANFORD_TRANSFORM_BASELINE_20260715.md`.

## 2. C3D Format Observation

The C3D files appear to use a simple raster height/depth layout:

```text
int32 width   = 1301
int32 height  = 1967
float32 data  = width * height samples
```

Evidence:

- First 8 bytes decode as `1301` and `1967`.
- File size equals `8 + 1301 * 1967 * 4 = 10,236,276` bytes exactly.
- Float sample count is `2,559,067`.
- All samples are finite in the checked file.
- The exact length leaves no trailing bytes for embedded pitch, unit, axis, or calibration metadata; no calibration sidecar is present under `3D/`.

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
3. Use `3D/PublicSamples/glTF/Box.glb` as the first external mesh import baseline. Current Viewer smoke renders this sample and records vertex/triangle/bounds evidence.
4. Use `3D/PublicSamples/glTF/BoxVertexColors.glb` to verify GLB `COLOR_0` vertex-color import. Current Viewer smoke records vertex-color evidence.
5. Use `3D/PublicSamples/glTF/BoxTextured.glb` to verify GLB `TEXCOORD_0` plus embedded PNG base-color texture import. Current Viewer smoke records UV/texture upload evidence.
6. Use `3D/PublicSamples/glTF/Avocado.glb` to verify a realistic non-box textured mesh. Current Viewer smoke records mesh bounds, triangle count, UVs, texture upload, fit camera distance, triangle-surface picking, triangle-index/normal metadata, visible surface-normal overlay, and two-point distance/model-Y height evidence.
7. Use `3D/PublicSamples/STL/Tetrahedron.stl` to verify ASCII STL triangle loading, bounds, surface picking, and two-point distance/model-Y height evidence without GLB material metadata.
8. Use `3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz` and `3D/PublicSamples/PointCloud/interesting.las` together to verify point-cloud camera fitting across dense local-scale and sparse large-coordinate data. Current Viewer smoke records bounds-fit camera distances for both, proves RGB/height point-cloud color modes with height range legend evidence, guards non-result deviation mode, and records load time plus sampling ratio for Balanced/Fast point-cloud density.
9. Use `3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz` to verify LAS/LAZ header metadata, LASzip compression detection, point count, decoded XYZ/RGB, sampled point rendering, picking, two-point distance/height measurement, and bounds matching. Viewer supports metadata, sampled point, picked-point, and two-point measurement smoke; Runner `--laz-probe` remains the headless decode check.
10. Use `3D/PublicSamples/PointCloud/interesting.las` to verify uncompressed LAS RGB decoding, local viewer-origin mapping for large source coordinates, picking, two-point measurement, and a small low-density point-cloud view.
11. Refresh the data loading test matrix before adding algorithm work, especially when a new sample or loader is added.
12. Define source/result entity contracts before any C3D rule result mutates or publishes geometry.
13. Start Thickness/Warpage algorithm work only after the viewer completion gate passes.

## 4. Guardrails

- Do not mutate sample files in place.
- Do not assume `Thickness` and `Warpage` represent different measurements until non-identical samples are available or the source format is confirmed.
- Do not build production C3D import around the inferred layout without a small validation command.
- Keep these files local sample data; do not copy them into `C:\Git\OpenVisionLab_Dev`.
- Keep public sample license notes with the downloaded files before committing or redistributing them.
- Keep the NIST pair probe-only until redistribution/derivative notices and a traceable large-mesh inspection path are accepted. Do not raise the triangle limit or silently decimate inspection geometry to make the sample render.
