# OpenVisionLab 3D Imported-Mesh Geometry Styles

Date: 2026-07-15
Status: Local fixed-sample checkpoint passed; mandatory Windows workflow gate configured; Actions run pending

## Decision

GLB and STL triangle meshes now support the Viewer-owned `Points`, `Wireframe`, `Surface`, and `Surface + Edges` display styles. This closes the local ImageJ-inspired imported-mesh Geometry Style checkpoint without changing inspection geometry, recipes, results, or Host API v1.0.

The implementation follows the required order:

1. **View**: the existing standalone Viewer and Shell `Geometry Style` ComboBoxes remain bound to `Display.AvailableGeometryStyles`, `Display.SelectedGeometryStyle`, and `Display.CanSelectGeometryStyle`. No duplicate Shell state or new code-behind selection behavior was added.
2. **ViewModel**: `ViewerDisplaySettingsViewModel` enables the four existing typed choices for `ImportedTriangleMesh`, validates unavailable values, raises only `RenderSettingsChanged`, and retains `Surface + Edges` as the source default.
3. **Model/render**: the existing immutable `ViewerDisplaySettingsSnapshot` and `ViewerGeometryStyle` model drive four SharpGL imported-mesh draw paths. Picking and measurement continue to read the original `ImportedMesh` positions and indices.

## Render Contract

| Style | SharpGL behavior | Source color behavior |
| --- | --- | --- |
| `Points` | Emits vertices from render-density-selected triangles as `GL_POINTS` | Uses texture coordinates, vertex colors, or Solid fallback per emitted vertex |
| `Wireframe` | Emits all edges of render-density-selected source triangles as `GL_LINES` | Uses texture coordinates, vertex colors, or Solid fallback along the edges |
| `Surface` | Emits the established source triangles as `GL_TRIANGLES` | Preserves filled source texture, vertex colors, or Solid fallback |
| `Surface + Edges` | Draws the source surface with polygon offset, then the established pale triangle-edge overlay | Preserves the filled source appearance; the overlay remains display-only |

The Viewer contract records `geometryRenderBridge=SharpGLImportedTriangleMesh`, the typed effective style, source color mode, mesh attributes, pick evidence, and measurement evidence. Geometry style changes do not invoke Preview or Publish.

## Fixed Verification

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-imported-mesh-geometry-styles.ps1
```

The verifier covers:

| Sample | Required source behavior | Styles |
| --- | --- | --- |
| `BoxTextured.glb` | Texture remains present and uploads successfully | 4 |
| `BoxVertexColors.glb` | All 24 vertex colors remain active | 4 |
| `Tetrahedron.stl` | Solid fallback remains active | 4 |

Result: `15/15` checks passed. All 12 sample/style cases produced accepted screenshots and the expected typed contract. Each sample produced four distinct screenshot hashes while retaining one pick contract and one two-point measurement contract across all styles.

Additional current-source evidence:

- Build: 0 warnings, 0 errors.
- Display ViewModel verification: `79` checks passed, including no Preview/Publish side effects.
- Fixed data/loading matrix: `128/128`, failures `0`.
- BinaryHost: zero `ProjectReference`, manifest `13/13`, required outputs `12/12`, Host API commands `3/3`, runtime exit `0`.
- Hosted capture: embedded Viewer and full Shell screenshots accepted on attempt 1 with the Shell `Wireframe` choice active.
- WPF pointer input: Viewer hash `4D6C926DA834ED6AE017D98FEB84BCB043C1FA77AD3364A36D1B1EB842C7CF4E`; Shell hash `2F2CBB688D8C3293C3176100CC6AE2D985BFF1A8F19DE840E77D98D72CCEA2A0`, unchanged from the established baseline.

Evidence root: `artifacts/imported_mesh_geometry_styles_20260715`.

## Limits

- This is local fixed-sample rendering evidence, not Windows CI portability evidence until the focused verifier passes in Actions.
- No large-mesh style performance threshold is claimed. The existing 3DBenchy default-style stride path remains covered by the fixed matrix.
- Points and Wireframe preserve source attributes only at emitted vertices and edges; only Surface modes show a filled texture.
- The work does not add mesh repair, topology inference, smoothing, contours, manual color ranges, physical calibration, or metrology accuracy.
- LAS/LAZ remains point-only. No surface topology is inferred.

## Windows CI Gate

`.github/workflows/ci.yml` now runs `scripts/verify-imported-mesh-geometry-styles.ps1` after the Debug build with `-SkipBuild` and writes its complete output under `artifacts/ci/imported-mesh-geometry-styles`. The step fails when the verifier exits nonzero, when `summary.txt` is missing, or when the summary does not contain `PASS (15/15 checks passed)`. The existing always-run artifact upload includes all 12 screenshots, quality reports, contracts, and the summary.

The workflow configuration and exact CI command pass locally. Cross-machine closure remains pending until the changes are committed, pushed, and the resulting Windows Actions run is inspected.
