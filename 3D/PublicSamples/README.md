# OpenVisionLab 3D Public Samples

Checked: 2026-07-09

These files are small public 3D samples for import-loader and viewer smoke work. Keep them separate from the local C3D inspection samples under `3D/Thickness` and `3D/Warpage`.

## Inventory

| Path | Format | Size | SHA256 | Source | License note | Viewer test purpose |
| --- | --- | ---: | --- | --- | --- | --- |
| `glTF/Box.glb` | GLB | 1,664 bytes | `ED52F7192B8311D700AC0CE80644E3852CD01537E4D62241B9ACBA023DA3D54E` | Khronos glTF Sample Models, `2.0/Box/glTF-Binary/Box.glb` | Creative Commons Attribution 4.0. | Minimal mesh, one material, baseline triangle/index decode. |
| `glTF/BoxTextured.glb` | GLB | 6,540 bytes | `2D055D2D56A492D1B9302DE6E733046B625CF51E5F2A3090BD3A8C11ACC93C17` | Khronos glTF Sample Models, `2.0/BoxTextured/glTF-Binary/BoxTextured.glb` | Creative Commons Attribution 4.0; follow Cesium trademark terms. | Texture/material path check after geometry load works. |
| `glTF/BoxVertexColors.glb` | GLB | 1,924 bytes | `9C48227F33B0BA2FBCF23B98EBF60D1C8AE0C6E6C5281E0AA3CC58AFFEE10382` | Khronos glTF Sample Models, `2.0/BoxVertexColors/glTF-Binary/BoxVertexColors.glb` | Public domain / CC0. | Vertex color path, axis/color sanity, bounding-box measurement. |
| `glTF/SimpleInstancing.glb` | GLB | 7,356 bytes | `1C9425627481346A8C226118F7000DB7CD7F80198E9F54331F8B664D16726F3F` | Khronos glTF Sample Models, `2.0/SimpleInstancing/glTF-Binary/SimpleInstancing.glb` | Public domain / CC0. | Static `EXT_mesh_gpu_instancing` sample: 125 cube instances expanded into imported mesh geometry for scene/node/instance coverage. |
| `glTF/Avocado.glb` | GLB | 8,110,040 bytes | `CCC9C3CE56423720B09399C2351537207CD5A65F859F9E6E2F30922762F3ABD4` | Khronos glTF Sample Assets, `Models/Avocado/glTF-Binary/Avocado.glb` | CC0 1.0 Universal; Microsoft for Everything. | Realistic non-box textured mesh for mesh framing, material, and bounds smoke. |
| `glTF/ToyCar.glb` | GLB | 5,422,412 bytes | `01A60862DE55CD4B9F3ACFAB0B0DEF86451800F9C42467FCD61052C16CB9838C` | Khronos glTF Sample Assets, `Models/ToyCar/glTF-Binary/ToyCar.glb` | CC0 1.0 Universal; Guido Odendahl for initial car model, Eric Chadwick for extensions and scene composition. | Complex real GLB ad-hoc probe for larger triangle count, embedded PNG texture, fit camera, picking, and two-point measurement. |
| `STL/Tetrahedron.stl` | STL | 534 bytes | `8C8F445CC8A9A621063B3AF2A4645A5D2E343064507A871AC5BA2918927C5D1E` | Local generated fixture. | No external license; generated only for loader smoke. | Minimal ASCII STL triangle mesh for format, bounds, picking, and two-point measurement smoke. |
| `STL/3DBenchy.stl` | STL | 11,285,384 bytes | `6AB57F1C3F8E86BC3CBD302C6FA6270ACF06277C6335454E922419C25D42E97E` | Official 3DBenchy GitHub download, `Single-part/3DBenchy.stl`. | Public Domain / CC0 according to the official 3DBenchy download page. | Complex real STL ad-hoc probe for high triangle count, binary STL bounds, picking, two-point measurement, and imported-mesh render-density behavior. |
| `PointCloud/xyzrgb_manuscript.laz` | LAZ | 5,351,794 bytes | `255569B7AE9FCE1FA98E0FD55F7FA887EA402FBD4EC2EE7989E4384FD984B26F` | PDAL data, `liblas/xyzrgb_manuscript.laz` via Git LFS media URL. | PDAL data repository is CC-BY-4.0. | LiDAR-style point-cloud loader, XYZ/RGB field discovery, downsample/display performance. |
| `PointCloud/interesting.las` | LAS | 37,698 bytes | `505E6A78E20B97CFD56ADE899686E1882C5C89CBA5598AAA75CB485147947130` | PDAL data, `workshop/interesting.las` via Git LFS media URL. | PDAL data repository is CC-BY-4.0. | Small uncompressed LAS RGB point-cloud baseline for format/scale diversity. |
| `Invalid/corrupt.glb` | Invalid GLB | 24 bytes | local fixture | Intentional invalid data, no external license. | Loader failure and smoke exit-code test only. |
| `Invalid/corrupt.stl` | Invalid STL | 158 bytes | `AD4D480EF181484BF2515913920830FB70CEAEAECFD1BDCEEA24D19D0F28627F` | local fixture | Intentional invalid data, no external license. | Loader failure and smoke exit-code test only. |
| `Invalid/corrupt.laz` | Invalid LAZ | 31 bytes | local fixture | Intentional invalid data, no external license. | Loader failure and smoke exit-code test only. |

## Import Test Order

1. Load `Box.glb` first. Expected import result is a small mesh with a stable bounding box and one material.
2. Load `BoxVertexColors.glb` next. Verify vertex colors are preserved or intentionally mapped to a fallback color mode.
3. Load `BoxTextured.glb` after texture/resource handling exists.
4. Load `SimpleInstancing.glb` to verify scene/node traversal and static `EXT_mesh_gpu_instancing` expansion before relying on real multi-node GLB data.
5. Load `Avocado.glb` to verify a non-box textured mesh with realistic framing and material metadata.
6. Probe `ToyCar.glb` when checking larger GLB behavior without adding it to the fixed matrix; it is useful for stress-checking texture, bounds, pick, and two-point measurement on a high-triangle real model.
7. Load `Tetrahedron.stl` to verify a non-GLB triangle mesh path with no texture or vertex-color metadata.
8. Probe `3DBenchy.stl` only when checking large STL behavior. It passes Viewer/Shell STL load, pick, and two-point measurement with imported-mesh render sampling active; keep it probe-only until the team decides the extra routine smoke time is worth the fixed-matrix coverage.
9. Load `xyzrgb_manuscript.laz` after the point-cloud loader path exists. Verify unit metadata, coordinate ranges, point count, RGB availability, and downsample behavior before rendering all points.
10. Load `interesting.las` to verify uncompressed LAS RGB decoding on a small 1,065-point sample.

Promote a probe-only sample into the fixed matrix only when it exposes a loader, camera, picking, measurement, Shell, or contract gap that the current matrix does not cover. Otherwise, keep it as an ad hoc probe to avoid slowing every routine smoke run.

## Guardrails

- Do not rewrite or normalize these files in place.
- Keep source geometry and imported result geometry separate.
- Do not use license-restricted samples as product branding, screenshots, or marketing assets unless the license is reviewed again.
- If a loader cannot read one of these files yet, record that as an import gap instead of modifying the sample.
- Keep `Invalid/` fixtures intentionally broken. They prove failure handling, not positive loader coverage.
