# Invalid Loader Samples

Checked: 2026-07-08

These files are intentionally invalid and are used only to verify loader failure handling. They are not public sample data and must not be treated as supported 3D formats.

| Path | Intended failure |
| --- | --- |
| `corrupt.glb` | GLB loader should reject the header and Viewer/Shell smoke should return failure. |
| `corrupt.stl` | STL loader should reject invalid vertex coordinates and Viewer/Shell smoke should return failure. |
| `corrupt.laz` | LAZ/LAS loader should reject the header and Viewer/Shell smoke should return failure. |

Guardrails:

- Do not replace these with valid sample data.
- Do not use these files for visual inspection, benchmarks, screenshots, or marketing.
- Keep failure evidence separate from the positive data loading matrix.
