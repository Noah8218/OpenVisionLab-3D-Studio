# OpenVisionLab 3D Line Fit Typed Adapter v1

Updated: 2026-07-19

Status: **Complete (local implementation and evidence)**

## Completed scope

`3D Line Fit` now consumes exactly one current Published
`C3DHeightDifferenceEdgePointSet` and emits an immutable full-XYZ
`C3DLineFeature`. It is an ordered feature-extraction step in this chain:

```text
FilteredHeightField -> Height Difference EdgePointSet -> 3D Line Feature
```

- Tools owns deterministic consensus candidate selection plus full-XYZ
  orthogonal TLS refinement; Runner calls the same rule.
- Candidate score order is inlier count, RMS orthogonal residual, scanline
  span, then original pair indices. The SHA-256 pair schedule uses explicit
  big-endian reads, so its ordering does not depend on host byte order.
- The adapter rejects absent/unpublished/mismatched upstream output, invalid
  parameters, degenerate/non-finite points, insufficient support, instability,
  and cancellation. It never reruns Filter or Edge.
- Workbench Preview, Cancel, stale invalidation, and Publish are explicit.
  Publish reuses the exact Preview object. The full recipe Run remains blocked
  because later typed adapters are intentionally absent.
- Viewer shows teal inliers, amber outliers, the finite teal fitted segment and
  direction cue, and only the selected residual connector. The same four
  display-only toggles exist in the top `View` and short-right-click menus.
- `Fit Diagnostics` is a hideable/floatable/dockable lower pane with residual
  chart and linked point rows. The pane is automatically selected after a Line
  Fit Preview, but it does not execute a tool or change recipe state.

The four real Line Fit rows in
`recipes/c3d-xyz-affine-teaching-template.ov3d-teach.json` retain their
explicit `Set explicitly` numerical limits. No smoke value was saved.

## Verification

| Gate | Current result |
| --- | --- |
| Solution build | `dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"` -> 0 warnings, 0 errors |
| Line Fit numerical Golden | `--verify-c3d-line-fit` -> 9/9 |
| Line Fit Workbench lifecycle/hash parity | `--verify-tool-line-fit-workbench` -> 14/14 |
| Dockable diagnostics | `--verify-workbench-docking` -> 17/17 |
| Upstream Edge regression | `--verify-c3d-edge` -> 13/13; `--verify-tool-edge-workbench` -> 11/11 |
| Current-source UI | actual Warpage C3D smoke -> accepted screenshot quality; 135 edge inputs, 135 Line Fit inliers, 135 residual plot points |

Artifacts:

- `artifacts/verification/20260719-line-fit-v1/golden.txt`
- `artifacts/verification/20260719-line-fit-v1/workbench.txt`
- `artifacts/verification/20260719-line-fit-v1/docking.txt`
- `artifacts/verification/20260719-line-fit-v1/ui-line-fit-smoke.txt`
- `artifacts/ui/20260719-line-fit-v1/after-line-fit-preview.png`

The CI workflow now runs the same Golden, Workbench, and actual-source smoke
gates under `Verify typed C3D Line Fit adapter`.

## Boundary and next dependency

Line Fit output is source-coordinate evidence only. It does not establish a
calibrated axis, physical line accuracy, GD&T, inspection OK/NG, or metrology.

The next product gate is a separately approved **Line Intersection** design:
two named `LineFeature` inputs, explicit skew-line closest-approach policy,
quality/parallel rejection, a typed intersection/correspondence output, and
linked Viewer evidence. Affine, re-grid, Thickness, and Warpage remain later
gates and require real owner-taught feature limits plus provenance.
