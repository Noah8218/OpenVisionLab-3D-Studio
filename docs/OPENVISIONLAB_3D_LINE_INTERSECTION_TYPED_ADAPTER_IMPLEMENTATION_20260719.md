# 3D Line Intersection v1 Typed Adapter Implementation

Updated: 2026-07-19

Status: current implementation and local-evidence record for the approved Line
Intersection v1 slice. The design and owner decisions remain in
`docs/OPENVISIONLAB_3D_LINE_INTERSECTION_TYPED_ADAPTER_DESIGN_20260719.md`.

## Delivered scope

Line Intersection now completes the next feature-extraction edge of the
teaching chain:

```text
Published FilteredHeightField
  -> Published EdgePointSet A/B
  -> Published LineFeature A/B
  -> Published CornerAnchor
```

- It accepts exactly two named, current, Published `LineFeature` inputs in
  authored order.
- It calculates full-XYZ infinite-line closest approach, retains both closest
  points, and emits the midpoint only after the taught gap, acute-angle, and
  bounded-support gates pass.
- The output is `CornerAnchor` source-coordinate feature evidence. It is not a
  calibrated physical intersection, an inspection decision, or an OK/NG
  result.
- Root source ID/SHA-256, unit, frame, and coordinate convention must match;
  stale or preview-only line inputs fail closed.
- The strict seven-field teaching schema is now in the Warpage template and
  typed WPG draft: `MaximumClosestApproachDistance`,
  `MinimumAcuteAngleDegrees`, `MaximumSupportExtension`, `OutputRole`, and the
  three fixed-policy fields.
- Preview, Cancel, stale propagation, and Publish are explicit. Opening a
  Tool Lab, selecting a dock pane, or toggling a viewer overlay does not run a
  tool.

## UI and presentation boundary

- `Intersection Tool Lab` is a separate single-instance custom-chrome WPF
  window, launched from the application header. It contains two 3D viewers:
  Published LineFeature inputs at left and CornerAnchor evidence at right.
- The primary Workbench and Expert dock layout have a floatable/hideable
  `Intersection Evidence` pane. The Recipe Manager remains a separate window.
- The Viewer draws first/second fitted segments in distinct teal/violet,
  closest-pair connector amber, and midpoint anchor magenta. The top `View`
  and short-right-click menus expose the same display-only visibility toggles.
- The Tool Lab accepts missing upstream data as an explicit state; its Preview
  and Publish buttons remain disabled until teaching and both named Published
  inputs are ready.

## Ownership

| Layer | Implementation responsibility |
| --- | --- |
| Core | Immutable `C3DLineIntersectionFeature` contract and canonical hash. |
| Tools | Closed-schema parser plus closest-points, angle, gap, and support rule. |
| Runner | Same Tools rule for chain execution and Golden entry point. |
| Shell ViewModel | Published-output registry, explicit lifecycle, WPG, artifacts, and stale state. |
| Viewer | Overlay/HUD and display-only visibility. |
| Shell views | Dedicated Tool Lab and dockable evidence presentation. |

## Verification record

Current local Debug evidence, all from the 2026-07-19 source build:

| Check | Result | Evidence |
| --- | --- | --- |
| Solution build | Pass, 0 warnings / 0 errors | `dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"` |
| Analytic Line Intersection Golden plus synthetic full Runner chain | Pass, 9/9 | `artifacts/verification/20260719-line-intersection-v1/golden.txt` |
| Full Workbench chain and Runner hash parity | Pass, 23/23 | `artifacts/verification/20260719-line-intersection-v1/workbench.txt` |
| Existing Line Fit Workbench regression | Pass, 14/14 | `artifacts/verification/20260719-line-intersection-v1/line-fit-regression.txt` |
| Existing Height Difference Edge Workbench regression | Pass, 11/11 | `artifacts/verification/20260719-line-intersection-v1/edge-regression.txt` |
| Eight-pane docking contract | Pass, 18/18 | `artifacts/verification/20260719-line-intersection-v1/docking.txt` |
| Current-build Tool Lab screenshot quality | Pass, attempt 1 | `artifacts/verification/20260719-line-intersection-v1/line-intersection-tool-lab-after.png` and `line-intersection-tool-lab-after-quality.txt` |

The GitHub Actions workflow now invokes the 9/9 Golden, the 23/23 Workbench
chain/hash-parity check, and the Tool Lab screenshot-quality check on Windows.
That workflow change is not remote-CI evidence until an Actions run completes.

## Boundaries and next dependency

- The supplied Warpage template intentionally retains `Set explicitly` limits;
  this implementation does not infer customer teaching values.
- The current UI capture shows the expected `missing/stale` state because the
  template has no Published LineFeature inputs. It validates current-build Tool
  Lab layout and source-view rendering, not a real corner result on production
  data.
- Real owner-taught source/reference corner pairs, correspondence evidence,
  XYZ affine, re-grid, Thickness/Warpage application, calibration provenance,
  uncertainty, and physical/metrology claims remain outside this slice.

Status: Complete
Scope: Approved Line Intersection v1 typed feature extraction, dedicated Tool
Lab, dockable evidence, recipe template schema, local verification, and CI
gate definition.
Acceptance criteria:
  - Exact two-Published-LineFeature full-XYZ output, full Runner chain, and
    fail-closed lineage: Pass (Golden 9/9 and Workbench 23/23).
  - Explicit Preview/Publish lifecycle and shared Workbench/Runner output hash:
    Pass (Workbench 23/23).
  - Separate Tool Lab, dockable evidence, and fresh current-build UI quality:
    Pass (docking 18/18 and accepted screenshot).
Verification: commands and artifact paths above.
Evidence: local artifact directory
`artifacts/verification/20260719-line-intersection-v1`.
Boundary / next dependency: real, distinct owner-taught aligned acquisitions
and explicit correspondence/reference evidence are required before XYZ affine
implementation or any physical claim.
